using System.ComponentModel.Composition;

namespace Orange
{
	static partial class CookGameAssets
	{
		[Export(nameof(OrangePlugin.MenuItemsWithErrorDetails))]
		[ExportMetadata("Label", "Cook Game Assets")]
		[ExportMetadata("Priority", 4)]
		[ExportMetadata("ApplicableToBundleSubset", true)]
		public static string CookGameAssetsAction()
		{
			var target = The.UI.GetActiveTarget();

			if (!AssetCooker.CookForTarget(target, The.UI.GetSelectedBundles(), out string errorMessage)) {
				return errorMessage;
			}
			return null;
		}
	}
}
