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
				.Select(i =>
					(i, SHA256.Compute(assetCooker.InputBundle.GetFileHash(i), AssetCooker.CookingRulesMap[i].Hash)));
		}

		public void Cook(string cookingUnit, SHA256 cookingUnitHash)
		{
			var node = InternalPersistence.Instance.ReadObjectFromBundle<Node>(assetCooker.InputBundle, cookingUnit);
			InternalPersistence.Instance.WriteObjectToBundle(
				assetCooker.OutputBundle, cookingUnit, node, Persistence.Format.Binary,
				cookingUnitHash, AssetAttributes.None);
		}
	}
}
