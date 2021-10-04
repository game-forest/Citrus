using Lime;

namespace Match3.Dialogs
{
	[ScenePath("Shell/AlertDialog")]
	public class AlertDialog : Dialog
	{
		public AlertDialog(string text)
		{
			Root["Title"].Text = text;
			Root["BtnOk"].Clicked = Close;
		}
	}
}
