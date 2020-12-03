using System.ComponentModel.Composition;

namespace Orange
{
	public static class CustomActions
	{
		[Export(nameof(OrangePlugin.MenuItemsWithErrorDetails))]
		[ExportMetadata("Label", "Build")]
		[ExportMetadata("Priority", 0)]
		public static string BuildAndRunAction()
		{
			var target = The.UI.GetActiveTarget();

			if (!AssetCooker.CookForTarget(target, null, out string errorMessage)) {
				return errorMessage;
			}
			if (!Actions.BuildGame(target)) {
				return "Can not BuildGame";
			}
			return null;
		}
	}
}
