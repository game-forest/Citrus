using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;

namespace Orange
{
	static class DeleteUnpackedBundles
	{
		[Export(nameof(OrangePlugin.MenuItems))]
		[ExportMetadata("Label", "Delete Unpacked Bundles")]
		[ExportMetadata("Priority", 31)]
		[ExportMetadata("ApplicableToBundleSubset", true)]
		public static void DeleteUnpackedBundlesAction() => DeleteBundles(The.UI.GetActiveTarget(), The.UI.GetSelectedBundles());

		private static void DeleteBundles(Target target, List<string> bundles = null)
		{
			if (bundles == null) {
				bundles = new AssetCooker(target).GetListOfAllBundles();
			}
			The.UI.SetupProgressBar(bundles.Count);
			foreach (var bundleName in bundles) {
				var bundlePath = The.Workspace.GetBundlePath(target.Platform, bundleName) + ".Unpacked";
				DeleteBundle(bundlePath);
				The.UI.IncreaseProgressBar();
			}
			The.UI.StopProgressBar();
		}

		private static void DeleteBundle(string bundlePath)
		{
			if (!Directory.Exists(bundlePath)) {
				Console.WriteLine($"WARNING: {bundlePath} do not exists! Skipping...");
				return;
			}
			try {
				Directory.Delete(bundlePath, true);
				Console.WriteLine($"{bundlePath} deleted.");
			} catch (Exception exception) {
				Console.WriteLine($"{bundlePath} deletion skipped because of exception: {exception.Message}");
			}
		}
	}
}
