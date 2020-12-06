using System;
using System.Collections.Generic;
using System.Linq;
using Lime;

namespace Orange
{
	class SyncTxtAssets : ICookingStage
	{
		private readonly AssetCooker assetCooker;

		public SyncTxtAssets(AssetCooker assetCooker)
		{
			this.assetCooker = assetCooker;
		}

		public IEnumerable<(string, SHA256)> EnumerateCookingUnits()
		{
			return assetCooker.InputBundle.EnumerateFiles(null, ".txt")
				.Where(i => !i.EndsWith(Model3DAttachment.FileExtension, StringComparison.Ordinal))
				.Select(i =>
					(i, SHA256.Compute(assetCooker.InputBundle.GetFileHash(i), AssetCooker.CookingRulesMap[i].Hash)));
		}

		public void Cook(string cookingUnit, SHA256 cookingUnitHash)
		{
			assetCooker.OutputBundle.ImportFile(
				assetCooker.InputBundle.ToSystemPath(cookingUnit), cookingUnit,
				cookingUnitHash, AssetAttributes.Zipped);
		}
	}
}
