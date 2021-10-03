using EmptyProject.Dialogs;
using EmptyProject.Scripts.Common;

namespace EmptyProject.Scripts.Coroutines
{
	public class OptionsCoroutines
	{
		public static async Coroutine Close(Options dialog = null, WaitDialogTaskParameters parameters = null) =>
			await CommonCoroutines.CloseDialog(dialog, parameters, "@BtnOk");
	}
}
