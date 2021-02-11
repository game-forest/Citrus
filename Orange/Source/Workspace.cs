using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Lime;
using Newtonsoft.Json.Linq;

namespace Orange
{
	public class Workspace
	{
		/// <summary>
		/// Absolute path of currently open project file.
		/// Located at the root level of project directory, has <c>*.citproj</c> extension.
		/// <c>null</c> when no project is opened.
		/// </summary>
		public string ProjectFilePath { get; private set; }
		public string ProjectDirectory => !string.IsNullOrEmpty(ProjectFilePath) ? Path.GetDirectoryName(ProjectFilePath) : null;
		public string AssetsDirectory { get; private set; }
		public string ProjectName { get; private set; }
		public string GeneratedScenesPath { get; private set; }
		[Obsolete("Use AssetBundle.Current or AssetCooker.InputBundle instead")]
		public IFileEnumerator AssetFiles { get; set; }
		public Json ProjectJson { get; private set; }
		public List<Target> Targets { get; private set; }
		public string TangerineCacheBundle { get; private set; }
		public string UnresolvedAssembliesDirectory { get; private set; }

		/// <summary>
		/// Absolute path to directory of Citrus used by loaded project which is not necessarily a location of running Citrus.
		/// </summary>
		private string projectRelatedCitrusDirectory;

		/// <summary>
		/// Currently used asset cache mode
		/// </summary>
		public AssetCacheMode AssetCacheMode;

		/// <summary>
		/// Currently used local asset cache path
		/// </summary>
		public string LocalAssetCachePath;
		public bool BenchmarkEnabled;
		public bool BundlePickerVisible;

		public Workspace()
		{
			Targets = new List<Target>();
		}

		public string GetPlatformSuffix(TargetPlatform platform)
		{
			return "." + platform.ToString();
		}

		public string GetTangerineCacheBundlePath()
		{
			var name = string
				.Join("_", ProjectFilePath.Split(new[] { "\\", "/", ":" }, StringSplitOptions.RemoveEmptyEntries))
				.ToLower(System.Globalization.CultureInfo.InvariantCulture);
			name = Path.ChangeExtension(name, "tancache");
			return Path.Combine(WorkspaceConfig.GetDataPath(), name);
		}

		/// <summary>
		/// Returns solution path. E.g: Zx3.Win/Zx3.Win.sln
		/// </summary>
		public string GetDefaultProjectSolutionPath(TargetPlatform platform)
		{
			if (string.IsNullOrEmpty(ProjectDirectory)) {
				throw new InvalidOperationException("Can't generate default solution path for project when there's no project loaded.");
			}
			string platformProjectName = ProjectName + GetPlatformSuffix(platform);
			return Path.Combine(ProjectDirectory, platformProjectName, platformProjectName + ".sln");
		}

		/// <summary>
		/// Returns Citrus/Lime project path.
		/// </summary>
		public string GetProjectRelatedLimeCsprojFilePath(TargetPlatform platform)
		{
			return Path.Combine(projectRelatedCitrusDirectory, "Lime", "Lime" + GetPlatformSuffix(platform) + ".csproj");
		}

		public static readonly Workspace Instance = new Workspace();

		public JObject JObject { get; private set; }

		public void Load(string projectFilePath = null)
		{
			// heuristic behavior: always try to go up and search for a citproj file
			// if found, ignore the one saved in app data, since we're opening citrus directory
			// related to found game project as a submodule
			var config = WorkspaceConfig.Load();
			if (projectFilePath != null) {
				Open(projectFilePath);
			} else if (Toolbox.TryFindCitrusProjectForExecutingAssembly(out projectFilePath)) {
				Open(projectFilePath);
			}
			var projectConfig = config.GetProjectConfig(projectFilePath);
			The.UI.LoadFromWorkspaceConfig(config, projectConfig);
			var citrusVersion = CitrusVersion.Load();
			if (citrusVersion.IsStandalone) {
				Console.WriteLine($"Welcome to Citrus. Version {citrusVersion.Version}, build number: {citrusVersion.BuildNumber}");
			}
			BenchmarkEnabled = config.BenchmarkEnabled;
			if (projectConfig != null) {
				BundlePickerVisible = projectConfig.BundlePickerVisible;
			}
#pragma warning disable CS4014
			Orange.Updater.CheckForUpdates();
#pragma warning restore CS4014
			LoadCacheSettings();
		}

		public void LoadCacheSettings()
		{
			var config = WorkspaceConfig.Load();
			var projectConfig = config.GetProjectConfig(ProjectFilePath);
			if (projectConfig != null) {
				AssetCacheMode = projectConfig.AssetCacheMode;
				LocalAssetCachePath = projectConfig.LocalAssetCachePath;
				if (ProjectDirectory != null && !Path.IsPathRooted(LocalAssetCachePath)) {
					LocalAssetCachePath = Path.Combine(ProjectDirectory, LocalAssetCachePath);
				}
			}
		}

		public void Save()
		{
			var config = WorkspaceConfig.Load();
			config.RegisterRecentProject(ProjectFilePath);
			var projectConfig = config.GetProjectConfig(ProjectFilePath);
			if (projectConfig != null) {
				projectConfig.AssetCacheMode = AssetCacheMode;
			}
			The.UI.SaveToWorkspaceConfig(ref config, projectConfig);
			WorkspaceConfig.Save(config);
		}

		public void Open(string projectFilePath)
		{
			try {
				The.UI.ClearLog();
				ProjectFilePath = projectFilePath;
				ReadProject(projectFilePath);
				var tangerineAssetBundle = new Tangerine.Core.TangerineAssetBundle(AssetsDirectory);
				if (!tangerineAssetBundle.IsActual()) {
					tangerineAssetBundle.CleanupBundle();
				}
				Lime.AssetBundle.Current = tangerineAssetBundle;
				PluginLoader.ScanForPlugins();
				if (defaultCsprojSynchronizationSkipUnwantedDirectoriesPredicate == null) {
					defaultCsprojSynchronizationSkipUnwantedDirectoriesPredicate = CsprojSynchronization.SkipUnwantedDirectoriesPredicate;
				}
				CsprojSynchronization.SkipUnwantedDirectoriesPredicate = (di) => {
					return defaultCsprojSynchronizationSkipUnwantedDirectoriesPredicate(di) && !di.FullName.StartsWith(AssetsDirectory, StringComparison.OrdinalIgnoreCase);
				};
				AssetFiles = new FileEnumerator(AssetsDirectory);
				LoadCacheSettings();
				The.UI.OnWorkspaceOpened();
				The.UI.ReloadBundlePicker();
				The.UI.UpdateOpenedProjectPath(projectFilePath);
			} catch (System.Exception e) {
				// TODO: make a general way to close project and reset everything to default state
				ProjectFilePath = null;
				AssetsDirectory = null;
				AssetFiles = null;
				TangerineCacheBundle = null;
				throw new System.Exception($"Can't open {projectFilePath}:\n{e.Message}");
			}
		}

		private Predicate<DirectoryInfo> defaultCsprojSynchronizationSkipUnwantedDirectoriesPredicate;

		private void FillDefaultTargets()
		{
			Targets.Clear();
			foreach (TargetPlatform platform in Enum.GetValues(typeof(TargetPlatform))) {
				Targets.Add(new Target(
					name: Enum.GetName(typeof(TargetPlatform), platform),
					projectPath: GetDefaultProjectSolutionPath(platform),
					cleanBeforeBuild: false,
					platform: platform,
					configuration: BuildConfiguration.Release,
					hidden: false
				));
			}
		}

		private void ReadProject(string file)
		{
			ProjectJson = new Json(file);

			ProjectName = ProjectJson["Name"] as string;
			if (string.IsNullOrEmpty(ProjectName)) {
				throw new Lime.Exception($"Project configuration incomplete: 'Name' is not set.");
			}

			AssetsDirectory = Path.Combine(ProjectDirectory, ProjectJson.GetValue("AssetsDirectory", "Data"));
			if (!Directory.Exists(AssetsDirectory)) {
				throw new Lime.Exception("Assets directory \"{0}\" doesn't exist", AssetsDirectory);
			}

			UnresolvedAssembliesDirectory = Path.Combine(ProjectDirectory,
				ProjectJson.GetValue("UnresolvedAssembliesDirectory", $"{ProjectName}.OrangePlugin/bin/$(CONFIGURATION)/"));

			GeneratedScenesPath = ProjectJson.GetValue("GeneratedScenesPath", "GeneratedScenes");

			Lime.Localization.DictionariesPath = ProjectJson.GetValue<string>("DictionariesPath", null) ?? Lime.Localization.DictionariesPath;

			// Standard Citrus locations are beside the project directory or inside it.
			// If the location deviates from the standard, it should be specified through "CitrusDirectory" in citproj file.
			projectRelatedCitrusDirectory = ProjectJson.GetValue("CitrusDirectory", string.Empty);
			if (!string.IsNullOrEmpty(projectRelatedCitrusDirectory)) {
				if (!System.IO.Path.IsPathRooted(projectRelatedCitrusDirectory)) {
					projectRelatedCitrusDirectory = Path.Combine(ProjectDirectory, projectRelatedCitrusDirectory);
				}
			} else {
				// If Citrus Directory is not set via citproj file, try default values from past precedents
				// first a Citrus directory inside project directory, second Citrus directory one level above project directory
				projectRelatedCitrusDirectory = Path.Combine(ProjectDirectory, "Citrus");
				if (!Directory.Exists(projectRelatedCitrusDirectory)) {
					projectRelatedCitrusDirectory = Path.Combine(Path.GetDirectoryName(ProjectDirectory), "Citrus");
				}
			}
			if (!File.Exists(Path.Combine(projectRelatedCitrusDirectory, CitrusVersion.Filename))) {
				throw new InvalidOperationException($"Unable to locate project related Citrus directory at \"{projectRelatedCitrusDirectory}\", check value of \"CitrusDirectory\" in \"{ProjectFilePath}\"");
			}

			// Initialize default and parse project specific targets.
			Targets = new List<Target>();
			FillDefaultTargets();
			var targetToBaseTarget = new Dictionary<Target, string>();
			foreach (var target in ProjectJson.GetArray("Targets", new Dictionary<string, object>[0])) {
				bool? cleanBeforeBuild = null;
				if (target.ContainsKey("CleanBeforeBuild")) {
					cleanBeforeBuild = (bool)target["CleanBeforeBuild"];
				}
				var targetName = target["Name"] as string;
				if (Targets.Where(t => t.Name == targetName).Any()) {
					throw new System.InvalidOperationException($"Target {targetName} already exists.");
				}
				string configuration = null;
				if (target.TryGetValue("Configuration", out object configurationValue)) {
					configuration = configurationValue as string;
				}
				string projectPath = null;
				if (target.TryGetValue("Project", out object projectPathValue)) {
					projectPath = target["Project"] as string;
					if (!string.IsNullOrEmpty(projectPath)) {
						if (!System.IO.Path.IsPathRooted(projectPath)) {
							projectPath = System.IO.Path.Combine(ProjectDirectory, projectPath);
						}
					}
				}
				bool? hidden = null;
				if (target.ContainsKey("Hidden")) {
					hidden = (bool)target["Hidden"];
				}
				Target newTarget = null;
				Targets.Add(newTarget = new Target(
					targetName,
					projectPath,
					cleanBeforeBuild,
					null,
					configuration,
					hidden
				));
				if (target.TryGetValue("BaseTarget", out object baseTargetName)) {
					targetToBaseTarget[newTarget] = baseTargetName as string;
				}
			}
			foreach (var (k, v) in targetToBaseTarget) {
				var derivedTarget = k;
				if (string.IsNullOrEmpty(v)) {
					continue;
				}
				var baseTarget = Targets.Where(t => t.Name == v).FirstOrDefault();
				if (baseTarget == null) {
					throw new System.InvalidOperationException($"Base target {v} not found.");
				}
				derivedTarget.BaseTarget = baseTarget;
			}
			var visited = new Dictionary<Target, int>();
			Action<Target> visit = null;
			visit = (t) => {
				if (t == null) {
					return;
				}
				if (!visited.ContainsKey(t)) {
					visited.Add(t, 0);
				}
				if (visited[t] == 1) {
					throw new Lime.CyclicDependencyException($"Cyclic dependency in target {t.Name}");
				}
				visited[t]++;
				visit(t.BaseTarget);
				visited[t]--;
			};
			foreach (var t in Targets) {
				visit(t);
			}
#if TANGERINE
			BlendAnimationEngine.ApplyAnimationBlenderInTangerine =
				The.Workspace.ProjectJson.GetValue<bool>("ApplyAnimationBlenderInTangerine");
#endif
		}

		public void SaveCurrentProject()
		{
			ProjectJson.RewriteOrigin();
		}

		public string GetMainBundlePath(TargetPlatform platform)
		{
			return Path.ChangeExtension(AssetsDirectory, platform.ToString());
		}

		public string GetBundlePath(TargetPlatform platform, string bundleName)
		{
			if (bundleName == CookingRulesBuilder.MainBundleName) {
				return GetMainBundlePath(platform);
			} else {
				return Path.Combine(Path.GetDirectoryName(AssetsDirectory), bundleName + GetPlatformSuffix(platform));
			}
		}

		private static TargetPlatform GetPlaformByName(string name)
		{
			try {
				return (TargetPlatform) Enum.Parse(typeof(TargetPlatform), name, true);
			} catch (ArgumentException) {
				throw new Lime.Exception($"Unknown sub-target platform name: {name}");
			}
		}

		public void RemoveRecentProject(string projectPath)
		{
			var config = WorkspaceConfig.Load();
			config.RemoveRecentProject(projectPath);
			WorkspaceConfig.Save(config);
		}
	}
}
