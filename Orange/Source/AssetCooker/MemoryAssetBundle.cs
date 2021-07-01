using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using Lime;

namespace Orange
{
	public class MemoryAssetBundle : AssetBundle
	{
		private struct Asset
		{
			public int Size;
			public int UnpackedSize;
			public AssetAttributes Attributes;
			public SHA256 Hash;
			public SHA256 CookingUnitHash;
			public byte[] Data;
		}

		private readonly SortedDictionary<string, Asset> assets = new SortedDictionary<string, Asset>(StringComparer.OrdinalIgnoreCase);

		public static MemoryAssetBundle ReadFromBundle(AssetBundle source)
		{
			var result = new MemoryAssetBundle();
			foreach (var path in source.EnumerateFiles()) {
				int size = source.GetFileSize(path);
				int unpackedSize = source.GetFileUnpackedSize(path);
				var buffer = ArrayPool<byte>.Shared.Rent(size);
				using (var stream = source.OpenFileRaw(path)) {
					if (stream.Read(buffer, 0, size) != size) {
						throw new IOException();
					}
				}
				result.assets[path] = new Asset {
					Size = size,
					UnpackedSize = unpackedSize,
					Attributes = source.GetFileAttributes(path),
					Hash = source.GetFileContentsHash(path),
					CookingUnitHash = source.GetFileCookingUnitHash(path),
					Data = buffer
				};
			}
			return result;
		}

		public void WriteToBundle(AssetBundle destination)
		{
			foreach (var (path, asset) in assets) {
				destination.ImportFileRaw(
					path,
					new MemoryStream(asset.Data, 0, asset.Size),
					asset.UnpackedSize,
					asset.Hash,
					asset.CookingUnitHash,
					asset.Attributes
				);
			}
		}

		public override void Dispose()
		{
			foreach (var (_, asset) in assets) {
				ArrayPool<byte>.Shared.Return(asset.Data);
			}
			assets.Clear();
		}

		public override Stream OpenFile(string path, FileMode fileMode = FileMode.Open)
		{
			throw new System.NotImplementedException();
		}

		public override Stream OpenFileRaw(string path, FileMode fileMode = FileMode.Open)
		{
			throw new System.NotImplementedException();
		}

		public override int GetFileSize(string path) => assets[path].Size;

		public override int GetFileUnpackedSize(string path) => assets[path].UnpackedSize;

		public override void DeleteFile(string path)
		{
			ArrayPool<byte>.Shared.Return(assets[path].Data);
			assets.Remove(path);
		}

		public override bool FileExists(string path) => assets.ContainsKey(path);

		public override void ImportFile(string destinationPath, Stream stream, SHA256 cookingUnitHash, AssetAttributes attributes)
		{
			var length = (int)stream.Length;
			var buffer = ArrayPool<byte>.Shared.Rent(length);
			try {
				if (stream.Read(buffer, 0, length) != length) {
					throw new IOException();
				}
				var hash = SHA256.Compute(buffer, 0, length);
				stream = new MemoryStream(buffer, 0, length);
				if ((attributes & AssetAttributes.Zipped) != 0) {
					stream = PackedAssetBundle.CompressAssetStream(stream, attributes);
				}
				ImportFileRaw(destinationPath, stream, length, hash, cookingUnitHash, attributes);
			} finally {
				ArrayPool<byte>.Shared.Return(buffer);
			}
		}

		public override void ImportFileRaw(string destinationPath, Stream stream, int unpackedSize, SHA256 hash, SHA256 cookingUnitHash, AssetAttributes attributes)
		{
			var length = (int)stream.Length;
			var buffer = ArrayPool<byte>.Shared.Rent(length);
			if (stream.Read(buffer, 0, length) != length) {
				throw new IOException();
			}
			assets[destinationPath] = new Asset {
				Size = length,
				UnpackedSize = unpackedSize,
				Attributes = attributes,
				Hash = hash,
				CookingUnitHash = cookingUnitHash,
				Data = buffer
			};
		}

		public override IEnumerable<string> EnumerateFiles(string path = null, string extension = null)
		{
			if (extension != null && !extension.StartsWith(".")) {
				throw new InvalidOperationException();
			}
			foreach (var file in assets.Keys) {
				if (path == null || file.StartsWith(path, StringComparison.OrdinalIgnoreCase)) {
					if (extension != null && !file.EndsWith(extension, StringComparison.OrdinalIgnoreCase)) {
						continue;
					}
					yield return file;
				}
			}
		}

		public override string ToSystemPath(string bundlePath) => throw new System.NotSupportedException();

		public override string FromSystemPath(string systemPath) => throw new System.NotSupportedException();

		public override SHA256 GetFileCookingUnitHash(string path) => assets[path].CookingUnitHash;

		public override SHA256 GetFileContentsHash(string path) => assets[path].Hash;
	}
}
