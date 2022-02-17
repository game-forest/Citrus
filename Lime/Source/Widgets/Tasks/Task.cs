using System;
using System.Collections.Generic;
using System.Linq;

namespace Lime
{
	/// <summary>
	/// Sequence of actions, based on IEnumerators.
	/// </summary>
	public class Task : IDisposable
	{
		private class StackEntry
		{
			public IEnumerator<object> Enumerator;
			public StackEntry Caller;
		}

		[ThreadStatic]
		private static Task current;
		private StackEntry callStack;
		private WaitPredicate waitPredicate;
		private float waitTime;

		public Task(IEnumerator<object> e, object tag = null)
		{
			Tag = tag;
			callStack = new StackEntry { Enumerator = e };
			InitialEnumeratorType = e.GetType();
		}

		/// <summary>
		/// Time delta since last Update.
		/// </summary>
		public float Delta { get; private set; }

		public static Task Current => current;

		public object Tag { get; set; }

		public bool Completed => callStack == null;

		public override string ToString() => Completed ? "Completed" : callStack.Enumerator.GetType().ToString();

		public Type InitialEnumeratorType { get; }

		/// <summary>
		/// Advances task to the next step of enumerator.
		/// </summary>
		public void Advance(float delta)
		{
			if (callStack == null) {
				return;
			}
			var savedCurrent = current;
			current = this;
			Delta = delta;
			try {
				if (waitTime > 0) {
					waitTime -= delta;
					if (waitTime >= 0) {
						return;
					}
					Delta = -waitTime;
				}
				if (waitPredicate != null) {
					waitPredicate.TotalTime += delta;
					if (waitPredicate.Evaluate()) {
						return;
					}
					waitPredicate = null;
				}
				if (callStack.Enumerator.MoveNext()) {
					if (callStack != null) {
						HandleYieldedResult(callStack.Enumerator.Current);
					}
				} else if (callStack != null) {
					callStack = callStack.Caller;
					if (callStack != null) {
						Advance(0);
					}
				}
			} finally {
				current = savedCurrent;
			}
		}

		/// <summary>
		/// Exits from all IEnumerators.
		/// </summary>
		public void Dispose()
		{
			while (callStack != null) {
				callStack.Enumerator.Dispose();
				callStack = callStack.Caller;
			}
			waitPredicate = null;
		}

		private void HandleYieldedResult(object result)
		{
			switch (result) {
				case null:
					waitTime = 0;
					break;
				case int i:
					waitTime = i;
					break;
				case float f:
					waitTime = f;
					break;
				case double d:
					waitTime = (float)d;
					break;
				case IEnumerator<object> enumerator:
					callStack = new StackEntry { Enumerator = enumerator, Caller = callStack };
					Advance(0);
					break;
				case WaitPredicate predicate:
					waitPredicate = predicate;
					break;
				case Node3D node3D: {
						var ac = node3D.Components.Get<AnimationComponent>();
						var firstAnimation = ac != null && ac.Animations.Count > 0 ? ac.Animations[0] : null;
						waitPredicate = firstAnimation != null ? WaitForAnimation(firstAnimation) : null;
						break;
					}
				case Animation animation:
					waitPredicate = WaitForAnimation(animation);
					break;
				case Node node: {
						var defaultAnimation = node.Components.Get<AnimationComponent>()?.DefaultAnimation;
						waitPredicate = defaultAnimation != null ? WaitForAnimation(defaultAnimation) : null;
						break;
					}
				case IEnumerable<object> _:
					throw new InvalidOperationException(
						"Use IEnumerator<object> instead of IEnumerable<object> for " + result
					);
				default:
					throw new InvalidOperationException("Invalid object yielded " + result);
			}
		}

		/// <summary>
		/// Proceeds while specified predicate returns true.
		/// </summary>
		public static WaitPredicate WaitWhile(Func<bool> predicate) => new BooleanWaitPredicate(predicate);

		/// <summary>
		/// Proceeds while specified predicate returns true. Argument of the predicate is
		/// time, that accumulates on Advance.
		/// </summary>
		public static WaitPredicate WaitWhile(Func<float, bool> timePredicate) => new TimeWaitPredicate(timePredicate);

		/// <summary>
		/// Proceeds while specified node is running animation.
		/// </summary>
		public static WaitPredicate WaitForAnimation(Animation animation) => new AnimationWaitPredicate(animation);

		/// <summary>
		/// Proceeds while there is no keystroke on the current window.
		/// </summary>
		public static WaitPredicate WaitForInput() => InputWaitPredicate.Instance;

		/// <summary>
		/// Proceeds asynchronously in separate thread. Returns null while specified action is incomplete.
		/// </summary>
		public static IEnumerator<object> ExecuteAsync(Action action)
		{
			var t = new System.Threading.Tasks.Task(action);
			t.Start();
			while (!t.IsCompleted && !t.IsCanceled && !t.IsFaulted) {
				yield return null;
			}
		}

		/// <summary>
		/// Creates a new Task executing all provided enumerators in sequential order
		/// </summary>
		public static Task Sequence(params IEnumerator<object>[] args) => new Task(args.Cast<object>().GetEnumerator());

		/// <summary>
		/// Returns a sequence of numbers, interpolated as sine in specified time period.
		/// Advances by using Current.Delta.
		/// </summary>
		public static IEnumerable<float> SinMotion(float timePeriod, float from, float to)
		{
			for (float t = 0; t < timePeriod; t += Current.Delta) {
				float v = Mathf.Sin(t / timePeriod * Mathf.HalfPi);
				yield return Mathf.Lerp(v, from, to);
			}
			yield return to;
		}

		/// <summary>
		/// Returns a sequence of numbers, interpolated as square root in specified time period.
		/// Advances by using Current.Delta.
		/// </summary>
		public static IEnumerable<float> SqrtMotion(float timePeriod, float from, float to)
		{
			for (float t = 0; t < timePeriod; t += Current.Delta) {
				float v = Mathf.Sqrt(t / timePeriod);
				yield return Mathf.Lerp(v, from, to);
			}
			yield return to;
		}

		/// <summary>
		/// Returns a sequence of numbers, linear interpolated in specified time period.
		/// Advances by using Current.Delta.
		/// </summary>
		public static IEnumerable<float> LinearMotion(float timePeriod, float from, float to)
		{
			for (float t = 0; t < timePeriod; t += Current.Delta) {
				yield return Mathf.Lerp(t / timePeriod, from, to);
			}
			yield return to;
		}

		public static IEnumerator<object> Repeat(Func<bool> f)
		{
			while (f()) {
				yield return null;
			}
		}

		public static IEnumerator<object> Delay(float time, Action action)
		{
			yield return time;
			action();
		}

		/// <summary>
		///  Wait while predicate is true before executing an action.
		/// </summary>
		public static IEnumerator<object> Delay(Func<bool> predicate, Action action)
		{
			while (predicate()) {
				yield return null;
			}
			action();
		}
	}
}
