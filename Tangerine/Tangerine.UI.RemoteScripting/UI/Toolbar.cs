using System;
using Lime;

namespace Tangerine.UI.RemoteScripting
{
	public class Toolbar : Widget
	{
		public Widget Content { get; }

		public Toolbar()
		{
			Padding = new Thickness(4);
			MinMaxHeight = Metrics.ToolbarHeight;
			MinWidth = 50;
			Presenter = new WidgetFlatFillPresenter(ColorTheme.Current.Toolbar.Background);
			Layout = new HBoxLayout {
				Spacing = 2,
				DefaultCell = new DefaultLayoutCell(Alignment.Center)
			};
			Nodes.AddRange(
				new Widget {
					Layout = new VBoxLayout(),
					Nodes = {
						(Content = new Widget {
							Layout = new HBoxLayout { Spacing = 2 }
						})
					}
				}
			);
		}

		public ToolbarButton AddButton(string title, Action action)
		{
			var button = new ToolbarButton(title) {
				Clicked = action
			};
			Content.Nodes.Add(button);
			return button;
		}

		public ToolbarButton AddMenuButton(string title, Menu menu, Action menuShowing = null)
		{
			var button = new ToolbarButton(title);
			button.Clicked += () => {
				Application.InvokeOnNextUpdate(() => {
					menuShowing?.Invoke();
					var aabb = button.CalcAABBInWindowSpace();
					var position = new Vector2(aabb.AX, aabb.BY);
					menu.Popup(Window.Current, position, 0, null);
				});
			};
			Content.Nodes.Add(button);
			return button;
		}

		public void AddSeparator(float leftOffset = 0, float rightOffset = 0)
		{
			Content.Nodes.Add(new Widget {
				MinMaxWidth = 1 + leftOffset + rightOffset,
				MinMaxHeight = Metrics.ToolbarHeight - Padding.Top - Padding.Bottom,
				Padding = new Thickness(leftOffset, rightOffset, top: 3),
				Presenter = new WidgetFlatFillPresenter(ColorTheme.Current.Toolbar.Separator),
			});
		}
	}
}
