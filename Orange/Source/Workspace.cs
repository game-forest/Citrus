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
		/// Absolute path to currently open project file. Located at the root level of project directory, has <c>*.citproj</c> extension.
		/// If no project is open has value of <c>null</c>.
		/// </summary>
		public string ProjectFilePath { get; private set; }
		public string ProjectDirectory { get; private set; }
		public string AssetsDirectory { get; private set; }
		public string Title { get; private set; }
		public string GeneratedScenesPath { get; private set; }
		[Obsolete("Use AssetBundle.Current or AssetCooker.InputBundle instead")]
		public IFileEnumerator AssetFiles { get; set; }
		public Json ProjectJson { get; private set; }
		public List<Target> Targets { get; private set; }

		private string dataFolderName;
		private string pluginName;
		/// <summary>
		/// Location of Citrus used by currently loaded project. Not a location of general Citrus project
		/// </summary>
		private string citrusLocation;

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
			FillDefaultTargets();
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
			string platformProjectName = Title + GetPlatformSuffix(platform);
			return Path.Combine(ProjectDirectory, platformProjectName, platformProjectName + ".sln");
		}

		/// <summary>
		/// Returns Citrus/Lime project path.
		/// </summary>
		public string GetLimeCsprojFilePath(TargetPlatform platform)
		{
			// Now Citrus can either be located beside the game or inside the game directory.
			// In future projects Citrus location should be specified through "CitrusLocation" in citproj config file
			var suffix = Path.Combine("Lime", "Lime" + GetPlatformSuffix(platform) + ".csproj");
			string result;
			if (!string.IsNullOrEmpty(citrusLocation)) {
				result = Path.Combine(ProjectDirectory, citrusLocation, suffix);
			} else {
				result = Path.Combine(ProjectDirectory, "Citrus", suffix);
				if (!File.Exists(result)) {
					result = Path.Combine(Path.GetDirectoryName(ProjectDirectory), "Citrus", suffix);
				}
			}
			return result;
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
				The.UI.UpdateOpenedProjectPath(projectFilePath);
			} else {
				throw new InvalidOperationException("Can't find .citproj");
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
				ProjectDirectory = Path.GetDirectoryName(projectFilePath);
				AssetsDirectory = Path.Combine(ProjectDirectory, dataFolderName);
				var tangerineAssetBundle = new Tangerine.Core.TangerineAssetBundle(AssetsDirectory);
				if (!tangerineAssetBundle.IsActual()) {
					tangerineAssetBundle.CleanupBundle();
				}
				Lime.AssetBundle.Current = tangerineAssetBundle;
				if (!Directory.Exists(AssetsDirectory)) {
					throw new Lime.Exception("Assets folder '{0}' doesn't exist", AssetsDirectory);
				}
				PluginLoader.ScanForPlugins(!string.IsNullOrWhiteSpace(pluginName)
					? Path.Combine(Path.GetDirectoryName(projectFilePath), pluginName)
					: projectFilePath);
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
			} catch (System.Exception e) {
				Console.WriteLine($"Can't open {projectFilePath}:\n{e.Message}");
			}
		}

		// Preserving default targets references just in case since they're used as keys in cooking rules for target
		private static List<Target> defaultTargets;
		private Predicate<DirectoryInfo> defaultCsprojSynchronizationSkipUnwantedDirectoriesPredicate;

		private void FillDefaultTargets()
		{
			if (defaultTargets == null) {
				defaultTargets = new List<Target>();
				foreach (TargetPlatform platform in Enum.GetValues(typeof(TargetPlatform))) {
					defaultTargets.Add(new Target(
						name: Enum.GetName(typeof(TargetPlatform), platform),
						projectPath: GetDefaultProjectSolutionPath(platform),
						cleanBeforeBuild: false,
						platform: platform,
						configuration: BuildConfiguration.Release
					));
				}
			}
			Targets.AddRange(defaultTargets);
		}

		private void ReadProject(string file)
		{
			ProjectJson = new Json(file);
			Title = ProjectJson["Title"] as string;
			var generatedScenesConfigPath = ProjectJson["GeneratedScenesPath"] as string;
			GeneratedScenesPath = string.IsNullOrEmpty(generatedScenesConfigPath) ? "GeneratedScenes" : generatedScenesConfigPath;
			dataFolderName = ProjectJson.GetValue("DataFolderName", "Data");
			pluginName = ProjectJson.GetValue("Plugin", "");
			citrusLocation = ProjectJson.GetValue("CitrusLocation", string.Empty);
			Lime.Localization.DictionariesPath = ProjectJson.GetValue<string>("DictionariesPath", null) ?? Lime.Localization.DictionariesPath;

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
				Target newTarget = null;
				Targets.Add(newTarget = new Target(
					targetName,
					projectPath,
					cleanBeforeBuild,
					null,
					configuration
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
				return The.Workspace.GetMainBundlePath(platform);
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
	}
}
