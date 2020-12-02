using System;
using System.Collections.Generic;
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
				.Where(i => AssetCooker.CookingRulesMap[i].TextureAtlas == null)
				.Select(i =>
					(i, SHA256.Compute(assetCooker.InputBundle.GetHash(i), AssetCooker.CookingRulesMap[i].Hash)));
		}

		public void Cook(string cookingUnit, SHA256 cookingUnitHash)
		{
			var rules = AssetCooker.CookingRulesMap[cookingUnit];
			using (var stream = assetCooker.InputBundle.OpenFile(cookingUnit)) {
				var bitmap = new Bitmap(stream);
				if (TextureTools.ShouldDownscale(assetCooker.Platform, bitmap, rules)) {
					var scaledBitmap = TextureTools.DownscaleTexture(assetCooker.Platform, bitmap, cookingUnit, rules);
					bitmap.Dispose();
					bitmap = scaledBitmap;
				}
				assetCooker.ImportTexture(cookingUnit, bitmap, rules, cookingUnitHash);
				bitmap.Dispose();
			}
		}
	}
}
