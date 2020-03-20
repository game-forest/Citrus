using System;
using System.Diagnostics;
using System.Linq;
using System.IO;
using System.Collections.Generic;
using System.Globalization;
using Lime;
using Orange.FbxImporter;
using Debug = Lime.Debug;
using FileInfo = Lime.FileInfo;

namespace Orange
{
	public class AssetCooker
	{
		public List<ICookStage> CookStages { get; } = new List<ICookStage>();

		public delegate bool Converter(string srcPath, string dstPath);

		public AssetBundle InputBundle => AssetBundle.Current;
		public AssetBundle OutputBundle { get; private set; }

		public static Dictionary<string, CookingRules> CookingRulesMap;
		public static HashSet<string> ModelsToRebuild = new HashSet<string>();

		private static string atlasesPostfix = string.Empty;

		public const int MaxAtlasChainLength = 1000;

		public static event Action BeginCookBundles;
		public static event Action EndCookBundles;

		private static bool cookCanceled = false;

		public readonly Target Target;
		public TargetPlatform Platform => Target.Platform;

		public static void CookForTarget(Target target, List<string> bundles = null)
		{
			var assetCooker = new AssetCooker(target);
			var skipCooking = The.Workspace.ProjectJson.GetValue<bool>("SkipAssetsCooking");
			if (!skipCooking) {
				assetCooker.Cook(bundles ?? assetCooker.GetListOfAllBundles());
			} else {
				Console.WriteLine("-------------  Skip Assets Cooking -------------");
			}
		}

		public void AddStage(ICookStage stage)
		{
			CookStages.Add(stage);
		}

		public void RemoveStage(ICookStage stage)
		{
			CookStages.Remove(stage);
		}

		public static string GetOriginalAssetExtension(string path)
		{
			var ext = Path.GetExtension(path).ToLower(CultureInfo.InvariantCulture);
			switch (ext) {
			case ".dds":
			case ".pvr":
			case ".atlasPart":
			case ".mask":
			case ".jpg":
				return ".png";
			case ".sound":
				return ".ogg";
			case ".t3d":
				return ".fbx";
			default:
				return ext;
			}
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

		public string GetPlatformTextureExtension()
		{
			switch (Target.Platform) {
				case TargetPlatform.iOS:
				case TargetPlatform.Android:
					return ".pvr";
				default:
					return ".dds";
			}
		}

		public void Cook(List<string> bundles)
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
			var bundleBackups = new List<string>();
			try {
				UserInterface.Instance.SetupProgressBar(CalculateOperationCount(bundles));
				BeginCookBundles?.Invoke();
				var assetsGroupedByBundles = GetAssetsGroupedByBundles(InputBundle.EnumerateFileInfos(), bundles);
				for (int i = 0; i < bundles.Count; i++) {
					var extraTimer = StartBenchmark();
					CookBundle(bundles[i], assetsGroupedByBundles[i], bundleBackups);
					StopBenchmark(extraTimer, $"{bundles[i]} cooked: ");
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
				RestoreBackups(bundleBackups);
			} finally {
				cookCanceled = false;
				RemoveBackups(bundleBackups);
				EndCookBundles?.Invoke();
				UserInterface.Instance.StopProgressBar();
			}
		}

		private int CalculateOperationCount(List<string> bundles)
		{
			var assetCount = 0;
			var assetsGroupedByBundles = GetAssetsGroupedByBundles(InputBundle.EnumerateFileInfos(), bundles);
			for (int i = 0; i < bundles.Count; i++) {
				var savedInputBundle = InputBundle;
				AssetBundle.SetCurrent(
					new CustomSetAssetBundle(InputBundle, assetsGroupedByBundles[i]),
					resetTexturePool: false);
				OutputBundle = CreateOutputBundle(bundles[i], bundleBackups: null);
				try {
					assetCount += CookStages.Sum(stage => stage.GetOperationCount());
				} finally {
					OutputBundle.Dispose();
					OutputBundle = null;
					AssetBundle.SetCurrent(savedInputBundle, resetTexturePool: false);
				}
			}
			return assetCount;
		}

		private void CookBundle(string bundleName, List<FileInfo> assets, List<string> bundleBackups)
		{
			OutputBundle = CreateOutputBundle(bundleName, bundleBackups);
			try {
				CookBundleHelper();
				// Open the bundle again in order to make some plugin post-processing (e.g. generate code from scene assets)
				using (new DirectoryChanger(The.Workspace.AssetsDirectory)) {
					PluginLoader.AfterAssetsCooked(bundleName);
				}
			} finally {
				OutputBundle.Dispose();
				OutputBundle = null;
			}

			void CookBundleHelper()
			{
				Console.WriteLine("------------- Cooking Assets ({0}) -------------", bundleName);
				var savedInputBundle = InputBundle;
				AssetBundle.SetCurrent(new CustomSetAssetBundle(InputBundle, assets), resetTexturePool: false);
				// Every asset bundle must have its own atlases folder, so they aren't conflict with each other
				atlasesPostfix = bundleName != CookingRulesBuilder.MainBundleName ? bundleName : "";
				try {
					foreach (var stage in CookStages) {
						CheckCookCancelation();
						stage.Action();
					}
					// Warn about non-power of two textures
					foreach (var path in OutputBundle.EnumerateFiles()) {
						if ((OutputBundle.GetAttributes(path) & AssetAttributes.NonPowerOf2Texture) != 0) {
							Console.WriteLine("Warning: non-power of two texture: {0}", path);
						}
					}
				} finally {
					AssetBundle.SetCurrent(savedInputBundle, resetTexturePool: false);
					ModelsToRebuild.Clear();
					atlasesPostfix = "";
				}
			}
		}

		List<FileInfo>[] GetAssetsGroupedByBundles(IEnumerable<FileInfo> fileInfos, List<string> bundles)
		{
			string[] emptyBundleNamesArray = {};
			string[] mainBundleNameArray = { CookingRulesBuilder.MainBundleName };

			var assets = Enumerable.Range(0, bundles.Count).Select(i => new List<FileInfo>()).ToArray(); 
			foreach (var fi in fileInfos) {
				foreach (var bundleName in GetAssetBundleNames(fi.Path)) {
					var i = bundles.IndexOf(bundleName);
					if (i != -1) {
						assets[i].Add(fi);
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

		private AssetBundle CreateOutputBundle(string bundleName, List<string> bundleBackups)
		{
			var bundlePath = The.Workspace.GetBundlePath(Target.Platform, bundleName);
			// Create directory for bundle if it placed in subdirectory
			try {
				Directory.CreateDirectory(Path.GetDirectoryName(bundlePath));
			} catch (System.Exception) {
				Lime.Debug.Write("Failed to create directory: {0} {1}", Path.GetDirectoryName(bundlePath));
				throw;
			}
			var bundle = new PackedAssetBundle(bundlePath, AssetBundleFlags.Writable);
			bundle.OnWrite += () => {
				var backupPath = bundlePath + ".bak";
				if (bundleBackups.Contains(backupPath) || !File.Exists(bundlePath)) {
					return;
				}
				try {
					File.Copy(bundlePath, backupPath, overwrite: true);
				} catch (System.Exception e) {
					Console.WriteLine(e);
				}
				bundleBackups.Add(backupPath);
			};
			return bundle;
		}
		
		public AssetCooker(Target target)
		{
			this.Target = target;
			AddStage(new RemoveDeprecatedModels(this));
			AddStage(new SyncAtlases(this));
			AddStage(new SyncDeleted(this));
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
			AddStage(new DeleteOrphanedMasks(this));
			AddStage(new DeleteOrphanedTextureParams(this));
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

		public void DeleteFileFromBundle(string path)
		{
			Console.WriteLine("- " + path);
			OutputBundle.DeleteFile(path);
		}

		public string GetAtlasPath(string atlasChain, int index)
		{
			var path = AssetPath.Combine(
				"Atlases" + atlasesPostfix, atlasChain + '.' + index.ToString("000") + GetPlatformTextureExtension());
			return path;
		}

		public static bool AreTextureParamsDefault(ICookingRules rules)
		{
			return rules.MinFilter == TextureParams.Default.MinFilter &&
				rules.MagFilter == TextureParams.Default.MagFilter &&
				rules.WrapMode == TextureParams.Default.WrapModeU;
		}

		public void ImportTexture(string path, Bitmap texture, ICookingRules rules, DateTime time, byte[] CookingRulesSHA1)
		{
			var textureParamsPath = Path.ChangeExtension(path, ".texture");
			var textureParams = new TextureParams {
				WrapMode = rules.WrapMode,
				MinFilter = rules.MinFilter,
				MagFilter = rules.MagFilter,
			};

			if (!AreTextureParamsDefault(rules)) {
				TextureTools.UpscaleTextureIfNeeded(ref texture, rules, false);
				var isNeedToRewriteTexParams = true;
				if (OutputBundle.FileExists(textureParamsPath)) {
					var oldTexParams = InternalPersistence.Instance.ReadObjectFromBundle<TextureParams>(OutputBundle, textureParamsPath);
					isNeedToRewriteTexParams = !oldTexParams.Equals(textureParams);
				}
				if (isNeedToRewriteTexParams) {
					InternalPersistence.Instance.WriteObjectToBundle(OutputBundle, textureParamsPath, textureParams, Persistence.Format.Binary, ".texture",
						InputBundle.GetFileLastWriteTime(textureParamsPath), AssetAttributes.None, null);
				}
			} else {
				if (OutputBundle.FileExists(textureParamsPath)) {
					DeleteFileFromBundle(textureParamsPath);
				}
			}
			if (rules.GenerateOpacityMask) {
				var maskPath = Path.ChangeExtension(path, ".mask");
				OpacityMaskCreator.CreateMask(OutputBundle, texture, maskPath);
			}
			var attributes = AssetAttributes.ZippedDeflate;
			if (!TextureConverterUtils.IsPowerOf2(texture.Width) || !TextureConverterUtils.IsPowerOf2(texture.Height)) {
				attributes |= AssetAttributes.NonPowerOf2Texture;
			}
			switch (Target.Platform) {
				case TargetPlatform.Android:
				//case TargetPlatform.iOS:
					var f = rules.PVRFormat;
					if (f == PVRFormat.ARGB8 || f == PVRFormat.RGB565 || f == PVRFormat.RGBA4) {
						TextureConverter.RunPVRTexTool(texture, OutputBundle, path, attributes, rules.MipMaps, rules.HighQualityCompression, rules.PVRFormat, CookingRulesSHA1, time);
					} else {
						TextureConverter.RunEtcTool(texture, OutputBundle, path, attributes, rules.MipMaps, rules.HighQualityCompression, CookingRulesSHA1, time);
					}
					break;
				case TargetPlatform.iOS:
					TextureConverter.RunPVRTexTool(texture, OutputBundle, path, attributes, rules.MipMaps, rules.HighQualityCompression, rules.PVRFormat, CookingRulesSHA1, time);
					break;
				case TargetPlatform.Win:
				case TargetPlatform.Mac:
					TextureConverter.RunNVCompress(texture, OutputBundle, path, attributes, rules.DDSFormat, rules.MipMaps, CookingRulesSHA1, time);
					break;
				default:
					throw new Lime.Exception();
			}
		}

		public void DeleteModelExternalAnimations(string pathPrefix)
		{
			foreach (var path in OutputBundle.EnumerateFiles().ToList()) {
				if (path.EndsWith(".ant") && path.StartsWith(pathPrefix)) {
					OutputBundle.DeleteFile(path);
					Console.WriteLine("- " + path);
				}
			}
		}

		public static string GetModelAnimationPathPrefix(string modelPath)
		{
			return Toolbox.ToUnixSlashes(Path.GetDirectoryName(modelPath) + "/" + Path.GetFileNameWithoutExtension(modelPath) + "@");
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

		private void RemoveBackups(List<string> bundleBackups)
		{
			foreach (var path in bundleBackups) {
				try {
					if (File.Exists(path)) {
						File.Delete(path);
					}
				} catch (System.Exception e) {
					Console.WriteLine($"Failed to delete a bundle backup: {path} {e}");
				}
			}
		}

		private void RestoreBackups(List<string> bundleBackups)
		{
			foreach (var backupPath in bundleBackups) {
				// Remove ".bak" extension.
				var bundlePath = Path.ChangeExtension(backupPath, null);
				try {
					if (File.Exists(bundlePath)) {
						File.Delete(bundlePath);
					}
					File.Move(backupPath, bundlePath);
				} catch (System.Exception e) {
					Console.WriteLine(e);
				}
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
			using (var w = File.AppendText(Path.Combine(The.Workspace.ProjectDirectory, "cache.log"))) {
				w.WriteLine(LogText);
				w.WriteLine();
				w.WriteLine();
			}
		}

		public int GetUpdateOperationCount(string fileExtension) => InputBundle.EnumerateFileInfos(null, fileExtension).Count();

		public void SyncUpdated(string fileExtension, string bundleAssetExtension, Converter converter, Func<string, string, bool> extraOutOfDateChecker = null)
		{
			foreach (var fileInfo in InputBundle.EnumerateFileInfos(null, fileExtension)) {
				UserInterface.Instance.IncreaseProgressBar();
				var srcPath = fileInfo.Path;
				var dstPath = Path.ChangeExtension(srcPath, bundleAssetExtension);
				var bundled = OutputBundle.FileExists(dstPath);
				var srcRules = CookingRulesMap[srcPath];
				var needUpdate = !bundled || fileInfo.LastWriteTime != OutputBundle.GetFileLastWriteTime(dstPath);
				needUpdate = needUpdate || !srcRules.SHA1.SequenceEqual(OutputBundle.GetCookingRulesSHA1(dstPath));
				needUpdate = needUpdate || (extraOutOfDateChecker?.Invoke(srcPath, dstPath) ?? false);
				if (needUpdate) {
					if (converter != null) {
						try {
							if (converter(srcPath, dstPath)) {
								Console.WriteLine((bundled ? "* " : "+ ") + dstPath);
								CookingRules rules = null;
								if (!string.IsNullOrEmpty(dstPath)) {
									CookingRulesMap.TryGetValue(dstPath, out rules);
								}
								PluginLoader.AfterAssetUpdated(OutputBundle, rules, dstPath);
							}
						} catch (System.Exception e) {
							Console.WriteLine(
								"An exception was caught while processing '{0}': {1}\n", srcPath, e.Message);
							throw;
						}
					} else {
						Console.WriteLine((bundled ? "* " : "+ ") + dstPath);
						using (var stream = InputBundle.OpenFile(srcPath)) {
							OutputBundle.ImportFile(dstPath, stream, 0, fileExtension,
								InputBundle.GetFileLastWriteTime(srcPath), AssetAttributes.None,
								CookingRulesMap[srcPath].SHA1);
						}
					}
				}
			}
		}
	}
}
