using System;
using System.Collections.Generic;
using System.Linq;
using Lime;
using Tangerine.Core;
using Tangerine.Core.Components;
using Tangerine.Core.Operations;
using Tangerine.UI.Timeline.Components;
using Tangerine.UI.Timeline.Operations;

namespace Tangerine.UI.Timeline
{
	public static class CommandBindings
	{
		public static void Bind()
		{
			ConnectCommand(
				command: TimelineCommands.EnterNode,
				action: () => Timeline.Instance.Roll.TreeView.ActivateItem(),
				enableChecker: Document.HasCurrent
			);
			ConnectCommand(
				command: TimelineCommands.EnterNodeAlias,
				action: () => Timeline.Instance.Roll.TreeView.ActivateItem(),
				enableChecker: Document.HasCurrent
			);
			ConnectCommand(
				command: TimelineCommands.EnterNodeMouse,
				action: () => Timeline.Instance.Roll.TreeView.ActivateItem(),
				enableChecker: Document.HasCurrent
			);
			ConnectCommand(TimelineCommands.Expand, ExpandOrCollapse, Document.HasCurrent);
			ConnectCommand(TimelineCommands.ExpandRecursively, ExpandOrCollapseRecursively, Document.HasCurrent);
			ConnectCommand(TimelineCommands.RenameSceneItem, RenameCurrentSceneItem);
			ConnectCommand(TimelineCommands.ExitNode, LeaveNode.Perform);
			ConnectCommand(TimelineCommands.ExitNodeAlias, LeaveNode.Perform);
			ConnectCommand(TimelineCommands.ExitNodeMouse, LeaveNode.Perform);
			ConnectCommand(TimelineCommands.ScrollUp, () => Timeline.Instance.Roll.TreeView.SelectPreviousItem());
			ConnectCommand(TimelineCommands.ScrollDown, () => Timeline.Instance.Roll.TreeView.SelectNextItem());
			ConnectCommand(
				TimelineCommands.SelectNodeUp,
				() => Timeline.Instance.Roll.TreeView.SelectRangePreviousItem()
			);
			ConnectCommand(
				TimelineCommands.SelectNodeDown,
				() => Timeline.Instance.Roll.TreeView.SelectRangeNextItem()
			);
			ConnectCommand(TimelineCommands.ScrollLeft, () => AdvanceCurrentColumn(-1));
			ConnectCommand(TimelineCommands.ScrollRight, () => AdvanceCurrentColumn(1));
			ConnectCommand(TimelineCommands.FastScrollLeft, () => AdvanceCurrentColumn(-10));
			ConnectCommand(TimelineCommands.FastScrollRight, () => AdvanceCurrentColumn(10));
			ConnectCommand(TimelineCommands.DeleteKeyframes, RemoveKeyframes);
			ConnectCommand(TimelineCommands.CreateMarkerPlay, () => CreateMarker(MarkerAction.Play));
			ConnectCommand(TimelineCommands.CreateMarkerStop, () => CreateMarker(MarkerAction.Stop));
			ConnectCommand(TimelineCommands.CreateMarkerJump, () => CreateMarker(MarkerAction.Jump));
			ConnectCommand(TimelineCommands.DeleteMarker, DeleteMarker);
			ConnectCommand(TimelineCommands.MoveDown, MoveNodesDown.Perform);
			ConnectCommand(TimelineCommands.MoveUp, MoveNodesUp.Perform);
			ConnectCommand(TimelineCommands.SelectAllSelectedSceneItemKeyframes, SelectAllSelectedSceneItemKeyframes);
			ConnectCommand(TimelineCommands.SelectAllKeyframes, SelectAllKeyframes);
			ConnectCommand(TimelineCommands.AdvanceToNextKeyframe, SelectNextKeyframe.Perform);
			ConnectCommand(TimelineCommands.AdvanceToPreviousKeyframe, SelectPreviousKeyframe.Perform);
		}

		private static void SelectAllKeyframes()
		{
			SelectKeyframes(Document.Current.VisibleSceneItems);
		}

		private static void SelectAllSelectedSceneItemKeyframes()
		{
			SelectKeyframes(Document.Current.SelectedSceneItems());
		}

		private static void SelectKeyframes(IEnumerable<SceneItem> sceneItems)
		{
			Operations.ClearGridSelection.Perform();
			foreach (var i in sceneItems) {
				if (i.Components.Get<NodeSceneItem>() is NodeSceneItem nodeItem) {
					foreach (var animator in nodeItem.Node.Animators) {
						foreach (var key in animator.ReadonlyKeys) {
							Operations.SelectGridSpan.Perform(
								i.GetTimelineSceneItemState().Index, key.Frame, key.Frame + 1
							);
						}
					}
				}
			}
		}

		private static void ConnectCommand(ICommand command, Action action, Func<bool> enableChecker = null)
		{
			CommandHandlerList.Global.Connect(command, new DocumentDelegateCommandHandler(action, enableChecker));
		}

		private static void ExpandOrCollapse()
		{
			var focusedItem = Document.Current.RecentlySelectedSceneItem();
			if (focusedItem != null) {
				Document.Current.History.DoTransaction(() => {
					DelegateOperation.Perform(null, Document.Current.BumpSceneTreeVersion, false);
					SetProperty.Perform(
						focusedItem.GetTimelineSceneItemState(),
						nameof(TimelineSceneItemStateComponent.NodesExpanded),
						!focusedItem.GetTimelineSceneItemState().NodesExpanded,
						isChangingDocument: false
					);
					DelegateOperation.Perform(Document.Current.BumpSceneTreeVersion, null, false);
				});
			}
		}

		private static void ExpandOrCollapseRecursively()
		{
			var focusedItem = Document.Current.RecentlySelectedSceneItem();
			if (focusedItem != null) {
				Document.Current.History.DoTransaction(() => {
					DelegateOperation.Perform(null, Document.Current.BumpSceneTreeVersion, false);
					ExpandOrCollapseHelper(
						Document.Current.RecentlySelectedSceneItem(),
						!focusedItem.GetTimelineSceneItemState().NodesExpanded
					);
					DelegateOperation.Perform(Document.Current.BumpSceneTreeVersion, null, false);
				});
			}

			void ExpandOrCollapseHelper(SceneItem sceneItem, bool expand)
			{
				if (sceneItem.GetTimelineSceneItemState().NodesExpanded != expand) {
					SetProperty.Perform(
						sceneItem.GetTimelineSceneItemState(),
						nameof(TimelineSceneItemStateComponent.NodesExpanded),
						expand,
						isChangingDocument: false
					);
				}
				foreach (var i in sceneItem.SceneItems) {
					ExpandOrCollapseHelper(i, expand);
				}
			}
		}

		private static void RenameCurrentSceneItem()
		{
			var doc = Document.Current;
			if (doc.SelectedSceneItems().Count() != 1) {
				return;
			}
			var item = doc.SelectedSceneItems().First();
			TreeViewComponent.GetTreeViewItem(item).Presentation.Rename();
		}

		private static void RemoveKeyframes()
		{
			foreach (var item in Document.Current.VisibleSceneItems.ToList()) {
				var node = item.Components.Get<NodeSceneItem>()?.Node ?? item.Components.Get<AnimatorSceneItem>()?.Node;
				if (node == null || node.EditorState().Locked) {
					continue;
				}
				var animation = node.Ancestors
					.SelectMany(n => n.Animations)
					.FirstOrDefault(a => a.Id == Document.Current.AnimationId);
				if (animation != Document.Current.Animation) {
					continue;
				}
				var property = item.Components.Get<AnimatorSceneItem>()?.Animator.TargetPropertyPath;
				var spans = item.Components.GetOrAdd<GridSpanListComponent>().Spans;
				foreach (var span in spans.GetNonOverlappedSpans()) {
					foreach (var a in node.Animators.ToList()) {
						if (a.AnimationId != Document.Current.AnimationId) {
							continue;
						}
						if (property != null && a.TargetPropertyPath != property) {
							continue;
						}
						foreach (var k in a.Keys.Where(k => k.Frame >= span.A && k.Frame < span.B).ToList()) {
							Core.Operations.RemoveKeyframe.Perform(a, k.Frame);
						}
					}
				}
			}
		}

		private static void AdvanceCurrentColumn(int stride)
		{
			Operations.SetCurrentColumn.Perform(Math.Max(0, Timeline.Instance.CurrentColumn + stride));
		}

		private static void CreateMarker(MarkerAction action)
		{
			var timeline = Timeline.Instance;
			var nearestMarker = Document.Current.Animation.Markers.LastOrDefault(
				m => m.Frame < timeline.CurrentColumn && m.Action == MarkerAction.Play);
			string markerId = (action == MarkerAction.Play)
				? GenerateMarkerId(Document.Current.Animation.Markers, "Start")
				: string.Empty;
			var newMarker = new Marker(
				markerId,
				timeline.CurrentColumn,
				action,
				action == MarkerAction.Jump && nearestMarker != null ? nearestMarker.Id : string.Empty
			);
			SetMarker.Perform(newMarker, true);
		}

		private static string GenerateMarkerId(MarkerList markers, string markerId)
		{
			int c = 1;
			string id = markerId;
			while (markers.Any(i => i.Id == id)) {
				id = markerId + c;
				c++;
			}
			return id;
		}

		private static void DeleteMarker()
		{
			var timeline = Timeline.Instance;
			var marker = Document.Current.Animation.Markers.GetByFrame(timeline.CurrentColumn);
			if (marker != null) {
				Core.Operations.DeleteMarker.Perform(marker, true);
			}
		}
	}
}
