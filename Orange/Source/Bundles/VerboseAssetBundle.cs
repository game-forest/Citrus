using System;
using System.Collections.Generic;
using System.IO;
using Lime;

namespace Orange
{
	/// <summary>
	/// Provides console output for bundle modifications.
	/// New files are prefixed with `+`, modified with `*` and imported but not changed with `=`.
	/// Deleted files are prefixed with `-`.
	/// Information about deleted files is being output on Dipose due to the nature of how AssetCooker works.
	/// </summary>
	internal class VerboseAssetBundle : WrappedAssetBundle
	{
		private readonly Dictionary<string, SHA256> deletedFiles = new Dictionary<string, SHA256>();
		public VerboseAssetBundle(AssetBundle bundle) : base(bundle)
		{ }

		public override void DeleteFile(string path)
		{
			deletedFiles.Add(path, GetFileContentsHash(path));
			base.DeleteFile(path);
		}

		public override void ImportFile(
			string path, Stream stream, SHA256 cookingUnitHash, AssetAttributes attributes
		) {
			base.ImportFile(path, stream, cookingUnitHash, attributes);
			if (deletedFiles.TryGetValue(path, out var hash)) {
				if (hash != GetFileContentsHash(path)) {
					Console.WriteLine("* " + path);
				} else {
					Console.WriteLine("= " + path);
				}
				deletedFiles.Remove(path);
			} else {
				Console.WriteLine("+ " + path);
			}
		}

		public override void Dispose()
		{
			base.Dispose();
			foreach (var (path, _) in deletedFiles) {
				Console.WriteLine("- " + path);
			}
		}
	}
}
