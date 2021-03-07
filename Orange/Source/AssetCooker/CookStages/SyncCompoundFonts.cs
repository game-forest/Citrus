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
				.Select(i => {
					var hash = SHA256.Compute(
						assetCooker.InputBundle.GetFilePathAndContentsHash(i),
						assetCooker.CookingRulesMap[i].Hash
					);
					return (i, hash);
				});
		}

		public void Cook(string cookingUnit, SHA256 cookingUnitHash)
		{
			var font = InternalPersistence.Instance.ReadObjectFromBundle<SerializableCompoundFont>(assetCooker.InputBundle, cookingUnit);
			InternalPersistence.Instance.WriteObjectToBundle(
				bundle: assetCooker.OutputBundle,
				path: cookingUnit,
				instance: font,
				format: Persistence.Format.Binary,
				cookingUnitHash: cookingUnitHash,
				attributes: AssetAttributes.None
			);
		}
	}
}
