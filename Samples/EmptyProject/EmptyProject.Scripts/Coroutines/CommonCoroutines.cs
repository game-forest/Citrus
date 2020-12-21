using EmptyProject.Dialogs;
using EmptyProject.Scripts.Common;

namespace EmptyProject.Scripts.Coroutines
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
