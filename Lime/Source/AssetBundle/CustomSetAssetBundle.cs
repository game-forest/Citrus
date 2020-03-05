using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Lime
{
	/// <summary>
	/// Asset bundle which substitutes enumeration results with the given list of files. 
	/// </summary>
	public class CustomSetAssetBundle : WrappedAssetBundle
	{
		private readonly List<FileInfo> fileInfos;

		public CustomSetAssetBundle(AssetBundle bundle, IEnumerable<FileInfo> fileInfos)
			: base (bundle)
		{
			this.fileInfos = fileInfos.ToList();
		}

		public override IEnumerable<FileInfo> EnumerateFileInfos(string path = null, string extension = null)
		{
			foreach (var fi in fileInfos) {
				if (path != null && !fi.Path.StartsWith(path, StringComparison.OrdinalIgnoreCase)) {
					continue;
				}
				if (extension != null && !fi.Path.EndsWith(extension, StringComparison.OrdinalIgnoreCase)) {
					continue;
				}
				yield return fi;
			}
		}
	}
}
