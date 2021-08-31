using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Lime;
using Tangerine.Core;
using Tangerine.Core.Operations;
using Tangerine.UI;
using Tangerine.UI.Timeline;

namespace Tangerine.Panels
{
	public class HierarchyPanel : IDocumentView
	{
		private readonly Widget panelWidget;
		private readonly Frame contentWidget;
		private readonly TreeView treeView;
		private readonly EditBox searchStringEditor;
		private readonly Func<bool> isSearchActiveGetter;

		public HierarchyPanel(Widget panelWidget)
		{
			this.panelWidget = panelWidget;
			panelWidget.TabTravesable = new TabTraversable();
			isSearchActiveGetter = () => searchStringEditor.Text.Length > 0;
			ThemedScrollView scrollView;

			contentWidget = new Frame {
				Id = nameof(HierarchyPanel),
				Padding = new Thickness(5),
				Layout = new VBoxLayout { Spacing = 5 },
				Nodes = {
					(searchStringEditor = new ThemedEditBox()),
					(scrollView = new ThemedScrollView())
				}
			};
			var presentation = new TreeViewPresentation(
				new TreeViewItemPresentationOptions {
					Minimalistic = true,
					SearchStringGetter = () => searchStringEditor.Text
				}
			);
			treeView = new TreeView(scrollView, presentation, new TreeViewOptions { ShowRoot = false });
			treeView.OnDragBegin += TreeView_OnDragBegin;
			treeView.OnDragEnd += TreeView_OnDragEnd;
			treeView.OnActivateItem += TreeViewOnActivateItem;
			treeView.OnCopy += TreeView_OnCopy;
			treeView.OnCut += TreeView_OnCut;
			treeView.OnDelete += TreeView_OnDelete;
			treeView.OnPaste += TreeView_OnPaste;
			contentWidget.AddChangeWatcher(
				() => Document.Current?.SceneTreeVersion ?? 0,_ => RebuildTreeView());
			RebuildTreeView();
			scrollView.CompoundPresenter.Add(new SyncDelegatePresenter<Widget>(w => {
				if (treeView.RootItem?.Items.Count > 0) {
					w.PrepareRendererState();
					Renderer.DrawRect(w.ContentPosition, w.ContentSize + w.ContentPosition,
						Theme.Colors.WhiteBackground);
				}
			}));
			searchStringEditor.Tasks.Add(SearchStringDebounceTask());
		}

		private IEnumerator<object> SearchStringDebounceTask()
		{
			string previousSearchText = null;
			var lastChangeAt = DateTime.Now;
			var needRefresh = false;
			while (true) {
				yield return null;
				if (searchStringEditor.Text != previousSearchText) {
					previousSearchText = searchStringEditor.Text;
					lastChangeAt = DateTime.Now;
					needRefresh = true;
				}
				if (needRefresh && DateTime.Now - lastChangeAt > TimeSpan.FromSeconds(0.25f)) {
					needRefresh = false;
					RebuildTreeView();
					if (searchStringEditor.Text.Length > 0 && treeView.RootItem != null) {
						ExpandTree(treeView.RootItem);
					}
				}
			}
		}

		private void TreeView_OnDragBegin(object sender, TreeView.DragEventArgs args)
		{
			if (IsExternalSceneItem(GetSceneItem(args.Parent))) {
				// Forbid drop into an external scene
				args.CancelDrag = true;
				return;
			}
			if (args.Items.Select(GetSceneItem).Select(i => i.Parent).Any(IsExternalSceneItem)) {
				// Forbid drag from an external scene
				args.CancelDrag = true;
				return;
			}
			var topSceneItems = SceneTreeUtils.EnumerateTopSceneItems(args.Items.Select(GetSceneItem)).ToList();
			// Don't allow drag parent into one of its child.
			if (topSceneItems.Any(GetSceneItem(args.Parent).SameOrDescendantOf)) {
				args.CancelDrag = true;
			} else {
				args.CancelDrag = !topSceneItems.All(
					i => LinkSceneItem.CanLink(GetSceneItem(args.Parent), i));
			}
		}

		private bool IsExternalSceneItem(Row item)
		{
			for (var i = item; i != null; i = i.Parent) {
				if (i.TryGetNode(out var n) && !string.IsNullOrEmpty(n.ContentsPath)) {
					return true;
				}
			}
			return false;
		}

		private void TreeView_OnDragEnd(object sender, TreeView.DragEventArgs args)
		{
			var parentSceneItem = GetSceneItem(args.Parent);
			var topSceneItems = SceneTreeUtils.EnumerateTopSceneItems(args.Items.Select(GetSceneItem)).ToList();
			Document.Current.History.DoTransaction(() => {
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

		private void TreeView_OnCopy(object sender, TreeView.CopyEventArgs args)
		{
			var topSceneItems = SceneTreeUtils.EnumerateTopSceneItems(args.Items.Select(GetSceneItem)).ToList();
			var stream = new MemoryStream();
			CopySceneItemsToStream.Perform(topSceneItems, stream);
			Clipboard.Text = System.Text.Encoding.UTF8.GetString(stream.ToArray());
		}

		private void TreeView_OnCut(object sender, TreeView.CopyEventArgs args)
		{
			TreeView_OnCopy(sender, args);
			TreeView_OnDelete(sender, args);
		}

		private void TreeView_OnDelete(object sender, TreeView.CopyEventArgs args)
		{
			if (args.Items.Any(i => IsExternalSceneItem(GetSceneItem(i.Parent)))) {
				return;
			}
			var topSceneItems = SceneTreeUtils.EnumerateTopSceneItems(args.Items.Select(GetSceneItem)).ToList();
			Document.Current.History.DoTransaction(() => {
				treeView.ClearSelection();
				foreach (var row in topSceneItems) {
					UnlinkSceneItem.Perform(row);
				}
			});
		}

		private void TreeView_OnPaste(object sender, TreeView.PasteEventArgs args)
		{
			if (IsExternalSceneItem(GetSceneItem(args.Parent))) {
				return;
			}
			var data = Clipboard.Text;
			if (!string.IsNullOrEmpty(data)) {
				Document.Current.History.DoTransaction(() => {
					var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(data));
					var index = TranslateTreeViewToSceneTreeIndex(args.Parent, args.Index);
					Paste.Perform(
						stream,
						GetSceneItem(args.Parent),
						new SceneTreeIndex(index),
						out var pastedItems
					);
					RebuildTreeView();
					treeView.Refresh();
					treeView.ClearSelection();
					contentWidget.LayoutManager.Layout();
					foreach (var item in pastedItems.Select(GetTreeViewItem)) {
						treeView.SelectItem(item, true, false);
					}
				});
			}
		}

		private TreeViewItem GetTreeViewItem(Row item)
		{
			var c = item.Components.GetOrAdd<TreeViewComponent>();
			c.TreeViewItem = c.TreeViewItem ?? new TreeViewSceneItem(item, isSearchActiveGetter);
			return c.TreeViewItem;
		}

		private static Row GetSceneItem(TreeViewItem item) => ((ISceneItemHolder) item).SceneItem;

		private void TreeViewOnActivateItem(object sender, TreeView.ActivateItemEventArgs args)
		{
			if (GetSceneItem(args.Item).TryGetNode(out var node)) {
				var enterIntoNode = args.Method == TreeView.ActivationMethod.Keyboard;
				node = NavigateToNode.Perform(node, enterIntoNode);
				if (enterIntoNode) {
					Document.Current.History.DoTransaction(() => {
						GetTreeViewItem(Document.Current.GetSceneItemForObject(node)).Expanded = true;
					});
				}
			}
		}

		private static void ExpandTree(TreeViewItem tree)
		{
			if (tree.Items.Count == 0) {
				return;
			}
			if (!tree.Expanded) {
				tree.Expanded = true;
			}
			foreach (var i in tree.Items) {
				ExpandTree(i);
			}
		}

		private void RebuildTreeView()
		{
			if (treeView.RootItem != null) {
				DestroyTree(treeView.RootItem);
			}
			var filter = searchStringEditor.Text;
			if (Document.Current != null) {
				treeView.RootItem = CreateTree(Document.Current.SceneTree);
			}
			treeView.Refresh();

			void DestroyTree(TreeViewItem tree)
			{
				foreach (var node in tree.Items) {
					DestroyTree(node);
				}
				tree.Items.Clear();
			}

			TreeViewItem CreateTree(Row sceneTree)
			{
				var currentItem = GetTreeViewItem(sceneTree);
				foreach (var i in sceneTree.Rows) {
					if (i.GetAnimator() != null || i.GetAnimation() != null) {
						continue;
					}
					var child = CreateTree(i);
					if (child != null) {
						currentItem.Items.Add(child);
					}
				}
				if (
					currentItem.Items.Count > 0 || filter.Length == 0 ||
					sceneTree.Id?.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0
				) {
					return currentItem;
				}
				return null;
			}
		}

		public void Attach()
		{
			panelWidget.PushNode(contentWidget);
		}

		public void Detach()
		{
			contentWidget.Unlink();
		}

		[NodeComponentDontSerialize]
		public class TreeViewComponent : Component
		{
			public TreeViewItem TreeViewItem { get; set; }

			public bool Selected
			{
				get => SelectionOrder > 0;
				set
				{
					if (Selected != value) {
						SelectionOrder = value ? selectionCounter++ : 0;
					}
				}
			}

			private static int selectionCounter = 1;

			public int SelectionOrder { get; private set; }

			public bool ExpandedIfSearchInactive { get; set; }
			public bool ExpandedIfSearchActive { get; set; }
		}

		private class TreeViewSceneItem : TreeViewItem, ISceneItemHolder
		{
			public Row SceneItem { get; }
			public Func<bool> IsSearchActiveGetter { get; }

			public TreeViewSceneItem(Row sceneItem, Func<bool> isSearchActiveGetter)
			{
				SceneItem = sceneItem;
				IsSearchActiveGetter = isSearchActiveGetter;
			}

			public override bool Selected
			{
				get => SceneItem.Components.Get<TreeViewComponent>().Selected;
				set
				{
					Document.Current.History.DoTransaction(() => {
						SetProperty.Perform(
							SceneItem.Components.Get<TreeViewComponent>(),
							nameof(TreeViewComponent.Selected), value, false);
					});
				}
			}

			public override int SelectionOrder => SceneItem.Components.Get<TreeViewComponent>().SelectionOrder;

			public override bool Expanded
			{
				get
				{
					var c = SceneItem.Components.Get<TreeViewComponent>();
					return IsSearchActiveGetter() ? c.ExpandedIfSearchActive : c.ExpandedIfSearchInactive;
				}

				set
				{
					Document.Current.History.DoTransaction(() => {
						DelegateOperation.Perform(null, Document.Current.BumpSceneTreeVersion, false);
						var propertyName = IsSearchActiveGetter()
							? nameof(TreeViewComponent.ExpandedIfSearchActive)
							: nameof(TreeViewComponent.ExpandedIfSearchInactive);
						SetProperty.Perform(
							SceneItem.Components.Get<TreeViewComponent>(), propertyName, value, false);
						DelegateOperation.Perform(Document.Current.BumpSceneTreeVersion, null, false);
					});
				}
			}

			public override bool CanExpand() => Items.Count > 0;

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
				if (SceneItem.TryGetAnimator(out _)) {
					return false;
				}
				if (SceneItem.TryGetNode(out var n1) && n1.EditorState().Locked) {
					return false;
				}
				for (var i = Parent; i != null; i = i.Parent) {
					if (GetSceneItem(i).TryGetNode(out var n2) && !string.IsNullOrEmpty(n2.ContentsPath)) {
						return false;
					}
				}
				return true;
			}

			public override ITexture Icon
			{
				get
				{
					var row = SceneItem;
					if (row.TryGetNode(out var node)) {
						return NodeIconPool.GetTexture(node);
					}
					if (row.TryGetFolder(out _)) {
						return IconPool.GetTexture("Tools.NewFolder");
					}
					return IconPool.GetTexture("Nodes.Unknown");
				}
				set => throw new NotSupportedException();
			}
		}
	}
}
