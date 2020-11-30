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
		private readonly List<string> files;

		public CustomSetAssetBundle(AssetBundle bundle, IEnumerable<string> files)
			: base (bundle)
		{
			this.files = files.ToList();
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
	}
}
