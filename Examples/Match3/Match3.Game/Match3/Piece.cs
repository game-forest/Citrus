using Lime;
using System;
using System.Collections.Generic;

namespace Match3
{
	public class Piece : WidgetBehaviorComponent
	{
		public Task Task { get; private set; }

		public void RunTask(IEnumerator<object> task)
		{
			System.Diagnostics.Debug.Assert(Task == null);
			Owner.CompoundPostPresenter.Add(hasTaskPresenter);
			Task = Task.Sequence(task, ClearTaskTask());
			Owner.Tasks.Add(Task);
		}

		private IEnumerator<object> ClearTaskTask()
		{
			Task = null;
			Owner.CompoundPostPresenter.Remove(hasTaskPresenter);
			yield break;
		}

		public IntVector2 GridPosition
		{
			get => gridPosition;
			set
			{
				onSetGridPosition.Invoke(this, value);
				this.gridPosition = value;
			}
		}

		private Action<Piece, IntVector2> onSetGridPosition;

		IntVector2 gridPosition = new IntVector2(int.MinValue, int.MinValue);
		private int kind;
		public int Kind => kind;
		private WidgetBoundsPresenter hasTaskPresenter;

		public IEnumerator<object> MoveTo(IntVector2 position, float time, Action<float> onStep = null)
		{
			GridPosition = position;
			var p0 = Widget.Position;
			var p1 = ((Vector2)position + Vector2.Half) * Match3Config.CellSize;
			if (time == 0.0f) {
				Widget.Position = p1;
				yield break;
			}
			var t = time;
			do {
				t -= Task.Current.Delta;
				Widget.Position = t < 0.0f ? p1 : Mathf.Lerp(1.0f - t / time, p0, p1);
				onStep?.Invoke(1.0f - (t < 0.0f ? 0.0f : t) / time);
				yield return null;
			} while (t > 0.0f);
		}

		public void ApplyAnimationPercent(float t, string animationName, string markerName)
		{
			var animation = Owner.Animations.Find(animationName);
			var markers = animation.Markers;
			var m0 = markers.Find(markerName);
			var m1 = markers[markers.IndexOf(m0) + 1];
			animation.Time = (m1.Time - m0.Time) * t + m0.Time;
			animation.ApplyAnimators();
		}

		public bool CanMatch(Piece otherPiece)
		{
			return otherPiece != null
				&& otherPiece.kind == this.kind;
		}

		public Piece(Node pieceWidget, IntVector2 gridPosition, int kind, Action<Piece, IntVector2> onSetGridPosition)
		{
			pieceWidget.Components.Add(this);
			this.onSetGridPosition = onSetGridPosition;
			GridPosition = gridPosition;
			Widget.Position = ((Vector2)gridPosition + Vector2.Half) * Match3Config.CellSize;
			var kindAnimation = Owner.Animations.Find("Kind");
			var marker = kindAnimation.Markers[kind];
			Owner.RunAnimation(marker.Id, kindAnimation.Id);
			this.kind = kind;
			hasTaskPresenter = new WidgetBoundsPresenter(Color4.Green, 2.0f);
		}

		protected override void Update(float delta)
		{
			base.Update(delta);
		}

		protected override void OnOwnerChanged(Node oldOwner)
		{
			base.OnOwnerChanged(oldOwner);
			if (Widget != null) {
				Widget.HitTestTarget = true;
			}
		}

		public Animation AnimateShow() => Owner.RunAnimation("Start", "Show");
		public Animation AnimateShown() => Owner.RunAnimation("Shown", "Show");
		public Animation AnimateDropDownFall() => Owner.RunAnimation("Fall", "DropDown");
		public Animation AnimateDropDownLand() => Owner.RunAnimation("Land", "DropDown");
		public Animation AnimateSelect() => Owner.RunAnimation("Select", "Selection");
		public Animation AnimateUnselect() => Owner.RunAnimation("Unselect", "Selection");
		public Animation AnimateMatch() => Owner.RunAnimation("Start", "Match");
	}
}

