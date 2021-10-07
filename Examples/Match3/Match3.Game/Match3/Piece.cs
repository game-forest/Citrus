using Lime;
using System;
using System.Collections.Generic;

namespace Match3
{
	public class Piece : WidgetBehaviorComponent
	{
		public Piece(Widget pieceWidget)
		{
			pieceWidget.Components.Add(this);
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

