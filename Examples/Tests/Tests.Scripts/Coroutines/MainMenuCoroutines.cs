using Tests.Dialogs;
using Tests.Scripts.Common;

namespace Tests.Scripts.Coroutines
{
	public class MainMenuCoroutines
	{
		public static async Coroutine<GameScreen> OpenGameScreen(MainMenu dialog = null, WaitDialogTaskParameters parameters = null)
		{
			await CommonCoroutines.CloseDialog(dialog, closeButtonName: "@BtnPlay");
			return await Command.WaitDialog<GameScreen>();
		}

		public static async Coroutine<Options> OpenOptions(MainMenu dialog = null, WaitDialogTaskParameters parameters = null)
		{
			if (dialog == null) {
				dialog = await Command.WaitDialog<MainMenu>(parameters);
				if (dialog == null) {
					return null;
				}
			}

			await Command.Click("@BtnOptions", dialog.Root);
			return await Command.WaitDialog<Options>();
		}
	}
}
