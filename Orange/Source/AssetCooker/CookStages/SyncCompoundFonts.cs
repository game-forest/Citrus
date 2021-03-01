using System.Collections.Generic;
using System.Linq;
using Lime;

namespace Orange
{
	class SyncCompoundFonts : ICookingStage
	{
		private readonly AssetCooker assetCooker;

		public SyncCompoundFonts(AssetCooker assetCooker)
		{
			this.assetCooker = assetCooker;
		}

		public IEnumerable<(string, SHA256)> EnumerateCookingUnits()
		{
			return assetCooker.InputBundle.EnumerateFiles(null, ".cft")
				.Select(i =>
					(i, SHA256.Compute(assetCooker.InputBundle.GetFileHash(i), assetCooker.CookingRulesMap[i].Hash)));
		}

		public void Cook(string cookingUnit, SHA256 cookingUnitHash)
		{
			var font = InternalPersistence.Instance.ReadObjectFromBundle<SerializableCompoundFont>(assetCooker.InputBundle, cookingUnit);
			InternalPersistence.Instance.WriteObjectToBundle(
				assetCooker.OutputBundle, cookingUnit, font, Persistence.Format.Binary,
				cookingUnitHash, AssetAttributes.None);
		}
	}
}
