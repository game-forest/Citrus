using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace Lime
{
	/// <summary>
	/// Asset bundle which substitutes enumeration results with the given list of files.
	/// All file operations will throw FileNotFoundException if provided file path
	/// does not exist in provided list of files.
	/// </summary>
	public class CustomSetAssetBundle : WrappedAssetBundle
	{
		private readonly List<string> files;
		private readonly HashSet<string> fileSet;

		public CustomSetAssetBundle(AssetBundle bundle, IEnumerable<string> files)
			: base (bundle)
		{
			this.files = files.ToList();
			fileSet = new HashSet<string>(files);
		}

		public override IEnumerable<string> EnumerateFiles(string path = null, string extension = null)
		{
			foreach (var file in files) {
				if (path != null && !file.StartsWith(path, StringComparison.OrdinalIgnoreCase)) {
					continue;
				}
				if (extension != null && !file.EndsWith(extension, StringComparison.OrdinalIgnoreCase)) {
					continue;
				}
				yield return file;
			}
		}

		public override bool FileExists(string path) => fileSet.Contains(AssetPath.CorrectSlashes(path));

		public override Stream OpenFile(string path, FileMode fileMode = FileMode.Open)
		{
			bool create = fileMode == FileMode.CreateNew ||
				fileMode == FileMode.OpenOrCreate ||
				fileMode == FileMode.Create;
			if (!FileExists(path) && !create) {
				throw new FileNotFoundException(null, path);
			}
			return base.OpenFile(path, fileMode);
		}

		public override Stream OpenFileRaw(string path, FileMode fileMode = FileMode.Open)
		{
			if (!FileExists(path)) {
				throw new FileNotFoundException(null, path);
			}
			return base.OpenFileRaw(path, fileMode);
		}

		public override void DeleteFile(string path)
		{
			if (!FileExists(path)) {
				throw new FileNotFoundException(null, path);
			}
			base.DeleteFile(path);
		}

		public override int GetFileSize(string path)
		{
			if (!FileExists(path)) {
				throw new FileNotFoundException(null, path);
			}
			return base.GetFileSize(path);
		}

		public override int GetFileUnpackedSize(string path)
		{
			if (!FileExists(path)) {
				throw new FileNotFoundException(null, path);
			}
			return base.GetFileUnpackedSize(path);
		}

		public override AssetAttributes GetFileAttributes(string path)
		{
			if (!FileExists(path)) {
				throw new FileNotFoundException(null, path);
			}
			return base.GetFileAttributes(path);
		}

		public override SHA256 GetFileCookingUnitHash(string path)
		{
			if (!FileExists(path)) {
				throw new FileNotFoundException(null, path);
			}
			return base.GetFileCookingUnitHash(path);
		}

		public override SHA256 GetFileContentsHash(string path)
		{
			if (!FileExists(path)) {
				throw new FileNotFoundException(null, path);
			}
			return base.GetFileContentsHash(path);
		}
	}
}
