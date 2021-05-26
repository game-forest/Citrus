using System;
using System.Linq;
using Lime;
using Tangerine.Core.Components;

namespace Tangerine.Core.Operations
{
	public static class SelectRow
	{
		public static void Perform(Row sceneItem, bool select = true)
		{
			SetProperty.Perform(sceneItem.GetTimelineItemState(), nameof(TimelineItemStateComponent.Selected), select, false);
		}
	}

	public static class SelectNode
	{
		public static void Perform(Node node, bool select = true)
		{
			var sceneItem = Document.Current.GetSceneItemForObject(node);
			if (select) {
				for (var i = sceneItem.Parent; i != null; i = i.Parent) {
					if (i.TryGetNode(out var n) && n == Document.Current.Container) {
						break;
					}
					if (!i.GetTimelineItemState().Expanded) {
						DelegateOperation.Perform(Document.Current.BumpSceneTreeVersion, null, false);
						SetProperty.Perform(i.GetTimelineItemState(), nameof(TimelineItemStateComponent.Expanded), true, false);
						DelegateOperation.Perform(null, Document.Current.BumpSceneTreeVersion, false);
					}
				}
			}
			SelectRow.Perform(sceneItem, select);
		}
	}
}
