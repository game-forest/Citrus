using System;
using Lime;

namespace Match3.Dialogs
{
	[ScenePath("Shell/Confirmation")]
	public class Confirmation : Dialog
	{
		public event Action OkClicked;

		public Confirmation(string text, bool cancelButtonVisible = true)
		{
			Root["Title"].Text = text;
			var cancelButton = Root["BtnCancel"];
			cancelButton.Visible = cancelButtonVisible;
			cancelButton.Clicked = Close;
			var okButton = Root["BtnOk"];
			okButton.Clicked = () => {
				OkClicked?.Invoke();
				Close();
			};
		}

		protected override void Closing()
		{
			OkClicked = null;
		}
	}
}
