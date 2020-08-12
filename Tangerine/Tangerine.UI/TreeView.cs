using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Lime;
using Tangerine.Core;

namespace Tangerine.UI
{
	public class TreeViewItem
	{
		private static int selectionCounter = 1;
		private TreeView treeView;

		public virtual string Label { get; set; }

		public virtual bool Selected
		{
			get => SelectionOrder > 0;
			set
			{
				if (Selected != value) {
					SelectionOrder = value ? selectionCounter++ : 0;
				}
			}
		}

		public virtual int SelectionOrder { get; private set; }
		public virtual bool Expanded { get; set; }
		public virtual ITexture Icon { get; set; }
		public virtual bool CanExpand() => true;
		public virtual bool CanRename() => false;
		public ITreeViewItemPresentation Presentation { get; internal set; }
		internal int Index { get; set; }

		public TreeView TreeView
		{
			get => treeView;
			internal set => PropagateTreeView(value);
		}

		public TreeViewItem Parent { get; internal set; }
		public TreeViewItemList Items { get; }

		public void Unlink() => Parent.Items.Remove(this);

		public TreeViewItem()
		{
			Items = new TreeViewItemList(this);
		}

		private void PropagateTreeView(TreeView treeView)
		{
			if (this.treeView != treeView) {
				this.treeView = treeView;
				foreach (var i in Items) {
					i.PropagateTreeView(treeView);
				}
			}
		}
	}

	public class TreeViewItemList : IList<TreeViewItem>
	{
		private readonly TreeViewItem parent;
		private readonly List<TreeViewItem> list = new List<TreeViewItem>();
		IEnumerator<TreeViewItem> IEnumerable<TreeViewItem>.GetEnumerator() => list.GetEnumerator();

		IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

		public List<TreeViewItem>.Enumerator GetEnumerator() => list.GetEnumerator();

		public TreeViewItemList(TreeViewItem parent) => this.parent = parent;

		public void Add(TreeViewItem item) => Insert(Count, item);

		public void Clear()
		{
			while (Count > 0) {
				RemoveAt(Count - 1);
			}
		}

		public bool Contains(TreeViewItem item) => list.Contains(item);

		public void CopyTo(TreeViewItem[] array, int arrayIndex) => list.CopyTo(array, arrayIndex);

		public bool Remove(TreeViewItem item)
		{
			var i = IndexOf(item);
			if (i < 0) {
				return false;
			}
			RemoveAt(i);
			return true;
		}

		public int Count => list.Count;
		public bool IsReadOnly => false;

		public int IndexOf(TreeViewItem item) => list.IndexOf(item);

		public void Insert(int index, TreeViewItem item)
		{
			if (item.Parent != null) {
				throw new InvalidOperationException();
			}
			list.Insert(index, item);
			item.Parent = parent;
			item.TreeView = parent.TreeView;
			parent.TreeView?.ScheduleRefresh();
		}

		public void RemoveAt(int index)
		{
			var item = list[index];
			list.RemoveAt(index);
			item.Parent = null;
			item.TreeView = null;
			parent.TreeView?.ScheduleRefresh();
		}

		public TreeViewItem this[int index]
		{
			get => list[index];
			set => throw new NotSupportedException();
		}
	}

	public interface ITreeViewPresentation
	{
		ITreeViewItemPresentation CreateItemPresentation(TreeViewItem item);
		IEnumerable<ITreeViewItemPresentationProcessor> Processors { get; }
		void RenderDragCursor(Widget scrollWidget, TreeViewItem parent, int childIndex);
	}

	public interface ITreeViewItemPresentation
	{
		Widget Widget { get; }
		void Rename();
	}

	public interface ITreeViewItemPresentationProcessor
	{
		void Process(ITreeViewItemPresentation presentation);
	}

	public class TreeViewOptions
	{
		public bool HandleCommands { get; set; } = true;
		public bool ShowRoot { get; set; } = true;
	}

	public class TreeView
	{
		public class DragEventArgs : EventArgs
		{
			public IEnumerable<TreeViewItem> Items;
			public TreeViewItem Parent;
			public int Index;
			public bool CancelDrag;
		}

		public class CopyEventArgs : EventArgs
		{
			public IEnumerable<TreeViewItem> Items;
		}

		public class PasteEventArgs : EventArgs
		{
			public TreeViewItem Parent;
			public int Index;
		}

		public class ActivateItemEventArgs : EventArgs
		{
			public TreeViewItem Item;
		}

		private static class Cmds
		{
			public static readonly ICommand Activate = new Command(Key.Enter);
			public static readonly ICommand SelectPrevious = new Command(Key.Up);
			public static readonly ICommand SelectNext = new Command(Key.Down);
			public static readonly ICommand SelectRangePrevious = new Command(Modifiers.Shift, Key.Up);
			public static readonly ICommand SelectRangeNext = new Command(Modifiers.Shift, Key.Down);
			public static readonly ICommand Toggle = new Command(Key.Space);
			public static readonly ICommand ExpandOrSelectNext = new Command(Key.Right);
			public static readonly ICommand CollapseOrSelectParent = new Command(Key.Left);
			public static readonly ICommand Rename = new Command(Key.F2);
		}

		private readonly ITreeViewPresentation presentation;
		private readonly ThemedScrollView scrollView;
		private readonly List<TreeViewItem> items;
		private TreeViewItem rootItem;
		private TreeViewItem rangeSelectionFirstItem;
		private bool refreshScheduled;
		private TreeViewOptions options;

		public event EventHandler<ActivateItemEventArgs> OnItemActivate;
		public event EventHandler<DragEventArgs> OnDragBegin;
		public event EventHandler<DragEventArgs> OnDragEnd;
		public event EventHandler<CopyEventArgs> OnCopy;
		public event EventHandler<CopyEventArgs> OnCut;
		public event EventHandler<CopyEventArgs> OnDelete;
		public event EventHandler<PasteEventArgs> OnPaste;

		public float ScrollVelocity = 300;

		public TreeViewItem RootItem
		{
			get => rootItem;
			set
			{
				if (rootItem?.Parent != null) {
					rootItem.Unlink();
					rootItem = null;
				}
				rootItem = value;
				if (rootItem != null) {
					rootItem.TreeView = this;
				}
				ScheduleRefresh();
			}
		}

		public TreeView(
			ThemedScrollView scrollView,
			ITreeViewPresentation presentation,
			TreeViewOptions options)
		{
			this.options = options;
#if MAC
			var toggleSelectionModificator = Key.Win;
#else // WIN
			var toggleSelectionModificator = Key.Control;
#endif
			scrollView.Behaviour.ScrollToItemVelocity = ScrollVelocity;
			this.scrollView = scrollView;
			this.presentation = presentation;
			items = new List<TreeViewItem>();
			var scrollContent = scrollView.Content;
			scrollContent.HitTestTarget = true;
			scrollContent.FocusScope = new KeyboardFocusScope(scrollContent);
			scrollContent.Gestures.Add(new DoubleClickGesture(() => {
				var item = GetItemUnderMouse();
				if (item != null) {
					SelectItem(item);
					RaiseActivated(item);
				}
			}));
			var dg = new DragGesture(0, DragDirection.Vertical);
			var dragCursorPresenter = new SyncDelegatePresenter<Widget>(w => {
				w.PrepareRendererState();
				if (TryCalcDragDestination(out var parent, out var childIndex)) {
					presentation.RenderDragCursor(w, parent, childIndex);
				}
			});
			var itemSelectedAtDragBegin = false;
			var dragRecognized = false;
			dg.Began += () => {
				dragRecognized = false;
				itemSelectedAtDragBegin = false;
				var itemUnderMouse = GetItemUnderMouse();
				if (itemUnderMouse == null) {
					return;
				}
				if (scrollContent.Input.IsKeyPressed(toggleSelectionModificator)) {
					itemSelectedAtDragBegin = true;
					SelectItem(itemUnderMouse, !itemUnderMouse.Selected, false);
				} else if (scrollContent.Input.IsKeyPressed(Key.Shift)) {
					itemSelectedAtDragBegin = true;
					SelectRange(itemUnderMouse);
				} else if (!itemUnderMouse.Selected) {
					itemSelectedAtDragBegin = true;
					SelectItem(itemUnderMouse);
				}
			};
			dg.Recognized += () => {
				dragRecognized = true;
				scrollContent.CompoundPostPresenter.Add(dragCursorPresenter);
				scrollContent.Tasks.Add(ScrollOnDragTask(dg));
			};
			dg.Ended += () => {
				if (!dragRecognized) {
					// Drag wasn't recognized -- treat it as a click.
					var itemUnderMouse = GetItemUnderMouse();
					if (itemUnderMouse != null && !itemSelectedAtDragBegin) {
						SelectItem(itemUnderMouse);
					}
					return;
				}
				scrollContent.CompoundPostPresenter.Remove(dragCursorPresenter);
				if (TryCalcDragDestination(out var parent, out var childIndex)) {
					if (!parent.Expanded) {
						parent.Expanded = true;
					}
					OnDragEnd?.Invoke(this,
						new DragEventArgs {
							Items = items.Where(i => i.Selected),
							Parent = parent,
							Index = childIndex
						});
				}
			};
			scrollContent.Gestures.Add(dg);
			scrollContent.Layout = new VBoxLayout();
			scrollContent.Tasks.Add(SyncTask());
			if (options.HandleCommands) {
				scrollContent.Tasks.Add(HandleCommands);
			}
		}

		private TreeViewItem GetRecentlySelected()
		{
			TreeViewItem result = null;
			int maxOrder = 0;
			foreach (var i in items) {
				if (i.SelectionOrder > maxOrder) {
					maxOrder = i.SelectionOrder;
					result = i;
				}
			}
			return result;
		}

		/// <summary>
		/// Invokes OnItemActivated. Maybe used by ITreeViewItemPresentation.
		/// </summary>
		public void RaiseActivated(TreeViewItem item) => OnItemActivate?.Invoke(
			this, new ActivateItemEventArgs { Item = item });

		private IEnumerator<object> ScrollOnDragTask(DragGesture dg)
		{
			TreeViewItem previousItem = null;
			while (dg.IsChanging()) {
				var item = GetItemUnderMouse();
				if (item != previousItem && item != null) {
					ScrollToItem(item, instantly: false);
				}
				previousItem = item;
				yield return null;
			}
		}

		private bool TryCalcDragDestination(out TreeViewItem parent, out int childIndex)
		{
			var p = scrollView.Content.LocalMousePosition().Y;
			parent = rootItem;
			childIndex = 0;
			int index = 0;
			foreach (var item in items) {
				var t = (p - item.Presentation.Widget.Y) / item.Presentation.Widget.Height;
				if (t > 0.5f && t < 1) {
					var a = new DragEventArgs { Items = items.Where(i => i.Selected), Parent = item };
					OnDragBegin?.Invoke(this, a);
					if (!a.CancelDrag) {
						parent = item;
						childIndex = 0;
						return true;
					}
				}
				if (
					index == 0 && t < 0.5f ||
					index == items.Count - 1 && t > 0.5f ||
					t >= 0 && t < 1
				) {
					parent = item.Parent;
					if (parent == null) {
						return false;
					}
					var a = new DragEventArgs { Items = items.Where(i => i.Selected), Parent = parent };
					OnDragBegin?.Invoke(this, a);
					if (!a.CancelDrag) {
						childIndex = parent.Items.IndexOf(item);
						if (t > 0.5f) {
							childIndex++;
						}
						return true;
					}
				}
				index++;
			}
			return false;
		}

		public void SelectItem(TreeViewItem item, bool select = true, bool clearSelection = true)
		{
			if (clearSelection) {
				ClearSelection();
			}
			rangeSelectionFirstItem = null;
			item.Selected = select;
			ScrollToItem(item, instantly: true);
			Invalidate();
		}

		public void ClearSelection()
		{
			ClearSelectionHelper(RootItem);

			void ClearSelectionHelper(TreeViewItem tree)
			{
				if (tree.Selected) {
					tree.Selected = false;
				}
				foreach (var i in tree.Items) {
					ClearSelectionHelper(i);
				}
			}
		}

		private void SelectRange(TreeViewItem item)
		{
			if (rangeSelectionFirstItem == null || !items.Contains(rangeSelectionFirstItem)) {
				rangeSelectionFirstItem = GetRecentlySelected();
			}
			foreach (var i in items.Where(i => i.Selected)) {
				i.Selected = false;
			}
			if (item.Index > rangeSelectionFirstItem.Index) {
				for (int i = rangeSelectionFirstItem.Index; i <= item.Index; i++) {
					items[i].Selected = true;
				}
			} else {
				for (int i = rangeSelectionFirstItem.Index; i >= item.Index; i--) {
					items[i].Selected = true;
				}
			}
			ScrollToItem(item, instantly: true);
			Invalidate();
		}

		public void SelectNextItem()
		{
			var focused = GetRecentlySelected();
			if (focused != null) {
				if (!focused.Selected) {
					SelectItem(focused);
				} else if (focused.Index < items.Count - 1) {
					SelectItem(items[focused.Index + 1]);
				}
			}
		}

		public void SelectRangeNextItem()
		{
			var focused = GetRecentlySelected();
			if (focused != null) {
				if (!focused.Selected) {
					SelectItem(focused);
				} else if (focused.Index < items.Count - 1) {
					SelectRange(items[focused.Index + 1]);
				}
			}
		}

		public void SelectPreviousItem()
		{
			var focused = GetRecentlySelected();
			if (focused != null) {
				if (!focused.Selected) {
					SelectItem(focused);
				} else if (focused.Index > 0) {
					SelectItem(items[focused.Index - 1]);
				}
			}
		}

		public void SelectRangePreviousItem()
		{
			var focused = GetRecentlySelected();
			if (focused != null) {
				if (!focused.Selected) {
					SelectRange(focused);
				} else if (focused.Index > 0) {
					SelectRange(items[focused.Index - 1]);
				}
			}
		}

		public void ToggleItem()
		{
			var focused = GetRecentlySelected();
			if (focused != null) {
				if (!focused.Selected) {
					SelectItem(focused);
				}
				focused.Expanded = !focused.Expanded;
			}
		}

		public void ExpandOrSelectNextItem()
		{
			var focused = GetRecentlySelected();
			if (focused != null) {
				if (!focused.Expanded) {
					ToggleItem();
				} else if (focused.Index < items.Count - 1) {
					SelectItem(items[focused.Index + 1]);
				}
			}
		}

		public void CollapseOrSelectParentItem()
		{
			var focused = GetRecentlySelected();
			if (focused != null) {
				if (focused.Expanded) {
					ToggleItem();
				} else if (focused.Parent != RootItem) {
					SelectItem(focused.Parent);
				}
			}
		}

		public void RenameItem()
		{
			var focused = GetRecentlySelected();
			if (focused != null && focused.CanRename()) {
				focused.Presentation.Rename();
			}
		}

		private IEnumerator<object> HandleCommands()
		{
			while (true) {
				if (!Widget.Focused?.SameOrDescendantOf(scrollView) ?? true) {
					yield return null;
					continue;
				}
				if (Command.Copy.Consume()) {
					OnCopy?.Invoke(this, new CopyEventArgs { Items = items.Where(i => i.Selected) });
				}
				if (Command.Cut.Consume()) {
					OnCut?.Invoke(this, new CopyEventArgs { Items = items.Where(i => i.Selected) });
				}
				if (Command.Delete.Consume()) {
					OnDelete?.Invoke(this, new CopyEventArgs { Items = items.Where(i => i.Selected) });
				}
				if (Command.Paste.Consume()) {
					var focused = GetRecentlySelected();
					if (focused?.Parent != null) {
						OnPaste?.Invoke(this, new PasteEventArgs {
							Parent = focused.Parent,
							Index = focused.Parent.Items.IndexOf(focused)
						});
					}
				}
				if (Cmds.SelectNext.Consume()) {
					SelectNextItem();
				}
				if (Cmds.SelectRangeNext.Consume()) {
					SelectRangeNextItem();
				}
				if (Cmds.SelectPrevious.Consume()) {
					SelectPreviousItem();
 				}
				if (Cmds.SelectRangePrevious.Consume()) {
					SelectRangePreviousItem();
				}
				if (Cmds.ExpandOrSelectNext.Consume()) {
					ExpandOrSelectNextItem();
				}
				if (Cmds.CollapseOrSelectParent.Consume()) {
					CollapseOrSelectParentItem();
				}
				if (Cmds.Toggle.Consume()) {
					ToggleItem();
				}
				if (Cmds.Rename.Consume()) {
					RenameItem();
				}
				yield return null;
			}
		}

		private void Invalidate()
		{
			((WindowWidget)scrollView.Manager.RootNodes[0]).Window.Invalidate();
		}

		private TreeViewItem GetItemUnderMouse()
		{
			var p = scrollView.Content.LocalMousePosition().Y;
			int i = 0;
			foreach (var n in scrollView.Content.Nodes) {
				var w = (Widget)n;
				if (w.Y < p && p <= w.Y + w.Height) {
					return items[i];
				}
				i++;
			}
			return null;
		}

		private IEnumerator<object> SyncTask()
		{
			while (true) {
				if (refreshScheduled) {
					refreshScheduled = false;
					RefreshPresentation();
				}
				yield return null;
			}
		}

		private void ScrollToItem(TreeViewItem item, bool instantly)
		{
			var itemTop = item.Presentation.Widget.Y;
			var itemBottom = item.Presentation.Widget.Y + item.Presentation.Widget.Height;
			if (itemTop - scrollView.ScrollPosition < 0) {
				scrollView.Behaviour.ScrollTo(itemTop, instantly);
			} else if (itemBottom - scrollView.ScrollPosition > scrollView.Height) {
				scrollView.Behaviour.ScrollTo(itemBottom - scrollView.Height, instantly);
			}
		}

		public void RefreshPresentation()
		{
			items.Clear();
			scrollView.Content.Nodes.Clear();
			int index = 0;
			if (RootItem != null) {
				BuildRecursively(RootItem);
			}
			foreach (var p in presentation.Processors) {
				foreach (var i in items) {
					p.Process(i.Presentation);
				}
			}

			void BuildRecursively(TreeViewItem item)
			{
				var skipRoot = !options.ShowRoot && item == RootItem;
				if (!skipRoot) {
					items.Add(item);
					item.Index = index++;
					item.Presentation = item.Presentation ?? presentation.CreateItemPresentation(item);
					scrollView.Content.AddNode(item.Presentation.Widget);
				}
				if (skipRoot || item.Expanded) {
					foreach (var i in item.Items) {
						BuildRecursively(i);
					}
				}
			}
		}

		public void ScheduleRefresh() => refreshScheduled = true;
	}
}
