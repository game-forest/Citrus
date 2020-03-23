using System;
using System.ComponentModel.Composition;

namespace Orange
{
	public static partial class Actions
	{
		public const string ConsoleCommandPassArguments = "--passargs";

		[Export(nameof(OrangePlugin.MenuItemsWithErrorDetails))]
		[ExportMetadata("Label", "Build and Run")]
		[ExportMetadata("Priority", 0)]
		[ExportMetadata("ApplicableToBundleSubset", true)]
		public static string BuildAndRunAction() => BuildAndRun(The.UI.GetActiveTarget());

		public static string BuildAndRun(Target target)
		{
			AssetCooker.CookForTarget(target, The.UI.GetSelectedBundles());
			if (!BuildGame(target)) {
				return "Can not BuildGame";
			}
			The.UI.ScrollLogToEnd();
			RunGame(target);
			return null;
		}

		public static bool RunGame(Target target)
		{
			var builder = new SolutionBuilder(target);
			string arguments = string.Join(" ",
				PluginLoader.GetCommandLineArguments(),
				Toolbox.GetCommandLineArg(ConsoleCommandPassArguments));
			int exitCode = builder.Run(arguments);
			if (exitCode != 0) {
				Console.WriteLine("Application terminated with exit code {0}", exitCode);
				return false;
			}
			return true;
		}
	}
}
