using System.ComponentModel.Composition;

namespace Orange
{
	public static partial class Actions
	{
		[Export(nameof(OrangePlugin.MenuItemsWithErrorDetails))]
		[ExportMetadata("Label", "Build")]
		[ExportMetadata("Priority", 1)]
		public static string BuildGameAction()
		{
			var target = The.UI.GetActiveTarget();

			AssetCooker.CookForTarget(target);
			return BuildGame(target) ? null : "Can not BuildGame";
		}

		public static bool BuildGame(Target target)
		{
			var builder = new SolutionBuilder(target);
			if (target.CleanBeforeBuild == true) {
				builder.Clean();
			}
			if (!builder.Build()) {
				UserInterface.Instance.ExitWithErrorIfPossible();
				return false;
			}
			return true;
		}
	}
}
