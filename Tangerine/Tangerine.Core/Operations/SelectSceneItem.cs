using System;
using System.Linq;
using Lime;
using Tangerine.Core.Components;

namespace Tangerine.Core.Operations
{
	public static class SelectSceneItem
	{
		public static void Perform(SceneItem sceneItem, bool select = true)
		{
			SetProperty.Perform(
				obj: sceneItem.GetTimelineSceneItemState(),
				propertyName: nameof(TimelineSceneItemStateComponent.Selected),
				value: select,
				isChangingDocument: false
			);
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
					if (!i.GetTimelineSceneItemState().NodesExpanded) {
						DelegateOperation.Perform(
							Document.Current.BumpSceneTreeVersion, undo: null, isChangingDocument: false
						);
						SetProperty.Perform(
							obj: i.GetTimelineSceneItemState(),
							propertyName: nameof(TimelineSceneItemStateComponent.NodesExpanded),
							value: true,
							isChangingDocument: false
						);
						DelegateOperation.Perform(
							null, Document.Current.BumpSceneTreeVersion, isChangingDocument: false
						);
					}
				}
			}
			SelectSceneItem.Perform(sceneItem, select);
		}
	}
}
