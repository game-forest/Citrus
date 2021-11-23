using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Lime;
using Tangerine.Core;
using Tangerine.Core.Components;
using Tangerine.Core.Operations;

namespace Tangerine.UI.Timeline
{
	public class RollPane
	{
		public readonly Widget ContentWidget;
		public readonly Widget RootWidget;
		public readonly ThemedScrollView ScrollView;
		public readonly TreeView TreeView;

		public RollPane()
		{
			RootWidget = new Widget {
				Id = nameof(RollPane),
				Padding = new Thickness { Top = 1, Bottom = 1 },
				Nodes = {
					(ScrollView = new ThemedScrollView())
				},
				Layout = new VBoxLayout(),
				Presenter = new SyncDelegatePresenter<Node>(RenderBackground)
			};
			var presentation = new TreeViewPresentation(
				new TreeViewItemPresentationOptions {
					SearchStringGetter = () => string.Empty
				}
			);
			TreeView = new TreeView(
				ScrollView, presentation,
				new TreeViewOptions { HandleCommands = false, ShowRoot = false }
			);
			TreeView.OnActivateItem += (sender, args) => {
				if (GetSceneItem(args.Item).TryGetNode(out var node)) {
					Document.Current.History.DoTransaction(() => EnterNode.Perform(node));
				}
			};
			TreeView.OnDragBegin += TreeView_OnDragBegin;
			TreeView.OnDragEnd += TreeView_OnDragEnd;
			((VBoxLayout) ScrollView.Content.Layout).Spacing = TimelineMetrics.RowSpacing;
			ContentWidget = new ThemedScrollView {
				Padding = new Thickness { Top = 1, Bottom = 1 },
				Layout = new VBoxLayout { Spacing = TimelineMetrics.RowSpacing },
				Presenter = new SyncDelegatePresenter<Node>(RenderBackground)
			};
			RootWidget.Gestures.Add(new ClickGesture(1, ShowContextMenu));
			RootWidget.AddLateChangeWatcher(
				() => (Document.Current.SceneTreeVersion, Document.Current.AnimationFrame),
				_ => RebuildTreeView());
			RebuildTreeView();
		}

		private void TreeView_OnDragBegin(object sender, TreeView.DragEventArgs args)
		{
			var topSceneItems = SceneTreeUtils.EnumerateTopSceneItems(args.Items.Select(GetSceneItem)).ToList();
			// Don't allow drag parent into one of its child.
			if (topSceneItems.Any(GetSceneItem(args.Parent).SameOrDescendantOf)) {
				args.CancelDrag = true;
			} else {
				args.CancelDrag = !topSceneItems.All(
					i => LinkSceneItem.CanLink(item: i, parent: GetSceneItem(args.Parent)));
			}
		}

		private void TreeView_OnDragEnd(object sender, TreeView.DragEventArgs args)
		{
			var parentSceneItem = GetSceneItem(args.Parent);
			var topSceneItems = SceneTreeUtils.EnumerateTopSceneItems(args.Items.Select(GetSceneItem)).ToList();
			Document.Current.History.DoTransaction(() => {
				DelegateOperation.Perform(null, Document.Current.BumpSceneTreeVersion, false);
				SetProperty.Perform(parentSceneItem.GetTimelineItemState(), nameof(TimelineItemStateComponent.NodesExpanded), true, false);
				DelegateOperation.Perform(Document.Current.BumpSceneTreeVersion, null, false);
				var index = TranslateTreeViewToSceneTreeIndex(args.Parent, args.Index);
				foreach (var item in topSceneItems) {
					if (item.Parent == parentSceneItem && index > parentSceneItem.Rows.IndexOf(item)) {
						index--;
					}
					UnlinkSceneItem.Perform(item);
				}

				foreach (var item in topSceneItems) {
					LinkSceneItem.Perform(parentSceneItem, new SceneTreeIndex(index), item);
					index = parentSceneItem.Rows.IndexOf(item) + 1;
				}
			});
		}

		private int TranslateTreeViewToSceneTreeIndex(TreeViewItem parent, int index)
		{
			if (parent.Items.Count == 0) {
				return 0;
			}
			var i = index >= parent.Items.Count ? 1 : 0;
			var item = GetSceneItem(parent.Items[index - i]);
			return GetSceneItem(parent).Rows.IndexOf(item) + i;
		}

		private static Row GetSceneItem(TreeViewItem item) => ((ISceneItemHolder) item).SceneItem;

		private void RebuildTreeView()
		{
			if (TreeView.RootItem != null) {
				DestroyTree(TreeView.RootItem);
			}
			TreeView.RootItem = null;
			foreach (var i in Document.Current.Rows) {
				var parent = TreeViewComponent.GetTreeViewItem(i.Parent);
				if (TreeView.RootItem == null) {
					parent.Items.Clear();
					TreeView.RootItem = parent;
				}
				var item = TreeViewComponent.GetTreeViewItem(i);
				parent.Items.Add(item);
			}
			TreeView.Refresh();
			WidgetContext.Current.Root.LayoutManager.Layout();

			void DestroyTree(TreeViewItem tree)
			{
				foreach (var node in tree.Items) {
					DestroyTree(node);
				}
				tree.Items.Clear();
			}
		}

		private void ShowContextMenu()
		{
			if (Document.Current.Animation.IsCompound) {
				new Menu {
					new Command("Add",
						() => AnimationTrackTreeViewItemPresentation.AddAnimationTrack()),
				}.Popup();
			}
		}

		private void RenderBackground(Node node)
		{
			RootWidget.PrepareRendererState();
			Renderer.DrawRect(Vector2.Zero, RootWidget.Size, ColorTheme.Current.TimelineRoll.Lines);
		}
	}
}
