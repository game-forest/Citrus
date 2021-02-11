using System;
using System.IO;
using System.Linq;
using System.Text;
using Orange;
using Lime;
using System.ComponentModel.Composition;

namespace OrangePlugin
{
	public static class OrangePlugin
	{
		private static ConfigWindow Window;

		private static void InstallHooks()
		{
			var gitRootPath = The.Workspace.ProjectDirectory;
			do {
				if (Directory.Exists(Path.Combine(gitRootPath, ".git"))) {
					gitRootPath = Path.Combine(gitRootPath, ".git");
					break;
				}
				gitRootPath = Path.Combine(gitRootPath, "..");
			} while (Directory.Exists(gitRootPath));
			if (Directory.Exists(gitRootPath)) {
				var installedHookPath = Path.Combine(gitRootPath, "hooks", "pre-commit");
				var distrHookPath = Path.Combine(The.Workspace.ProjectDirectory, "hooks", "pre-commit");
				var fiInstalledHook = new System.IO.FileInfo(installedHookPath);
				var fiDistrHook = new System.IO.FileInfo(distrHookPath);
				if (!fiDistrHook.Exists) {
					return;
				}
				if (!fiInstalledHook.Exists || fiInstalledHook.LastWriteTime != fiDistrHook.LastWriteTime) {
					System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(installedHookPath));
					var citrusDirectory = Orange.Toolbox.FindCitrusDirectory();
					var hookText = File.ReadAllText(distrHookPath)
						.Replace("$(CITRUS_DIRECTORY)"
							, Path.GetRelativePath(Path.Combine(gitRootPath, ".."), citrusDirectory).Replace("\\", "/"));
					File.WriteAllText(installedHookPath, hookText);
					File.SetLastWriteTime(installedHookPath, fiDistrHook.LastWriteTime);
					if (!fiInstalledHook.Exists) {
						Console.WriteLine("Installed git hook.");
					} else if (fiInstalledHook.LastWriteTime != fiDistrHook.LastWriteTime) {
						Console.WriteLine("Updated git hook.");
					}
				}
			}
		}

		[Export(nameof(Orange.OrangePlugin.Initialize))]
		public static void DoInitialization()
		{
			InstallHooks();
			Console.WriteLine("Orange plugin initialized.");
		}

		[Export(nameof(Orange.OrangePlugin.BuildUI))]
		public static void BuildUI(IPluginUIBuilder builder)
		{
			Window = new ConfigWindow(builder);
		}

		[Export(nameof(Orange.OrangePlugin.Finalize))]
		public static void DoFinalization()
		{
			Window = null;
			Console.WriteLine("Orange plugin finalized.");
		}

		[Export(nameof(Orange.OrangePlugin.CommandLineArguments))]
		public static string GetCommandLineArguments()
		{
			return Window.GetCommandLineArguments();
		}

		[Export(nameof(Orange.OrangePlugin.MenuItems))]
		[ExportMetadata(nameof(IMenuItemMetadata.Label), "Cook Selected Bundles")]
		public static void CookSelectedBundles()
		{
			var target = The.UI.GetActiveTarget();
			if (!AssetCooker.CookForTarget(
				target,
				Orange.Toolbox.GetCommandLineArg("--bundles").Split(',').Select(s => s.Trim()).ToList(),
				out var errorMessage
			)) {
				Console.WriteLine("Error cooking selected bundles: " + errorMessage);
			}
		}
	}
}
