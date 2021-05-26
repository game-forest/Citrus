using System;
using Tangerine.Core;

namespace Tangerine.Core.Operations
{
	public static class SelectRowRange
	{
		public static void Perform(Row startItem, Row endItem)
		{
			var startItemState = startItem.GetTimelineItemState();
			var endItemState = endItem.GetTimelineItemState();
			if (endItemState.Index >= startItemState.Index) {
				for (int i = startItemState.Index; i <= endItemState.Index; i++) {
					SelectRow.Perform(Document.Current.Rows[i]);
				}
			} else {
				for (int i = startItemState.Index; i >= endItemState.Index; i--) {
					SelectRow.Perform(Document.Current.Rows[i]);
				}
			}
		}
	}
}

