using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
			set
			{
				SetCurrent(value, resetTexturePool: true);
			}
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
			using (var stream = OpenFile(path, FileMode.Open)) {
				using (var memoryStream = new MemoryStream()) {
					stream.CopyTo(memoryStream);
					return memoryStream.ToArray();
				}
			}
		}

		public string ReadAllText(string path, Encoding encoding)
		{
			using (var streamReader = new StreamReader(OpenFile(path), encoding, detectEncodingFromByteOrderMarks: true)) {
				return streamReader.ReadToEnd();
			}
		}

		public abstract int GetFileSize(string path);

		public abstract void DeleteFile(string path);
		public abstract bool FileExists(string path);

		public abstract void ImportFile(
			string path, Stream stream, int reserve,
			string sourceExtension, SHA1 sourceSHA1, AssetAttributes attributes);

		/// <summary>
		/// Imports a file assuming that the input stream is already compressed.
		/// </summary>
		public abstract void ImportFileRaw(
			string path, Stream stream, int reserve,
			string sourceExtension, SHA1 sourceSHA1, AssetAttributes attributes);

		/// <summary>
		/// Enumerates all files by given path and having the given extension.
		/// </summary>
		public abstract IEnumerable<string> EnumerateFiles(string path = null, string extension = null);

		public void ImportFile(
			string srcPath, string dstPath, int reserve,
			string sourceExtension, SHA1 sourceSHA1, AssetAttributes attributes)
		{
			using (var stream = new FileStream(srcPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)) {
				ImportFile(dstPath, stream, reserve, sourceExtension, sourceSHA1, attributes);
			}
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
			if (string.IsNullOrEmpty(Application.CurrentLanguage))
				return path;
			string language = Application.CurrentLanguage;
			string extension = Path.GetExtension(path);
			string pathWithoutExtension = Path.ChangeExtension(path, null);
			string localizedParth = pathWithoutExtension + "." + language + extension;
			return FileExists(localizedParth) ? localizedParth : path;
		}

		public virtual AssetAttributes GetAttributes(string path)
		{
			return AssetAttributes.None;
		}

		public abstract string GetSourceExtension(string path);

		public abstract SHA1 GetSourceSHA1(string path);

		public virtual void SetAttributes(string path, AssetAttributes attributes)
		{
		}
	}
}
