using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using Lime;

namespace Orange
{
	class SyncAtlases : AssetCookerCookStage, ICookStage
	{
		public IEnumerable<string> ImportedExtensions { get { yield return textureExtension; } }
		public IEnumerable<string> BundleExtensions { get { yield return atlasPartExtension; } }

		private readonly string textureExtension = ".png";
		private readonly string atlasPartExtension = ".atlasPart";

		public SyncAtlases(AssetCooker assetCooker) : base(assetCooker) { }

		public int GetOperationCount() => AssetCooker.InputBundle.EnumerateFiles(null, textureExtension).Count();

		public void Action()
		{
			var textureHashes = new Dictionary<string, SHA1>(StringComparer.OrdinalIgnoreCase);
			foreach (var file in AssetCooker.InputBundle.EnumerateFiles(null, textureExtension)) {
				textureHashes[file] = AssetCooker.InputBundle.GetSourceSHA1(file);
			}
			var atlasChainsToRebuild = new HashSet<string>();
			// Figure out atlas chains to rebuild
			foreach (var atlasPartPath in AssetCooker.OutputBundle.EnumerateFiles().ToList()) {
				if (!atlasPartPath.EndsWith(atlasPartExtension, StringComparison.OrdinalIgnoreCase))
					continue;

				// If atlas part has been outdated we should rebuild full atlas chain
				var srcTexturePath = Path.ChangeExtension(atlasPartPath, textureExtension);
				if (
					!textureHashes.ContainsKey(srcTexturePath) /* .png has been deleted */ ||
					// Check whether png or cooking rules have been modified.
					AssetCooker.OutputBundle.GetSourceSHA1(atlasPartPath) !=
						SHA1.Compute(textureHashes[srcTexturePath], AssetCooker.CookingRulesMap[srcTexturePath].SHA1)
				) {
					srcTexturePath = AssetPath.Combine(The.Workspace.AssetsDirectory, srcTexturePath);
					var part = InternalPersistence.Instance.ReadObjectFromBundle<TextureAtlasElement.Params>(AssetCooker.OutputBundle, atlasPartPath);
					var atlasChain = Path.GetFileNameWithoutExtension(part.AtlasPath);
					atlasChainsToRebuild.Add(atlasChain);
					if (!textureHashes.ContainsKey(srcTexturePath)) {
						AssetCooker.DeleteFileFromBundle(atlasPartPath);
					} else {
						srcTexturePath = Path.ChangeExtension(atlasPartPath, textureExtension);
						if (AssetCooker.CookingRulesMap[srcTexturePath].TextureAtlas != null) {
							var rules = AssetCooker.CookingRulesMap[srcTexturePath];
							atlasChainsToRebuild.Add(rules.TextureAtlas);
						} else {
							AssetCooker.DeleteFileFromBundle(atlasPartPath);
						}
					}
				}
			}
			// Find which new textures must be added to the atlas chain
			foreach (var t in textureHashes) {
				var atlasPartPath = Path.ChangeExtension(t.Key, atlasPartExtension);
				var cookingRules = AssetCooker.CookingRulesMap[t.Key];
				var atlasNeedRebuild = cookingRules.TextureAtlas != null && !AssetCooker.OutputBundle.FileExists(atlasPartPath);
				if (atlasNeedRebuild) {
					atlasChainsToRebuild.Add(cookingRules.TextureAtlas);
				} else {
					UserInterface.Instance.IncreaseProgressBar();
				}
			}
			foreach (var atlasChain in atlasChainsToRebuild) {
				AssetCooker.CheckCookCancelation();
				BuildAtlasChain(atlasChain);
			}
		}

		private void BuildAtlasChain(string atlasChain)
		{
			for (var i = 0; i < AssetCooker.MaxAtlasChainLength; i++) {
				var atlasPath = AssetCooker.GetAtlasPath(atlasChain, i);
				if (AssetCooker.OutputBundle.FileExists(atlasPath)) {
					AssetCooker.DeleteFileFromBundle(atlasPath);
				}
				else {
					break;
				}
			}
			var pluginItems = new Dictionary<string, List<TextureTools.AtlasItem>>();
			var items = new Dictionary<(AtlasOptimization AtlasOptimization, int MaxAtlasSize), List<TextureTools.AtlasItem>>();
			foreach (var file in AssetBundle.Current.EnumerateFiles(null, textureExtension)) {
				var cookingRules = AssetCooker.CookingRulesMap[file];
				if (cookingRules.TextureAtlas == atlasChain) {
					var item = new TextureTools.AtlasItem {
						Path = Path.ChangeExtension(file, atlasPartExtension),
						CookingRules = cookingRules,
						SourceExtension = Path.GetExtension(file)
					};
					var bitmapInfo = TextureTools.BitmapInfo.FromFile(AssetCooker.InputBundle, file);
					if (bitmapInfo == null) {
						using (var bitmap = TextureTools.OpenAtlasItemBitmapAndRescaleIfNeeded(AssetCooker.Platform, item)) {
							item.BitmapInfo = TextureTools.BitmapInfo.FromBitmap(bitmap);
						}
					} else {
						var srcTexturePath = AssetPath.Combine(The.Workspace.AssetsDirectory, Path.ChangeExtension(item.Path, item.SourceExtension));
						if (TextureTools.ShouldDownscale(AssetCooker.Platform, bitmapInfo, item.CookingRules)) {
							TextureTools.DownscaleTextureInfo(AssetCooker.Platform, bitmapInfo, srcTexturePath, item.CookingRules);
						}
						// Ensure that no image exceeded maxAtlasSize limit
						TextureTools.DownscaleTextureToFitAtlas(bitmapInfo, srcTexturePath, item.CookingRules.MaxAtlasSize);
						item.BitmapInfo = bitmapInfo;
					}
					var k = cookingRules.AtlasPacker;
					if (!string.IsNullOrEmpty(k) && k != "Default") {
						List<TextureTools.AtlasItem> l;
						if (!pluginItems.TryGetValue(k, out l)) {
							pluginItems.Add(k, l = new List<TextureTools.AtlasItem>());
						}
						l.Add(item);
					} else {
						var key = (cookingRules.AtlasOptimization, cookingRules.MaxAtlasSize);
						if (!items.TryGetValue(key, out var list)) {
							items.Add(key, list = new List<TextureTools.AtlasItem>());
						}
						list.Add(item);
					}
				}
			}
			var initialAtlasId = 0;
			foreach (var ((atlasOptimization, maxAtlasSize), atlasItems) in items) {
				if (atlasItems.Any()) {
					if (AssetCooker.Platform == TargetPlatform.iOS) {
						Predicate<PVRFormat> isRequireSquare = (format) => {
							return
								format == PVRFormat.PVRTC2 ||
								format == PVRFormat.PVRTC4 ||
								format == PVRFormat.PVRTC4_Forced;
						};
						var square = atlasItems.Where(item => isRequireSquare(item.CookingRules.PVRFormat)).ToList();
						var nonSquare = atlasItems.Where(item => !isRequireSquare(item.CookingRules.PVRFormat)).ToList();
						initialAtlasId = PackItemsToAtlas(atlasChain, square, atlasOptimization, maxAtlasSize, initialAtlasId, true);
						initialAtlasId = PackItemsToAtlas(atlasChain, nonSquare, atlasOptimization, maxAtlasSize, initialAtlasId, false);
					} else {
						initialAtlasId = PackItemsToAtlas(atlasChain, atlasItems, atlasOptimization, maxAtlasSize, initialAtlasId, false);
					}
				}
			}
			var packers = PluginLoader.CurrentPlugin.AtlasPackers.ToDictionary(i => i.Metadata.Id, i => i.Value);
			foreach (var kv in pluginItems) {
				if (!packers.ContainsKey(kv.Key)) {
					throw new InvalidOperationException($"Packer {kv.Key} not found");
				}
				initialAtlasId = packers[kv.Key](atlasChain, kv.Value, initialAtlasId);
			}
		}

		private int PackItemsToAtlas(string atlasChain, List<TextureTools.AtlasItem> items,
			AtlasOptimization atlasOptimization, int maxAtlasSize, int initialAtlasId, bool squareAtlas)
		{
			// Sort images in descending size order
			items.Sort((x, y) => {
				var a = Math.Max(x.BitmapInfo.Width, x.BitmapInfo.Height);
				var b = Math.Max(y.BitmapInfo.Width, y.BitmapInfo.Height);
				return b - a;
			});

			var atlasId = initialAtlasId;
			while (items.Count > 0) {
				if (atlasId >= AssetCooker.MaxAtlasChainLength) {
					throw new Lime.Exception("Too many textures in the atlas chain {0}", atlasChain);
				}
				var bestSize = new Size(0, 0);
				double bestPackRate = 0;
				int minItemsLeft = Int32.MaxValue;

				// TODO: Fix for non-square atlases
				var maxTextureSize = items.Max(item => Math.Max(item.BitmapInfo.Height, item.BitmapInfo.Width));
				var minAtlasSize = Math.Max(64, TextureTools.CalcUpperPowerOfTwo(maxTextureSize));

				foreach (var size in EnumerateAtlasSizes(squareAtlas: squareAtlas, minSize: minAtlasSize, maxSize: maxAtlasSize)) {
					var prevAllocated = items.Where(i => i.Allocated).ToList();
					PackItemsToAtlas(items, size, out double packRate);
					switch (atlasOptimization) {
						case AtlasOptimization.Memory:
							if (packRate * 0.95f > bestPackRate) {
								bestPackRate = packRate;
								bestSize = size;
							}
							break;
						case AtlasOptimization.DrawCalls: {
								var notAllocatedCount = items.Count(item => !item.Allocated);
								if (notAllocatedCount < minItemsLeft) {
									minItemsLeft = notAllocatedCount;
									bestSize = size;
								} else if (notAllocatedCount == minItemsLeft) {
									if (items.Where(i => i.Allocated).SequenceEqual(prevAllocated)) {
										continue;
									} else {
										minItemsLeft = notAllocatedCount;
										bestSize = size;
									}
								}
								if (notAllocatedCount == 0) {
									goto end;
								}
								break;
							}
					}
				}
				end:
				if (atlasOptimization == AtlasOptimization.Memory && bestPackRate == 0) {
					throw new Lime.Exception("Failed to create atlas '{0}'", atlasChain);
				}
				PackItemsToAtlas(items, bestSize, out bestPackRate);
				CopyAllocatedItemsToAtlas(items, atlasChain, atlasId, bestSize);
				items.RemoveAll(x => x.Allocated);
				atlasId++;
			}
			return atlasId;
		}

		private void PackItemsToAtlas(List<TextureTools.AtlasItem> items, Size atlasSize, out double packRate)
		{
			items.ForEach(i => i.Allocated = false);
			// Reserve space for default one-pixel padding.
			atlasSize.Width += 2;
			atlasSize.Height += 2;
			var rectAllocator = new RectAllocator(atlasSize);
			TextureTools.AtlasItem firstAllocatedItem = null;
			foreach (var item in items) {
				var padding = item.CookingRules.AtlasItemPadding;
				var paddedItemSize = new Size(item.BitmapInfo.Width + padding * 2, item.BitmapInfo.Height + padding * 2);
				if (firstAllocatedItem == null || AreAtlasItemsCompatible(items, firstAllocatedItem, item)) {
					if (rectAllocator.Allocate(paddedItemSize, out item.AtlasRect)) {
						item.Allocated = true;
						firstAllocatedItem = firstAllocatedItem ?? item;
					}
				}
			}
			packRate = rectAllocator.GetPackRate();
			// Adjust item rects according to theirs paddings.
			foreach (var item in items) {
				if (!item.Allocated) {
					continue;
				}
				var atlasRect = item.AtlasRect;
				atlasRect.A += new IntVector2(item.CookingRules.AtlasItemPadding);
				atlasRect.B -= new IntVector2(item.CookingRules.AtlasItemPadding);
				// Don't leave space between item rectangle and texture boundaries for items with 1 pixel padding.
				if (item.CookingRules.AtlasItemPadding == 1) {
					atlasRect.A -= new IntVector2(1);
					atlasRect.B -= new IntVector2(1);
				}
				item.AtlasRect = atlasRect;
			}
		}

		/// <summary>
		/// Checks whether two items can be packed to the same texture
		/// </summary>
		private bool AreAtlasItemsCompatible(List<TextureTools.AtlasItem> items, TextureTools.AtlasItem item1, TextureTools.AtlasItem item2)
		{
			if (item1.CookingRules.GenerateOpacityMask != item2.CookingRules.GenerateOpacityMask) {
				return false;
			}
			if (item1.CookingRules.WrapMode != item2.CookingRules.WrapMode) {
				return false;
			}
			if (item1.CookingRules.MinFilter != item2.CookingRules.MinFilter) {
				return false;
			}
			if (item1.CookingRules.MagFilter != item2.CookingRules.MagFilter) {
				return false;
			}
			if (item1.CookingRules.MipMaps != item2.CookingRules.MipMaps) {
				return false;
			}
			if (item1.CookingRules.MaxAtlasSize != item2.CookingRules.MaxAtlasSize) {
				return false;
			}
			if (items.Count > 0) {
				if (item1.CookingRules.WrapMode != TextureWrapMode.Clamp || item2.CookingRules.WrapMode != TextureWrapMode.Clamp) {
					return false;
				}
			}
			switch (AssetCooker.Platform) {
				case TargetPlatform.Android:
				case TargetPlatform.iOS:
					return item1.CookingRules.PVRFormat == item2.CookingRules.PVRFormat && item1.BitmapInfo.HasAlpha == item2.BitmapInfo.HasAlpha;
				case TargetPlatform.Win:
				case TargetPlatform.Mac:
					return item1.CookingRules.DDSFormat == item2.CookingRules.DDSFormat;
				default:
					throw new ArgumentException();
			}
		}

		private void CopyAllocatedItemsToAtlas(List<TextureTools.AtlasItem> items, string atlasChain, int atlasId, Size size)
		{
			var atlasPath = AssetCooker.GetAtlasPath(atlasChain, atlasId);
			var atlasPixels = new Color4[size.Width * size.Height];
			foreach (var item in items.Where(i => i.Allocated)) {
				var atlasRect = item.AtlasRect;
				using (var bitmap = TextureTools.OpenAtlasItemBitmapAndRescaleIfNeeded(AssetCooker.Platform, item)) {
					CopyPixels(bitmap, atlasPixels, atlasRect.A.X, atlasRect.A.Y, size.Width, size.Height);
				}
				var atlasPart = new TextureAtlasElement.Params {
					AtlasRect = atlasRect,
					AtlasPath = Path.ChangeExtension(atlasPath, null)
				};
				var srcPath = Path.ChangeExtension(item.Path, item.SourceExtension);
				InternalPersistence.Instance.WriteObjectToBundle(AssetCooker.OutputBundle, item.Path, atlasPart, Persistence.Format.Binary,
					item.SourceExtension, SHA1.Compute(AssetCooker.InputBundle.GetSourceSHA1(srcPath), item.CookingRules.SHA1), AssetAttributes.None);
				// Delete non-atlased texture since now its useless
				var texturePath = Path.ChangeExtension(item.Path, AssetCooker.GetPlatformTextureExtension());
				if (AssetCooker.OutputBundle.FileExists(texturePath)) {
					AssetCooker.DeleteFileFromBundle(texturePath);
				}
				UserInterface.Instance.IncreaseProgressBar();
			}
			Console.WriteLine("+ " + atlasPath);
			var firstItem = items.First(i => i.Allocated);
			using (var atlas = new Bitmap(atlasPixels, size.Width, size.Height)) {
				AssetCooker.ImportTexture(atlasPath, atlas, firstItem.CookingRules, default);
			}
		}

		private IEnumerable<Size> EnumerateAtlasSizes(bool squareAtlas, int minSize, int maxSize)
		{
			if (squareAtlas) {
				for (var i = minSize; i <= maxSize; i *= 2) {
					yield return new Size(i, i);
				}
			} else {
				for (var i = minSize; i <= maxSize / 2; i *= 2) {
					yield return new Size(i, i);
					yield return new Size(i * 2, i);
					yield return new Size(i, i * 2);
				}
				yield return new Size(maxSize, maxSize);
			}
		}

		private void CopyPixels(Bitmap source, Color4[] dstPixels, int dstX, int dstY, int dstWidth, int dstHeight)
		{
			if (source.Width > dstWidth - dstX || source.Height > dstHeight - dstY) {
				throw new Lime.Exception(
					"Unable to copy pixels. Source image runs out of the bounds of destination image.");
			}
			var srcPixels = source.GetPixels();
			// Make 1-pixel border around image by duplicating image edges
			for (int y = -1; y <= source.Height; y++) {
				int dstRow = y + dstY;
				if (dstRow < 0 || dstRow >= dstHeight) {
					continue;
				}
				int srcRow = y.Clamp(0, source.Height - 1);
				int srcOffset = srcRow * source.Width;
				int dstOffset = (y + dstY) * dstWidth + dstX;
				Array.Copy(srcPixels, srcOffset, dstPixels, dstOffset, source.Width);
				if (dstX > 0) {
					dstPixels[dstOffset - 1] = srcPixels[srcOffset];
				}
				if (dstX + source.Width < dstWidth) {
					dstPixels[dstOffset + source.Width] = srcPixels[srcOffset + source.Width - 1];
				}
			}
		}
	}
}
