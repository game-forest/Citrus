using System;
using Tangerine.Core;

namespace Tangerine.Core.Operations
{
	public static class SelectSceneItemRange
	{
		public static void Perform(SceneItem startItem, SceneItem endItem)
		{
			var startItemState = startItem.GetTimelineSceneItemState();
			var endItemState = endItem.GetTimelineSceneItemState();
			if (endItemState.Index >= startItemState.Index) {
				for (int i = startItemState.Index; i <= endItemState.Index; i++) {
					SelectSceneItem.Perform(Document.Current.VisibleSceneItems[i]);
				}
			} else {
				for (int i = startItemState.Index; i >= endItemState.Index; i--) {
					SelectSceneItem.Perform(Document.Current.VisibleSceneItems[i]);
				}
			}
		}
	}
}
