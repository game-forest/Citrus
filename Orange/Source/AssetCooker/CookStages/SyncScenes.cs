using System.Collections.Generic;
using System.Linq;
using Lime;

namespace Orange
{
	class SyncScenes : ICookingStage
	{
		private readonly AssetCooker assetCooker;

		public SyncScenes(AssetCooker assetCooker)
		{
			this.assetCooker = assetCooker;
		}

		public IEnumerable<(string, SHA256)> EnumerateCookingUnits()
		{
			return assetCooker.InputBundle.EnumerateFiles(null, ".tan")
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
			var node = InternalPersistence.Instance.ReadObjectFromBundle<Node>(assetCooker.InputBundle, cookingUnit);
			InternalPersistence.Instance.WriteObjectToBundle(
				bundle: assetCooker.OutputBundle,
				path: cookingUnit,
				instance: node,
				format: Persistence.Format.Binary,
				cookingUnitHash: cookingUnitHash,
				attributes: AssetAttributes.None
			);
		}
	}
}
