using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using Yuzu;

namespace Lime
{
	public class UnpackedAssetBundle : AssetBundle
	{
		public struct FileInfo
		{
			[YuzuMember]
			public SHA256 Hash;

			[YuzuMember]
			public DateTime DateModified;
		}

		public readonly string BaseDirectory;
		private IFileSystemWatcher watcher;
		private bool indexValid;
		private SortedDictionary<string, FileInfo> index;

		private const string IndexFile = ".index";

		public UnpackedAssetBundle(string baseDirectory)
		{
			// Path.GetFullPath will resolve ".." special symbol in path and
			// concatenate current directory if path is not rooted.
			BaseDirectory = NormalizeDirectoryPath(Path.GetFullPath(baseDirectory));
			watcher = new FileSystemWatcher(BaseDirectory, includeSubdirectories: true);
			watcher.Changed += p => OnChanged(p);
			watcher.Created += p => OnChanged(p);
			watcher.Deleted += p => OnChanged(p);
			watcher.Renamed += (_, p) => OnChanged(p);
			void OnChanged(string p)
			{
				if (!Path.GetFileName(p)?.Equals(IndexFile, StringComparison.Ordinal) ?? false) {
					indexValid = false;
				}
			}
			var indexPath = Path.Combine(BaseDirectory, IndexFile);
			index = new SortedDictionary<string, FileInfo>(StringComparer.Ordinal);
			if (File.Exists(indexPath)) {
				try {
					using (var fs = File.OpenRead(indexPath)) {
						InternalPersistence.Instance.ReadObject<SortedDictionary<string, FileInfo>>(
							indexPath, fs, index);
					}
				} catch (System.Exception) {
					// ignored
				}
			}
		}

		public override void Dispose()
		{
			var indexPath = Path.Combine(BaseDirectory, IndexFile);
			InternalPersistence.Instance.WriteObjectToFile(indexPath, index, Persistence.Format.Binary);
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

		public override int GetFileSize(string path)
		{
			return (int)new System.IO.FileInfo(ToSystemPath(path)).Length;
		}

		public override int GetFileUnpackedSize(string path) => throw new NotSupportedException();

		public override SHA256 GetFileCookingUnitHash(string path) => throw new NotSupportedException();

		public override SHA256 GetFileHash(string path)
		{
			ValidateIndex();
			if (!index.TryGetValue(path, out var i) || i.Hash == default) {
				i = new FileInfo {
					DateModified = File.GetLastWriteTimeUtc(ToSystemPath(path)),
					Hash = SHA256.Compute(
						SHA256.Compute(AssetPath.CorrectSlashes(path)),
						SHA256.Compute(File.ReadAllBytes(ToSystemPath(path)))
					)
				};
				index[path] = i;
			}
			return i.Hash;
		}

		public override void DeleteFile(string path)
		{
			File.Delete(ToSystemPath(path));
		}

		public override bool FileExists(string path)
		{
			return File.Exists(ToSystemPath(path));
		}

		public override void ImportFile(string path, Stream stream, SHA256 cookingUnitHash, AssetAttributes attributes)
		{
			ImportFileRaw(path, stream, 0, default, cookingUnitHash, attributes);
		}

		public override void ImportFileRaw(string path, Stream stream, int unpackedSize, SHA256 hash, SHA256 cookingUnitHash, AssetAttributes attributes)
		{
			stream.Seek(0, SeekOrigin.Begin);
			var length = (int)stream.Length;
			var buffer = ArrayPool<byte>.Shared.Rent(length);
			try {
				stream.Read(buffer, 0, length);
				var systemPath = ToSystemPath(path);
				Directory.CreateDirectory(Path.GetDirectoryName(systemPath));
				using (var fs = new FileStream(systemPath, FileMode.Create, FileAccess.Write, FileShare.Read)) {
					fs.Write(buffer, 0, length);
				}
			} finally {
				ArrayPool<byte>.Shared.Return(buffer);
			}
		}

		public override IEnumerable<string> EnumerateFiles(string path = null, string extension = null)
		{
			if (extension != null && !extension.StartsWith(".")) {
				throw new InvalidOperationException();
			}
			ValidateIndex();
			if (path != null) {
				path = NormalizeDirectoryPath(path);
			}
			foreach (var asset in index.Keys) {
				if (path != null && !asset.StartsWith(path, StringComparison.OrdinalIgnoreCase)) {
					continue;
				}
				if (extension != null && !asset.EndsWith(extension, StringComparison.OrdinalIgnoreCase)) {
					continue;
				}
				yield return asset;
			}
		}

		private void ValidateIndex()
		{
			if (indexValid) {
				return;
			}
			var oldIndex = index;
			index = new SortedDictionary<string, FileInfo>(StringComparer.Ordinal);
			var di = new DirectoryInfo(BaseDirectory);
			foreach (var fi in di.GetFiles("*", SearchOption.AllDirectories)) {
				var path = fi.FullName.Substring(di.FullName.Length).Replace('\\', '/');
				if (path == IndexFile) {
					continue;
				}
				if (oldIndex.TryGetValue(path, out var item)) {
					index[path] = new FileInfo {
						DateModified = fi.LastWriteTimeUtc,
						Hash = fi.LastWriteTimeUtc == item.DateModified ? item.Hash : default
					};
				} else {
					index[path] = new FileInfo { DateModified = fi.LastWriteTimeUtc, Hash = default };
				}
			}
			indexValid = true;
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
