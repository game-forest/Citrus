using System.Collections.Generic;
using Lime;
using Tangerine.Core;

namespace Tangerine.UI.RemoteScripting
{
	public class ExplorableScrollView : ThemedScrollView
	{
		private readonly float rowHeight = Theme.Metrics.DefaultEditBoxSize.Y;
		private readonly List<ExplorableItem> items = new List<ExplorableItem>();
		private int selectedIndex = -1;

		public Widget ExploringWidget { get; set; }
		public ExplorableItem SelectedItem => selectedIndex >= 0 ? items[selectedIndex] : null;

		public ExplorableScrollView()
		{
			var mouseDownGesture = new ClickGesture(0);
			mouseDownGesture.Began += SelectItemBasedOnMousePosition;
			Gestures.Add(mouseDownGesture);
			Content.CompoundPresenter.Insert(0, new SyncDelegatePresenter<Widget>(w => {
				w.PrepareRendererState();
				Renderer.DrawRect(
					1, rowHeight * selectedIndex + 1,
					w.Width - 1, rowHeight * (selectedIndex + 1),
					IsFocused() ? Theme.Colors.SelectedBackground : Theme.Colors.SelectedInactiveBackground
				);
			}));

			void SelectItemBasedOnMousePosition()
			{
				SetFocus();
				var index = (Content.LocalMousePosition().Y / rowHeight).Floor();
				if (index < items.Count) {
					SelectItem(index);
				}
			}
		}

		public void AddItem(ExplorableItem item)
		{
			ThemedSimpleText simpleText;
			var widget = new Widget {
				MinMaxHeight = rowHeight,
				Padding = new Thickness(4, 10, 3, 0),
				Layout = new HBoxLayout(),
				Nodes = {
					(simpleText = new ThemedSimpleText(item.Name) {
						Id = "Label",
						LayoutCell = new LayoutCell { VerticalAlignment = VAlignment.Center },
						ForceUncutText = false,
					}),
				}
			};
			Content.Nodes.Add(widget);
			item.NameUpdated += name => simpleText.Text = name;
			items.Add(item);

			if (items.Count == 1) {
				SelectItem(0);
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
	}
}
