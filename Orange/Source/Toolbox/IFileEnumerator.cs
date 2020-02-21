using System;
using System.Collections.Generic;
using Lime;

namespace Orange
{
	public interface IFileEnumerator
	{
		string Directory { get; }
		Predicate<FileInfo> EnumerationFilter { get; set; }
		IEnumerable<FileInfo> Enumerate(string extension = null);
		void Rescan();
	}
}