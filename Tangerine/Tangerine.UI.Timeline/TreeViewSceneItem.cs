using System;
using Lime;
using Tangerine.Core;
using Tangerine.Core.Operations;

namespace Tangerine.UI.Timeline
{
	[NodeComponentDontSerialize]
	public class TreeViewComponent : Component
	{
		private TreeViewItem TreeViewItem { get; set; }

		public static TreeViewItem GetTreeViewItem(Row item)
		{
			var c = item.Components.GetOrAdd<TreeViewComponent>();
			c.TreeViewItem ??= new TreeViewSceneItem(item);
			return c.TreeViewItem;
		}
	}

	public interface ISceneItemHolder
	{
		Row SceneItem { get; }
	}

	public class TreeViewSceneItem : TreeViewItem, ISceneItemHolder
	{
		public Row SceneItem { get; }

		public TreeViewSceneItem(Row sceneItem)
		{
			SceneItem = sceneItem;
		}

		public override bool Selected
		{
			get => SceneItem.GetTimelineItemState().Selected;
			set { Document.Current.History.DoTransaction(() => SelectRow.Perform(SceneItem, value)); }
		}

		public override int SelectionOrder => SceneItem.GetTimelineItemState().SelectionOrder;

		/// <summary>
		/// All the control of Expanded property is now laid upon the TimelineItemStateComponent,
		/// since we've separated Expanded into NodesExpanded and AnimatorsExpanded.
		/// TreeView considers all items are already expanded.
		/// </summary>
		public override bool Expanded
		{
			get => true;
			set { }
		}

		public override bool CanExpand() => SceneItem.GetTimelineItemState().NodesExpandable;

		public override string Label
		{
			get => SceneItem.Id;
			set
			{
				if (SceneItem.Id != value) {
					SceneItem.Id = value;
				}
			}
		}

		public override bool CanRename()
		{
			if (SceneItem.TryGetNode(out var n1) && n1.EditorState().Locked) {
				return false;
			}
			for (var i = SceneItem.Parent; i != null; i = i.Parent) {
				if (i.TryGetNode(out var n2) && !string.IsNullOrEmpty(n2.ContentsPath)) {
					return false;
				}
			}
			return true;
		}

		public override ITexture Icon
		{
			get
			{
				if (SceneItem.TryGetNode(out var node)) {
					return NodeIconPool.GetTexture(node);
				}
				if (SceneItem.TryGetFolder(out _)) {
					return IconPool.GetTexture("Tools.NewFolder");
				}
				if (SceneItem.TryGetAnimator(out _)) {
					return IconPool.GetTexture("Timeline.Animator");
				}
				return IconPool.GetTexture("Nodes.Unknown");
			}
			set => throw new NotSupportedException();
		}
	}
}
