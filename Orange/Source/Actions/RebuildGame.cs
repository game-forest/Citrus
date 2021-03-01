using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.IO;

namespace Orange
{
	static partial class Actions
	{
		[Export(nameof(OrangePlugin.MenuItemsWithErrorDetails))]
		[ExportMetadata("Label", "Rebuild Game")]
		[ExportMetadata("Priority", 2)]
		[ExportMetadata("ApplicableToBundleSubset", true)]
		[ExportMetadata("UsesTargetBundles", true)]
		public static string RebuildGameAction()
		{
			var target = The.UI.GetActiveTarget();
			if (The.UI.AskConfirmation("Are you sure you want to rebuild the game?")) {
				CleanupGame(target);
				var bundles = The.UI.GetSelectedBundles().Concat(target.Bundles).Distinct().ToList();
				if (!AssetCooker.CookForTarget(target, bundles, out string errorMessage)) {
					return errorMessage;
				}
				if (!BuildGame(target)) {
					return "Can not BuildGame";
				}
			}
			return null;
		}

		public static bool CleanupGame(Target target)
		{
			DeleteAllBundlesReferredInCookingRules(target);
			var builder = new SolutionBuilder(target);
			if (!builder.Clean()) {
				Console.WriteLine("CLEANUP FAILED");
				return false;
			}
			return true;
		}

		private static void DeleteAllBundlesReferredInCookingRules(Target target)
		{
			var bundles = Toolbox.GetListOfAllBundles(target);
			foreach (var path in bundles.Select(bundle => The.Workspace.GetBundlePath(target.Platform, bundle)).Where(File.Exists)) {
				try {
					Console.WriteLine("Deleting {0}", path);
					File.Delete(path);
				} catch (System.Exception e) {
					Console.WriteLine("Can not remove {0}: {1}", path, e.Message);
				}
			}
		}
	}
}
