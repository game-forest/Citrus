using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Lime;

namespace Orange
{
	class SyncTextures : ICookingStage
	{
		private readonly string originalTextureExtension = ".png";
		private readonly AssetCooker assetCooker;

		public SyncTextures(AssetCooker assetCooker)
		{
			this.assetCooker = assetCooker;
		}

		public IEnumerable<(string, SHA256)> EnumerateCookingUnits()
		{
			return assetCooker.InputBundle.EnumerateFiles(null, originalTextureExtension)
				.Where(i => assetCooker.CookingRulesMap[i].TextureAtlas == null)
				.Select(i =>
					(i, SHA256.Compute(assetCooker.InputBundle.GetFileHash(i), assetCooker.CookingRulesMap[i].Hash)));
		}

		public void Cook(string cookingUnit, SHA256 cookingUnitHash)
		{
			var rules = assetCooker.CookingRulesMap[cookingUnit];
			using (var stream = assetCooker.InputBundle.OpenFile(cookingUnit)) {
				var bitmap = new Bitmap(stream);
				if (TextureTools.ShouldDownscale(assetCooker.Platform, bitmap, rules)) {
					var scaledBitmap = TextureTools.DownscaleTexture(assetCooker.Platform, bitmap, cookingUnit, rules);
					bitmap.Dispose();
					bitmap = scaledBitmap;
				}
				ImportTexture(assetCooker, cookingUnit, bitmap, rules, cookingUnitHash);
				bitmap.Dispose();
			}
		}

		public static void ImportTexture(AssetCooker assetCooker, string path, Bitmap texture, ICookingRules rules, SHA256 cookingUnitHash)
		{
			var textureParamsPath = Path.ChangeExtension(path, ".texture");
			var textureParams = new TextureParams {
				WrapMode = rules.WrapMode,
				MinFilter = rules.MinFilter,
				MagFilter = rules.MagFilter,
			};
			if (!AreTextureParamsDefault()) {
				TextureTools.UpscaleTextureIfNeeded(ref texture, rules, false);
				InternalPersistence.Instance.WriteObjectToBundle(
					assetCooker.OutputBundle, textureParamsPath, textureParams, Persistence.Format.Binary, cookingUnitHash, AssetAttributes.None);
			}
			if (rules.GenerateOpacityMask) {
				var maskPath = Path.ChangeExtension(path, ".mask");
				OpacityMaskCreator.CreateMask(assetCooker.OutputBundle, texture, maskPath, cookingUnitHash);
			}
			var attributes = AssetAttributes.ZippedDeflate;
			if (!TextureConverterUtils.IsPowerOf2(texture.Width) || !TextureConverterUtils.IsPowerOf2(texture.Height)) {
				attributes |= AssetAttributes.NonPowerOf2Texture;
			}
			switch (assetCooker.Target.Platform) {
				case TargetPlatform.Android:
				//case TargetPlatform.iOS:
					var f = rules.PVRFormat;
					if (f == PVRFormat.ARGB8 || f == PVRFormat.RGB565 || f == PVRFormat.RGBA4) {
						TextureConverter.RunPVRTexTool(texture, assetCooker.OutputBundle, path, attributes, rules.MipMaps, rules.HighQualityCompression, rules.PVRFormat, cookingUnitHash);
					} else {
						TextureConverter.RunEtcTool(texture, assetCooker.OutputBundle, path, attributes, rules.MipMaps, rules.HighQualityCompression, cookingUnitHash);
					}
					break;
				case TargetPlatform.iOS:
					TextureConverter.RunPVRTexTool(texture, assetCooker.OutputBundle, path, attributes, rules.MipMaps, rules.HighQualityCompression, rules.PVRFormat, cookingUnitHash);
					break;
				case TargetPlatform.Win:
				case TargetPlatform.Mac:
					TextureConverter.RunNVCompress(texture, assetCooker.OutputBundle, path, attributes, rules.DDSFormat, rules.MipMaps, cookingUnitHash);
					break;
				default:
					throw new Lime.Exception();
			}

			bool AreTextureParamsDefault()
			{
				return rules.MinFilter == TextureParams.Default.MinFilter &&
				       rules.MagFilter == TextureParams.Default.MagFilter &&
				       rules.WrapMode == TextureParams.Default.WrapModeU;
			}
		}
	}
}
