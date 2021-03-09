using System.Collections.Generic;
using System.Linq;
using Lime;

namespace Orange
{
	class SyncFonts : ICookingStage
	{
		private readonly AssetCooker assetCooker;

		public SyncFonts(AssetCooker assetCooker)
		{
			this.assetCooker = assetCooker;
		}

		public IEnumerable<(string, SHA256)> EnumerateCookingUnits()
		{
			return assetCooker.InputBundle.EnumerateFiles(null, ".tft")
				.Select(i => {
					var hash = assetCooker.InputBundle.ComputeCookingUnitHash(
						i, assetCooker.CookingRulesMap[i]
					);
					return (i, hash);
				});
		}

		public void Cook(string fontPath, SHA256 cookingUnitHash)
		{
			var font = InternalPersistence.Instance.ReadObjectFromBundle<Font>(assetCooker.InputBundle, fontPath);
			InternalPersistence.Instance.WriteObjectToBundle(
				bundle: assetCooker.OutputBundle,
				path: fontPath,
				instance: font,
				format: Persistence.Format.Binary,
				cookingUnitHash: cookingUnitHash,
				attributes: AssetAttributes.None
			);
		}
	}
}
