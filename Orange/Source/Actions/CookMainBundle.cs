using System.ComponentModel.Composition;
using System.Collections.Generic;

namespace Orange
{
	static partial class CookMainBundle
	{
		[Export(nameof(OrangePlugin.MenuItemsWithErrorDetails))]
		[ExportMetadata("Label", "Cook Main Bundle")]
		[ExportMetadata("Priority", 4)]
		public static string CookMainBundleAction()
		{
			var target = The.UI.GetActiveTarget();

			if (!AssetCooker.CookForTarget(
				target,
				new List<string>() {CookingRulesBuilder.MainBundleName}, out string errorMessage
			)) {
				return errorMessage;
			}
			return null;
		}
	}
}
