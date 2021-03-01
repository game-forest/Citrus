using System;
using System.Collections.Generic;

namespace Orange
{
	public interface IFileEnumerator
	{
		string Directory { get; }
		Predicate<string> EnumerationFilter { get; set; }
		IEnumerable<string> Enumerate(string extension = null);
		void Rescan();
	}
}