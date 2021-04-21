using System.Linq;
using System.Collections.Generic;

namespace Orange
{
	public class Target
	{
		/// <summary>
		/// Target name.
		/// Can be used to specify cooking rule only for this target in form <c><CookingRuleName>(TargetName) <CookingRuleValue></c>
		/// Names for default targets correspond to default target platforms: "Win", "Mac", "iOS", "Android".
		/// </summary>
		public readonly string Name;

		/// <summary>
		/// Relative to project directory or absolute path to *.csproj or *.sln file.
		/// </summary>
		public string ProjectPath => projectPath ?? BaseTarget.ProjectPath;

		/// <summary>
		/// Configuration name for build system.
		/// </summary>
		public string Configuration => configuration ?? BaseTarget.Configuration;

		/// <summary>
		/// If true clean step will be performed before build step.
		/// </summary>
		public bool CleanBeforeBuild => cleanBeforeBuild ?? BaseTarget.CleanBeforeBuild;

		/// <summary>
		/// Platform.
		/// </summary>
		public TargetPlatform Platform => platform ?? BaseTarget.Platform;

		/// <summary>
		/// Target specific required bundles.
		/// </summary>
		public IEnumerable<string> Bundles => bundles.Any() || BaseTarget == null ? bundles : BaseTarget.Bundles;

		/// <summary>
		/// If any property but name is initialized to null, then it's redirected to corresponding property of base target.
		/// Also affects application of cooking rules. Cooking rules applied to the base target are also applied to this target.
		/// CookingRules related to derived target take priority.
		/// </summary>
		public Target BaseTarget;

		/// <summary>
		/// If true will be hidden in UI.
		/// </summary>
		public bool Hidden => hidden ?? BaseTarget.Hidden;

		private readonly string projectPath;
		private readonly string configuration;
		private readonly bool? cleanBeforeBuild;
		private readonly TargetPlatform? platform;
		private readonly bool? hidden;
		private readonly IEnumerable<string> bundles;

		public Target(
			string name,
			string projectPath,
			bool? cleanBeforeBuild,
			TargetPlatform? platform,
			string configuration,
			bool? hidden,
			string[] bundles = null
		) {
			Name = name;
			this.bundles = bundles ?? Enumerable.Empty<string>();
			this.cleanBeforeBuild = cleanBeforeBuild;
			this.platform = platform;
			this.projectPath = projectPath;
			this.configuration = configuration;
			this.hidden = hidden;
		}
	}
}
