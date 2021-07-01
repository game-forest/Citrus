using System;
using System.Collections.Generic;
using System.IO;
using Lime;

namespace Orange
{
	class SyncSounds : AssetCookerCookStage, ICookStage
	{
		public IEnumerable<string> ImportedExtensions { get { yield return oggExtension; } }
		public IEnumerable<string> BundleExtensions { get { yield return soundExtension; } }

		private readonly string oggExtension = ".ogg";
		private readonly string soundExtension = ".sound";

		public SyncSounds(AssetCooker assetCooker) : base(assetCooker) { }

		public int GetOperationCount() => AssetCooker.GetUpdateOperationCount(oggExtension);

		public void Action() => AssetCooker.SyncUpdated(oggExtension, soundExtension, Converter);

		private bool Converter(string srcPath, string dstPath)
		{
			using (var stream = AssetCooker.InputBundle.OpenFile(srcPath)) {
				// All sounds below 100kb size (can be changed with cooking rules) are converted
				// from OGG to Wav/Adpcm
				var rules = AssetCooker.CookingRulesMap[srcPath];
				if (stream.Length > rules.ADPCMLimit * 1024) {
					AssetCooker.OutputBundle.ImportFile(dstPath, stream, 0, oggExtension,
						SHA1.Compute(AssetCooker.InputBundle.GetSourceSHA1(srcPath), AssetCooker.CookingRulesMap[srcPath].SHA1),
						AssetAttributes.None);
				}
				else {
					Console.WriteLine("Converting sound to ADPCM/IMA4 format...");
					using (var input = new OggDecoder(stream)) {
						using (var output = new MemoryStream()) {
							WaveIMA4Converter.Encode(input, output);
							output.Seek(0, SeekOrigin.Begin);
							AssetCooker.OutputBundle.ImportFile(dstPath, output, 0, oggExtension,
								SHA1.Compute(AssetCooker.InputBundle.GetSourceSHA1(srcPath), AssetCooker.CookingRulesMap[srcPath].SHA1),
								AssetAttributes.None);
						}
					}
				}
				return true;
			}
		}
	}
}
