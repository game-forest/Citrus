using System.Collections;
using System.Collections.Generic;
using Lime;
using Tangerine.Core;

namespace Tangerine.UI.RemoteScripting
{
	public class ExplorableScrollView : ThemedScrollView, IEnumerable<ExplorableItem>
	{
		private readonly float rowHeight = Theme.Metrics.DefaultEditBoxSize.Y;
		private readonly List<ExplorableItem> items = new List<ExplorableItem>();
		private int selectedIndex = -1;

		public Widget ExploringWidget { get; set; }
		public ExplorableItem SelectedItem
		{
			get
			{
				return selectedIndex >= 0 && selectedIndex < items.Count ? items[selectedIndex] : null;
			}
		}

		public ExplorableScrollView()
		{
			var mouseDownGesture = new ClickGesture(0);
			mouseDownGesture.Began += SelectItemBasedOnMousePosition;
			Gestures.Add(mouseDownGesture);
			Content.CompoundPresenter.Insert(0, new SyncDelegatePresenter<Widget>(w => {
				w.PrepareRendererState();
				Renderer.DrawRect(
					x0: 1,
					y0: rowHeight * selectedIndex + 1,
					x1: w.Width - 1,
					y1: rowHeight * (selectedIndex + 1),
					color: Theme.Colors.SelectedBackground
				);
			}));
			Updating += delta => {
				foreach (var item in items) {
					item.OnUpdate(delta);
				}
			};

			void SelectItemBasedOnMousePosition()
			{
				SetFocus();
				if (TryGetItemIndexUnderMouse(out var index)) {
					SelectItem(index);
				}
			}
		}

		public void AddItem(ExplorableItem item)
		{
			Image icon;
			ThemedSimpleText simpleText;
			var widget = new Widget {
				MinMaxHeight = rowHeight,
				Padding = new Thickness(left: 4, right: 10, top: 3),
				Layout = new HBoxLayout { Spacing = 4 },
				Nodes = {
					(icon = new Image {
						Padding = new Thickness(top: 1),
						MinMaxSize = new Vector2(16),
					}),
					(simpleText = new ThemedSimpleText(item.Name) {
						Id = "Label",
						LayoutCell = new LayoutCell { VerticalAlignment = VAlignment.Center },
						ForceUncutText = false,
					}),
				},
			};
			Content.Nodes.Add(widget);
			items.Add(item);

			item.NameUpdated += () => simpleText.Text = item.Name;
			item.IconUpdated += SetupIcon;
			SetupIcon();

			if (items.Count == 1) {
				SelectItem(0);
			}

			void SetupIcon()
			{
				icon.Visible = item.IconTexture != null || item.IconPresenter != null;
				icon.Texture = item.IconTexture;
				icon.Presenter = item.IconPresenter ?? DefaultPresenter.Instance;
			}
		}

		public void RemoveItem(ExplorableItem item)
		{
			var index = items.IndexOf(item);
			var selectedItem = SelectedItem;
			if (index >= 0) {
				ExploringWidget.Nodes.Clear();
				Content.Nodes.RemoveAt(index);
				items.RemoveAt(index);
				SelectItem(items.IndexOf(selectedItem));
				Window.Current.Invalidate();
			}
		}

		public void SelectItem(ExplorableItem item)
		{
			var index = items.IndexOf(item);
			if (index >= 0) {
				SelectItem(index);
			}
		}

		private void SelectItem(int index)
		{
			ExploringWidget.Nodes.Clear();
			if (items.Count > 0) {
				index = index.Clamp(0, items.Count - 1);
				EnsureRowVisible(index);
				selectedIndex = index;
				ExploringWidget.PushNode(SelectedItem.Content);
			} else {
				selectedIndex = -1;
			}
			Window.Current.Invalidate();

			void EnsureRowVisible(int row)
			{
				while ((row + 1) * rowHeight > ScrollPosition + Height) {
					ScrollPosition++;
				}
				while (row * rowHeight < ScrollPosition) {
					ScrollPosition--;
				}
			}
		}

		public bool TryGetItemUnderMouse(out ExplorableItem item)
		{
			var found = TryGetItemIndexUnderMouse(out var index);
			item = found ? items[index] : null;
			return found;
		}

		private bool TryGetItemIndexUnderMouse(out int index)
		{
			index = (Content.LocalMousePosition().Y / rowHeight).Floor();
			return index < items.Count;
		}

		public List<ExplorableItem>.Enumerator GetEnumerator() => items.GetEnumerator();

		IEnumerator<ExplorableItem> IEnumerable<ExplorableItem>.GetEnumerator() => items.GetEnumerator();

		IEnumerator IEnumerable.GetEnumerator() => items.GetEnumerator();
	}
}
