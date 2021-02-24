using System;
using System.Diagnostics;
using System.Linq;
using System.IO;
using System.Collections.Generic;
using Lime;

namespace Orange
{
	public class AssetCooker
	{
		public List<ICookingStage> CookStages { get; } = new List<ICookingStage>();

		public AssetBundle InputBundle => AssetBundle.Current;
		public AssetBundle OutputBundle { get; private set; }

		public static Dictionary<string, CookingRules> CookingRulesMap;

		public string BundleBeingCookedName { get; set; }

		public static event Action BeginCookBundles;
		public static event Action EndCookBundles;

		private static bool cookCanceled = false;

		public readonly Target Target;
		public TargetPlatform Platform => Target.Platform;

		public static bool CookForTarget(Target target, List<string> bundles, out string errorMessage)
		{
			var assetCooker = new AssetCooker(target);
			var skipCooking = The.Workspace.ProjectJson.GetValue<bool>("SkipAssetsCooking");
			if (!skipCooking) {
				return assetCooker.Cook(bundles ?? assetCooker.GetListOfAllBundles(), out errorMessage);
			} else {
				Console.WriteLine("-------------  Skip Assets Cooking -------------");
				errorMessage = null;
				return true;
			}
		}

		public void AddStage(ICookingStage stage)
		{
			CookStages.Add(stage);
		}

		public void RemoveStage(ICookingStage stage)
		{
			CookStages.Remove(stage);
		}

		// TODO: Shouldn't be part of asset cooker
		public List<string> GetListOfAllBundles()
		{
			var cookingRulesMap = CookingRulesBuilder.Build(InputBundle, Target);
			var bundles = new HashSet<string>();
			foreach (var dictionaryItem in cookingRulesMap) {
				foreach (var bundle in dictionaryItem.Value.Bundles) {
					bundles.Add(bundle);
				}
			}
			return bundles.ToList();
		}

		private bool Cook(List<string> bundles, out string errorMessage)
		{
			AssetCache.Instance.Initialize();
			CookingRulesMap = CookingRulesBuilder.Build(InputBundle, Target);
			LogText = "";
			var allTimer = StartBenchmark(
				$"Asset cooking. Asset cache mode: {AssetCache.Instance.Mode}. Active platform: {Target.Platform}" +
				System.Environment.NewLine +
				DateTime.Now +
				System.Environment.NewLine
			);
			PluginLoader.BeforeBundlesCooking();
			var requiredCookCode = !The.Workspace.ProjectJson.GetValue<bool>("SkipCodeCooking");
			try {
				GetBundlesToCook(bundles, out var bundlesToCook, out var cookingUnitCount);
				UserInterface.Instance.SetupProgressBar(cookingUnitCount);
				BeginCookBundles?.Invoke();
				var assetsGroupedByBundles = GetAssetsGroupedByBundles(InputBundle.EnumerateFiles(), bundlesToCook);
				for (int i = 0; i < bundlesToCook.Count; i++) {
					var extraTimer = StartBenchmark();
					CookBundle(bundlesToCook[i], assetsGroupedByBundles[i]);
					StopBenchmark(extraTimer, $"{bundlesToCook[i]} cooked: ");
				}
				if (UserInterface.Instance.ShouldUnpackBundles()) {
					foreach (var bundle in bundles) {
						UnpackBundle(bundle);
					}
				}
				var extraBundles = bundles.ToList();
				extraBundles.Remove(CookingRulesBuilder.MainBundleName);
				extraBundles.Reverse();
				PluginLoader.AfterBundlesCooked(extraBundles);
				if (requiredCookCode) {
					CodeCooker.Cook(Target, CookingRulesMap, bundles.ToList());
				}
				StopBenchmark(allTimer, "All bundles cooked: ");
				PrintBenchmark();
			} catch (System.Exception e) {
				Console.WriteLine(e.Message);
				errorMessage = e.Message;
			} finally {
				cookCanceled = false;
				EndCookBundles?.Invoke();
				UserInterface.Instance.StopProgressBar();
			}
			errorMessage = null;
			return true;
		}

		private void UnpackBundle(string bundleName)
		{
			var destinationPath = The.Workspace.GetBundlePath(Target.Platform, bundleName) + ".Unpacked";
			if (!Directory.Exists(destinationPath)) {
				Directory.CreateDirectory(destinationPath);
			}
			using (var source = CreateOutputBundle(bundleName))
			using (var destination = new UnpackedAssetBundle(destinationPath)) {
				foreach (var file in destination.EnumerateFiles()) {
					if (!source.FileExists(file)) {
						try {
							destination.DeleteFile(file);
						} catch {
							// Do nothing.
						}
					}
				}
				foreach (var file in source.EnumerateFiles()) {
					var exists = destination.FileExists(file);
					if (!exists || destination.GetFileHash(file) != source.GetFileHash(file)) {
						try {
							if (exists) {
								destination.DeleteFile(file);
							}
							destination.ImportFile(
								file,
								new MemoryStream(source.ReadFile(file)),
								source.GetFileCookingUnitHash(file),
								source.GetFileAttributes(file)
							);
						} catch {
							// Do nothing.
						}
					}
				}
				try {
					DeleteEmptyDirectories(destination.BaseDirectory);
				} catch {
					// Do nothing.
				}
			}

			static void DeleteEmptyDirectories(string baseDirectory)
			{
				foreach (var directory in Directory.GetDirectories(baseDirectory)) {
					DeleteEmptyDirectories(directory);
					if (Directory.GetFiles(directory).Length == 0 &&
					    Directory.GetDirectories(directory).Length == 0)
					{
						Directory.Delete(directory, false);
					}
				}
			}
		}

		private void GetBundlesToCook(List<string> allBundles, out List<string> bundlesToCook, out int cookingUnitCount)
		{
			cookingUnitCount = 0;
			bundlesToCook = new List<string>();
			var assetsGroupedByBundles = GetAssetsGroupedByBundles(InputBundle.EnumerateFiles(), allBundles);
			for (int i = 0; i < allBundles.Count; i++) {
				var savedInputBundle = InputBundle;
				AssetBundle.SetCurrent(
					new CustomSetAssetBundle(InputBundle, assetsGroupedByBundles[i]),
					resetTexturePool: false);
				OutputBundle = CreateOutputBundle(allBundles[i]);
				try {
					var unitsToCookHashes = new SortedSet<SHA256>();
					var cookedUnitsHashes = new SortedSet<SHA256>();
					foreach (var stage in CookStages) {
						foreach (var (_, hash) in stage.EnumerateCookingUnits()) {
							unitsToCookHashes.Add(hash);
						}
					}
					foreach (var path in OutputBundle.EnumerateFiles()) {
						cookedUnitsHashes.Add(OutputBundle.GetFileCookingUnitHash(path));
					}
					if (!unitsToCookHashes.SetEquals(cookedUnitsHashes)) {
						bundlesToCook.Add(allBundles[i]);
						cookingUnitCount += unitsToCookHashes.Count;
					}
				} finally {
					OutputBundle.Dispose();
					OutputBundle = null;
					AssetBundle.SetCurrent(savedInputBundle, resetTexturePool: false);
				}
			}
		}

		private void CookBundle(string bundleName, List<string> assets)
		{
			MemoryAssetBundle memoryBundle;
			using (var packedBundle = CreateOutputBundle(bundleName)) {
				OutputBundle = new VerboseAssetBundle(memoryBundle = MemoryAssetBundle.ReadFromBundle(packedBundle));
			}
			try {
				Console.WriteLine("------------- Cooking Assets ({0}) -------------", bundleName);
				CookBundleHelper();
				// Open the bundle again in order to make some plugin post-processing (e.g. generate code from scene assets)
				using (new DirectoryChanger(The.Workspace.AssetsDirectory)) {
					PluginLoader.AfterAssetsCooked(bundleName);
				}
				DeleteBundle();
				using var packedBundle = CreateOutputBundle(bundleName);
				memoryBundle.WriteToBundle(packedBundle);
			} finally {
				OutputBundle.Dispose();
				OutputBundle = null;
			}

			void DeleteBundle()
			{
				var bundlePath = The.Workspace.GetBundlePath(Target.Platform, bundleName);
				if (File.Exists(bundlePath)) {
					File.Delete(bundlePath);
				}
			}

			void CookBundleHelper()
			{
				var savedInputBundle = InputBundle;
				AssetBundle.SetCurrent(new CustomSetAssetBundle(InputBundle, assets), resetTexturePool: false);
				BundleBeingCookedName = bundleName;
				try {
					var unitsToCookHashes = new HashSet<SHA256>();
					foreach (var stage in CookStages) {
						foreach (var (cookUnit, hash) in stage.EnumerateCookingUnits()) {
							CheckCookCancelation();
							unitsToCookHashes.Add(hash);
						}
					}
					// Delete outdated assets from the OutputBundle
					foreach (var file in OutputBundle.EnumerateFiles().ToList()) {
						CheckCookCancelation();
						if (!unitsToCookHashes.Contains(OutputBundle.GetFileCookingUnitHash(file))) {
							OutputBundle.DeleteFile(file);
						}
					}
					// Cook missing assets to the OutputBundle
					var cookedUnitsHashes = new HashSet<SHA256>();
					foreach (var file in OutputBundle.EnumerateFiles()) {
						cookedUnitsHashes.Add(OutputBundle.GetFileCookingUnitHash(file));
					}
					foreach (var stage in CookStages) {
						CheckCookCancelation();
						foreach (var (cookingUnit, hash) in stage.EnumerateCookingUnits()) {
							if (!cookedUnitsHashes.Contains(hash)) {
								stage.Cook(cookingUnit, hash);
							}
							UserInterface.Instance.IncreaseProgressBar();
						}
					}
					// Warn about non-power of two textures
					foreach (var path in OutputBundle.EnumerateFiles()) {
						if ((OutputBundle.GetFileAttributes(path) & AssetAttributes.NonPowerOf2Texture) != 0) {
							Console.WriteLine("Warning: non-power of two texture: {0}", path);
						}
					}
				} finally {
					AssetBundle.SetCurrent(savedInputBundle, resetTexturePool: false);
					BundleBeingCookedName = null;
				}
			}
		}

		private class VerboseAssetBundle : WrappedAssetBundle
		{
			private Dictionary<string, SHA256> deletedFiles = new Dictionary<string, SHA256>();
			public VerboseAssetBundle(AssetBundle bundle) : base(bundle)
			{ }

			public override void DeleteFile(string path)
			{
				deletedFiles.Add(path, GetFileHash(path));
				base.DeleteFile(path);
			}

			public override void ImportFile(string path, Stream stream, SHA256 cookingUnitHash, AssetAttributes attributes)
			{
				base.ImportFile(path, stream, cookingUnitHash, attributes);
				if (deletedFiles.TryGetValue(path, out var hash)) {
					if (hash != GetFileHash(path)) {
						Console.WriteLine("* " + path);
					}
					deletedFiles.Remove(path);
				} else {
					Console.WriteLine("+ " + path);
				}
			}

			public override void Dispose()
			{
				base.Dispose();
				foreach (var (path, _) in deletedFiles) {
					Console.WriteLine("- " + path);
				}
			}
		}

		List<string>[] GetAssetsGroupedByBundles(IEnumerable<string> files, List<string> bundles)
		{
			string[] emptyBundleNamesArray = {};
			string[] mainBundleNameArray = { CookingRulesBuilder.MainBundleName };

			var assets = Enumerable.Range(0, bundles.Count).Select(i => new List<string>()).ToArray();
			foreach (var file in files) {
				foreach (var bundleName in GetAssetBundleNames(file)) {
					var i = bundles.IndexOf(bundleName);
					if (i != -1) {
						assets[i].Add(file);
					}
				}
			}
			return assets;

			string[] GetAssetBundleNames(string path)
			{
				if (CookingRulesMap.TryGetValue(path, out var rules)) {
					return rules.Ignore ? emptyBundleNamesArray : rules.Bundles;
				} else {
					// There are no cooking rules for text files, consider them as part of the main bundle.
					return mainBundleNameArray;
				}
			}
		}

		private AssetBundle CreateOutputBundle(string bundleName)
		{
			var bundlePath = The.Workspace.GetBundlePath(Target.Platform, bundleName);
			// Create directory for bundle if it placed in subdirectory
			try {
				Directory.CreateDirectory(Path.GetDirectoryName(bundlePath));
			} catch (System.Exception) {
				Lime.Debug.Write("Failed to create directory: {0} {1}", Path.GetDirectoryName(bundlePath));
				throw;
			}
			AssetBundle packedBundle;
			try {
				packedBundle = new PackedAssetBundle(bundlePath, AssetBundleFlags.Writable);
			} catch (InvalidBundleVersionException) {
				File.Delete(bundlePath);
				packedBundle = new PackedAssetBundle(bundlePath, AssetBundleFlags.Writable);
			}
			return packedBundle;
		}

		public AssetCooker(Target target)
		{
			this.Target = target;
			AddStage(new SyncAtlases(this));
			AddStage(new SyncRawAssets(this, ".json", AssetAttributes.ZippedDeflate));
			AddStage(new SyncRawAssets(this, ".cfg", AssetAttributes.ZippedDeflate));
			AddStage(new SyncTxtAssets(this));
			AddStage(new SyncRawAssets(this, ".csv", AssetAttributes.ZippedDeflate));
			var rawAssetExtensions = The.Workspace.ProjectJson["RawAssetExtensions"] as string;
			if (rawAssetExtensions != null) {
				foreach (var extension in rawAssetExtensions.Split(' ')) {
					AddStage(new SyncRawAssets(this, extension, AssetAttributes.ZippedDeflate));
				}
			}
			AddStage(new SyncTextures(this));
			AddStage(new SyncFonts(this));
			AddStage(new SyncCompoundFonts(this));
			AddStage(new SyncRawAssets(this, ".ttf"));
			AddStage(new SyncRawAssets(this, ".otf"));
			AddStage(new SyncRawAssets(this, ".ogv"));
			AddStage(new SyncScenes(this));
			AddStage(new SyncSounds(this));
			AddStage(new SyncRawAssets(this, ".shader"));
			AddStage(new SyncRawAssets(this, ".xml"));
			AddStage(new SyncRawAssets(this, ".raw"));
			AddStage(new SyncRawAssets(this, ".bin"));
			AddStage(new SyncModels(this));
		}

		public static void CancelCook()
		{
			cookCanceled = true;
		}

		public static void CheckCookCancelation()
		{
			if (cookCanceled) {
				throw new OperationCanceledException("------------- Cooking canceled -------------");
			}
		}

		private static string LogText;
		private static Stopwatch StartBenchmark(string text="")
		{
			if (!The.Workspace.BenchmarkEnabled) {
				return null;
			}
			LogText += text;
			var timer = new Stopwatch();
			timer.Start();
			return timer;
		}

		private static void StopBenchmark(Stopwatch timer, string text)
		{
			if (!The.Workspace.BenchmarkEnabled) {
				return;
			}
			timer.Stop();
			LogText += text + $"{timer.ElapsedMilliseconds} ms" + System.Environment.NewLine;
		}

		private static void PrintBenchmark()
		{
			if (!The.Workspace.BenchmarkEnabled) {
				return;
			}
			using var w = File.AppendText(Path.Combine(The.Workspace.ProjectDirectory, "cache.log"));
			w.WriteLine(LogText);
			w.WriteLine();
			w.WriteLine();
		}
	}
}
