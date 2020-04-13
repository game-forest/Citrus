using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace Lime
{
	public class UnpackedAssetBundle : AssetBundle
	{
		public readonly string BaseDirectory;
		private IFileSystemWatcher watcher;
		private bool assetsCached;
		private List<FileInfo> cachedAssets;

		public UnpackedAssetBundle(string baseDirectory)
		{
			BaseDirectory = NormalizeDirectoryPath(baseDirectory);
			watcher = new FileSystemWatcher(BaseDirectory, includeSubdirectories: true);
			watcher.Changed += _ => assetsCached = false;
			watcher.Created += _ => assetsCached = false;
			watcher.Deleted += _ => assetsCached = false;
			watcher.Renamed += (_, __) => assetsCached = false;
		}

		public override void Dispose()
		{
			watcher?.Dispose();
			watcher = null;
			base.Dispose();
		}

		public override Stream OpenFile(string path, FileMode mode = FileMode.Open)
		{
			return OpenFileRaw(path, mode);
		}

		public override Stream OpenFileRaw(string path, FileMode mode = FileMode.Open)
		{
			return new FileStream(
				ToSystemPath(path), mode,
				mode == FileMode.Append ? FileAccess.Write : FileAccess.ReadWrite,
				FileShare.Read);
		}

		public override DateTime GetFileLastWriteTime(string path)
		{
			return File.GetLastWriteTime(ToSystemPath(path));
		}

		public override void SetFileLastWriteTime(string path, DateTime time)
		{
			assetsCached = false;
			File.SetLastWriteTime(ToSystemPath(path), time);
		}

		public override int GetFileSize(string path)
		{
			return (int)(new System.IO.FileInfo(ToSystemPath(path)).Length);
		}

		public override byte[] GetCookingRulesSHA1(string path)
		{
			throw new NotImplementedException();
		}

		public override void DeleteFile(string path)
		{
			File.Delete(ToSystemPath(path));
		}

		public override bool FileExists(string path)
		{
			return File.Exists(ToSystemPath(path));
		}
		
		public override void ImportFile(
			string path, Stream stream, int reserve, string sourceExtension, DateTime time,
			AssetAttributes attributes, byte[] cookingRulesSHA1)
		{
			if ((attributes & (AssetAttributes.Zipped | AssetAttributes.ZippedDeflate)) != 0) {
				throw new NotSupportedException();
			}
			ImportFileRaw(path, stream, reserve, sourceExtension, time, attributes, cookingRulesSHA1);
		}

		public override void ImportFileRaw(
			string path, Stream stream, int reserve, string sourceExtension, DateTime time,
			AssetAttributes attributes, byte[] cookingRulesSHA1)
		{
			stream.Seek(0, SeekOrigin.Begin);
			var bytes = new byte[stream.Length];
			stream.Read(bytes, 0, bytes.Length);
			var dir = Path.Combine(BaseDirectory, Path.GetDirectoryName(path));
			Directory.CreateDirectory(dir);
			File.WriteAllBytes(Path.Combine(BaseDirectory, path), bytes);
		}
		
		public override IEnumerable<FileInfo> EnumerateFileInfos(string path = null, string extension = null)
		{
			if (extension != null && !extension.StartsWith(".")) {
				throw new InvalidOperationException();
			}
			if (!assetsCached) {
				assetsCached = true;
				cachedAssets = cachedAssets ?? new List<FileInfo>();
				cachedAssets.Clear();
				var dirInfo = new DirectoryInfo(BaseDirectory);
				foreach (var fileInfo in dirInfo.GetFiles("*", SearchOption.AllDirectories)) {
					var file = fileInfo.FullName;
					file = file.Substring(dirInfo.FullName.Length).Replace('\\', '/');
					cachedAssets.Add(new FileInfo { Path = file, LastWriteTime = fileInfo.LastWriteTime });
				}
				// According to documentation the file order is not guaranteed.
				cachedAssets.Sort((a, b) => string.Compare(a.Path, b.Path, StringComparison.Ordinal));
			}
			if (path != null) {
				path = NormalizeDirectoryPath(path);
			}
			foreach (var asset in cachedAssets) {
				if (path != null && !asset.Path.StartsWith(path, StringComparison.OrdinalIgnoreCase)) {
					continue;
				}
				if (extension != null && !asset.Path.EndsWith(extension, StringComparison.OrdinalIgnoreCase)) {
					continue;
				}
				yield return asset;
			}
		}

		private static string NormalizeDirectoryPath(string path)
		{
			path = path.Replace('\\', '/');
			if (!path.EndsWith("/")) {
				path += '/';
			}
			return path;
		}

		public override string ToSystemPath(string bundlePath)
		{
			if (Path.IsPathRooted(bundlePath)) {
				throw new InvalidOperationException();
			}
			return Path.Combine(BaseDirectory, bundlePath);
		}

		public override string FromSystemPath(string systemPath)
		{
			systemPath = systemPath.Replace('\\', '/');
			// Check if systemPath starts with BaseDirectory without trailing '/'
			if (string.Compare(
				systemPath,0,BaseDirectory, 0, BaseDirectory.Length - 1,
				StringComparison.OrdinalIgnoreCase) != 0)
			{
				throw new InvalidOperationException(
					$"'{systemPath}' outside of the bundle directory '{BaseDirectory}'");
			}
			return systemPath.Length == BaseDirectory.Length - 1 ?
				string.Empty :
				systemPath.Substring(BaseDirectory.Length);
		}
	}
}
