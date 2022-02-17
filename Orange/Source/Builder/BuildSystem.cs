using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Orange.Source
{
	public enum BuildAction
	{
		Clean,
		Restore,
		Build,
	}

	internal abstract class BuildSystem
	{
		private readonly List<string> arguments = new List<string>();
		protected readonly Target target;

		protected string Args => string.Join(" ", arguments);

		public string BinariesDirectory => Path.Combine(
			Path.GetDirectoryName(target.ProjectPath), "bin", target.Configuration);

		public BuildSystem(Target target)
		{
			this.target = target;
		}

		public int Execute(BuildAction buildAction, StringBuilder output = null)
		{
			arguments.Clear();
			switch (buildAction) {
				case BuildAction.Clean: {
						PrepareForClean();
						return Execute(output);
					}
				case BuildAction.Restore: {
						PrepareForRestore();
						return Execute(output);
					}
				case BuildAction.Build: {
						PrepareForBuild();
						return Execute(output);
					}
				default: {
						throw new InvalidOperationException($"Unknown {nameof(buildAction)}: {buildAction}");
					}
			}
		}

		protected abstract void DecorateBuild();
		protected abstract void DecorateClean();
		protected abstract void DecorateRestore();
		protected abstract void DecorateConfiguration();
		protected abstract int Execute(StringBuilder output);

		protected void AddArgument(string argument)
		{
			arguments.Add(argument);
		}

		private void PrepareForBuild()
		{
			DecorateBuild();
			DecorateConfiguration();
		}

		private void PrepareForRestore()
		{
			DecorateRestore();
			DecorateConfiguration();
		}

		private void PrepareForClean()
		{
			DecorateClean();
			DecorateConfiguration();
		}
	}
}
