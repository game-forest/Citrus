using System;
using System.Collections.Generic;

namespace Lime
{
	public class DoubleClickGesture : Gesture
	{
		enum State
		{
			Idle,
			FirstPress,
			WaitSecondPress,
		};

		private readonly float MaxDelayBetweenClicks =
#if WIN
			(float)TimeSpan.FromMilliseconds(System.Windows.Forms.SystemInformation.DoubleClickTime).TotalSeconds;
#else // WIN
			0.3f;
#endif // WIN

		private readonly Vector2 DoubleClickThreshold =
#if WIN
			new Vector2(
				System.Windows.Forms.SystemInformation.DoubleClickSize.Width,
				System.Windows.Forms.SystemInformation.DoubleClickSize.Height
			) / 2f;
#else // WIN
			new Vector2(5f, 5f) / 2f;
#endif // WIN

		private State state;
		private float firstPressTime;
		private Vector2 firstPressPosition;

		public int ButtonIndex { get; }

		public event Action Recognized
		{
			add => recognized.Handler += value;
			remove => recognized.Handler -= value;
		}

		private PollableEvent recognized;
		public bool WasRecognized() => recognized.HasOccurred;

		public DoubleClickGesture(int buttonIndex, Action recognized = null)
		{
			if (recognized != null) {
				Recognized += recognized;
			}
			ButtonIndex = buttonIndex;
		}

		public DoubleClickGesture(Action recognized = null)
			: this(0, recognized)
		{ }

		internal protected override bool OnUpdate()
		{
			bool result = false;

			if (state != State.Idle && (WidgetContext.Current.GestureManager.AccumulatedDelta - firstPressTime) > MaxDelayBetweenClicks) {
				state = State.Idle;
			}
			if (state == State.Idle && Input.WasMousePressed(ButtonIndex)) {
				firstPressTime = WidgetContext.Current.GestureManager.AccumulatedDelta;
				firstPressPosition = Input.MousePosition;
				state = State.FirstPress;
			}
			if (state == State.FirstPress && !Input.IsMousePressed(ButtonIndex)) {
				state = State.WaitSecondPress;
			}
			if (state == State.WaitSecondPress && Input.WasMousePressed(ButtonIndex)) {
				state = State.Idle;
				if (Input.GetNumTouches() == 1 && IsCloseToFirstPressPosition(Input.MousePosition)) {
					recognized.Raise();
					result = true;
				}
			}

			return result;

			bool IsCloseToFirstPressPosition(Vector2 mousePosition)
			{
				return new Rectangle(
					firstPressPosition - DoubleClickThreshold,
					firstPressPosition + DoubleClickThreshold
				).Contains(mousePosition);
			}
		}
	}
}
