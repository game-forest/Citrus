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
					Theme.Colors.SelectedBackground
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
				var index = (Content.LocalMousePosition().Y / rowHeight).Floor();
				if (index < items.Count) {
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
				}
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
	}
}
