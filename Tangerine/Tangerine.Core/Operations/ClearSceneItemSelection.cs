using System;
using System.Collections.Generic;
using System.Linq;
using Lime;
using Tangerine.Core;

namespace Tangerine.Core.Operations
{
	public static class ClearSceneItemSelection
	{
		public static void Perform()
		{
			var items = Document.Current.VisibleSceneItems.ToList();
			// Use temporary scene item list to avoid 'Collection was modified' exception
			foreach (var item in items) {
				if (item.GetTimelineSceneItemState().Selected) {
					SelectSceneItem.Perform(item, false);
				}
			}
		}
	}
}
