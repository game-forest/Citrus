using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Lime;

namespace Orange
{
	class SyncSounds : ICookingStage
	{
		private readonly AssetCooker assetCooker;

		public SyncSounds(AssetCooker assetCooker)
		{
			this.assetCooker = assetCooker;
		}

		public IEnumerable<(string, SHA256)> EnumerateCookingUnits()
		{
			return assetCooker.InputBundle.EnumerateFiles(null, ".ogg")
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
			using var stream = assetCooker.InputBundle.OpenFile(cookingUnit);
			// All sounds below 100kb size (can be changed with cooking rules) are converted
			// from OGG to Wav/Adpcm
			var rules = assetCooker.CookingRulesMap[cookingUnit];
			if (stream.Length > rules.ADPCMLimit * 1024) {
				assetCooker.OutputBundle.ImportFile(
					path: Path.ChangeExtension(cookingUnit, ".sound"),
					stream: stream,
					cookingUnitHash: cookingUnitHash,
					attributes: AssetAttributes.None
				);
			} else {
				Console.WriteLine("Converting sound to ADPCM/IMA4 format...");
				using var input = new OggDecoder(stream);
				using var output = new MemoryStream();
				WaveIMA4Converter.Encode(input, output);
				output.Seek(0, SeekOrigin.Begin);
				assetCooker.OutputBundle.ImportFile(
					path: Path.ChangeExtension(cookingUnit, ".sound"),
					stream: output,
					cookingUnitHash: cookingUnitHash,
					attributes: AssetAttributes.None
				);
			}
		}
	}
}
