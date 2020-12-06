using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace Lime
{
	public class AggregateAssetBundle : AssetBundle
	{
		private readonly List<AssetBundle> bundles = new List<AssetBundle>();
		private ReaderWriterLockSlim sync = new ReaderWriterLockSlim();

		public AggregateAssetBundle(params AssetBundle[] bundles)
		{
			sync.EnterWriteLock();
			try {
				this.bundles.AddRange(bundles);
			} finally {
				sync.ExitWriteLock();
			}
		}

		public void Attach(AssetBundle bundle)
		{
			sync.EnterWriteLock();
			try {
				bundles.Add(bundle);
			} finally {
				sync.ExitWriteLock();
			}
		}

		public void Detach(AssetBundle bundle)
		{
			sync.EnterWriteLock();
			try {
				bundles.Remove(bundle);
			} finally {
				sync.ExitWriteLock();
			}
		}

		public override Stream OpenFile(string path, FileMode mode = FileMode.Open) => OpenFileHelper(false, path, mode);

		public override Stream OpenFileRaw(string path, FileMode mode = FileMode.Open) => OpenFileHelper(true, path, mode);

		private Stream OpenFileHelper(bool raw, string path, FileMode mode = FileMode.Open)
		{
			sync.EnterReadLock();
			try {
				foreach (var bundle in bundles) {
					if (bundle.FileExists(path)) {
						return raw ? bundle.OpenFileRaw(path, mode) : bundle.OpenFile(path, mode);
					}
				}
			} finally {
				sync.ExitReadLock();
			}
			throw new FileNotFoundException($"File {path} not found in aggregate asset bundle.");
		}

		public override void Dispose()
		{
			sync.EnterWriteLock();
			try {
				foreach (var bundle in bundles) {
					bundle.Dispose();
				}
				bundles.Clear();
			} finally {
				sync.ExitWriteLock();
			}
		}

		public override SHA256 GetFileHash(string path)
		{
			sync.EnterReadLock();
			try {
				foreach (var bundle in bundles) {
					if (bundle.FileExists(path)) {
						return bundle.GetFileHash(path);
					}
				}
			} finally {
				sync.ExitReadLock();
			}
			throw new InvalidOperationException($"Path {path} not found in aggregate asset bundle.");
		}

		public override SHA256 GetFileCookingUnitHash(string path)
		{
			sync.EnterReadLock();
			try {
				foreach (var bundle in bundles) {
					if (bundle.FileExists(path)) {
						return bundle.GetFileCookingUnitHash(path);
					}
				}
			} finally {
				sync.ExitReadLock();
			}
			throw new InvalidOperationException($"Path {path} not found in aggregate asset bundle.");
		}

		public override int GetFileSize(string path)
		{
			sync.EnterReadLock();
			try {
				foreach (var bundle in bundles) {
					if (bundle.FileExists(path)) {
						return bundle.GetFileSize(path);
					}
				}
			} finally {
				sync.ExitReadLock();
			}
			throw new InvalidOperationException($"Path {path} not found in aggregate asset bundle.");
		}

		public override int GetFileUnpackedSize(string path)
		{
			sync.EnterReadLock();
			try {
				foreach (var bundle in bundles) {
					if (bundle.FileExists(path)) {
						return bundle.GetFileUnpackedSize(path);
					}
				}
			} finally {
				sync.ExitReadLock();
			}
			throw new InvalidOperationException($"Path {path} not found in aggregate asset bundle.");
		}

		public override void DeleteFile(string path)
		{
			throw new InvalidOperationException("Not supported by aggregate asset bundle.");
		}

		public override bool FileExists(string path)
		{
			sync.EnterReadLock();
			try {
				foreach (var bundle in bundles) {
					if (bundle.FileExists(path)) {
						return true;
					}
				}
			} finally {
				sync.ExitReadLock();
			}
			return false;
		}

		public override void ImportFile(string path, Stream stream, SHA256 cookingUnitHash, AssetAttributes attributes = AssetAttributes.None)
		{
			throw new InvalidOperationException("Not supported by aggregate asset bundle.");
		}

		public override void ImportFileRaw(string path, Stream stream, int unpackedSize, SHA256 hash, SHA256 cookingUnitHash, AssetAttributes attributes = AssetAttributes.None)
		{
			throw new InvalidOperationException("Not supported by aggregate asset bundle.");
		}

		public override IEnumerable<string> EnumerateFiles(string path = null, string extension = null)
		{
			sync.EnterReadLock();
			try {
				return bundles.SelectMany(bundle => bundle.EnumerateFiles(path, extension));
			} finally {
				sync.ExitReadLock();
			}
		}

		public override string FromSystemPath(string systemPath) => throw new NotImplementedException();

		public override string ToSystemPath(string bundlePath)
		{
			sync.EnterReadLock();
			try {
				foreach (var bundle in bundles) {
					if (bundle.FileExists(bundlePath)) {
						return bundle.ToSystemPath(bundlePath);
					}
				}
			} finally {
				sync.ExitReadLock();
			}
			throw new InvalidOperationException($"Path {bundlePath} not found in aggregate asset bundle.");
		}
	}
}
