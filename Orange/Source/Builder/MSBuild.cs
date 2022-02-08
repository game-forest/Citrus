using System;
using System.IO;
using System.Text;
using Microsoft.Win32;

namespace Orange.Source
{
	internal class MSBuildNotFound : System.Exception
	{
		public readonly string DownloadUrl;

		public MSBuildNotFound(string message, string downloadUrl) : base(message)
		{
			DownloadUrl = downloadUrl;
		}
	}

	internal class MSBuild : BuildSystem
	{
		private readonly string builderPath;

		public MSBuild(Target target)
			: base(target)
		{
			if (!TryGetMSBuildPath(out builderPath)) {
#pragma warning disable MEN002 // Line is too long
				const string MSBuildDownloadUrl = "https://visualstudio.microsoft.com/ru/thank-you-downloading-visual-studio/?sku=BuildTools&rel=15";
#pragma warning restore MEN002 // Line is too long
				throw new MSBuildNotFound(
					$"Please install Microsoft Build Tools 2015: {MSBuildDownloadUrl}",
					MSBuildDownloadUrl
				);
			} else {
				Console.WriteLine("MSBuild located in: " + builderPath);
			}
		}

		protected override int Execute(StringBuilder output)
		{
			return Process.Start(
				$"cmd",
				$"/C \"set BUILDING_WITH_ORANGE=true & \"{builderPath}\" \"{target.ProjectPath}\" {Args}\"",
				output: output
			);
		}

		protected override void DecorateBuild()
		{
			AddArgument("/verbosity:minimal");
			if (target.Platform == TargetPlatform.Android) {
				if (target.ProjectPath.EndsWith(".sln")) {
					AddArgument("/p:AndroidBuildApplicationPackage=true");
				} else {
					AddArgument("/t:PackageForAndroid");
					AddArgument("/t:SignAndroidPackage");
				}
			}
		}

		protected override void DecorateClean()
		{
			AddArgument("/t:Clean");
		}

		protected override void DecorateRestore()
		{
			AddArgument("/t:Restore");
		}

		protected override void DecorateConfiguration()
		{
			AddArgument("/p:Configuration=" + target.Configuration);
		}

		public static bool TryGetMSBuildPath(out string path)
		{
			var msBuild16Path = Path.Combine(
				Environment.GetFolderPath(
					Environment.SpecialFolder.ProgramFilesX86),
					@"Microsoft Visual Studio\2019\BuildTools\MSBuild\Current\Bin\",
					"MSBuild.exe"
				);
			if (File.Exists(msBuild16Path)) {
				path = msBuild16Path;
				return true;
			}

			msBuild16Path = Path.Combine(
				Environment.GetFolderPath(
					Environment.SpecialFolder.ProgramFilesX86),
					@"Microsoft Visual Studio\2019\Community\MSBuild\Current\Bin\",
					"MSBuild.exe"
				);
			if (File.Exists(msBuild16Path)) {
				path = msBuild16Path;
				return true;
			}

			var visualStudioRegistryPath = Registry.LocalMachine.OpenSubKey(
				@"SOFTWARE\WOW6432Node\Microsoft\VisualStudio\SxS\VS7"
			);
			if (visualStudioRegistryPath != null) {
				var vsPath = visualStudioRegistryPath.GetValue("15.0", string.Empty) as string;
				var vsBuild15Path = Path.Combine(vsPath, "MSBuild", "15.0", "Bin", "MSBuild.exe");
				if (File.Exists(vsBuild15Path)) {
					path = vsBuild15Path;
					return true;
				}
			}

			var msBuild15Path = Path.Combine(
				Environment.GetFolderPath(
					Environment.SpecialFolder.ProgramFilesX86),
					@"Microsoft Visual Studio\2017\BuildTools\MSBuild\15.0\Bin\",
					"MSBuild.exe"
				);
			if (File.Exists(msBuild15Path)) {
				path = msBuild15Path;
				return true;
			}

			var msBuild14Path = Path.Combine(
				Environment.GetFolderPath(
					Environment.SpecialFolder.ProgramFilesX86),
					@"MSBuild\14.0\Bin\",
					"MSBuild.exe"
				);
			if (File.Exists(msBuild14Path)) {
				path = msBuild14Path;
				return true;
			}

			path = null;
			return false;
		}
	}
}
