using Match3.Dialogs;
using Match3.Scripts.Common;

namespace Match3.Scripts.Coroutines
{
	public class OptionsCoroutines
	{
		public static async Coroutine Close(Options dialog = null, WaitDialogTaskParameters parameters = null) =>
			await CommonCoroutines.CloseDialog(dialog, parameters, "@BtnOk");
	}
}
