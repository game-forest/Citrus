using Lime;
using Tangerine.UI;

namespace Tangerine
{
	public class LookupDialog
	{
		private static Window window;
		private static WindowWidget windowWidget;
		private static LookupWidget lookupWidget;

		public static LookupSections Sections { get; private set; }

		public LookupDialog()
		{
			if (window == null) {
				CreateWindow();
			} else {
				window.Activate();
				window.Visible = true;
			}
			windowWidget.FocusScope.SetDefaultFocus();
			Sections.Push(Sections.Initial);
		}

		private static void CreateWindow()
		{
			lookupWidget = new LookupWidget {
				LayoutCell = new LayoutCell(Alignment.LeftCenter),
			};
			Sections = new LookupSections(lookupWidget);
			window = new Window(new WindowOptions {
				Title = "Go To Anything",
				Style = WindowStyle.Borderless,
			});
			windowWidget = new ThemedInvalidableWindowWidget(window) {
				Padding = new Thickness(8),
				Layout = new VBoxLayout(),
				Nodes = { lookupWidget }
			};
			windowWidget.FocusScope = new KeyboardFocusScope(windowWidget);

			windowWidget.LateTasks.AddLoop(() => {
				if (window.Visible && !window.Active) {
					lookupWidget.Cancel();
				}
			});
			lookupWidget.Submitted += () => {
				if (Sections.StackCount == 0) {
					window.Visible = false;
				}
			};
			lookupWidget.Canceled += () => {
				Sections.Drop();
				window.Visible = false;
			};
		}
	}
}
