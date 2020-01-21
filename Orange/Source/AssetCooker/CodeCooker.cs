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
		private static readonly string[] scenesExtensions = { ".scene", ".tan", ".model" };

		public static void Cook(Target target, Dictionary<string, CookingRules> assetToCookingRules, List<string> cookingBundles)
		{
			var cache = LoadCodeCookerCache();
			var scenesToCook = new List<string>();
			var visitedScenes = new HashSet<string>();
			var usedBundles = new HashSet<string>();
			var sceneToBundleMap = new Dictionary<string, string>();
			var allScenes = new List<string>();
			var modifiedScenes = new List<string>();

			var aliasedFiles = new AliasedFileEnumerator(
				new FilteredFileEnumerator(new FileEnumerator(The.Workspace.AssetsDirectory), info => {
					var rule = assetToCookingRules[info.SrcPath];
					bool presentInCookingBundles = rule.Bundles.Any(cookingBundles.Contains);
					bool isCookingObject = scenesExtensions.Any(
						f => info.DstPath.EndsWith(f, StringComparison.OrdinalIgnoreCase));

					return !rule.Ignore && presentInCookingBundles && isCookingObject;
				})
			);

			using (new DirectoryChanger(The.Workspace.AssetsDirectory)) {
				foreach (var fileInfo in aliasedFiles.Enumerate()) {
					var srcRule = assetToCookingRules[fileInfo.SrcPath];
					var dateModified = File.GetLastWriteTime(fileInfo.SrcPath).ToUniversalTime();

					allScenes.Add(fileInfo.DstPath);
					sceneToBundleMap[fileInfo.DstPath] = srcRule.Bundles.First();

					if (!cache.SceneFiles.ContainsKey(fileInfo.DstPath)) {
						modifiedScenes.Add(fileInfo.DstPath);
						scenesToCook.Add(fileInfo.DstPath);

						var bundles = srcRule.Bundles;
						foreach (string bundle in bundles) {
							usedBundles.Add(bundle);
						}

						cache.SceneFiles.Add(fileInfo.DstPath, new SceneRecord {
							Bundle = bundles.First(),
							DateModified = dateModified
						});
					} else {
						var cacheRecord = cache.SceneFiles[fileInfo.DstPath];

						if (dateModified == cacheRecord.DateModified) {
							continue;
						}

						var queue = new Queue<string>();
						if (!visitedScenes.Contains(fileInfo.DstPath)) {
							queue.Enqueue(fileInfo.DstPath);
							visitedScenes.Add(fileInfo.DstPath);
						}

						while (queue.Count != 0) {
							var scene = queue.Dequeue();
							scenesToCook.Add(scene);
							var bundles = assetToCookingRules[scene].Bundles;
							foreach (string bundle in bundles) {
								usedBundles.Add(bundle);
							}
							foreach (string referringScene in cache.SceneFiles[scene].ReferringScenes) {
								if (!visitedScenes.Contains(referringScene)) {
									visitedScenes.Add(referringScene);
									queue.Enqueue(referringScene);
								}
							}
						}
						cache.SceneFiles[fileInfo.DstPath].DateModified = dateModified;
						modifiedScenes.Add(fileInfo.DstPath);
					}
				}
			}
			try {
				// Don't return early even if there's nothing modified since there may be stuff to delete
				// Also, don't bother with loading ony usedBundles for now, just load all of them
				AssetBundle.SetCurrent(new AggregateAssetBundle(cookingBundles.Select(bundleName => new PackedAssetBundle(The.Workspace.GetBundlePath(target.Platform, bundleName))).ToArray()), false);
				new ScenesCodeCooker(
					The.Workspace.ProjectDirectory,
					The.Workspace.GeneratedScenesPath,
					The.Workspace.Title,
					CookingRulesBuilder.MainBundleName,
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
			var scenesPath = $@"{The.Workspace.ProjectDirectory}/{The.Workspace.Title}.{The.Workspace.GeneratedScenesPath}/Scenes";
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
						var projectName = The.Workspace.Title;
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
				var projectName = The.Workspace.Title;
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
			using (FileStream stream = new FileStream(codeCookerCachePath, FileMode.Create, FileAccess.Write, FileShare.None)) {
				var js = new Yuzu.Json.JsonSerializer();
				js.ToStream(codeCookerCache, stream);
			}
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
