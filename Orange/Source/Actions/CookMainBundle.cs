using System.ComponentModel.Composition;
using System.Collections.Generic;

namespace Orange
{
	static partial class CookMainBundle
	{
		[Export(nameof(OrangePlugin.MenuItems))]
		[ExportMetadata("Label", "Cook Main Bundle")]
		[ExportMetadata("Priority", 4)]
		public static void CookMainBundleAction()
		{
			var target = The.UI.GetActiveTarget();

			AssetCooker.CookForTarget(
				target,
				new List<string>() { CookingRulesBuilder.MainBundleName }
			);
		}
	}
}
