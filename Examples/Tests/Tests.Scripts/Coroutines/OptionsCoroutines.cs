using Tests.Dialogs;
using Tests.Scripts.Common;

namespace Tests.Scripts.Coroutines
{
	public class OptionsCoroutines
	{
		public static async Coroutine Close(Options dialog = null, WaitDialogTaskParameters parameters = null) =>
			await CommonCoroutines.CloseDialog(dialog, parameters, "@BtnOk");
	}
}
