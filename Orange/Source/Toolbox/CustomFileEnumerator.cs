using System;
using System.Collections.Generic;
using System.Linq;
using Lime;

namespace Orange
{
	public class CustomFilesEnumerator : IFileEnumerator
	{
		public string Directory { get; }
		public Predicate<string> EnumerationFilter { get; set; }
		private readonly List<string> files;

		public CustomFilesEnumerator(string directory, List<string> files)
		{
			Directory = directory;
			this.files = files;
		}

		public void Rescan() { }

		public IEnumerable<string> Enumerate(string extension = null)
		{
			if (extension == null && EnumerationFilter == null) {
				return files;
			}
			return files
				.Where(file => extension == null || file.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
				.Where(file => EnumerationFilter == null || EnumerationFilter(file));
		}
	}
}
