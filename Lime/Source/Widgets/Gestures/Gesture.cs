using System;

namespace Lime
{
	public abstract class Gesture
	{
		public Node Owner { get; internal set; }
		protected WindowInput Input => CommonWindow.Current.Input;

		internal protected virtual void OnCancel(Gesture sender)
		{ }

		internal protected abstract bool OnUpdate();

		protected struct PollableEvent
		{
			private int occurredOnIteration;
			public event Action Handler;
			private int CurrentIteration => WidgetContext.Current.GestureManager.CurrentIteration;
			public bool HasOccurred => occurredOnIteration == CurrentIteration;
			public void Raise()
			{
				CommonWindow.Current.Invalidate();
				occurredOnIteration = CurrentIteration;
				Handler?.Invoke();
			}
		}
	}
}
