using System;
using System.Collections.Generic;
using static Lime.Mathf;

namespace Lime
{
	public enum DragDirection
	{
		Any,
		Horizontal,
		Vertical
	}

	public class DragGesture : Gesture
	{
		enum State
		{
			Idle,
			Recognizing,
			Changing,
			ChangingByMotion,
		}

		protected const float DefaultDragThreshold = 5;

		private State state;
		private readonly IMotionStrategy motionStrategy;
		private const int MaxTouchHistorySize = 3;
		private readonly Queue<(Vector2 Distance, float Duration)> touchHistory;
		private Vector2 previousMotionStrategyPosition;
		private float motionStrategyStartTime;
		private Vector2 lastDragDelta;

		public int ButtonIndex { get; }
		public DragDirection Direction { get; private set; }
		public float DragThreshold { get; set; }
		// Will cancel any other drag gestures
		public bool Exclusive { get; set; }

		public Vector2 MousePressPosition { get; private set; }
		/// <summary>
		/// MousePosition depends on DragDirection so (MousePosition - DragDirection).X or Y
		/// will always be zero if DragDirection is Vertical or Horizontal respectively.
		/// This fact is used when checking for threshold.
		/// </summary>
		public virtual Vector2 MousePosition => state == State.ChangingByMotion ? motionStrategy.Position : ClampMousePositionByDirection(Direction);

		public Vector2 TotalDragDistance => MousePosition - MousePressPosition;
		public Vector2 LastDragDistance => MousePosition - PreviousMousePosition;
		private float previousTime;
		private Vector2 previousMousePosition;
		protected virtual Vector2 PreviousMousePosition => state == State.ChangingByMotion ? previousMotionStrategyPosition : previousMousePosition;


		private Vector2 ClampMousePositionByDirection(DragDirection direction)
		{
			if (!TryGetDragPosition(out var position)) {
				return Vector2.NaN;
			}

			switch (direction) {
				case DragDirection.Horizontal:
					return new Vector2(position.X, MousePressPosition.Y);
				case DragDirection.Vertical:
					return new Vector2(MousePressPosition.X, position.Y);
				case DragDirection.Any:
					return position;
				default:
					throw new ArgumentOutOfRangeException();
			}
		}

		protected virtual bool TryGetStartDragPosition(out Vector2 position)
		{
			if (Input.WasMousePressed(ButtonIndex)) {
				return TryGetDragPosition(out position);
			}
			position = Vector2.NaN;
			return false;
		}
		protected virtual bool TryGetDragPosition(out Vector2 position)
		{
			position = Input.MousePosition;
			return true;
		}

		protected virtual bool IsDragging() => Input.IsMousePressed(ButtonIndex);
		protected virtual bool CanStartDrag() => (MousePosition - MousePressPosition).SqrLength > DragThreshold.Sqr();

		private PollableEvent began;
		private PollableEvent recognized;
		protected PollableEvent changed;
		private PollableEvent ending;
		private PollableEvent ended;

		/// <summary>
		/// Occurs when gesture activates and starts recognition.
		/// </summary>
		public event Action Began
		{
			add { began.Handler += value; }
			remove { began.Handler -= value; }
		}
		/// <summary>
		/// Occurs if a user started gesture in a valid direction.
		/// </summary>
		public event Action Recognized
		{
			add { recognized.Handler += value; }
			remove { recognized.Handler -= value; }
		}
		/// <summary>
		/// Occurs when the drag is completed and the motion defined by motion strategy begins.
		/// </summary>
		public virtual event Action Ending
		{
			add => ending.Handler += value;
			remove => ending.Handler -= value;
		}
		/// <summary>
		/// Occurs when drag position is being changed by either user or motion strategy.
		/// </summary>
		public event Action Changed
		{
			add { changed.Handler += value; }
			remove { changed.Handler -= value; }
		}
		/// <summary>
		/// Occurs when either a user released input or motion strategy is done.
		/// </summary>
		public event Action Ended
		{
			add { ended.Handler += value; }
			remove { ended.Handler -= value; }
		}

		public bool WasBegan() => began.HasOccurred;
		public bool WasRecognized() => recognized.HasOccurred;
		public bool WasChanged() => changed.HasOccurred;
		public bool WasEnding() => ending.HasOccurred;
		public bool WasEnded() => ended.HasOccurred;

		public bool IsRecognizing() => state == State.Recognizing;
		public bool IsChanging() => state == State.Changing || state == State.ChangingByMotion;

		internal bool IsChangingByMotion() => state == State.ChangingByMotion;

		public DragGesture(
			int buttonIndex = 0,
			DragDirection direction = DragDirection.Any,
			bool exclusive = false,
			float dragThreshold = DefaultDragThreshold,
			IMotionStrategy motionStrategy = null
		)
			: this(motionStrategy)
		{
			ButtonIndex = buttonIndex;
			Direction = direction;
			DragThreshold = dragThreshold;
			Exclusive = exclusive;
		}

		public DragGesture(IMotionStrategy motionStrategy)
		{
			this.motionStrategy = motionStrategy;
			if (motionStrategy != null) {
				touchHistory = new Queue<(Vector2 distance, float duration)>();
			}
		}

		internal protected override void OnCancel(Gesture sender)
		{
			if (sender != null || state != State.ChangingByMotion) {
				if (state == State.Changing || state == State.ChangingByMotion) {
					ended.Raise();
				}
				state = State.Idle;
			}
		}

		internal protected override bool OnUpdate()
		{
			var result = false;
			if (state == State.ChangingByMotion) {
				if (Input.WasMousePressed(ButtonIndex) && Owner.IsMouseOverThisOrDescendant()) {
					state = State.Idle;
					ended.Raise();
				} else {
					previousMotionStrategyPosition = motionStrategy.Position;
					motionStrategy.Update(Min(WidgetContext.Current.GestureManager.AccumulatedDelta - motionStrategyStartTime, motionStrategy.Duration));
					changed.Raise();
					if ((WidgetContext.Current.GestureManager.AccumulatedDelta - motionStrategyStartTime) > motionStrategy.Duration) {
						state = State.Idle;
						ended.Raise();
					}
				}
			}
			var savedPreviousMousePosition = previousMousePosition;
			bool wasChanging = state == State.Changing;
			if (state == State.Idle && TryGetStartDragPosition(out var startPosition)) {
				state = State.Recognizing;
				savedPreviousMousePosition = previousMousePosition = MousePressPosition = startPosition;
				began.Raise();
			}
			if (state == State.Recognizing) {
				if (!Input.IsMousePressed(ButtonIndex)) {
					ended.Raise();
					state = State.Idle;
				} else if (CanStartDrag()) {
					state = State.Changing;
					if (TryGetDragPosition(out var mousePosition)) {
						previousMousePosition = mousePosition;
					}
					previousTime = WidgetContext.Current.GestureManager.AccumulatedDelta;
					recognized.Raise();
					result = true;
				}
			}
			if (state == State.Changing) {
				if (
					TryGetDragPosition(out var mousePosition) &&
					(previousMousePosition != mousePosition)
				) {
					changed.Raise();
					previousMousePosition = mousePosition;
				}
				if (!IsDragging()) {
					if (motionStrategy != null) {
						ending.Raise();
					} else {
						ended.Raise();
					}
					state = State.Idle;
				}
			}
			if (motionStrategy != null && wasChanging) {
				if (WasEnding()) {
					if (motionStrategy.Start(ClampMousePositionByDirection(Direction), touchHistory)) {
						touchHistory.Clear();
						motionStrategyStartTime = WidgetContext.Current.GestureManager.AccumulatedDelta;
						state = State.ChangingByMotion;
					} else {
						ended.Raise();
					}
				} else {
					lastDragDelta = ClampMousePositionByDirection(Direction) - savedPreviousMousePosition;
					touchHistory.Enqueue((lastDragDelta, WidgetContext.Current.GestureManager.AccumulatedDelta - previousTime));
					if (touchHistory.Count > MaxTouchHistorySize) {
						touchHistory.Dequeue();
					}
				}
			}
			return result;
		}


		/// <summary>
		/// Defines a motion strategy.
		/// </summary>
		public interface IMotionStrategy
		{
			Vector2 Position { get; }
			float Duration { get; }
			/// <summary>
			/// Start position calculation.
			/// </summary>
			/// <param name="startPosition">The position motion should begin at.</param>
			/// <param name="touchHistory">Sequence of distance and duration pairs for a couple of last frames.</param>
			bool Start(Vector2 startPosition, IEnumerable<(Vector2 Distance, float Duration)> touchHistory);

			/// <summary>
			/// Recalculates position.
			/// </summary>
			/// <param name="time">Time elapsed from the start.</param>
			void Update(float time);
		}

		/// <summary>
		/// Motion damping based on Gauss error function. Duration is calculated so motion lasts until min speed reached.
		/// </summary>
		public class DampingMotionStrategy : IMotionStrategy
		{
			private readonly float minSpeed;
			private readonly float maxSpeed;
			private Vector2 direction;
			private float speed;
			private float initialSpeed;
			private Vector2 initialPosition;
			private float k1;
			private float k2;
			private float k3;
			private float p0;
			private readonly float dampingFactor1;
			private readonly float dampingFactor2;
			public Vector2 Position { get; private set; }
			public float Duration { get; private set; }

			/// <param name="dampingFactor1">Initial speed damping factor. Should be in range (0; 1].
			/// The lower the dampingFactor2, the lower damingFactor1 can be. Otherwise it may result in overflow.</param>
			/// <param name="dampingFactor2">Damping factor applied to speed damping factor each frame. Valid range is (0; 1).</param>
			/// <param name="minSpeed">The minimum speed that is considered a full stop.</param>
			/// <param name="maxStartSpeed">Maximum initial speed of movement.</param>
			public DampingMotionStrategy(
				float dampingFactor1,
				float dampingFactor2,
				float minSpeed = 25.0f,
				float maxStartSpeed = float.PositiveInfinity)
			{
				this.dampingFactor1 = dampingFactor1;
				this.dampingFactor2 = dampingFactor2;
				this.minSpeed = minSpeed;
				maxSpeed = maxStartSpeed;
			}

			public bool Start(Vector2 startPosition, IEnumerable<(Vector2 Distance, float Duration)> touchHistory)
			{
				direction = Vector2.Zero;
				float totalDuration = 0.0f;
				foreach (var (distance, duration) in touchHistory) {
					direction += distance;
					totalDuration += duration;
				}
				if (direction.Length == 0.0f) {
					return false;
				}
				if (totalDuration > 0.0f) {
					speed = direction.Length / totalDuration;
					direction = direction.Normalized;
					speed = Min(speed, maxSpeed);
					Position = initialPosition = startPosition;
					initialSpeed = speed;
					(k1, k2, k3) = CalculateErfFactors(dampingFactor1, dampingFactor2);
					// check if max possible speed is lower than min speed. -k3/k2 is the root of V'
					Duration = SpeedOverTime(-k3 / k2, k1 * initialSpeed, k2, k3) < minSpeed
						? 0.0f
						: CalcDurationUntilMinSpeedReached(minSpeed, k1 * initialSpeed, k2, k3);
					p0 = PositionOverTime(0, k1 * initialSpeed, k2, k3);
				}
				return true;
			}

			public void Update(float time) =>
				Position = initialPosition + direction * (PositionOverTime(time, initialSpeed * k1, k2, k3) - p0);

			// See https://www.desmos.com/calculator/imdpwcwylb for graphs with sliders
			private static float PositionOverTime(float t, float a1, float a2, float a3) => a1 * Erf(a2 * t + a3);

			private static float SpeedOverTime(float t, float a1, float a2, float a3) => 2 * a1 * a2 * Exp(-(a2 * t + a3).Sqr()) / Sqrt(Pi);

			// Solves SpeedOverTime = targetSpeed for time
			private static float CalcDurationUntilMinSpeedReached(float targetSpeed, float a1, float a2, float a3) =>
				(Sqrt(-Log(Sqrt(Pi) * targetSpeed / (2.0f * a1 * a2))) - a3) / a2;

			private static (float k1, float k2, float k3) CalculateErfFactors(float d1, float d2)
			{
				var ld1 = Log(d1);
				var ld2 = Log(d2);
				var is2 = 1.0f / Sqrt(2.0f);
				var fps = 60.0f;
				var smld2 = Sqrt(-ld2);
				var k1 = is2 * Sqrt(Pi) * Exp(-(2.0f * ld1 - ld2).Sqr() / (8.0f * ld2)) / (smld2 * fps);
				var k2 = fps * is2 * smld2;
				var k3 = -(2.0f * ld1 - ld2) / (Pow(2.0f, 3.0f / 2.0f) * smld2);
				return (k1, k2, k3);
			}
		}
	}
}
