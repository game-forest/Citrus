using Lime;
using Tangerine.UI;
using Tangerine.UI.Docking;

namespace Tangerine
{
	public class LookupDialog
	{
		public LookupDialog(LookupSections.SectionType? sectionType = null)
		{
			Vector2? displayCenter = null;
			try {
				var display = CommonWindow.Current.Display;
				displayCenter = display.Position + display.Size / 2;
			} catch (System.ObjectDisposedException) {
				// Suppress
			}
			var window = new Window(new WindowOptions {
				Title = "Go To Anything",
				Style = WindowStyle.Borderless,
			});
			if (!displayCenter.HasValue) {
				var display = DockManager.Instance.MainWindowWidget.Window.Display;
				displayCenter = display.Position + display.Size / 2;
			}
			window.DecoratedPosition = displayCenter.Value - window.DecoratedSize / 2f;

			LookupWidget lookupWidget;
			var windowWidget = new ThemedInvalidableWindowWidget(window) {
				Padding = new Thickness(8),
				Layout = new VBoxLayout(),
				Nodes = {
					(lookupWidget = new LookupWidget {
						LayoutCell = new LayoutCell(Alignment.LeftCenter),
					})
				},
			};
			windowWidget.FocusScope = new KeyboardFocusScope(windowWidget);
			windowWidget.LateTasks.AddLoop(() => {
				if (window.Visible && !window.Active) {
					lookupWidget.Cancel();
				}
			});
			var sections = new LookupSections(lookupWidget);

			void CloseWindow()
			{
				window.Close();
				lookupWidget.UnlinkAndDispose();
			}

			void LookupSubmitted()
			{
				if (sections.StackCount == 0) {
					CloseWindow();
				}
			}

			void LookupCanceled()
			{
				sections.Drop();
				CloseWindow();
			}

			lookupWidget.Submitted += LookupSubmitted;
			lookupWidget.Canceled += LookupCanceled;
			windowWidget.FocusScope.SetDefaultFocus();
			sections.Initialize(sectionType);
		}
	}
}
