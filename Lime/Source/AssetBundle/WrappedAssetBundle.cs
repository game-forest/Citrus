using System;
using System.Collections.Generic;
using System.IO;

namespace Lime
{
	public class WrappedAssetBundle : AssetBundle
	{
		public readonly AssetBundle Bundle;

		public WrappedAssetBundle(AssetBundle bundle)
		{
			this.Bundle = bundle;
		}

		public override void DeleteFile(string path) => Bundle.DeleteFile(path);

		public override IEnumerable<string> EnumerateFiles(string path = null, string extension = null) => Bundle.EnumerateFiles(path, extension);

		public override bool FileExists(string path) => Bundle.FileExists(path);

		public override SHA256 GetHash(string path) => Bundle.GetHash(path);

		public override SHA256 GetCookingUnitHash(string path) => Bundle.GetCookingUnitHash(path);

		public override int GetFileSize(string path) => Bundle.GetFileSize(path);

		public override void ImportFile(string path, Stream stream, SHA256 cookingUnitHash, AssetAttributes attributes)
		{
			Bundle.ImportFile(path, stream, cookingUnitHash, attributes);
		}

		public override void ImportFileRaw(string path, Stream stream, SHA256 hash, SHA256 cookingUnitHash, AssetAttributes attributes)
		{
			Bundle.ImportFileRaw(path, stream, hash, cookingUnitHash, attributes);
		}

		public override Stream OpenFile(string path, FileMode mode = FileMode.Open) => Bundle.OpenFile(path, mode);

		public override Stream OpenFileRaw(string path, FileMode mode = FileMode.Open) => Bundle.OpenFileRaw(path, mode);

		public override string ToSystemPath(string bundlePath) => Bundle.ToSystemPath(bundlePath);

		public override string FromSystemPath(string systemPath) => Bundle.FromSystemPath(systemPath);
	}
}
