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
				.Select(i =>
					(i, SHA256.Compute(assetCooker.InputBundle.GetFileHash(i), assetCooker.CookingRulesMap[i].Hash)));
		}

		public void Cook(string cookingUnit, SHA256 cookingUnitHash)
		{
			var font = InternalPersistence.Instance.ReadObjectFromBundle<Font>(assetCooker.InputBundle, cookingUnit);
			InternalPersistence.Instance.WriteObjectToBundle(
				assetCooker.OutputBundle, cookingUnit, font, Persistence.Format.Binary,
				cookingUnitHash, AssetAttributes.None);
		}
	}
}
