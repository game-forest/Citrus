using System.IO;
using System.IO.Compression;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Lzma;
using Yuzu;

namespace Lime
{
	public class InvalidBundleVersionException : Lime.Exception
	{
		public InvalidBundleVersionException(string message) : base(message) { }
	}

	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	internal struct AssetDescriptor
	{
		public int Offset;
		public int Size;
		public int AllocatedSize;
		public int UnpackedSize;
		public AssetAttributes Attributes;
		public SHA256 ContentsHash;
		public SHA256 CookingUnitHash;
	}

	public static class AssetPath
	{
		public static string GetDirectoryName(string path) => CorrectSlashes(Path.GetDirectoryName(path));

		public static string Combine(string path1, string path2) => CorrectSlashes(Path.Combine(path1, path2));

		public static string Combine(params string[] paths) => paths.Aggregate(string.Empty, Combine);

		public static string CorrectSlashes(string path) => path.IndexOf('\\') >= 0 ? path.Replace('\\', '/') : path;
	}

	public sealed class AssetStream : Stream
	{
		private readonly PackedAssetBundle bundle;
		internal AssetDescriptor descriptor;
		private int position;
		private Stream stream;

		public AssetStream(PackedAssetBundle bundle, string path)
		{
			this.bundle = bundle;
			if (!bundle.index.TryGetValue(AssetPath.CorrectSlashes(path), out descriptor)) {
				throw new Exception($"Can't open asset: {path}");
			}
			stream = bundle.AllocateStream();
			Seek(0, SeekOrigin.Begin);
		}

		public override bool CanRead => true;

		public override bool CanWrite => false;

		public override long Length => descriptor.Size;

		public override long Position
		{
			get => position;
			set => Seek(value, SeekOrigin.Begin);
		}

		public override bool CanSeek => true;

		public override int Read(byte[] buffer, int offset, int count)
		{
			ThrowIfDisposed();
			count = Math.Min(count, descriptor.Size - position);
			if (count > 0) {
				count = stream.Read(buffer, offset, count);
				if (count < 0) {
					return count;
				}

				position += count;
			} else {
				count = 0;
			}
			return count;
		}

		public override long Seek(long offset, SeekOrigin origin)
		{
			ThrowIfDisposed();
			if (origin == SeekOrigin.Begin) {
				position = (int)offset;
			} else if (origin == SeekOrigin.Current) {
				position += (int)offset;
			} else {
				position = descriptor.Size - (int)offset;
			}
			position = Math.Max(0, Math.Min(position, descriptor.Size));
			stream.Seek(position + descriptor.Offset, SeekOrigin.Begin);
			return position;
		}

		public override void Flush() => throw new NotSupportedException();
		public override void SetLength(long value) => throw new NotSupportedException();
		public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

		private bool disposedValue;

		protected override void Dispose(bool disposing)
		{
			if (!disposedValue) {
				if (stream != null) {
					bundle.ReleaseStream(stream);
					stream = null;
				}
				disposedValue = true;
			}
		}

		private void ThrowIfDisposed()
		{
			if (disposedValue) {
				throw new ObjectDisposedException(GetType().Name);
			}
		}
	}

	[Flags]
	public enum AssetBundleFlags
	{
		None = 0,
		Writable = 1,
	}

	public class PackedAssetBundle : AssetBundle
	{
		/// <summary>
		/// This class contains a specific data needed for applying asset bundle patches.
		/// </summary>
		public class Manifest
		{
			public const string FileName = ".manifest";

			[YuzuMember]
			public int? BaseBundleVersion { get; set; }

			[YuzuMember]
			public int? BundleVersion { get; set; }

			[YuzuMember]
			public List<string> DeletedAssets { get; } = new List<string>();

			public static Manifest Create(AssetBundle bundle)
			{
				if (bundle.FileExists(FileName)) {
					return InternalPersistence.Instance.ReadFromBundle<Manifest>(bundle, FileName);
				}
				return new Manifest();
			}

			public void Save(AssetBundle bundle)
			{
				InternalPersistence.Instance.WriteToBundle(
					bundle: bundle,
					path: FileName,
					@object: this,
					format: Persistence.Format.Binary,
					cookingUnitHash: default,
					attributes: AssetAttributes.None
				);
			}
		}

		private const int Signature = 0x13AF;

		private readonly Stack<Stream> streamPool = new Stack<Stream>();
		private int indexOffset;
		private BinaryReader reader;
		private BinaryWriter writer;
		private Stream stream;
		internal readonly SortedDictionary<string, AssetDescriptor> index
			= new SortedDictionary<string, AssetDescriptor>(StringComparer.OrdinalIgnoreCase);
		private readonly List<AssetDescriptor> trash = new List<AssetDescriptor>();
		private readonly System.Reflection.Assembly resourcesAssembly;
		private bool WasModified { get; set; }
		public string Path { get; }

		public PackedAssetBundle(string resourceId, string assemblyName)
		{
			try {
				this.Path = resourceId;
				resourcesAssembly = AppDomain.CurrentDomain.GetAssemblies().
					SingleOrDefault(a => a.GetName().Name == assemblyName);
				if (resourcesAssembly == null) {
					throw new Lime.Exception($"Assembly {assemblyName} doesn't exist");
				}
				stream = AllocateStream();
				reader = new BinaryReader(stream);
				ReadIndexTable();
			} catch {
				Dispose();
				throw;
			}
		}

		public PackedAssetBundle(string path, AssetBundleFlags flags = Lime.AssetBundleFlags.None)
		{
			try {
				this.Path = path;
				if ((flags & AssetBundleFlags.Writable) != 0) {
					stream = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);
					reader = new BinaryReader(stream);
					writer = new BinaryWriter(stream);
				} else {
					stream = AllocateStream();
					reader = new BinaryReader(stream);
				}
				ReadIndexTable();
			} catch {
				Dispose();
				throw;
			}
		}

		public static int CalcBundleChecksum(string bundlePath)
		{
			// "Modified FNV with good avalanche behavior and uniform distribution with larger hash sizes."
			// See http://papa.bretmulvey.com/post/124027987928/hash-functions for algorithm.
			using var stream = new FileStream(bundlePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
			var data = new byte[16 * 1024];
			int size = stream.Read(data, 0, data.Length);
			if (size < 8) {
				return 0;
			}
			data[4] = 0;
			data[5] = 0;
			data[6] = 0;
			data[7] = 0;
			unchecked {
				const int p = 16777619;
				int hash = (int)2166136261;
				while (size > 0) {
					for (int i = 0; i < size; i++) {
						hash = (hash ^ data[i]) * p;
					}
					size = stream.Read(data, 0, data.Length);
				}
				hash += hash << 13;
				hash ^= hash >> 7;
				hash += hash << 3;
				hash ^= hash >> 17;
				hash += hash << 5;
				return hash;
			}
		}

		public static bool IsBundleCorrupted(string bundlePath)
		{
			using var stream = new FileStream(bundlePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
			if (stream.Length < 8) {
				return true;
			}
			var reader = new BinaryReader(stream);
			// Bundle signature
			reader.ReadInt32();
			int storedChecksum = reader.ReadInt32();
			int actualChecksum = CalcBundleChecksum(bundlePath);
			return storedChecksum != actualChecksum;
		}

		public static void RefreshBundleChecksum(string bundlePath)
		{
			int checksum = CalcBundleChecksum(bundlePath);
			using var stream = new FileStream(bundlePath, FileMode.Open, FileAccess.Write, FileShare.ReadWrite);
			if (stream.Length > 8) {
				using var writer = new BinaryWriter(stream);
				writer.Seek(4, SeekOrigin.Begin);
				writer.Write(checksum);
			}
		}

		private void MoveBlock(int offset, int size, int delta)
		{
			if (delta > 0) {
				throw new NotImplementedException();
			}
			byte[] buffer = new byte[4096];
			while (size > 0) {
				stream.Seek(offset, SeekOrigin.Begin);
				int readCount = stream.Read(buffer, 0, Math.Min(size, buffer.Length));
				stream.Seek(offset + delta, SeekOrigin.Begin);
				stream.Write(buffer, 0, readCount);
				size -= readCount;
				offset += readCount;
			}
		}

		public void CleanupBundle()
		{
			if (trash.Count == 0) {
				// return early to avoid modifying Date Modified of bundle file with stream.SetLength
				return;
			}
			trash.Sort((x, y) => x.Offset - y.Offset);
			int moveDelta = 0;
			var indexKeys = new string[index.Keys.Count];
			index.Keys.CopyTo(indexKeys, 0);
			for (int i = 0; i < trash.Count; i++) {
				moveDelta += trash[i].AllocatedSize;
				int blockBegin = trash[i].Offset + trash[i].AllocatedSize;
				int blockEnd = (i < trash.Count - 1) ? trash[i + 1].Offset : indexOffset;
				MoveBlock(blockBegin, blockEnd - blockBegin, -moveDelta);
				foreach (var k in indexKeys) {
					var d = index[k];
					if (d.Offset >= blockBegin && d.Offset < blockEnd) {
						d.Offset -= moveDelta;
						index[k] = d;
					}
				}
			}
			trash.Clear();
			indexOffset -= moveDelta;
			stream.SetLength(stream.Length - moveDelta);
		}

		public override void Dispose()
		{
			base.Dispose();
			if (writer != null) {
				if (WasModified) {
					CleanupBundle();
					WriteIndexTable();
					RefreshBundleChecksum(Path);
				}
				writer.Close();
				writer = null;
			}
			if (reader != null) {
				reader.Close();
				reader = null;
			}
			if (stream != null) {
				stream.Close();
				stream = null;
			}
			index.Clear();
			while (streamPool.Count > 0) {
				streamPool.Pop().Dispose();
			}
		}

		public override Stream OpenFile(string path, FileMode mode = FileMode.Open)
		{
			var stream = (AssetStream)OpenFileRaw(path, mode);
			if ((stream.descriptor.Attributes & AssetAttributes.Zipped) != 0) {
				return DecompressAssetStream(stream, stream.descriptor.Attributes);
			}
			return stream;
		}

		public override Stream OpenFileRaw(string path, FileMode mode = FileMode.Open)
		{
			if (mode != FileMode.Open) {
				throw new NotSupportedException();
			}
			var stream = new AssetStream(this, path);
			if (CommandLineArgs.SimulateSlowExternalStorage) {
				ExternalStorageLagsSimulator.SimulateReadDelay(path, stream.descriptor.Size);
			}
			return stream;
		}

		private static Stream DecompressAssetStream(AssetStream stream, AssetAttributes attributes)
		{
			if ((attributes & AssetAttributes.ZippedDeflate) != 0) {
				return new DeflateStream(stream, CompressionMode.Decompress);
			}
			if ((attributes & AssetAttributes.ZippedLZMA) != 0) {
				return new LzmaDecompressionStream(stream);
			}
			throw new NotImplementedException();
		}

		public override SHA256 GetFileContentsHash(string path) => GetDescriptor(path).ContentsHash;

		public override SHA256 GetFileCookingUnitHash(string path) => GetDescriptor(path).CookingUnitHash;

		public override int GetFileSize(string path) => GetDescriptor(path).Size;

		public override int GetFileUnpackedSize(string path) => GetDescriptor(path).UnpackedSize;

		public override void DeleteFile(string path)
		{
			path = AssetPath.CorrectSlashes(path);
			var desc = GetDescriptor(path);
			index.Remove(path);
			trash.Add(desc);
			WasModified = true;
		}

		public override bool FileExists(string path) => index.ContainsKey(AssetPath.CorrectSlashes(path));

		public override AssetAttributes GetFileAttributes(string path) => GetDescriptor(path).Attributes;

		public override void ImportFile(
			string destinationPath, Stream stream, SHA256 cookingUnitHash, AssetAttributes attributes
		) {
			var length = (int)stream.Length;
			var buffer = ArrayPool<byte>.Shared.Rent(length);
			try {
				if (stream.Read(buffer, 0, length) != length) {
					throw new IOException();
				}
				var hash = SHA256.Compute(buffer, 0, length);
				stream = new MemoryStream(buffer, 0, length);
				if ((attributes & AssetAttributes.Zipped) != 0) {
					stream = CompressAssetStream(stream, attributes);
				}
				ImportFileRaw(destinationPath, stream, length, hash, cookingUnitHash, attributes);
			} finally {
				ArrayPool<byte>.Shared.Return(buffer);
			}
		}

		public override void ImportFileRaw(
			string destinationPath,
			Stream stream,
			int unpackedSize,
			SHA256 hash,
			SHA256 cookingUnitHash,
			AssetAttributes attributes
		) {
			var reuseExistingDescriptor =
				index.TryGetValue(AssetPath.CorrectSlashes(destinationPath), out AssetDescriptor d) &&
				d.AllocatedSize == stream.Length;
			if (reuseExistingDescriptor) {
				d.Size = (int)stream.Length;
				d.Attributes = attributes;
				d.ContentsHash = hash;
				d.CookingUnitHash = cookingUnitHash;
				d.UnpackedSize = unpackedSize;
				index[AssetPath.CorrectSlashes(destinationPath)] = d;
				this.stream.Seek(d.Offset, SeekOrigin.Begin);
				stream.CopyTo(this.stream);
			} else {
				if (FileExists(destinationPath)) {
					DeleteFile(destinationPath);
				}
				d = new AssetDescriptor();
				d.Size = (int)stream.Length;
				d.Offset = indexOffset;
				d.AllocatedSize = d.Size;
				d.UnpackedSize = unpackedSize;
				d.Attributes = attributes;
				d.ContentsHash = hash;
				d.CookingUnitHash = cookingUnitHash;
				index[AssetPath.CorrectSlashes(destinationPath)] = d;
				indexOffset += d.AllocatedSize;
				this.stream.Seek(d.Offset, SeekOrigin.Begin);
				stream.CopyTo(this.stream);
				this.stream.Flush();
			}
			WasModified = true;
		}

		internal static Stream CompressAssetStream(Stream stream, AssetAttributes attributes)
		{
			var memoryStream = new MemoryStream((int)stream.Length);
			using (var compressionStream = CreateCompressionStream(memoryStream, attributes)) {
				stream.CopyTo(compressionStream);
			}
			memoryStream.Seek(0, SeekOrigin.Begin);
			stream = memoryStream;
			return stream;
		}

		private static Stream CreateCompressionStream(Stream stream, AssetAttributes attributes)
		{
			if ((attributes & AssetAttributes.ZippedDeflate) != 0) {
				return new DeflateStream(stream, CompressionMode.Compress, leaveOpen: true);
			}
			if ((attributes & AssetAttributes.ZippedLZMA) != 0) {
				return new LzmaCompressionStream(stream, leaveOpen: true);
			}
			throw new NotImplementedException();
		}

		private unsafe void ReadIndexTable()
		{
			if (stream.Length == 0) {
				indexOffset = sizeof(int) * 4;
				index.Clear();
				return;
			}
			stream.Seek(0, SeekOrigin.Begin);
			var signature = reader.ReadInt32();
			if (signature != Signature) {
				throw new Exception($"The asset bundle at \"{Path}\" has been corrupted");
			}
			// Checksum. Use IsBundleCorrupted to validate.
			reader.ReadInt32();
			var version = reader.ReadInt32();
			if (version != Version.GetBundleFormatVersion()) {
				throw new InvalidBundleVersionException(
					$"The bundle format has been changed. Please update Citrus and rebuild game.\n" +
							$"Bundle format version: {version}, but expected: {Version.GetBundleFormatVersion()}");
			}
			indexOffset = reader.ReadInt32();
			stream.Seek(indexOffset, SeekOrigin.Begin);
			int numDescriptors = reader.ReadInt32();
			index.Clear();
			for (int i = 0; i < numDescriptors; i++) {
				var name = reader.ReadString();
				var descriptor = new AssetDescriptor();
				stream.Read(new Span<byte>((byte*)&descriptor, sizeof(AssetDescriptor)));
				index[name] = descriptor;
			}
		}

		private unsafe void WriteIndexTable()
		{
			stream.Seek(0, SeekOrigin.Begin);
			writer.Write(Signature);
			// Checksum. Will be updated on Dispose with RefreshBundleChecksum.
			writer.Write(0);
			writer.Write(Lime.Version.GetBundleFormatVersion());
			writer.Write(indexOffset);
			stream.Seek(indexOffset, SeekOrigin.Begin);
			int numDescriptors = index.Count;
			writer.Write(numDescriptors);
			foreach (var (path, descriptor) in index) {
				writer.Write(path);
				stream.Write(new ReadOnlySpan<byte>((byte*)&descriptor, sizeof(AssetDescriptor)));
			}
			writer.Flush();
		}

		public override IEnumerable<string> EnumerateFiles(string path = null, string extension = null)
		{
			if (extension != null && !extension.StartsWith(".")) {
				throw new InvalidOperationException();
			}
			foreach (var file in index.Keys) {
				if (path == null || file.StartsWith(path, StringComparison.OrdinalIgnoreCase)) {
					if (extension != null && !file.EndsWith(extension, StringComparison.OrdinalIgnoreCase)) {
						continue;
					}
					yield return file;
				}
			}
		}

		internal Stream AllocateStream()
		{
			lock (streamPool) {
				if (streamPool.Count > 0) {
					return streamPool.Pop();
				}
			}
			if (resourcesAssembly != null) {
				var stream = resourcesAssembly.GetManifestResourceStream(Path);
				if (stream == null) {
					throw new Lime.Exception(
						"Resource '{0}' doesn't exist. Available resources: {1}",
						Path,
						string.Join(", ", resourcesAssembly.GetManifestResourceNames())
					);
				}
				return stream;
			} else {
				return new FileStream(Path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
			}
		}

		internal void ReleaseStream(Stream stream)
		{
			lock (streamPool) {
				streamPool.Push(stream);
			}
		}

		private AssetDescriptor GetDescriptor(string path)
		{
			if (index.TryGetValue(AssetPath.CorrectSlashes(path), out var descriptor)) {
				return descriptor;
			}
			throw new Exception($"Asset '{path}' doesn't exist");
		}

		public override string ToSystemPath(string bundlePath) => throw new NotSupportedException();

		public override string FromSystemPath(string systemPath) => throw new NotSupportedException();

		public void ApplyPatch(PackedAssetBundle patchBundle)
		{
			var manifest = Manifest.Create(this);
			var patchManifest = Manifest.Create(patchBundle);
			if (patchManifest.BaseBundleVersion.HasValue) {
				if (manifest.BundleVersion.HasValue && patchManifest.BaseBundleVersion != manifest.BundleVersion) {
					throw new InvalidOperationException("Patch base version should be equal patched bundle version");
				} else if (patchManifest.BundleVersion.HasValue) {
					manifest.BundleVersion = patchManifest.BundleVersion;
				}
			}
			foreach (var file in patchBundle.EnumerateFiles()) {
				manifest.DeletedAssets.Remove(file);
				if (FileExists(file)) {
					DeleteFile(file);
				}
				using var stream = patchBundle.OpenFileRaw(file);
				ImportFileRaw(
					file,
					stream,
					patchBundle.GetFileUnpackedSize(file),
					patchBundle.GetFileContentsHash(file),
					patchBundle.GetFileCookingUnitHash(file),
					patchBundle.GetFileAttributes(file)
				);
			}
			foreach (var file in patchManifest.DeletedAssets) {
				if (FileExists(file)) {
					DeleteFile(file);
					if (!manifest.DeletedAssets.Contains(file)) {
						manifest.DeletedAssets.Add(file);
					}
				}
			}
			manifest.Save(this);
		}
	}
}
