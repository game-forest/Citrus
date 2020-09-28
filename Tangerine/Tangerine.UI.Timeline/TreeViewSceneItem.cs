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
			c.TreeViewItem = c.TreeViewItem ?? new TreeViewSceneItem(item);
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
			get => SceneItem.Selected;
			set { Document.Current.History.DoTransaction(() => SelectRow.Perform(SceneItem, value)); }
		}

		public override int SelectionOrder => SceneItem.SelectionOrder;

		public override bool Expanded
		{
			get => SceneItem.Expanded;
			set
			{
				Document.Current.History.DoTransaction(() => {
					DelegateOperation.Perform(null, Document.Current.BumpSceneTreeVersion, false);
					SetProperty.Perform(SceneItem, nameof(Row.Expanded), value, false);
					DelegateOperation.Perform(Document.Current.BumpSceneTreeVersion, null, false);
				});
			}
		}

		public override bool CanExpand() => SceneItem.Expandable;

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
				return IconPool.GetTexture("Nodes.Unknown");
			}
			set => throw new NotSupportedException();
		}
	}
}
