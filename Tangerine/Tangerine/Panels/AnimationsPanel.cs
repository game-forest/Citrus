using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Lime;
using Tangerine.Core;
using Tangerine.Core.Operations;
using Tangerine.UI;
using Tangerine.UI.Timeline;
using Tangerine.UI.Timeline.Operations;

namespace Tangerine.Panels
{
	public class AnimationsPanel : IDocumentView
	{
		private readonly Widget panelWidget;
		private readonly Frame contentWidget;
		private readonly EditBox searchStringEditor;
		private readonly ThemedScrollView scrollView;
		private TreeViewMode mode;

		public AnimationsPanel(Widget panelWidget)
		{
			this.panelWidget = panelWidget;
			panelWidget.TabTravesable = new TabTraversable();
			ToolbarButton expandAll, collapseAll, showAll, showCurrent;
			contentWidget = new Frame {
				Id = nameof(AnimationsPanel),
				Padding = new Thickness(0),
				Layout = new VBoxLayout { Spacing = 5 },
				Nodes = {
					new Frame {
						Layout = new VBoxLayout { Spacing = 5 },
						Padding = new Thickness { Top = 4 },
						Nodes = {
							new Widget {
								Padding = new Thickness(2, 10, 0, 0),
								MinMaxHeight = Metrics.ToolbarHeight,
								Presenter = new WidgetFlatFillPresenter(ColorTheme.Current.Toolbar.Background),
								Layout = new HBoxLayout { DefaultCell = new DefaultLayoutCell(Alignment.Center) },
								Nodes = {
									(expandAll = new ToolbarButton {
										Texture = IconPool.GetTexture("AnimationsPanel.ExpandAll"),
										Tooltip = "Expand All",
									}),
									(collapseAll = new ToolbarButton {
										Texture = IconPool.GetTexture("AnimationsPanel.CollapseAll"),
										Tooltip = "Collapse All",
									}),
									(showAll = new ToolbarButton {
										Texture = IconPool.GetTexture("AnimationsPanel.ShowAll"),
										Tooltip = "Show All",
									}),
									(showCurrent = new ToolbarButton {
										Texture = IconPool.GetTexture("AnimationsPanel.ShowCurrent"),
										Tooltip = "Show Current Animation",
									}),
									(searchStringEditor = new ThemedEditBox()),
								}
							},
							(scrollView = new ThemedScrollView()),
						}
					}
				}
			};
			var itemProvider = new TreeViewItemProvider {
				IsSearchActiveGetter = () => searchStringEditor.Text.Length > 0,
				ItemStateProvider = sceneItem => {
					var c = sceneItem.Components.GetOrAdd<TreeViewItemStateComponent>();
					return mode == TreeViewMode.AllHierarchy
						? c.StateForAllHierarchy ?? (c.StateForAllHierarchy = new TreeViewItemState())
						: c.StateForCurrentBranch ?? (c.StateForCurrentBranch = new TreeViewItemState {
							ExpandedIfSearchActive = sceneItem.GetNode() != null,
							ExpandedIfSearchInactive = sceneItem.GetNode() != null,
						});
				}
			};
			var treeView = CreateTreeView(scrollView, itemProvider,
				new TreeViewItemPresentationOptions { Minimalistic = true, SearchStringGetter = () => searchStringEditor.Text });
			searchStringEditor.Tasks.Add(SearchStringDebounceTask(treeView, itemProvider));
			expandAll.Clicked = () => ExpandAll(treeView.RootItem, true);
			collapseAll.Clicked = () => ExpandAll(treeView.RootItem, false);
			showCurrent.AddTransactionClickHandler(() => ScrollToCurrentAnimation(treeView, itemProvider));
			showAll.Clicked = () => {
				mode = mode == TreeViewMode.CurrentBranch ? TreeViewMode.AllHierarchy : TreeViewMode.CurrentBranch;
				RebuildTreeView(treeView, itemProvider);
			};
			showAll.AddChangeWatcher(() => mode, _ => showAll.Checked = mode == TreeViewMode.AllHierarchy);
		}

		private void ExpandAll(TreeViewItem item, bool expand)
		{
			item.Expanded = expand;
			foreach (var i in item.Items) {
				i.Expanded = expand;
			}
		}

		private IEnumerator<object> SearchStringDebounceTask(TreeView treeView,
			TreeViewItemProvider provider)
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
					RebuildTreeView(treeView, provider);
					if (searchStringEditor.Text.Length > 0 && treeView.RootItem != null) {
						ExpandTree(treeView.RootItem);
					}
				}
			}
		}

		private class TreeViewItemStateComponent : Component
		{
			public TreeViewItemState StateForCurrentBranch;
			public TreeViewItemState StateForAllHierarchy;
		}

		private class TreeViewItemProvider
		{
			public Func<Row, TreeViewItemState> ItemStateProvider;
			public Func<bool> IsSearchActiveGetter;

			public TreeViewItem GetNodeTreeViewItem(Row sceneItem)
			{
				var s = ItemStateProvider(sceneItem);
				s.TreeViewItem = s.TreeViewItem ?? new NodeTreeViewItem(
					sceneItem, s, sceneItem.GetNode(), IsSearchActiveGetter);
				return s.TreeViewItem;
			}

			public TreeViewItem GetAnimationTreeViewItem(Row sceneItem)
			{
				var s = ItemStateProvider(sceneItem);
				s.TreeViewItem = s.TreeViewItem ?? new AnimationTreeViewItem(
					sceneItem, s, sceneItem.GetAnimation(), IsSearchActiveGetter);
				return s.TreeViewItem;
			}

			public TreeViewItem GetMarkerTreeViewItem(Row sceneItem)
			{
				var s = ItemStateProvider(sceneItem);
				s.TreeViewItem = s.TreeViewItem ?? new MarkerTreeViewItem(
					sceneItem, s, sceneItem.GetMarker(), IsSearchActiveGetter);
				return s.TreeViewItem;
			}
		}

		enum TreeViewMode
		{
			CurrentBranch,
			AllHierarchy
		}

		private TreeView CreateTreeView(
			ThemedScrollView scrollView, TreeViewItemProvider provider,
			TreeViewItemPresentationOptions presentationOptions)
		{
			var presentation = new TreeViewPresentation(provider, presentationOptions, treeViewModeGetter: () => mode);
			var treeView = new TreeView(scrollView, presentation, new TreeViewOptions { ShowRoot = false });
			treeView.OnDragBegin += TreeView_OnDragBegin;
			treeView.OnDragEnd += TreeView_OnDragEnd;
			treeView.OnCopy += TreeView_OnCopy;
			treeView.OnCut += (s, e) => TreeView_OnCut(s, e, provider);
			treeView.OnDelete += (s, e) => TreeView_OnDelete(s, e, provider);
			treeView.OnPaste += (s, e) => TreeView_OnPaste(s, e, provider);
			treeView.OnActivateItem += (s, e) => {
				switch (e.Item) {
					case NodeTreeViewItem ni:
						Document.Current.History.DoTransaction(() => NavigateToNode.Perform(ni.Node, true));
						break;
					case AnimationTreeViewItem ai:
						Document.Current.History.DoTransaction(() => NavigateToAnimation.Perform(ai.Animation));
						AnimationTreeViewItemPresentation.UpdateWarnings(treeView, mode);
						break;
					case MarkerTreeViewItem mi:
						Document.Current.History.DoTransaction(() => {
							NavigateToAnimation.Perform(mi.Marker.Owner);
							// Wrap into transaction, as the document could be changed in case of an external scene.
							Document.Current.History.DoTransaction(() => {
								SetCurrentColumn.Perform(mi.Marker.Frame);
								CenterTimelineOnCurrentColumn.Perform();
							});
						});
						break;
				}
			};
			scrollView.CompoundPresenter.Add(new SyncDelegatePresenter<Widget>(w => {
				if (treeView.RootItem?.Items.Count > 0) {
					w.PrepareRendererState();
					Renderer.DrawRect(w.ContentPosition, w.ContentSize + w.ContentPosition,
						Theme.Colors.WhiteBackground);
				}
			}));
			contentWidget.AddChangeWatcher(
				() => (
					Document.Current?.SceneTreeVersion ?? 0,
					Document.Current?.Container,
					Document.Current?.AnimationFrame),
				_ => RebuildTreeView(treeView, provider));
			RebuildTreeView(treeView, provider);
			return treeView;
		}

		private void ScrollToCurrentAnimation(TreeView treeView, TreeViewItemProvider provider)
		{
			if (
				mode == TreeViewMode.CurrentBranch &&
				!SetKeyframe.CheckAnimationScope(Document.Current.Animation, Document.Current.Container)
			) {
				NavigateToAnimation.Perform(Document.Current.Animation);
			}
			var sceneItem = Document.Current.GetSceneItemForObject(Document.Current.Animation);
			var treeViewItem = provider.GetAnimationTreeViewItem(sceneItem);
			if (treeViewItem.Parent != null) {
				treeViewItem.Parent.Expanded = true;
				treeView.Refresh();
				contentWidget.LayoutManager.Layout();
				treeView.ScrollToItem(treeViewItem, true);
				treeView.ClearSelection();
				provider.GetAnimationTreeViewItem(sceneItem).Selected = true;
				scrollView.Content.SetFocus();
			}
		}

		private void TreeView_OnDragBegin(object sender, TreeView.DragEventArgs args)
		{
			args.CancelDrag = !(args.Parent is NodeTreeViewItem) || !args.Items.All(i => i is AnimationTreeViewItem);
		}

		private void TreeView_OnDragEnd(object sender, TreeView.DragEventArgs args)
		{
			if (!(args.Parent is NodeTreeViewItem parentItem)) {
				return;
			}
			var animationItems = args.Items.OfType<AnimationTreeViewItem>().ToList();
			Document.Current.History.DoTransaction(() => {
				var index = TranslateTreeViewToSceneTreeIndex(args.Parent, args.Index);
				foreach (var item in animationItems) {
					var itemIndex = parentItem.SceneItem.Rows.IndexOf(item.SceneItem);
					if (item.Parent == parentItem && index > itemIndex) {
						index--;
					}
					UnlinkSceneItem.Perform(item.SceneItem);
				}
				if (!animationItems.All(i => LinkSceneItem.CanLink(parentItem.SceneItem, i.SceneItem))) {
					AlertDialog.Show("Can't drag the animation. Probably an animation with the same id already exists", "Ok");
					Document.Current.History.RollbackTransaction();
					return;
				}
				foreach (var item in animationItems) {
					LinkSceneItem.Perform(parentItem.SceneItem, index++, item.SceneItem);
				}
			});
		}

		private static int TranslateTreeViewToSceneTreeIndex(TreeViewItem parent, int index)
		{
			if (parent.Items.Count == 0) {
				return 0;
			}
			var i = index >= parent.Items.Count ? 1 : 0;
			var item = ((CommonTreeViewItem)parent.Items[index - i]).SceneItem;
			return ((CommonTreeViewItem)parent).SceneItem.Rows.IndexOf(item) + i;
		}

		private void TreeView_OnCopy(object sender, TreeView.CopyEventArgs args)
		{
			var stream = new MemoryStream();
			var container = new Frame();
			var animations = args.Items.OfType<AnimationTreeViewItem>().
				Select(i => i.Animation).
				Where(a => !a.IsLegacy);
			foreach (var animation in animations) {
				container.Animations.Add(Cloner.Clone(animation));
			}
			TangerinePersistence.Instance.WriteObject(null, stream, container, Persistence.Format.Json);
			Clipboard.Text = System.Text.Encoding.UTF8.GetString(stream.ToArray());
		}

		private void TreeView_OnCut(object sender, TreeView.CopyEventArgs args, TreeViewItemProvider provider)
		{
			TreeView_OnCopy(sender, args);
			TreeView_OnDelete(sender, args, provider);
		}

		private void TreeView_OnDelete(object sender, TreeView.CopyEventArgs args, TreeViewItemProvider provider)
		{
			var animationItems = args.Items.OfType<AnimationTreeViewItem>().
				Where(a => !a.Animation.IsLegacy).ToList();
			if (animationItems.Count == 0) {
				return;
			}
			Document.Current.History.DoTransaction(() => {
				((TreeView)sender).ClearSelection();
				AnimationTreeViewItem nextItem = null;
				foreach (var item in animationItems) {
					var parent = item.Parent;
					nextItem = GetNextItem(parent, parent.Items.IndexOf(item));
					if (item.Animation == Document.Current.Animation) {
						Document.Current.History.DoTransaction(() => NavigateToAnimation.Perform(nextItem.Animation));
					}
					if (item.SceneItem.TryGetAnimation(out var animation)) {
						DeleteAnimators(animation.Id, animation.OwnerNode);
					}
					UnlinkSceneItem.Perform(item.SceneItem);
					RebuildTreeView((TreeView)sender, provider);
				}
				// Not null because Legacy animation of current container is always present and cannot be removed.
				nextItem.Selected = true;
			});

			// Searches for the next animation item. Starts within the parent. Next one above parent.Items[index] is
			// selected, unless there are no such items, then the item below is selected. If, besides the provided
			// index, there are no more items in that node, selects last item in first non empty node above
			// (in the tree view) and if there are no non empty nodes above the parent, it will start to search for
			// the first animation item in first non empty node below the parent. A next item is guaranteed to
			// be found since there's always legacy animation in the current container which can't be removed.
			// This way next element selection behaves as there's no parent nodes and all animations from different
			// nodes are in the same list.
			AnimationTreeViewItem GetNextItem(TreeViewItem parent, int index)
			{
				index = index > 0 ? index - 1 : index + 1;
				if (parent.Items.Count > index && index >= 0) {
					return (AnimationTreeViewItem)parent.Items[index];
				}
				var items = parent.Parent.Items;
				index = items.IndexOf(parent);
				for (int i = index - 1; i >= 0; i--) {
					if (items[i].Items.Count > 0) {
						return (AnimationTreeViewItem)items[i].Items.Last();
					}
				}
				for (int i = index + 1; i < items.Count; i++) {
					if (items[i].Items.Count > 0) {
						return (AnimationTreeViewItem)items[i].Items.First();
					}
				}
				throw new InvalidOperationException();
			}

			void DeleteAnimators(string animationId, Node node)
			{
				foreach (var child in node.Nodes) {
					foreach (var animator in child.Animators.ToList()) {
						if (animator.AnimationId == animationId) {
							RemoveFromCollection<AnimatorCollection, IAnimator>.Perform(child.Animators, animator);
						}
					}
					if (child.ContentsPath != null) {
						continue;
					}
					if (child.Animations.Any(i => i.Id == animationId)) {
						continue;
					}
					DeleteAnimators(animationId, child);
				}
			}
		}

		private void TreeView_OnPaste(object sender, TreeView.PasteEventArgs args, TreeViewItemProvider provider)
		{
			var data = Clipboard.Text;
			if (!string.IsNullOrEmpty(data)) {
				Document.Current.History.DoTransaction(() => {
					var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(data));
					var container = TangerinePersistence.Instance.ReadObject<Frame>(null, stream);
					if (args.Parent is CommonTreeViewItem parent && parent.SceneItem.GetNode() != null) {
						RebuildTreeView((TreeView)sender, provider);
						((TreeView)sender).ClearSelection();
						var index = TranslateTreeViewToSceneTreeIndex(args.Parent, args.Index);
						foreach (var animation in container.Animations.ToList()) {
							if (animation.Id == CopySceneItemsToStream.AnimationTracksContainerAnimationId) {
								continue;
							}
							container.Animations.Remove(animation);
							var baseAnimationId = animation.Id;
							var counter = 1;
							while (
								parent.SceneItem.TryGetNode(out var node) &&
								node.Animations.Any(i => i.Id == animation.Id)
							) {
								animation.Id = baseAnimationId + " - Copy" + (counter == 1 ? "" : counter.ToString());
								counter++;
							}
							var animationSceneItem = LinkSceneItem.Perform(
								parent.SceneItem, index++, animation);
							provider.GetAnimationTreeViewItem(animationSceneItem).Selected = true;
						}
					}
				});
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

		private readonly List<TreeViewItem> nodeItems = new List<TreeViewItem>();
		private Node previousContainer;

		private void RebuildTreeView(TreeView treeView, TreeViewItemProvider provider)
		{
			if (treeView.RootItem != null) {
				DestroyTree(treeView.RootItem);
			}

			nodeItems.Clear();
			var filter = searchStringEditor.Text;
			var initialTreeViewHeight = scrollView.Content.Height;
			if (mode == TreeViewMode.CurrentBranch) {
				TraverseSceneTreeForCurrentBranch(Document.Current.GetSceneItemForObject(Document.Current.Container));
			} else {
				TraverseSceneTree(Document.Current.GetSceneItemForObject(Document.Current.RootNode));
			}

			treeView.RootItem = treeView.RootItem ?? new TreeViewItem();
			if (mode == TreeViewMode.AllHierarchy) {
				nodeItems.Sort((a, b) =>
					string.Compare(a.Label, b.Label, StringComparison.Ordinal));
			} else if (!CoreUserPreferences.Instance.AnimationPanelReversedOrder) {
				nodeItems.Reverse();
			}
			foreach (var item in nodeItems) {
				treeView.RootItem.Items.Add(item);
			}
			treeView.Refresh();
			AnimationTreeViewItemPresentation.UpdateWarnings(treeView, mode);

			var animated = previousContainer != Document.Current.Container;
			previousContainer = Document.Current.Container;
			// Animated (dis)appearance of list header.
			if (animated && mode == TreeViewMode.CurrentBranch && scrollView.LayoutManager != null) {
				scrollView.LayoutManager.Layout();
				var reversed = CoreUserPreferences.Instance.AnimationPanelReversedOrder;
				var treeViewHeightDelta = scrollView.Content.Height - initialTreeViewHeight;
				var savedScrollPosition = reversed ?
					scrollView.Behaviour.ScrollPosition : scrollView.Content.Height - scrollView.Height;
				scrollView.Behaviour.ScrollPosition += reversed ? treeViewHeightDelta : 0;
				scrollView.Behaviour.ScrollToItemVelocity = Math.Abs(treeViewHeightDelta * 5);
				scrollView.Behaviour.ScrollTo(savedScrollPosition);
			}

			void TraverseSceneTreeForCurrentBranch(Row sceneTree)
			{
				// Skip folders
				while (!sceneTree.TryGetNode(out _)) {
					sceneTree = sceneTree.Parent;
				}
				TraverseNodeChildren(sceneTree);
				if (sceneTree.Parent != null) {
					TraverseSceneTreeForCurrentBranch(sceneTree.Parent);
				}
			}

			void TraverseSceneTree(Row sceneTree)
			{
				if (
					sceneTree.TryGetNode(out var node) &&
					(node.NeedSerializeAnimations() || Document.Current?.Animation.OwnerNode == node)
				) {
					TraverseNodeChildren(sceneTree);
				}
				foreach (var child in sceneTree.Rows) {
					TraverseSceneTree(child);
				}
			}

			void TraverseNodeChildren(Row sceneTree)
			{
				var nodeItem = provider.GetNodeTreeViewItem(sceneTree);
				bool isCurrentNode = ((NodeTreeViewItem)nodeItem).Node == Document.Current.Container;
				var nodeSatisfyFilter = Filter(nodeItem.Label);
				bool isContainsEmptyLegacyAnimation = false;
				foreach (var animationSceneItem in sceneTree.Rows) {
					if (animationSceneItem.GetAnimation() == null) {
						// Do not use LINQ trying to reduce GC pressure
						continue;
					}
					var animationItem = provider.GetAnimationTreeViewItem(animationSceneItem);
					foreach (var markerSceneItem in animationSceneItem.Rows) {
						if (markerSceneItem.TryGetMarker(out var marker) && Filter(marker.Id)) {
							animationItem.Items.Add(provider.GetMarkerTreeViewItem(markerSceneItem));
						}
					}
					var animation = animationSceneItem.GetAnimation();
					bool hasItems = animationItem.Items.Count > 0;
					bool hasAnimators = animation.ValidatedEffectiveAnimators.Count > 0;
					if (!hasItems & !hasAnimators & animation.IsLegacy & !isCurrentNode) {
						isContainsEmptyLegacyAnimation = true;
					} else if (hasItems || nodeSatisfyFilter || Filter(animationItem.Label)) {
						nodeItem.Items.Add(animationItem);
					}
				}
				if (nodeItem.Items.Count > 0 || string.IsNullOrEmpty(filter) && isContainsEmptyLegacyAnimation) {
					((NodeTreeViewItem) nodeItem).RefreshLabel();
					nodeItems.Add(nodeItem);
				}
			}

			bool Filter(string text)
			{
				return filter.Length == 0 || text?.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0;
			}

			void DestroyTree(TreeViewItem tree)
			{
				foreach (var node in tree.Items) {
					DestroyTree(node);
				}
				tree.Items.Clear();
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

		public class TreeViewItemState
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

		private class CommonTreeViewItem : TreeViewItem
		{
			private readonly Func<bool> isSearchActiveGetter;
			private readonly TreeViewItemState itemState;

			public Row SceneItem { get; }

			protected CommonTreeViewItem(Row sceneItem, TreeViewItemState itemState, Func<bool> isSearchActiveGetter)
			{
				this.SceneItem = sceneItem;
				this.itemState = itemState;
				this.isSearchActiveGetter = isSearchActiveGetter;
			}

			public override bool CanExpand() => Items.Count > 0;

			public override bool Selected
			{
				get => itemState.Selected;
				set
				{
					if (Selected != value) {
						Document.Current.History.DoTransaction(() => {
							SetProperty.Perform(
								itemState,
								nameof(itemState.Selected), value, false);
						});
					}
				}
			}

			public override int SelectionOrder => itemState.SelectionOrder;

			public override bool Expanded
			{
				get
				{
					var s = itemState;
					return isSearchActiveGetter() ? s.ExpandedIfSearchActive : s.ExpandedIfSearchInactive;
				}

				set
				{
					if (Expanded != value) {
						Document.Current.History.DoTransaction(() => {
							DelegateOperation.Perform(null, Document.Current.BumpSceneTreeVersion, false);
							var propertyName = isSearchActiveGetter()
								? nameof(itemState.ExpandedIfSearchActive)
								: nameof(itemState.ExpandedIfSearchInactive);
							SetProperty.Perform(
								itemState, propertyName, value, false);
							DelegateOperation.Perform(Document.Current.BumpSceneTreeVersion, null, false);
						});
					}
				}
			}
		}

		private interface INodeTreeViewItem
		{
			Node Node { get; }
		}

		private class NodeTreeViewItem : CommonTreeViewItem, INodeTreeViewItem
		{
			public Node Node { get; }

			public NodeTreeViewItem(Row sceneItem, TreeViewItemState itemState, Node node, Func<bool> isSearchActiveGetter)
				: base(sceneItem, itemState, isSearchActiveGetter)
			{
				Node = node;
			}

			public override string Label
			{
				get => label;
				set { }
			}

			public string Tooltip { get; private set; }

			private static readonly StringBuilder stringBuilder = new StringBuilder(128);

			private string label;

			public void RefreshLabel()
			{
				stringBuilder.Clear();
				label = null;
				bool isRoot = Node == Document.Current.RootNode;
				if (!isRoot) {
					BuildTooltipForNode(Node);
				}
				label = string.IsNullOrEmpty(Node.Id) ? $"<{Node.GetType().Name}>" : Node.Id;
				Tooltip = stringBuilder.ToString();

				void BuildTooltipForNode(Node node)
				{
					if (node.Parent != null) {
						BuildTooltipForNode(node.Parent);
					}
					if (!string.IsNullOrEmpty(node.Tag)) {
						stringBuilder.Append(' ');
						stringBuilder.Append('(');
						stringBuilder.Append(node.Tag);
						stringBuilder.Append(')');
					}
					var contentsPath = Node.ContentsPath;
					if (contentsPath != null) {
						stringBuilder.Append(' ');
						stringBuilder.Append('(');
						stringBuilder.Append(contentsPath);
						stringBuilder.Append(')');
					}
					if (stringBuilder.Length > 0) {
						stringBuilder.Append('/');
					}
					if (string.IsNullOrEmpty(node.Id)) {
						stringBuilder.Append('<');
						stringBuilder.Append(node.GetType().Name);
						stringBuilder.Append('>');
					} else {
						stringBuilder.Append(node.Id);
					}
				}
			}

			public override bool CanRename() => false;

			public override ITexture Icon
			{
				get => NodeIconPool.GetTexture(Node);
				set => throw new NotSupportedException();
			}
		}

		private class AnimationTreeViewItem : CommonTreeViewItem
		{
			public Animation Animation { get; }

			public AnimationTreeViewItem(
				Row sceneItem, TreeViewItemState itemState, Animation animation, Func<bool> isSearchActiveGetter)
				: base(sceneItem, itemState, isSearchActiveGetter)
			{
				Animation = animation;
			}

			public override string Label
			{
				get => Animation.IsLegacy ? "Legacy" : Animation.Id;
				set
				{
					Document.Current.History.DoTransaction(() => {
						SetProperty.Perform(Animation, nameof(Animation.Id), value);
					});
				}
			}

			public override bool CanRename() => !Animation.IsLegacy && Animation.Id != Animation.ZeroPoseId;

			public override ITexture Icon
			{
				get => IconPool.GetTexture(
					Animation.Id == Animation.ZeroPoseId ? "AnimationsPanel.ZeroPose" :
					Animation.IsLegacy ? "AnimationsPanel.LegacyAnimation" :
					Animation.IsCompound ? "AnimationsPanel.CompoundAnimation" : "AnimationsPanel.Animation");
				set => throw new NotSupportedException();
			}
		}

		private class MarkerTreeViewItem : CommonTreeViewItem
		{
			public Marker Marker { get; }

			public MarkerTreeViewItem(Row sceneItem, TreeViewItemState itemState, Marker marker, Func<bool> isSearchActiveGetter)
				: base(sceneItem, itemState, isSearchActiveGetter)
			{
				Marker = marker;
			}

			public override string Label
			{
				get => Marker.Id;
				set
				{
					Document.Current.History.DoTransaction(() => {
						SetProperty.Perform(Marker, nameof(Marker.Id), value);
					});
				}
			}

			public override bool CanRename() => true;

			public override ITexture Icon
			{
				get => IconPool.GetTexture(
					Marker.Action == MarkerAction.Jump ? "Lookup.MarkerJumpAction" :
						(Marker.Action == MarkerAction.Play ? "Lookup.MarkerPlayAction" : "Lookup.MarkerStopAction"));
				set => throw new NotSupportedException();
			}
		}

		private class TreeViewPresentation : ITreeViewPresentation
		{
			public const float IndentWidth = 20;

			private readonly TreeViewItemPresentationOptions options;
			private readonly Func<TreeViewMode> treeViewModeGetter;
			private TreeViewItemProvider itemProvider;

			public IEnumerable<ITreeViewItemPresentationProcessor> Processors { get; }

			public TreeViewPresentation(
				TreeViewItemProvider itemProvider,
				TreeViewItemPresentationOptions options,
				Func<TreeViewMode> treeViewModeGetter
			) {
				this.treeViewModeGetter = treeViewModeGetter;
				this.itemProvider = itemProvider;
				this.options = options;
				Processors = new List<ITreeViewItemPresentationProcessor> {
					new CommonTreeViewItemPresentationProcessor(),
					new MarkerTreeViewItemPresentationProcessor(),
					new AnimationTreeViewItemPresentationProcessor(),
				};
			}

			public ITreeViewItemPresentation CreateItemPresentation(TreeView treeView, TreeViewItem item)
			{
				switch (item) {
					case AnimationTreeViewItem _:
						return new AnimationTreeViewItemPresentation(treeView, item, options, treeViewModeGetter);
					case MarkerTreeViewItem _:
						return new MarkerTreeViewItemPresentation(treeView, item, options);
					case NodeTreeViewItem _:
						return new NodeTreeViewItemPresentation(treeView, item, options, itemProvider);
					default:
						throw new InvalidOperationException();
				}
			}

			public void RenderDragCursor(Widget scrollWidget, TreeViewItem parent, int childIndex, bool dragInto)
			{
				if (dragInto) {
					var x = (CalcIndent(parent) + 1) * IndentWidth;
					var y = parent.Presentation.Widget.Top();
					Renderer.DrawRectOutline(x, y - 0.5f, scrollWidget.Width, y + TimelineMetrics.DefaultRowHeight + 0.5f, Theme.Colors.SeparatorDragColor, 2);
				} else {
					var x = (CalcIndent(parent) + 2) * IndentWidth;
					var y = CalcDragCursorY(parent, childIndex);
					Renderer.DrawRect(x, y - 0.5f, scrollWidget.Width, y + 1.5f, Theme.Colors.SeparatorDragColor);
				}
			}

			private static float CalcDragCursorY(TreeViewItem parent, int childIndex)
			{
				if (childIndex == 0) {
					if (parent.Parent == null) {
						return 0;
					}
					return parent.Presentation.Widget.Bottom();
				}
				var i = parent.Items[childIndex - 1];
				while (i.Expanded && i.Items.Count > 0) {
					i = i.Items.Last();
				}
				return i.Presentation.Widget.Bottom();
			}

			public static int CalcIndent(TreeViewItem item)
			{
				int indent = 0;
				for (var p = item; p.Parent != null; p = p.Parent) {
					indent++;
				}
				return indent - 1;
			}
		}

		private class CommonTreeViewItemPresentation
		{
			private readonly Widget ExpandButtonContainer;
			public readonly TreeViewItem Item;
			public readonly ToolbarButton ExpandButton;
			public readonly SimpleText Label;
			public readonly Widget IndentationSpacer;
			public readonly TreeView TreeView;
			public Widget Widget { get; }

			public CommonTreeViewItemPresentation(
				TreeView treeView, TreeViewItem item, TreeViewItemPresentationOptions options)
			{
				TreeView = treeView;
				Item = item;
				Widget = new Widget {
					MinMaxHeight = TimelineMetrics.DefaultRowHeight,
					Layout = new HBoxLayout {
						DefaultCell = new DefaultLayoutCell { VerticalAlignment = VAlignment.Center }
					},
					Padding = new Thickness { Right = 10 },	// Add padding for the scrollbar.
					Presenter = new SyncDelegatePresenter<Widget>(w => {
						w.PrepareRendererState();
						Renderer.DrawRect(
							Vector2.Zero, w.Size,
							Item.Selected ? Widget.Focused == w.Parent ? Theme.Colors.SelectedBackground :
							Theme.Colors.SelectedInactiveBackground : Theme.Colors.WhiteBackground
						);
						HighlightLabel(Widget, Label, options.SearchStringGetter?.Invoke());
					})
				};
				Label = CreateLabel(treeView, Item);
				ExpandButton = CreateExpandButton(Item);
				var icon = new Image(Item.Icon) {
					HitTestTarget = true,
					MinMaxSize = new Vector2(16),
				};
				IndentationSpacer = new Widget();
				Widget.Nodes.Add(IndentationSpacer);
				ExpandButtonContainer = new Widget {
					MinMaxSize = Theme.Metrics.DefaultToolbarButtonSize,
					Nodes = { ExpandButton }
				};
				Widget.Nodes.Add(ExpandButtonContainer);
				Widget.Nodes.Add(Spacer.HSpacer(3));
				Widget.Nodes.Add(icon);
				Widget.Nodes.Add(Spacer.HSpacer(3));
				Widget.Nodes.Add(Label);
			}

			protected void HighlightLabel(Widget widget, SimpleText label, string searchString)
			{
				if (string.IsNullOrEmpty(searchString)) {
					return;
				}
				int index;
				int previousIndex = 0;
				var pos = label.CalcPositionInSpaceOf(widget);
				while ((index = label.Text.IndexOf(searchString, previousIndex, StringComparison.OrdinalIgnoreCase)) >= 0) {
					var skipSize = label.Font.MeasureTextLine(
						label.Text, label.FontHeight, previousIndex, index - previousIndex, label.LetterSpacing);
					var searchStringSize = label.Font.MeasureTextLine(
						label.Text, label.FontHeight, index, searchString.Length, label.LetterSpacing);
					pos.X += skipSize.X;
					Renderer.DrawRect(pos.X, 0, pos.X + searchStringSize.X, widget.Height,
						ColorTheme.Current.Hierarchy.MatchColor);
					pos.X += searchStringSize.X;
					previousIndex = index + searchString.Length;
				}
			}

			private SimpleText CreateLabel(TreeView treeView, TreeViewItem item)
			{
				var label = new ThemedSimpleText {
					HitTestTarget = true,
					ForceUncutText = false, // We want ellipsed text if the panel is too narrow.
					VAlignment = VAlignment.Center,
					OverflowMode = TextOverflowMode.Ellipsis,
					LayoutCell = new LayoutCell(Alignment.LeftCenter, float.MaxValue),
				};
				label.Gestures.Add(new DoubleClickGesture(() => {
					var labelExtent = label.MeasureUncutText();
					if (item.CanRename() && label.LocalMousePosition().X < labelExtent.X) {
						Rename();
					} else {
						treeView.RaiseActivated(item, TreeView.ActivationMethod.Mouse);
					}
				}));
				return label;
			}

			public void Rename()
			{
				((WindowWidget) Label.GetRoot()).Window.Activate();
				Label.Visible = false;
				var idx = Label.Parent.Nodes.IndexOf(Label);
				var editBoxContainer = new Widget {
					Layout = new HBoxLayout(),
					LayoutCell = new LayoutCell(Alignment.LeftCenter, float.MaxValue)
				};
				Label.Parent.Nodes.Insert(idx, editBoxContainer);
				var editBox = new ThemedEditBox {
					LayoutCell = new LayoutCell(Alignment.Center),
					Text = Item.Label
				};
				editBoxContainer.AddNode(editBox);
				editBox.Tasks.Add(EditObjectIdTask(editBoxContainer, editBox, Label, Item));
			}

			private IEnumerator<object> EditObjectIdTask(Widget container, EditBox editBox, SimpleText label, TreeViewItem item)
			{
				// Skip one update since tabbedWidget gets the focus in the first place.
				yield return null;
				editBox.SetFocus();
				while (editBox.IsFocused()) {
					yield return null;
					if (!item.Selected) {
						editBox.RevokeFocus();
					}
				}
				if (item.Label != editBox.Text) {
					DoRename(item, editBox.Text);
				}
				container.Unlink();
				label.Visible = true;
			}

			protected virtual void DoRename(TreeViewItem item, string newLabel)
			{
				item.Label = newLabel;
			}

			private ToolbarButton CreateExpandButton(TreeViewItem item)
			{
				var button = new ToolbarButton { Highlightable = false };
				button.Clicked += () => {
					item.Expanded = !item.Expanded;
				};
				button.Components.Add(new DisableAncestralGesturesComponent());
				return button;
			}
		}

		private class AnimationTreeViewItemPresentation : CommonTreeViewItemPresentation, ITreeViewItemPresentation
		{
			private static readonly Warning higherPriorityWarning = new Warning {
				Icon = IconPool.GetTexture("Inspector.Warning"),
				Text = "This animation has a higher priority than the active one."
			};
			private static readonly Warning insufficientPriorityWarning = new Warning {
				Icon = IconPool.GetTexture("Universal.NoEdit"),
				Text = "This animation has insufficient priority for editing."
			};

			private readonly Func<TreeViewMode> treeViewModeGetter;
			private readonly Image warningWidget;

			private string warningText;

			public AnimationTreeViewItemPresentation(
				TreeView treeView, TreeViewItem item,
				TreeViewItemPresentationOptions options,
				Func<TreeViewMode> treeViewModeGetter)
				: base(treeView, item, options)
			{
				this.treeViewModeGetter = treeViewModeGetter;
				Widget.Gestures.Add(new ClickGesture(1, ShowContextMenu));
				warningWidget = new Image {
					MinMaxSize = Vector2.One * TimelineMetrics.DefaultRowHeight,
					Padding = new Thickness(1),
					HitTestTarget = true,
					Visible = false
				};
				warningWidget.Components.Add(new TooltipComponent(() => warningText));
				Widget.AddNode(warningWidget);
			}

			private void ShowContextMenu()
			{
				if (!Item.Selected) {
					TreeView.ClearSelection();
					Item.Selected = true;
				}
				var menu = new Menu { Command.Cut, Command.Copy, Command.Paste, Command.Delete };
				menu.Popup();
			}

			protected override void DoRename(TreeViewItem item, string newId)
			{
				var animation = ((AnimationTreeViewItem)item).Animation;
				string error = null;
				if (newId.IsNullOrWhiteSpace() || newId == Animation.ZeroPoseId) {
					error = "Invalid animation id";
				} else if (TangerineDefaultCharsetAttribute.IsValid(newId, out var message) != ValidationResult.Ok) {
					error = message;
				} else if (animation.Owner.Animations.Any(a => a.Id == newId)) {
					error = $"An animation '{newId}' already exists";
				}
				if (error != null) {
					AlertDialog.Show(error, "Ok");
					return;
				}
				Document.Current.History.DoTransaction(() => {
					var oldId = animation.Id;
					SetProperty.Perform(animation, nameof(animation.Id), newId);
					foreach (var a in animation.Owner.Animations) {
						foreach (var track in a.Tracks) {
							foreach (var animator in track.Animators) {
								if (animator.AnimationId == oldId) {
									SetProperty.Perform(animator, nameof(IAnimator.AnimationId), newId);
								}
							}
						}
					}
					ChangeAnimatorsAnimationId(animation.OwnerNode);

					void ChangeAnimatorsAnimationId(Node node)
					{
						foreach (var child in node.Nodes) {
							foreach (var animator in child.Animators) {
								if (animator.AnimationId == oldId) {
									SetProperty.Perform(animator, nameof(IAnimator.AnimationId), newId);
								}
							}
							if (child.ContentsPath != null) {
								continue;
							}
							if (child.Animations.Any(i => i.Id == oldId)) {
								continue;
							}
							ChangeAnimatorsAnimationId(child);
						}
					}
				});
				UpdateWarnings(TreeView, treeViewModeGetter());
			}

			public static void UpdateWarnings(TreeView treeView, TreeViewMode mode)
			{
				var activeAnimation = Document.Current.Animation;
				AnimationTreeViewItemPresentation topmostPresentation = null;
				AnimationTreeViewItemPresentation activePresentation = null;
				var nodeItems = treeView.RootItem.Items;
				foreach (var nodeItem in NodeItemsFromRootToLeaves(nodeItems)) {
					foreach (var item in nodeItem.Items) {
						if (item is AnimationTreeViewItem animationItem) {
							if (animationItem.Presentation == null) {
								continue;
							}
							var animation = animationItem.Animation;
							var presentation = (AnimationTreeViewItemPresentation)animationItem.Presentation;
							if (activeAnimation == null || activeAnimation.IsLegacy || mode == TreeViewMode.AllHierarchy) {
								ClearWarning(presentation);
							} else {
								if (animation == activeAnimation) {
									activePresentation = presentation;
									topmostPresentation = presentation;
								} else if (animation.Id == activeAnimation.Id) {
									topmostPresentation = presentation;
									SetWarning(presentation, insufficientPriorityWarning);
								} else {
									ClearWarning(presentation);
								}
							}
						}
					}
				}
				if (activePresentation != null) {
					if (activePresentation == topmostPresentation) {
						ClearWarning(activePresentation);
					} else {
						SetWarning(topmostPresentation, higherPriorityWarning);
						SetWarning(activePresentation, insufficientPriorityWarning);
					}
				}

				void ClearWarning(AnimationTreeViewItemPresentation presentation)
				{
					presentation.warningText = string.Empty;
					presentation.warningWidget.Visible = false;
				}

				void SetWarning(AnimationTreeViewItemPresentation presentation, Warning warning)
				{
					presentation.warningText = warning.Text;
					presentation.warningWidget.Texture = warning.Icon;
					presentation.warningWidget.Visible = true;
				}

				IEnumerable<TreeViewItem> NodeItemsFromRootToLeaves(TreeViewItemList nodeItems) =>
					nodeItems.Count > 0 ? ((NodeTreeViewItem)nodeItems[0]).Node == Document.Current.RootNode ?
						nodeItems : nodeItems.Reverse() : Enumerable.Empty<TreeViewItem>();
			}

			private struct Warning
			{
				public ITexture Icon;
				public string Text;
			}
		}

		private class NodeTreeViewItemPresentation : CommonTreeViewItemPresentation, ITreeViewItemPresentation
		{
			private readonly TreeViewItemProvider itemProvider;

			public NodeTreeViewItemPresentation(TreeView treeView, TreeViewItem item,
				TreeViewItemPresentationOptions options,
				TreeViewItemProvider itemProvider)
				: base(treeView, item, options)
			{
				this.itemProvider = itemProvider;
				Widget.Gestures.Add(new ClickGesture(1, ShowContextMenu));
				Label.Components.Add(new TooltipComponent(() => ((NodeTreeViewItem)Item).Tooltip));
			}

			private void ShowContextMenu()
			{
				TreeView.ClearSelection();
				Item.Selected = true;
				var menu = new Menu();
				var node = ((INodeTreeViewItem)Item).Node;
				menu.Add(new Command("Add", () => AddAnimation( false)));
				menu.Add(new Command("Add Compound", () => AddAnimation(true)));
				menu.Add(new Command("Add ZeroPose", AddZeroPoseAnimation) {
					Enabled = !node.Animations.TryFind(Animation.ZeroPoseId, out _)
				});
				menu.Popup();

				void AddAnimation(bool compound)
				{
					Document.Current.History.DoTransaction(() => {
						var animation = new Animation
							{ Id = GenerateAnimationId(node, "NewAnimation"), IsCompound = compound };
						var nodeSceneItem = Document.Current.GetSceneItemForObject(node);
						var animationSceneItem = LinkSceneItem.Perform(nodeSceneItem, 0, animation);
						TreeView.ClearSelection();
						itemProvider.GetNodeTreeViewItem(nodeSceneItem).Expanded = true;
						itemProvider.GetAnimationTreeViewItem(animationSceneItem).Selected = true;
						if (compound) {
							var track = new AnimationTrack { Id = "Track1" };
							var animationTrackSceneItem = LinkSceneItem.Perform(animationSceneItem, 0,
								track);
							SelectRow.Perform(animationTrackSceneItem);
						}
					});
				}

				void AddZeroPoseAnimation()
				{
					Document.Current.History.DoTransaction(() => {
						var animation = new Animation { Id = Animation.ZeroPoseId };
						var nodeSceneItem = Document.Current.GetSceneItemForObject(node);
						var animationSceneItem = LinkSceneItem.Perform(nodeSceneItem, 0, animation);
						TreeView.ClearSelection();
						itemProvider.GetNodeTreeViewItem(nodeSceneItem).Expanded = true;
						itemProvider.GetAnimationTreeViewItem(animationSceneItem).Selected = true;
						foreach (var a in node.Descendants.SelectMany(n => n.Animators).ToList()) {
							var (propertyData, animable, index) =
								AnimationUtils.GetPropertyByPath(a.Owner, a.TargetPropertyPath);
							var zeroPoseKey = Keyframe.CreateForType(propertyData.Info.PropertyType);
							zeroPoseKey.Value = index == -1
								? propertyData.Info.GetValue(animable)
								: propertyData.Info.GetValue(animable, new object[] { index });
							zeroPoseKey.Function = KeyFunction.Steep;
							SetKeyframe.Perform(a.Owner, a.TargetPropertyPath, animation, zeroPoseKey);
						}
					});
				}
			}

			private static string GenerateAnimationId(Node node, string prefix)
			{
				var animations = GetAnimations(node);
				for (int i = 1; ; i++) {
					var id = prefix + (i > 1 ? i.ToString() : "");
					if (animations.All(a => a.Id != id)) {
						return id;
					}
				}
			}

			private static List<Animation> GetAnimations(Node node)
			{
				var usedAnimations = new HashSet<string>();
				var animations = new List<Animation>();
				var ancestor = node;
				while (true) {
					foreach (var a in ancestor.Animations) {
						if (!a.IsLegacy && usedAnimations.Add(a.Id)) {
							animations.Add(a);
						}
					}
					if (ancestor == Document.Current.RootNode) {
						return animations;
					}
					ancestor = ancestor.Parent;
				}
			}
		}

		private class MarkerTreeViewItemPresentation : CommonTreeViewItemPresentation, ITreeViewItemPresentation
		{
			public MarkerTreeViewItemPresentation(TreeView treeView, TreeViewItem item, TreeViewItemPresentationOptions options)
				: base(treeView, item, options)
			{
			}
		}

		private class CommonTreeViewItemPresentationProcessor : ITreeViewItemPresentationProcessor
		{
			public void Process(ITreeViewItemPresentation presentation)
			{
				if (presentation is CommonTreeViewItemPresentation p) {
					p.ExpandButton.Texture = p.Item.Expanded
						? IconPool.GetTexture("Timeline.Expanded")
						: IconPool.GetTexture("Timeline.Collapsed");
					p.ExpandButton.Visible = p.Item.Items.Count > 0;
					p.IndentationSpacer.MinMaxWidth =
						TreeViewPresentation.CalcIndent(p.Item) * TreeViewPresentation.IndentWidth;
					p.Label.Text = p.Item.Label;
				}
			}
		}

		private class AnimationTreeViewItemPresentationProcessor : ITreeViewItemPresentationProcessor
		{
			public void Process(ITreeViewItemPresentation presentation)
			{
				if (presentation is AnimationTreeViewItemPresentation p && p.Item is AnimationTreeViewItem ai) {
					var isBold = p.Label.Font.Name == FontPool.DefaultBoldFontName;
					var isCurrentAnimation = ai.Animation == Document.Current.Animation;
					if (isCurrentAnimation && !isBold) {
						p.Label.Font = new SerializableFont(FontPool.DefaultBoldFontName);
					} else if (!isCurrentAnimation && isBold) {
						p.Label.Font = new SerializableFont(FontPool.DefaultFontName);
					}
				}
			}
		}

		private class MarkerTreeViewItemPresentationProcessor : ITreeViewItemPresentationProcessor
		{
			public void Process(ITreeViewItemPresentation presentation)
			{
				if (presentation is MarkerTreeViewItemPresentation p && p.Item is MarkerTreeViewItem mi) {
					var isTimelineCursorOnMarker = mi.Marker.Frame == Document.Current.AnimationFrame;
					var isBold = p.Label.Font.Name == FontPool.DefaultBoldFontName;
					var parent = (AnimationTreeViewItem)mi.Parent;
					var isCurrentAnimation = parent.Animation == Document.Current.Animation;
					if (String.IsNullOrWhiteSpace(mi.Label)) {
						p.Label.Text = mi.Marker.Action == MarkerAction.Stop ? "<Stop>" :
							(mi.Marker.Action == MarkerAction.Jump ? "<Jump>" : "<Start>");
					}
					if (isTimelineCursorOnMarker && !isBold && isCurrentAnimation) {
						p.Label.Font = new SerializableFont(FontPool.DefaultBoldFontName);
					} else if (isBold && (!isCurrentAnimation || !isTimelineCursorOnMarker)) {
						p.Label.Font = new SerializableFont(FontPool.DefaultFontName);
					}
				}
			}
		}
	}
}
