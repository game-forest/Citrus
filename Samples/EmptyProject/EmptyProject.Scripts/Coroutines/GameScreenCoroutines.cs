using EmptyProject.Dialogs;
using EmptyProject.Scripts.Common;

namespace EmptyProject.Scripts.Coroutines
{
	public class GameScreenCoroutines
	{
		public static async Coroutine Close(GameScreen dialog = null, WaitDialogTaskParameters parameters = null)
		{
			if (dialog == null) {
				dialog = await Command.WaitDialog<GameScreen>(parameters);
				if (dialog == null) {
					return;
				}
			}

			await Command.Click("@BtnExit", dialog.Root);
			await ConfirmationCoroutines.Confirm();
			await Command.WaitWhileDialogOnScreen(dialog);
		}
	}
}
