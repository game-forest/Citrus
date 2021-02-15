using System;
using System.ComponentModel.Composition;
using System.IO;
using Orange.Source;

namespace Orange
{
	public static class RunTangerineAction
	{
		[Export(nameof(OrangePlugin.MenuItemsWithErrorDetails))]
		[ExportMetadata("Label", "Run Tangerine")]
		[ExportMetadata("Priority", 2)]
		public static string RunTangerine()
		{
			const string projectName = "Tangerine";
			var projectDirectory = Path.Combine(Toolbox.FindCitrusDirectory(), projectName);
#if WIN
			var solutionPath = Path.Combine(projectDirectory, projectName + ".Win.sln");
			var solutionBuilder = new SolutionBuilder(new Target("Tangerine.Win", solutionPath, false, TargetPlatform.Win, BuildConfiguration.Release));
#elif MAC
			var solutionPath = Path.Combine(projectDirectory, projectName + ".Win.sln");
			Nuget.Restore(solutionPath);
			// "Release" configuration requires code signing, use debug for a while.
			var solutionBuilder = new SolutionBuilder(new Target("Tangerine.Mac", solutionPath, false, TargetPlatform.Mac, BuildConfiguration.Debug));
#endif
			if (!solutionBuilder.Build()) {
				return "Build system has returned error";
			}

			var p = new System.Diagnostics.Process();
#if WIN
			p.StartInfo.FileName = Path.Combine(
				projectDirectory,
				"Tangerine",
				"bin",
				BuildConfiguration.Release,
				"Tangerine.exe");
#elif MAC
			p.StartInfo.FileName = Path.Combine(
				projectDirectory,
				"Tangerine",
				"bin",
				BuildConfiguration.Debug,
				"Tangerine.app/Contents/MacOS/Tangerine");
			p.StartInfo.UseShellExecute = false;
			p.StartInfo.EnvironmentVariables.Clear();
			p.StartInfo.EnvironmentVariables.Add("PATH", "/usr/bin");
#endif
			p.Start();
			return null;
		}
	}
}
