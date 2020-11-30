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

		public override SHA1 GetSourceSHA1(string path) => Bundle.GetSourceSHA1(path);

		public override string GetSourceExtension(string path) => Bundle.GetSourceExtension(path);

		public override int GetFileSize(string path) => Bundle.GetFileSize(path);

		public override void ImportFile(string path, Stream stream, int reserve, string sourceExtension, SHA1 sourceSHA1, AssetAttributes attributes)
		{
			Bundle.ImportFile(path, stream, reserve, sourceExtension, sourceSHA1, attributes);
		}

		public override void ImportFileRaw(string path, Stream stream, int reserve, string sourceExtension, SHA1 sourceSHA1, AssetAttributes attributes)
		{
			Bundle.ImportFileRaw(path, stream, reserve, sourceExtension, sourceSHA1, attributes);
		}

		public override Stream OpenFile(string path, FileMode mode = FileMode.Open) => Bundle.OpenFile(path, mode);

		public override Stream OpenFileRaw(string path, FileMode mode = FileMode.Open) => Bundle.OpenFileRaw(path, mode);
		
		public override string ToSystemPath(string bundlePath) => Bundle.ToSystemPath(bundlePath);

		public override string FromSystemPath(string systemPath) => Bundle.FromSystemPath(systemPath);
	}
}
