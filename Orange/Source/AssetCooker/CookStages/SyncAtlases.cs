using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using Lime;

namespace Orange
{
	class SyncAtlases : ICookingStage
	{
		private const int MaxAtlasChainLength = 1000;
		private readonly string atlasPartExtension = ".atlasPart";
		private readonly AssetCooker assetCooker;

		public SyncAtlases(AssetCooker assetCooker)
		{
			this.assetCooker = assetCooker;
		}

		public IEnumerable<(string, SHA256)> EnumerateCookingUnits()
		{
			var atlasToHash = new Dictionary<string, SHA256>();
			foreach (var texturePath in assetCooker.InputBundle.EnumerateFiles(null, ".png")) {
				var textureCookingRules = AssetCooker.CookingRulesMap[texturePath];
				var atlas = textureCookingRules.TextureAtlas;
				if (atlas == null) {
					continue;
				}
				var textureHash = SHA256.Compute(
					assetCooker.InputBundle.GetFileHash(texturePath),
					textureCookingRules.Hash);
				atlasToHash[atlas] = atlasToHash.TryGetValue(atlas, out var atlasHash) ?
					SHA256.Compute(atlasHash, textureHash) : textureHash;
			}
			foreach (var (atlas, hash) in atlasToHash) {
				yield return (atlas, hash);
			}
		}

		public void Cook(string cookingUnit, SHA256 cookingUnitHash)
		{
			var pluginItems = new Dictionary<string, List<TextureTools.AtlasItem>>();
			var items = new Dictionary<(AtlasOptimization AtlasOptimization, int MaxAtlasSize), List<TextureTools.AtlasItem>>();
			foreach (var file in AssetBundle.Current.EnumerateFiles(null, ".png")) {
				var cookingRules = AssetCooker.CookingRulesMap[file];
				if (cookingRules.TextureAtlas == cookingUnit) {
					var item = new TextureTools.AtlasItem {
						Path = Path.ChangeExtension(file, atlasPartExtension),
						CookingRules = cookingRules,
						SourceExtension = Path.GetExtension(file)
					};
					var bitmapInfo = TextureTools.BitmapInfo.FromFile(assetCooker.InputBundle, file);
					if (bitmapInfo == null) {
						using (var bitmap = TextureTools.OpenAtlasItemBitmapAndRescaleIfNeeded(assetCooker.Platform, item)) {
							item.BitmapInfo = TextureTools.BitmapInfo.FromBitmap(bitmap);
						}
					} else {
						var srcTexturePath = AssetPath.Combine(The.Workspace.AssetsDirectory, Path.ChangeExtension(item.Path, item.SourceExtension));
						if (TextureTools.ShouldDownscale(assetCooker.Platform, bitmapInfo, item.CookingRules)) {
							TextureTools.DownscaleTextureInfo(assetCooker.Platform, bitmapInfo, srcTexturePath, item.CookingRules);
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
					if (assetCooker.Platform == TargetPlatform.iOS) {
						Predicate<PVRFormat> isRequireSquare = (format) => {
							return
								format == PVRFormat.PVRTC2 ||
								format == PVRFormat.PVRTC4 ||
								format == PVRFormat.PVRTC4_Forced;
						};
						var square = atlasItems.Where(item => isRequireSquare(item.CookingRules.PVRFormat)).ToList();
						var nonSquare = atlasItems.Where(item => !isRequireSquare(item.CookingRules.PVRFormat)).ToList();
						initialAtlasId = PackItemsToAtlas(cookingUnit, cookingUnitHash, square, atlasOptimization, maxAtlasSize, initialAtlasId, true);
						initialAtlasId = PackItemsToAtlas(cookingUnit, cookingUnitHash, nonSquare, atlasOptimization, maxAtlasSize, initialAtlasId, false);
					} else {
						initialAtlasId = PackItemsToAtlas(cookingUnit, cookingUnitHash, atlasItems, atlasOptimization, maxAtlasSize, initialAtlasId, false);
					}
				}
			}
			var packers = PluginLoader.CurrentPlugin.AtlasPackers.ToDictionary(i => i.Metadata.Id, i => i.Value);
			foreach (var kv in pluginItems) {
				if (!packers.ContainsKey(kv.Key)) {
					throw new InvalidOperationException($"Packer {kv.Key} not found");
				}
				initialAtlasId = packers[kv.Key](cookingUnit, kv.Value, initialAtlasId);
			}
		}

		private int PackItemsToAtlas(string cookingUnit, SHA256 cookingUnitHash, List<TextureTools.AtlasItem> items,
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
				if (atlasId >= MaxAtlasChainLength) {
					throw new Lime.Exception($"Too many textures in the atlas chain {cookingUnit}");
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
					throw new Lime.Exception("Failed to create atlas '{0}'", cookingUnit);
				}
				PackItemsToAtlas(items, bestSize, out bestPackRate);
				CopyAllocatedItemsToAtlas(items, cookingUnit, atlasId, bestSize, cookingUnitHash);
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
			switch (assetCooker.Platform) {
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

		private void CopyAllocatedItemsToAtlas(List<TextureTools.AtlasItem> items, string atlasChain, int atlasId, Size size, SHA256 cookingUnitHash)
		{
			var atlasPath = GetAtlasPath(atlasChain, atlasId);
			var atlasPixels = new Color4[size.Width * size.Height];
			foreach (var item in items.Where(i => i.Allocated)) {
				var atlasRect = item.AtlasRect;
				using (var bitmap = TextureTools.OpenAtlasItemBitmapAndRescaleIfNeeded(assetCooker.Platform, item)) {
					CopyPixels(bitmap, atlasPixels, atlasRect.A.X, atlasRect.A.Y, size.Width, size.Height);
				}
				var atlasPart = new TextureAtlasElement.Params {
					AtlasRect = atlasRect,
					AtlasPath = Path.ChangeExtension(atlasPath, null)
				};
				InternalPersistence.Instance.WriteObjectToBundle(
					assetCooker.OutputBundle, item.Path, atlasPart, Persistence.Format.Binary,
					cookingUnitHash, AssetAttributes.None);
			}
			var firstItem = items.First(i => i.Allocated);
			using (var atlas = new Bitmap(atlasPixels, size.Width, size.Height)) {
				SyncTextures.ImportTexture(assetCooker, atlasPath, atlas, firstItem.CookingRules, cookingUnitHash);
			}
		}

		private string GetAtlasPath(string atlasChain, int index)
		{
			// Every asset bundle must have its own atlases folder, so they aren't conflict with each other
			var postfix = assetCooker.BundleBeingCookedName ?? "";
			var path = AssetPath.Combine(
				"Atlases_" + postfix,
				atlasChain + '.' + index.ToString("000") + GetPlatformTextureExtension());
			return path;
		}

		private string GetPlatformTextureExtension()
		{
			switch (assetCooker.Target.Platform) {
				case TargetPlatform.iOS:
				case TargetPlatform.Android:
					return ".pvr";
				default:
					return ".dds";
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
