using Lime;

namespace Tests.Dialogs
{
	public class AlertDialog : Dialog<Scenes.Data.AlertDialog>
	{
		public AlertDialog(string text)
		{
			var label = Scene._Title.It;
			label.OverflowMode = TextOverflowMode.Minify;
			label.Text = text;
			Scene._BtnOk.It.Clicked = Close;
		}
	}
}
