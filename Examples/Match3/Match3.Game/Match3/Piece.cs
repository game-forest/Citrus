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
			Task = Task.Sequence(task, ClearTaskTask());
			Owner.Tasks.Add(Task);
		}

		private IEnumerator<object> ClearTaskTask()
		{
			Task = null;
			yield break;
		}

		public IntVector2 GridPosition
		{
			get => gridPosition;
			set
			{
				onSetGridPosition.Invoke(this, value);
				this.gridPosition = value;
				Widget.Position = ((Vector2)GridPosition + Vector2.Half) * Match3Config.CellSize;
			}
		}

		private Action<Piece, IntVector2> onSetGridPosition;

		IntVector2 gridPosition = new IntVector2(int.MinValue, int.MinValue);
		private int kind;

		public Piece(Node pieceWidget, IntVector2 gridPosition, int kind, Action<Piece, IntVector2> onSetGridPosition)
		{
			pieceWidget.Components.Add(this);
			this.onSetGridPosition = onSetGridPosition;
			GridPosition = gridPosition;
			var kindAnimation = Owner.Animations.Find("Kind");
			var marker = kindAnimation.Markers[kind];
			Owner.RunAnimation(marker.Id, kindAnimation.Id);
			this.kind = kind;
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

