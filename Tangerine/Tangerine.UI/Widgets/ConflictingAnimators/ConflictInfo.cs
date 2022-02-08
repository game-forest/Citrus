using System;
using System.Collections.Generic;

namespace Tangerine.UI.Widgets.ConflictingAnimators
{
	public class ConflictInfo
	{
		public readonly Type NodeType;
		public readonly string DocumentPath;
		public readonly string RelativeTextPath;
		public readonly List<int> RelativeIndexedPath;
		public readonly Property[] AffectedProperties;
		public readonly SortedSet<string>[] ConcurrentAnimations;

		public ConflictInfo(
			Type nodeType,
			string documentPath,
			string relativeTextPath,
			List<int> relativeIndexedPath,
			Property[] affectedProperties,
			SortedSet<string>[] concurrentAnimations
		) {
			NodeType = nodeType;
			DocumentPath = documentPath;
			RelativeTextPath = relativeTextPath;
			RelativeIndexedPath = relativeIndexedPath;
			AffectedProperties = affectedProperties;
			ConcurrentAnimations = concurrentAnimations;
		}

		public struct Property
		{
			public string Path;
			public int KeyframeColorIndex;
		}
	}
}
