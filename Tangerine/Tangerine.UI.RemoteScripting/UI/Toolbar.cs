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
	}
}
