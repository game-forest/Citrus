using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;

namespace Lime
{
	public struct TapGestureOptions
	{
		public int ButtonIndex;
		public float MinTapDuration;
		public bool CanRecognizeByTapDuration;
	}

	public abstract class TapGesture : Gesture
	{
		enum State
		{
			Idle,
			Scheduled,
			Began
		};

		private readonly float ClickBeginDelay = 0.064f;
		private readonly TapGestureOptions options;

		private State state;
		private float tapDuration;
		private PollableEvent began;
		private PollableEvent canceled;
		private PollableEvent recognized;

		internal Action InternalRecognized;
		public static float Threshold = 3;
		internal bool Deferred;

		/// <summary>
		/// Occurs if a user has touched upon the widget.
		/// If the widget lies within a scrollable panel,
		/// the began event might be deferred in order to give the priority to drag gesture.
		/// </summary>
		public event Action Began { add { began.Handler += value; } remove { began.Handler -= value; } }
		/// <summary>
		/// Occurs if click was canceled by drag gesture.
		/// </summary>
		public event Action Canceled { add { canceled.Handler += value; } remove { canceled.Handler -= value; } }
		/// <summary>
		/// Occurs when the gesture is fully recognized.
		/// </summary>
		public event Action Recognized { add { recognized.Handler += value; } remove { recognized.Handler -= value; } }

		public Vector2 MousePressPosition { get; private set; }

		public bool WasBegan() => began.HasOccurred;
		public bool WasCanceled() => canceled.HasOccurred;
		public bool WasRecognized() => recognized.HasOccurred;
		public bool WasRecognizedOrCanceled() => WasCanceled() || WasRecognized();

		public int ButtonIndex => options.ButtonIndex;

		public TapGesture(Action onRecognized, TapGestureOptions options)
		{
			this.options = options;
			if (onRecognized != null) {
				Recognized += onRecognized;
			}
		}

		internal protected override void OnCancel()
		{
			if (state == State.Began) {
				canceled.Raise();
			}
			state = State.Idle;
		}

		internal protected override bool OnUpdate(float delta)
		{
			if (Input.GetNumTouches() > 1) {
				OnCancel();
				return false;
			}
			bool result = false;
			if (state == State.Idle && Input.WasMousePressed(ButtonIndex)) {
				tapDuration = 0.0f;
				MousePressPosition = Input.MousePosition;
				state = State.Scheduled;
			}
			tapDuration += delta;
			if (state == State.Scheduled) {
				// Defer began event if there are any drag gesture.
				if (
					!Deferred ||
					tapDuration > ClickBeginDelay ||
					!Input.IsMousePressed(ButtonIndex)
				) {
					state = State.Began;
					began.Raise();
				}
			}
			if (state == State.Began) {
				bool isTapTooShort = tapDuration < options.MinTapDuration;
				if (!Input.IsMousePressed(ButtonIndex)) {
					state = State.Idle;
					if (!isTapTooShort) {
						result = Finish();
					} else {
						state = State.Idle;
						canceled.Raise();
					}
				} else if (options.CanRecognizeByTapDuration && !isTapTooShort) {
					result = Finish();
				}
			}
			return result;
		}

		private bool Finish()
		{
			if ((Input.MousePosition - MousePressPosition).SqrLength < Threshold.Sqr() ||
			    Owner.IsMouseOverThisOrDescendant()
			) {
				InternalRecognized?.Invoke();
				recognized.Raise();
				return true;
			} else {
				canceled.Raise();
				return false;
			}
		}
	}
}
