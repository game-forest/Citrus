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
		public int Index { get; internal set; }
		public TreeViewItem Parent { get; internal set; }
		public TreeViewItemList Items { get; }

		public void Unlink() => Parent.Items.Remove(this);

		public TreeViewItem()
		{
			Items = new TreeViewItemList(this);
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
		}

		public void RemoveAt(int index)
		{
			var item = list[index];
			list.RemoveAt(index);
			item.Parent = null;
		}

		public TreeViewItem this[int index]
		{
			get => list[index];
			set => throw new NotSupportedException();
		}
	}

	public interface ITreeViewPresentation
	{
		ITreeViewItemPresentation CreateItemPresentation(TreeView treeView, TreeViewItem item);
		IEnumerable<ITreeViewItemPresentationProcessor> Processors { get; }
		void RenderDragCursor(Widget scrollWidget, TreeViewItem parent, int childIndex, bool dragInto);
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

		/// <summary>
		/// Specifies whether a TreeViewItem will be activated immediately after clicking on it.
		/// Otherwise it will be activated on double click or pressing enter key.
		/// </summary>
		public bool ActivateOnSelect { get; set; }
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

		public enum ActivationMethod
		{
			Mouse,
			Keyboard
		}

		public class ActivateItemEventArgs : EventArgs
		{
			public TreeViewItem Item;
			public ActivationMethod Method;
		}

		private static class Commands
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
		private List<TreeViewItem> currentItems;
		private List<TreeViewItem> previousItems;
		private TreeViewItem rootItem;
		private TreeViewItem rangeSelectionFirstItem;
		private readonly TreeViewOptions options;

		public event EventHandler<ActivateItemEventArgs> OnActivateItem;
		public event EventHandler<DragEventArgs> OnDragBegin;
		public event EventHandler<DragEventArgs> OnDragEnd;
		public event EventHandler<CopyEventArgs> OnCopy;
		public event EventHandler<CopyEventArgs> OnCut;
		public event EventHandler<CopyEventArgs> OnDelete;
		public event EventHandler<PasteEventArgs> OnPaste;

		public float ScrollSpeed = 300;

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
			scrollView.Behaviour.ScrollToItemVelocity = ScrollSpeed;
			this.scrollView = scrollView;
			this.presentation = presentation;
			currentItems = new List<TreeViewItem>();
			previousItems = new List<TreeViewItem>();
			var scrollContent = scrollView.Content;
			scrollContent.HitTestTarget = true;
			scrollContent.FocusScope = new KeyboardFocusScope(scrollContent);
			if (!options.ActivateOnSelect) {
				scrollContent.Gestures.Add(new DoubleClickGesture(() => {
					var item = GetItemUnderMouse();
					if (item != null) {
						SelectItem(item, activateIfNeeded: false);
						RaiseActivated(item, ActivationMethod.Mouse);
					}
				}));
			}
			var dg = new DragGesture(0, DragDirection.Vertical);
			var dragCursorPresenter = new SyncDelegatePresenter<Widget>(w => {
				w.PrepareRendererState();
				if (TryCalcDragDestination(out var parent, out var childIndex, out var dragInto)) {
					presentation.RenderDragCursor(w, parent, childIndex, dragInto);
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
					SelectItem(itemUnderMouse, !itemUnderMouse.Selected, false, activateIfNeeded: false);
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
				if (TryCalcDragDestination(out var parent, out var childIndex, out _)) {
					if (!parent.Expanded) {
						parent.Expanded = true;
					}
					OnDragEnd?.Invoke(this,
						new DragEventArgs {
							Items = currentItems.Where(i => i.Selected),
							Parent = parent,
							Index = childIndex
						});
				}
			};
			scrollContent.Gestures.Add(dg);
			scrollContent.Layout = new VBoxLayout();
			if (options.HandleCommands) {
				scrollContent.Tasks.Add(HandleCommands);
			}
		}

		private TreeViewItem GetRecentlySelected()
		{
			TreeViewItem result = null;
			int maxOrder = 0;
			foreach (var i in currentItems) {
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
		public void RaiseActivated(TreeViewItem item, ActivationMethod method) => OnActivateItem?.Invoke(
			this, new ActivateItemEventArgs { Item = item, Method = method });

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

		private bool TryCalcDragDestination(out TreeViewItem parent, out int childIndex, out bool dragInto)
		{
			var p = scrollView.Content.LocalMousePosition().Y;
			parent = null;
			childIndex = 0;
			dragInto = false;
			var selectedItems = currentItems.Where(i => i.Selected);
			foreach (var item in currentItems) {
				var t = (p - item.Presentation.Widget.Y) / item.Presentation.Widget.Height;
				if (t > 0.25f && t < 0.75f) {
					var a = new DragEventArgs { Items = selectedItems, Parent = item };
					OnDragBegin?.Invoke(this, a);
					if (!a.CancelDrag) {
						parent = item;
						childIndex = item.Items.Count;
						dragInto = true;
						return true;
					}
				}
				// Special case: drag into root item on the last position.
				if (item == currentItems.Last() && t > 0.5f) {
					parent = RootItem;
					childIndex = RootItem.Items.Count;
					var a = new DragEventArgs { Items = selectedItems, Parent = parent };
					OnDragBegin?.Invoke(this, a);
					if (!a.CancelDrag) {
						return true;
					}
				}
				if (
					item == currentItems.First() && t < 0.5f ||
					t >= 0 && t < 1
				) {
					if (t > 0.5f) {
						if (item.Expanded && item.Items.Count > 0) {
							parent = item;
							childIndex = 0;
						} else {
							parent = item.Parent;
							childIndex = parent.Items.IndexOf(item) + 1;
						}
					} else {
						parent = item.Parent;
						childIndex = parent.Items.IndexOf(item);
					}
					if (parent == null) {
						return false;
					}
					var a = new DragEventArgs { Items = selectedItems, Parent = parent };
					OnDragBegin?.Invoke(this, a);
					if (!a.CancelDrag) {
						return true;
					}
				}
			}
			return false;
		}

		public void ActivateItem()
		{
			var focused = GetRecentlySelected();
			if (focused != null) {
				if (!focused.Selected) {
					SelectItem(focused, activateIfNeeded: false);
				}
				RaiseActivated(focused, ActivationMethod.Keyboard);
			}
		}

		public void SelectItem(TreeViewItem item,
			bool select = true, bool clearSelection = true, bool activateIfNeeded = true)
		{
			if (clearSelection) {
				ClearSelection();
			}
			rangeSelectionFirstItem = null;
			item.Selected = select;
			if (options.ActivateOnSelect && activateIfNeeded) {
				ActivateItem();
			}
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
			if (rangeSelectionFirstItem == null || !currentItems.Contains(rangeSelectionFirstItem)) {
				rangeSelectionFirstItem = GetRecentlySelected() ?? item;
			}
			foreach (var i in currentItems.Where(i => i.Selected)) {
				i.Selected = false;
			}
			if (item.Index > rangeSelectionFirstItem.Index) {
				for (int i = rangeSelectionFirstItem.Index; i <= item.Index; i++) {
					currentItems[i].Selected = true;
				}
			} else {
				for (int i = rangeSelectionFirstItem.Index; i >= item.Index; i--) {
					currentItems[i].Selected = true;
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
				} else if (focused.Index < currentItems.Count - 1) {
					SelectItem(currentItems[focused.Index + 1]);
				}
			}
		}

		public void SelectRangeNextItem()
		{
			var focused = GetRecentlySelected();
			if (focused != null) {
				if (!focused.Selected) {
					SelectItem(focused);
				} else if (focused.Index < currentItems.Count - 1) {
					SelectRange(currentItems[focused.Index + 1]);
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
					SelectItem(currentItems[focused.Index - 1]);
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
					SelectRange(currentItems[focused.Index - 1]);
				}
			}
		}

		public void ToggleItem()
		{
			var focused = GetRecentlySelected();
			if (focused != null) {
				if (!focused.Selected) {
					SelectItem(focused, activateIfNeeded: false);
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
				} else if (focused.Index < currentItems.Count - 1) {
					SelectItem(currentItems[focused.Index + 1]);
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
				if (!scrollView.Content.IsFocused()) {
					yield return null;
					continue;
				}
				if (Command.Copy.Consume()) {
					OnCopy?.Invoke(this, new CopyEventArgs { Items = currentItems.Where(i => i.Selected) });
				}
				if (Command.Cut.Consume()) {
					OnCut?.Invoke(this, new CopyEventArgs { Items = currentItems.Where(i => i.Selected) });
				}
				if (Command.Delete.Consume()) {
					OnDelete?.Invoke(this, new CopyEventArgs { Items = currentItems.Where(i => i.Selected) });
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
				if (!options.ActivateOnSelect && Commands.Activate.Consume()) {
					ActivateItem();
				}
				if (Commands.SelectNext.Consume()) {
					SelectNextItem();
				}
				if (Commands.SelectRangeNext.Consume()) {
					SelectRangeNextItem();
				}
				if (Commands.SelectPrevious.Consume()) {
					SelectPreviousItem();
				}
				if (Commands.SelectRangePrevious.Consume()) {
					SelectRangePreviousItem();
				}
				if (Commands.ExpandOrSelectNext.Consume()) {
					ExpandOrSelectNextItem();
				}
				if (Commands.CollapseOrSelectParent.Consume()) {
					CollapseOrSelectParentItem();
				}
				if (Commands.Toggle.Consume()) {
					ToggleItem();
				}
				if (Commands.Rename.Consume()) {
					RenameItem();
				}
				yield return null;
			}
		}

		private void Invalidate()
		{
			((WindowWidget)scrollView.Manager?.RootNodes[0])?.Window?.Invalidate();
		}

		private TreeViewItem GetItemUnderMouse()
		{
			var p = scrollView.Content.LocalMousePosition().Y;
			int i = 0;
			foreach (var n in scrollView.Content.Nodes) {
				var w = (Widget)n;
				if (w.Y < p && p <= w.Y + w.Height) {
					return currentItems[i];
				}
				i++;
			}
			return null;
		}

		public void ScrollToItem(TreeViewItem item, bool instantly)
		{
			var itemTop = item.Presentation.Widget.Y;
			var itemBottom = item.Presentation.Widget.Y + item.Presentation.Widget.Height;
			if (itemTop - scrollView.ScrollPosition < 0) {
				scrollView.Behaviour.ScrollTo(itemTop, instantly);
			} else if (itemBottom - scrollView.ScrollPosition > scrollView.Height) {
				scrollView.Behaviour.ScrollTo(itemBottom - scrollView.Height, instantly);
			}
		}

		private readonly List<int> indexToItem = new List<int>();
		private readonly List<int> itemToIndex = new List<int>();

		public void Refresh()
		{
			(currentItems, previousItems) = (previousItems, currentItems);
			currentItems.Clear();
			foreach (var i in previousItems) {
				i.Index = -1;
			}
			int index = 0;
			if (RootItem != null) {
				BuildRecursively(RootItem);
			}
			// Remove widgets which are not in the list anymore.
			for (var i = previousItems.Count - 1; i >= 0; i--) {
				if (previousItems[i].Index == -1) {
					var j = previousItems.Count - 1;
					// Swap current element with the last one.
					if (i < j) {
						scrollView.Content.Nodes.Swap(i, j);
						(previousItems[i], previousItems[j]) = (previousItems[j], previousItems[i]);
					}
					// Remove the last element.
					scrollView.Content.Nodes.RemoveAt(j);
					previousItems.RemoveAt(j);
				}
			}
			// Add new widgets at the end of the list.
			foreach (var i in currentItems) {
				if (i.Presentation.Widget.Parent == null) {
					scrollView.Content.Nodes.Add(i.Presentation.Widget);
					previousItems.Add(i);
				}
			}
			// Reorder widgets.
			indexToItem.Clear();
			itemToIndex.Clear();
			for (int i = 0; i < previousItems.Count; i++) {
				indexToItem.Add(i);
				itemToIndex.Add(i);
			}
			for (int i = 0; i < indexToItem.Count; i++) {
				var t = itemToIndex[i];
				var d = previousItems[i].Index;
				if (d != t) {
					scrollView.Content.Nodes.Swap(d, t);
					var a = indexToItem[t];
					var b = indexToItem[d];
					(indexToItem[d], indexToItem[t]) = (a, b);
					(itemToIndex[a], itemToIndex[b]) = (itemToIndex[b], itemToIndex[a]);
				}
			}
			// Update presentation.
			foreach (var p in presentation.Processors) {
				foreach (var i in currentItems) {
					p.Process(i.Presentation);
				}
			}

			void BuildRecursively(TreeViewItem item)
			{
				var skipRoot = !options.ShowRoot && item == RootItem;
				if (!skipRoot) {
					currentItems.Add(item);
					item.Index = index++;
					item.Presentation = item.Presentation ?? presentation.CreateItemPresentation(this, item);
				}
				if (skipRoot || item.Expanded) {
					foreach (var i in item.Items) {
						BuildRecursively(i);
					}
				}
			}
		}
	}
}
