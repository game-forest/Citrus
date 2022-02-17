using System.Collections.Generic;
using System.Linq;
using Lime;

namespace Orange
{
	internal class SyncRawAssets : ICookingStage
	{
		private readonly string extension;
		private readonly AssetAttributes attributes;
		private readonly AssetCooker assetCooker;

		public SyncRawAssets(
			AssetCooker assetCooker, string extension, AssetAttributes attributes = AssetAttributes.None
		) {
			this.assetCooker = assetCooker;
			this.extension = extension;
			this.attributes = attributes;
		}

		public IEnumerable<(string, SHA256)> EnumerateCookingUnits()
		{
			return assetCooker.InputBundle.EnumerateFiles(null, extension)
				.Select(i => {
					var hash = assetCooker.InputBundle.ComputeCookingUnitHash(
						i, assetCooker.CookingRulesMap[i]
					);
					return (i, hash);
				});
		}

		public void Cook(string filePath, SHA256 cookingUnitHash)
		{
			assetCooker.OutputBundle.ImportFile(
				sourcePath: assetCooker.InputBundle.ToSystemPath(filePath),
				destinationPath: filePath,
				cookingUnitHash: cookingUnitHash,
				attributes: attributes
			);
		}
	}
}
