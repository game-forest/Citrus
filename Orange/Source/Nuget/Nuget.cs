using System;
using System.IO;
using Lime;

namespace Orange
{
	internal static class Nuget
	{
		private static readonly string nugetPath;
#if MAC
		private static readonly string monoPath;
#endif

		static Nuget()
		{
#if MAC
			nugetPath = Path.Combine(Toolbox.GetApplicationDirectory(), "nuget.exe");
#else
			nugetPath = Path.Combine(Toolbox.GetApplicationDirectory(), "Toolchain.Win", "nuget.exe");
#endif
			if (!File.Exists(nugetPath)) {
				nugetPath = Path.Combine(Toolbox.FindCitrusDirectory(), "Orange", "Toolchain.Win", "nuget.exe");
			}

			if (!File.Exists(nugetPath)) {
				throw new InvalidOperationException($"Can't find nuget.exe.");
			}
#if MAC
			monoPath = Toolbox.GetMonoPath();
#endif
		}

		public static int Restore(string projectPath, string builderPath = null)
		{
			var command = $"restore \"{projectPath}\" ";
#if MAC
			// MSBuildVersion is a workaround because msbuild 16 doesn't work with any version of nuget.
			command += builderPath == null ? "" : $"-MSBuildVersion 15";
#else
			command += builderPath == null ? "" : $"-MSBuildPath \"{Path.GetDirectoryName(builderPath)}\"";
#endif
			return Start(command);
		}

		private static int Start(string args)
		{
#if WIN
			return Process.Start(nugetPath, args);
#elif MAC
			return Process.Start(monoPath, $"{nugetPath} {args}");
#endif
		}
	}
}
