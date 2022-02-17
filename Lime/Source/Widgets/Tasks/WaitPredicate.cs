using System;

namespace Lime
{
	public abstract class WaitPredicate
	{
		public float TotalTime;
		public abstract bool Evaluate();
	}

	internal class AnimationWaitPredicate : WaitPredicate
	{
		private Animation animation;

		public AnimationWaitPredicate(Animation animation) { this.animation = animation; }
		public override bool Evaluate() { return animation.IsRunning; }
	}

	internal class InputWaitPredicate : WaitPredicate
	{
		public static readonly InputWaitPredicate Instance = new InputWaitPredicate();

		private InputWaitPredicate() { }
		public override bool Evaluate() { return !Window.Current.Input.Changed; }
	}

	internal class BooleanWaitPredicate : WaitPredicate
	{
		private Func<bool> predicate;

		public BooleanWaitPredicate(Func<bool> predicate) { this.predicate = predicate; }
		public override bool Evaluate() { return predicate(); }
	}

	internal class TimeWaitPredicate : WaitPredicate
	{
		private Func<float, bool> predicate;

		public TimeWaitPredicate(Func<float, bool> predicate) { this.predicate = predicate; }
		public override bool Evaluate() { return predicate(TotalTime); }
	}
}
