using System;
using System.IO;
using System.Text;

namespace Orange.Source
{
	internal class MDTool : BuildSystem
	{
		private readonly string builderPath;

		public MDTool(Target target)
			: base(target)
		{
#if MAC
			builderPath = "/Library/Frameworks/Mono.framework/Versions/Current/Commands/msbuild";
			if (!File.Exists(builderPath)) {
				throw new System.Exception(
					@"Please install Visual Studio with Mono framework: https://www.visualstudio.com/ru/downloads/"
				);
			}
#elif WIN
			builderPath = @"C:\Program Files(x86)\MonoDevelop\bin\mdtool.exe";
#endif
		}

		protected override int Execute(StringBuilder output)
		{
			return Process.Start(builderPath, $"\"{target.ProjectPath}\" {Args}", output: output);
		}

		protected override void DecorateBuild()
		{
			AddArgument("-t:Build");
		}

		protected override void DecorateClean()
		{
			AddArgument("-t:Clean");
		}

		protected override void DecorateRestore()
		{
			AddArgument("-t:Restore");
		}

		protected override void DecorateConfiguration()
		{
			string platformSpecification;
			switch (target.Platform) {
				case TargetPlatform.iOS: {
						AddArgument($"-p:Platform=\"iPhone\"");
						break;
					}
				// Need to research strange behaviour due to this string
				// platformSpecification = "|x86";
				case TargetPlatform.Win:
				case TargetPlatform.Mac:
				case TargetPlatform.Android: {
						break;
					}
				default: {
						throw new NotSupportedException();
					}
			}
			AddArgument($"-p:Configuration=\"{target.Configuration}\"");
		}
	}
}
