using System;
using ProtoBuf;

namespace Lime
{
	[ProtoContract]
	public class Button : Widget
	{
		SimpleText textPresenter;

		[ProtoMember(1)]
		public string Caption { get; set; }

		public event EventHandler<EventArgs> OnClick;

		void UpdateHelper (int delta)
		{
			if (textPresenter == null) {
				textPresenter = Find<SimpleText> ("TextPresenter", false);
			}
			if (textPresenter != null) {
				textPresenter.Text = Caption;
			}
			if (HitTest (Input.MousePosition)) {
				if (Widget.InputFocus == null) {
					PlayAnimation ("Focus");
					Widget.InputFocus = this;
				}
			} else {
				if (Widget.InputFocus == this) {
					PlayAnimation ("Normal");
					Widget.InputFocus = null;
				}
			}
			if (Widget.InputFocus == this) {
				if (Input.GetKeyDown (Key.Mouse0)) {
					PlayAnimation ("Press");
					Input.ConsumeKeyEvent (Key.Mouse0, true);
				}
				if (Input.GetKeyUp (Key.Mouse0)) {
					if (HitTest (Input.MousePosition))
						PlayAnimation ("Focus");
					else
						PlayAnimation ("Normal");
					Input.ConsumeKeyEvent (Key.Mouse0, true);
					if (OnClick != null) {
						OnClick (this, null);
					}
				}
			}
			if (Widget.InputFocus != this && CurrentAnimation != "Normal") {
				PlayAnimation ("Normal");
			}
		}

		public override void Update (int delta)
		{
			if (worldShown) {
				UpdateHelper (delta);
			}
			base.Update (delta);
		}
	}
}
