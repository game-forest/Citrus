using System.Collections.Generic;
using System.Linq;
using Lime;

namespace Orange
{
	class SyncRawAssets : ICookingStage
	{
		private readonly string extension;
		private readonly AssetAttributes attributes;
		private readonly AssetCooker assetCooker;

		public SyncRawAssets(AssetCooker assetCooker, string extension, AssetAttributes attributes = AssetAttributes.None)
		{
			this.assetCooker = assetCooker;
			this.extension = extension;
			this.attributes = attributes;
		}

		public IEnumerable<(string, SHA256)> EnumerateCookingUnits()
		{
			return assetCooker.InputBundle.EnumerateFiles(null, extension)
				.Select(i =>
					(i, SHA256.Compute(assetCooker.InputBundle.GetHash(i), AssetCooker.CookingRulesMap[i].Hash)));
		}

		public void Cook(string cookingUnit, SHA256 cookingUnitHash)
		{
			assetCooker.OutputBundle.ImportFile(
				assetCooker.InputBundle.ToSystemPath(cookingUnit), cookingUnit, cookingUnitHash, attributes);
		}
	}
}
