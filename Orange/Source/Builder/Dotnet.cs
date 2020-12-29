using System.Text;

namespace Orange.Source
{
	class Dotnet : BuildSystem
	{

		public Dotnet(Target target) : base(target)
		{
		}

		protected override void DecorateBuild()
		{
			AddArgument("build");
		}

		protected override void DecorateClean()
		{
			AddArgument("clean");
		}

		protected override void DecorateConfiguration()
		{
			AddArgument("-c " + target.Configuration);
		}

		protected override void DecorateRestore()
		{
			AddArgument("restore");
		}

		protected override int Execute(StringBuilder output) =>
			Process.Start("dotnet", $"{Args} \"{target.ProjectPath}\"", output: output);

	}
}
