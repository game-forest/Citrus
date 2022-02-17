using System.Collections.Generic;
using System.Linq;
using Lime;

namespace Orange
{
	internal class SyncCompoundFonts : ICookingStage
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
					var hash = assetCooker.InputBundle.ComputeCookingUnitHash(
						i, assetCooker.CookingRulesMap[i]
					);
					return (i, hash);
				});
		}

		public void Cook(string fontPath, SHA256 cookingUnitHash)
		{
			var font = InternalPersistence.Instance
				.ReadFromBundle<SerializableCompoundFont>(assetCooker.InputBundle, fontPath);
			InternalPersistence.Instance.WriteToBundle(
				bundle: assetCooker.OutputBundle,
				path: fontPath,
				@object: font,
				format: Persistence.Format.Binary,
				cookingUnitHash: cookingUnitHash,
				attributes: AssetAttributes.None
			);
		}
	}
}
