using Lime;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using Exception = System.Exception;

namespace System.Runtime.CompilerServices
{
	public sealed class AsyncMethodBuilderAttribute : Attribute
	{
		public AsyncMethodBuilderAttribute(Type taskLike) { }
	}
}

namespace Tests.Scripts.Common
{
	public class CoroutineBase : IDisposable
	{
		private static int nextCoroutineId;

		private bool isCompleted;
		protected Action Continuation;
		protected bool continuationCalled;
		protected CoroutineBase Child;
		protected ExceptionHolder Exception;
		protected bool Disposed;
		public int Id { get; } = nextCoroutineId++;

		public virtual bool IsCompleted => isCompleted;

		public void SetChild(CoroutineBase child) => Child = child;

		protected virtual void InternalUpdate(float delta)
		{
			if (Child != null) {
				Child.InternalUpdate(delta);
				if (Child.IsCompleted) {
					Child = null;
				}
			}
			if (IsCompleted && !continuationCalled) {
				CallContinuation();
			}
		}

		public void Update(float delta)
		{
			if (Disposed) {
				throw new ObjectDisposedException(nameof(CoroutineBase));
			}
			var upwardStackDisposing = true;
			try {
				try {
					InternalUpdate(delta);
				} finally {
					ThrowExceptionIfRequired();
				}
				upwardStackDisposing = false;
			} finally {
				if (IsCompleted || upwardStackDisposing) {
					Dispose();
				}
			}
		}

		protected void CallContinuation()
		{
			if (Disposed) {
				throw new ObjectDisposedException(nameof(CoroutineBase));
			}
			continuationCalled = true;
			Continuation?.Invoke();
		}

		public void SetResult() => isCompleted = true;

		public void SetException(Exception e)
		{
			Exception = new ExceptionHolder(ExceptionDispatchInfo.Capture(e));
			isCompleted = true;
		}

		public void ThrowExceptionIfRequired()
		{
			var coroutine = this;
			do {
				coroutine.Exception?.GetException().Throw();
				coroutine = coroutine.Child;
			} while (coroutine != null);
		}

		public virtual void Dispose()
		{
			if (Disposed) {
				return;
			}
			Child?.Dispose();
			Child = null;
			Continuation = null;
			Disposed = true;
		}
	}

	[AsyncMethodBuilder(typeof(CoroutineMethodBuilder))]
	public class Coroutine : CoroutineBase, INotifyCompletion
	{
		public Coroutine GetAwaiter() => this;

		public void GetResult()
		{
			Exception?.GetException().Throw();
			//canceled
		}

		public void OnCompleted(Action continuation)
		{
			Continuation = continuation;
			if (IsCompleted) {
				CallContinuation();
			}
		}

		public static Coroutine WaitNextFrame() => new WaitNextFrameCoroutine();

		public static Coroutine Wait(float seconds) => seconds > 0 ? new DelayCoroutine(seconds) : WaitNextFrame();

		public static Coroutine WaitWhile(Func<bool> predicate) => new WaitPredicateCoroutine(Task.WaitWhile(predicate));

		public static Coroutine WaitWhile(Func<float, bool> timePredicate) => new WaitPredicateCoroutine(Task.WaitWhile(timePredicate));

		public static Coroutine WaitForAnimation(Animation animation) => new WaitPredicateCoroutine(Task.WaitForAnimation(animation));

		/// <summary>
		/// Proceeds while node's running marker id in given animation is EQUAL to given <paramref name="markerId"/>
		/// </summary>
		/// <param name="node">Node whose animation to watch</param>
		/// <param name="markerId">Id of the marker that should be traced</param>
		/// <param name="animationId">Default animation will be used if not given</param>
		public static Coroutine WaitForMarker(Lime.Node node, string markerId, string animationId = null)
		{
			var animation = animationId == null ? node.Animations[0] : node.Animations.Find(animationId);
			return WaitWhile(() => animation.RunningMarkerId == markerId && animation.IsRunning);
		}

	}

	[AsyncMethodBuilder(typeof(CoroutineMethodBuilder<>))]
	public class Coroutine<T> : CoroutineBase, INotifyCompletion
	{
		private T result;

		public Coroutine<T> GetAwaiter() => this;

		public T GetResult()
		{
			Exception?.GetException().Throw();
			return result;
		}

		public void OnCompleted(Action continuation)
		{
			Continuation = continuation;
			if (IsCompleted) {
				CallContinuation();
			}
		}

		public void SetResult(T result)
		{
			this.result = result;
			SetResult();
		}
	}

	public static class CoroutineExtensions
	{
		public static IEnumerator<object> ToLimeTask(this Coroutine coroutine)
		{
			try {
				try {
					while (!coroutine.IsCompleted) {
						coroutine.Update(Task.Current.Delta);
						yield return null;
					}
				} finally {
					coroutine.ThrowExceptionIfRequired();
				}
			} finally {
				coroutine.Dispose();
			}
		}

		public static Coroutine ToCoroutine(this IEnumerator<object> enumerator) => new LimeTaskCoroutine(new Task(enumerator));
	}

	public class DelayCoroutine : Coroutine
	{
		private float waitTime;

		public DelayCoroutine(float waitTime)
		{
			this.waitTime = waitTime;
		}

		public override bool IsCompleted => waitTime <= 0 || Exception != null;

		protected override void InternalUpdate(float delta)
		{
			waitTime -= delta;
			base.InternalUpdate(delta);
		}
	}

	public class WaitNextFrameCoroutine : Coroutine
	{
		private bool completed;

		public override bool IsCompleted => completed;

		protected override void InternalUpdate(float delta)
		{
			completed = true;
			base.InternalUpdate(delta);
		}
	}

	public class WaitPredicateCoroutine : Coroutine
	{
		private WaitPredicate predicate;

		public WaitPredicateCoroutine(WaitPredicate predicate)
		{
			this.predicate = predicate;
		}

		public override bool IsCompleted => !predicate.Evaluate();

		protected override void InternalUpdate(float delta)
		{
			predicate.TotalTime += delta;
			base.InternalUpdate(delta);
		}

		public override void Dispose()
		{
			if (Disposed) {
				return;
			}
			predicate = null;
			base.Dispose();
		}
	}

	public class LimeTaskCoroutine : Coroutine
	{
		private Task task;

		public LimeTaskCoroutine(Task task)
		{
			this.task = task;
		}

		public override bool IsCompleted => task.Completed;

		protected override void InternalUpdate(float delta)
		{
			task.Advance(delta);
			base.InternalUpdate(delta);
		}

		public override void Dispose()
		{
			if (Disposed) {
				return;
			}
			task?.Dispose();
			task = null;
			base.Dispose();
		}
	}

	public class ExceptionHolder
	{
		private readonly ExceptionDispatchInfo exception;
		private bool calledGet;

		public ExceptionHolder(ExceptionDispatchInfo exception)
		{
			this.exception = exception;
		}

		public ExceptionDispatchInfo GetException()
		{
			if (!calledGet) {
				calledGet = true;
				GC.SuppressFinalize(this);
			}
			return exception;
		}

		~ExceptionHolder()
		{
			if (!calledGet) {
				Logger.Instance.Error($@"Unobserved coroutine exception: {exception.SourceException}");
			}
		}
	}

	public sealed class CoroutineMethodBuilder
	{
		public Coroutine Task { get; }

		public CoroutineMethodBuilder()
		{
			Task = new Coroutine();
		}

		public static CoroutineMethodBuilder Create() => new CoroutineMethodBuilder();

		public void SetResult() => Task.SetResult();

		public void Start<TStateMachine>(ref TStateMachine stateMachine) where TStateMachine : IAsyncStateMachine => stateMachine.MoveNext();

		// AwaitOnCompleted, AwaitUnsafeOnCompleted, SetException
		// and SetStateMachine are empty
		public void AwaitOnCompleted<TAwaiter, TStateMachine>(ref TAwaiter awaiter, ref TStateMachine stateMachine)
			where TAwaiter : INotifyCompletion where TStateMachine : IAsyncStateMachine
		{
			Task.SetChild(awaiter as CoroutineBase);
			awaiter.OnCompleted(stateMachine.MoveNext);
		}

		public void AwaitUnsafeOnCompleted<TAwaiter, TStateMachine>(ref TAwaiter awaiter, ref TStateMachine stateMachine)
			where TAwaiter : ICriticalNotifyCompletion where TStateMachine : IAsyncStateMachine
		{
		}

		public void SetException(Exception e) => Task.SetException(e);

		public void SetStateMachine(IAsyncStateMachine stateMachine) { }
	}

	public sealed class CoroutineMethodBuilder<T>
	{
		public Coroutine<T> Task { get; }

		public CoroutineMethodBuilder()
		{
			Task = new Coroutine<T>();
		}

		public static CoroutineMethodBuilder<T> Create() => new CoroutineMethodBuilder<T>();

		public void SetResult(T result) => Task.SetResult(result);

		public void Start<TStateMachine>(ref TStateMachine stateMachine) where TStateMachine : IAsyncStateMachine => stateMachine.MoveNext();

		// AwaitOnCompleted, AwaitUnsafeOnCompleted, SetException
		// and SetStateMachine are empty
		public void AwaitOnCompleted<TAwaiter, TStateMachine>(ref TAwaiter awaiter, ref TStateMachine stateMachine)
			where TAwaiter : INotifyCompletion where TStateMachine : IAsyncStateMachine
		{
			Task.SetChild(awaiter as CoroutineBase);
			awaiter.OnCompleted(stateMachine.MoveNext);
		}

		public void AwaitUnsafeOnCompleted<TAwaiter, TStateMachine>(ref TAwaiter awaiter, ref TStateMachine stateMachine)
			where TAwaiter : ICriticalNotifyCompletion where TStateMachine : IAsyncStateMachine
		{
		}

		public void SetException(Exception e) => Task.SetException(e);

		public void SetStateMachine(IAsyncStateMachine stateMachine) { }
	}
}
