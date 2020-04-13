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

		public override IEnumerable<FileInfo> EnumerateFileInfos(string path = null, string extension = null) => Bundle.EnumerateFileInfos(path, extension);

		public override bool FileExists(string path) => Bundle.FileExists(path);

		public override byte[] GetCookingRulesSHA1(string path) => Bundle.GetCookingRulesSHA1(path);

		public override DateTime GetFileLastWriteTime(string path) => Bundle.GetFileLastWriteTime(path);

		public override void SetFileLastWriteTime(string path, DateTime time) => Bundle.SetFileLastWriteTime(path, time);

		public override int GetFileSize(string path) => Bundle.GetFileSize(path);

		public override void ImportFile(string path, Stream stream, int reserve, string sourceExtension, DateTime time, AssetAttributes attributes, byte[] cookingRulesSHA1)
		{
			Bundle.ImportFile(path, stream, reserve, sourceExtension, time, attributes, cookingRulesSHA1);
		}

		public override void ImportFileRaw(string path, Stream stream, int reserve, string sourceExtension, DateTime time, AssetAttributes attributes, byte[] cookingRulesSHA1)
		{
			Bundle.ImportFileRaw(path, stream, reserve, sourceExtension, time, attributes, cookingRulesSHA1);
		}

		public override Stream OpenFile(string path, FileMode mode = FileMode.Open) => Bundle.OpenFile(path, mode);

		public override Stream OpenFileRaw(string path, FileMode mode = FileMode.Open) => Bundle.OpenFileRaw(path, mode);
		
		public override string ToSystemPath(string bundlePath) => Bundle.ToSystemPath(bundlePath);

		public override string FromSystemPath(string systemPath) => Bundle.FromSystemPath(systemPath);
	}
}
