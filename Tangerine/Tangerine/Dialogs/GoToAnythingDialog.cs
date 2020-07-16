using Lime;
using Tangerine.Core;
using Tangerine.UI;

namespace Tangerine
{
	public class GoToAnythingDialog
	{
		private static Window window;
		private static WindowWidget windowWidget;
		private static Lookup lookup;

		public GoToAnythingDialog()
		{
			if (window == null) {
				CreateWindow();
			} else {
				window.Activate();
				window.Visible = true;
			}
			windowWidget.FocusScope.SetDefaultFocus();
			FillItems();
		}

		private static void CreateWindow()
		{
			lookup = new Lookup {
				Widget = { LayoutCell = new LayoutCell(Alignment.LeftCenter) }
			};
			window = new Window(new WindowOptions {
				Title = "Go To Anything",
				Style = WindowStyle.Borderless,
			});
			windowWidget = new ThemedInvalidableWindowWidget(window) {
				Padding = new Thickness(8),
				Layout = new VBoxLayout(),
				Nodes = { lookup.Widget }
			};
			windowWidget.FocusScope = new KeyboardFocusScope(windowWidget);

			void HideWindow() => window.Visible = false;

			windowWidget.LateTasks.AddLoop(() => {
				if (window.Visible && !window.Active) {
					lookup.Cancel();
				}
			});
			lookup.Submitted += HideWindow;
			lookup.Canceled += HideWindow;
		}

		private static void FillItems()
		{
			if (Document.Current == null) {
				return;
			}
			foreach (var m in Document.Current.Animation.Markers) {
				var mClosed = m;
				var text = m.Id;
				text = $"{m.Action} Marker '{text}' at Frame: {m.Frame} in {Document.Current.Animation.Owner.Owner}";
				lookup.AddItem(
					text,
					() => {
						Document.SetCurrentFrameToNode(Document.Current.Animation, mClosed.Frame, true);
						UI.Timeline.Operations.CenterTimelineOnCurrentColumn.Perform();
					}
				);
			}
		}
	}
}
