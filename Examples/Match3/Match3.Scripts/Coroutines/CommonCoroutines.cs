using Match3.Dialogs;
using Match3.Scripts.Common;

namespace Match3.Scripts.Coroutines
{
	public static class CommonCoroutines
	{
		public static async Coroutine CloseDialog<T>(T dialog = null, WaitDialogTaskParameters parameters = null, string closeButtonName = "BtnClose") where T : Dialog
		{
			if (dialog == null) {
				dialog = await Command.WaitDialog<T>(parameters);
				if (dialog == null) {
					return;
				}
			}

			await Command.Click(closeButtonName, dialog.Root);
			await Command.WaitWhileDialogOnScreen(dialog);
		}
	}
}
