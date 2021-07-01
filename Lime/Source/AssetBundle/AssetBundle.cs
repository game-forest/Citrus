using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Lime
{
	[Flags]
	public enum AssetAttributes
	{
		None = 0,
		ZippedDeflate = 1 << 0,
		NonPowerOf2Texture = 1 << 1,
		ZippedLZMA = 1 << 2,
		Zipped = ZippedDeflate | ZippedLZMA
	}

	public abstract class AssetBundle : IDisposable
	{
		[ThreadStatic]
		private static AssetBundle current;

		public static AssetBundle Current
		{
			get
			{
				if (current == null) {
					throw new Lime.Exception("AssetBundle.Current must be initialized before use.");
				}
				return current;
			}
			set => SetCurrent(value, resetTexturePool: true);
		}

		public static void SetCurrent(AssetBundle bundle, bool resetTexturePool)
		{
			if (current != bundle) {
				current = bundle;
				if (resetTexturePool) {
					TexturePool.Instance.DiscardAllStubTextures();
				}
			}
		}

		public static bool Initialized => current != null;

		public virtual void Dispose()
		{
			if (current == this) {
				SetCurrent(null, false);
			}
		}

		public abstract Stream OpenFile(string path, FileMode fileMode = FileMode.Open);

		/// <summary>
		/// Returns file data as it stored in the asset bundle, e.g. compressed.
		/// </summary>
		public abstract Stream OpenFileRaw(string path, FileMode fileMode = FileMode.Open);

		public byte[] ReadFile(string path)
		{
			using var stream = OpenFile(path, FileMode.Open);
			using var memoryStream = new MemoryStream();
			stream.CopyTo(memoryStream);
			return memoryStream.ToArray();
		}

		public string ReadAllText(string path, Encoding encoding)
		{
			using var streamReader = new StreamReader(
				stream: OpenFile(path),
				encoding: encoding,
				detectEncodingFromByteOrderMarks: true
			);
			return streamReader.ReadToEnd();
		}

		public abstract void DeleteFile(string path);

		public abstract bool FileExists(string path);

		public abstract void ImportFile(string destinationPath, Stream stream, SHA256 cookingUnitHash, AssetAttributes attributes);

		/// <summary>
		/// Imports a file assuming that the input stream is already compressed.
		/// </summary>
		public abstract void ImportFileRaw(string destinationPath, Stream stream, int unpackedSize, SHA256 hash, SHA256 cookingUnitHash, AssetAttributes attributes);

		/// <summary>
		/// Enumerates all files by given path and having the given extension.
		/// Order is specific to implementation.
		/// </summary>
		public abstract IEnumerable<string> EnumerateFiles(string path = null, string extension = null);

		public void ImportFile(
			string sourcePath, string destinationPath, SHA256 cookingUnitHash, AssetAttributes attributes)
		{
			using var stream = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
			ImportFile(destinationPath, stream, cookingUnitHash, attributes);
		}

		/// <summary>
		/// Translates bundle path to the asset in the file system. Raises UnsupportedException() for PackedAssetBundle.
		/// </summary>
		public abstract string ToSystemPath(string bundlePath);

		/// <summary>
		/// Translates absolute system file path to the asset path. Raises UnsupportedException() for PackedAssetBundle.
		/// </summary>
		public abstract string FromSystemPath(string systemPath);

		public Stream OpenFileLocalized(string path)
		{
			var stream = OpenFile(GetLocalizedPath(path));
			return stream;
		}

		public string GetLocalizedPath(string path)
		{
			if (string.IsNullOrEmpty(Application.CurrentLanguage)) {
				return path;
			}
			string language = Application.CurrentLanguage;
			string extension = Path.GetExtension(path);
			string pathWithoutExtension = Path.ChangeExtension(path, null);
			string localizedParth = pathWithoutExtension + "." + language + extension;
			return FileExists(localizedParth) ? localizedParth : path;
		}

		public abstract int GetFileSize(string path);

		public abstract int GetFileUnpackedSize(string path);

		public virtual AssetAttributes GetFileAttributes(string path) => AssetAttributes.None;

		/// <summary>
		/// Returns SHA256 that was passed to the ImportFile or ImportFileRaw methods.
		/// </summary>
		public abstract SHA256 GetFileCookingUnitHash(string path);

		/// <summary>
		/// Returns SHA256 based on the file contents.
		/// </summary>
		public abstract SHA256 GetFileContentsHash(string path);
	}
}
