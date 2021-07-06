using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Kumquat;
using Lime;

namespace Orange
{
	public static class CodeCooker
	{
		public static void Cook(
			Target target,
			AssetBundle inputBundle,
			Dictionary<string, CookingRules> assetToCookingRules,
			List<string> cookingBundles
		) {
			var cache = LoadCodeCookerCache();
			var scenesToCook = new List<string>();
			var visitedScenes = new HashSet<string>();
			var usedBundles = new HashSet<string>();
			var sceneToBundleMap = new Dictionary<string, string>();
			var allScenes = new List<string>();
			var modifiedScenes = new List<string>();

			using (var dc = new DirectoryChanger(The.Workspace.AssetsDirectory)) {
				foreach (var scenePath in inputBundle.EnumerateFiles()) {
					if (
						!scenePath.EndsWith(".tan", StringComparison.OrdinalIgnoreCase) &&
						!scenePath.EndsWith(".model", StringComparison.OrdinalIgnoreCase)
					) {
						continue;
					}
					var rules = assetToCookingRules[scenePath];
					allScenes.Add(scenePath);
					sceneToBundleMap.Add(scenePath, rules.Bundles.First());
					var sourceHash = AssetBundle.Current.ComputeCookingUnitHash(
						scenePath, assetToCookingRules[scenePath]
					);
					if (!cache.SceneFiles.ContainsKey(scenePath)) {
						modifiedScenes.Add(scenePath);
						scenesToCook.Add(scenePath);
						var bundles = assetToCookingRules[scenePath].Bundles;
						foreach (var bundle in bundles) {
							usedBundles.Add(bundle);
						}
						cache.SceneFiles.Add(scenePath, new SceneRecord {
							Bundle = bundles.First(),
							CookingUnitHash = sourceHash
						});
					} else {
						var cacheRecord = cache.SceneFiles[scenePath];
						if (sourceHash != cacheRecord.CookingUnitHash) {
							var queue = new Queue<string>();
							if (!visitedScenes.Contains(scenePath)) {
								queue.Enqueue(scenePath);
								visitedScenes.Add(scenePath);
							}
							while (queue.Count != 0) {
								var scene = queue.Dequeue();
								scenesToCook.Add(scene);
								var bundles = assetToCookingRules[scene].Bundles;
								foreach (var bundle in bundles) {
									usedBundles.Add(bundle);
								}
								foreach (var referringScene in cache.SceneFiles[scene].ReferringScenes) {
									if (!visitedScenes.Contains(referringScene)) {
										visitedScenes.Add(referringScene);
										queue.Enqueue(referringScene);
									}
								}
							}
							cache.SceneFiles[scenePath].CookingUnitHash = sourceHash;
							modifiedScenes.Add(scenePath);
						}
					}
				}
			}
			try {
				// Don't return early even if there's nothing modified since there may be stuff to delete
				// Also, don't bother with loading only usedBundles for now, just load all of them
				var allBundles = cookingBundles
					.Select(bundleName => new PackedAssetBundle(
						The.Workspace.GetBundlePath(target.Platform, bundleName)
					)).ToArray();
				AssetBundle.SetCurrent(new AggregateAssetBundle(allBundles), false);
				new ScenesCodeCooker(
					The.Workspace.ProjectDirectory,
					The.Workspace.GeneratedScenesPath,
					The.Workspace.ProjectName,
					sceneToBundleMap,
					scenesToCook,
					allScenes,
					modifiedScenes,
					cache
				).Start();
				SaveCodeCookerCache(cache);
			} finally {
				AssetBundle.Current.Dispose();
				AssetBundle.SetCurrent(null, false);
			}
		}

		public static string GetCodeCachePath()
		{
			var name = string.Join("_", The.Workspace.ProjectFilePath.Split(new string[] { "\\", "/", ":" }, StringSplitOptions.RemoveEmptyEntries)).ToLower(CultureInfo.InvariantCulture);
			return Path.Combine(WorkspaceConfig.GetDataPath(), name, "code_cooker_cache.json");
		}

		public static CodeCookerCache LoadCodeCookerCache()
		{
			var scenesPath = $@"{The.Workspace.ProjectDirectory}/{The.Workspace.ProjectName}.{The.Workspace.GeneratedScenesPath}/Scenes";
			var codeCachePath = GetCodeCachePath();
			if (!File.Exists(codeCachePath)) {
				return InvalidateCache(scenesPath);
			} else if (!Directory.Exists(scenesPath)) {
				return InvalidateCache(scenesPath);
			} else {
				try {
					CodeCookerCache cache;
					using (FileStream stream = new FileStream(codeCachePath, FileMode.Open, FileAccess.Read, FileShare.None)) {
						var jd = new Yuzu.Json.JsonDeserializer();
						cache = (CodeCookerCache)jd.FromStream(new CodeCookerCache(), stream);
					}
					if (!cache.IsActual) {
						throw new System.Exception("Code cooker cache has deprecated version.");
					}
					using (new DirectoryChanger(The.Workspace.ProjectDirectory)) {
						var projectName = The.Workspace.ProjectName;
						foreach (var platform in Enum.GetValues(typeof(TargetPlatform))) {
							var platformName = Enum.GetName(typeof(TargetPlatform), platform);
							var projectPath = $"{projectName}.{The.Workspace.GeneratedScenesPath}/{projectName}.GeneratedScenes.{platformName}.csproj";
							if (File.Exists(projectPath)) {
								var projectFilesCache = cache.GeneratedProjectFileToModificationDate;
								if (!projectFilesCache.ContainsKey(projectPath) || File.GetLastWriteTime(projectPath) > projectFilesCache[projectPath]) {
									// Consider cache inconsistent if generated project files were modified from outside
									return InvalidateCache(scenesPath);
								}
							}
						}
					}
					return cache;
				} catch {
					return InvalidateCache(scenesPath);
				}
			}
		}

		public static void SaveCodeCookerCache(CodeCookerCache codeCookerCache)
		{
			codeCookerCache.GeneratedProjectFileToModificationDate.Clear();
			using (new DirectoryChanger(The.Workspace.ProjectDirectory)) {
				var projectName = The.Workspace.ProjectName;
				foreach (var platform in Enum.GetValues(typeof(TargetPlatform))) {
					var platformName = Enum.GetName(typeof(TargetPlatform), platform);
					var projectPath = $"{projectName}.{The.Workspace.GeneratedScenesPath}/{projectName}.GeneratedScenes.{platformName}.csproj";
					if (File.Exists(projectPath)) {
						CsprojSynchronization.SynchronizeProject(projectPath);
						codeCookerCache.GeneratedProjectFileToModificationDate.Add(projectPath, File.GetLastWriteTime(projectPath));
					}
				}
			}
			var codeCookerCachePath = GetCodeCachePath();
			Directory.CreateDirectory(Path.GetDirectoryName(codeCookerCachePath));
			using FileStream stream = new FileStream(codeCookerCachePath, FileMode.Create, FileAccess.Write, FileShare.None);
			var js = new Yuzu.Json.JsonSerializer();
			js.ToStream(codeCookerCache, stream);
		}

		private static CodeCookerCache InvalidateCache(string scenesPath)
		{
			if (Directory.Exists(scenesPath)) {
				ScenesCodeCooker.RetryUntilSuccessDeleteDirectory(scenesPath);
			}
			return new CodeCookerCache();
		}
	}
}
