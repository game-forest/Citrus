using System;
using System.Collections.Generic;
using System.Linq;
using Lime;
using Tangerine.UI;
using Tangerine.UI.Docking;

namespace Tangerine
{
	public class LookupDialog
	{
		private LookupDialog(out LookupSections sections)
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
					}),
				},
			};
			windowWidget.FocusScope = new KeyboardFocusScope(windowWidget);
			windowWidget.LateTasks.AddLoop(() => {
				if (window.Visible && !window.Active) {
					lookupWidget.Cancel();
				}
			});
			var sectionsCopy = sections = new LookupSections(lookupWidget);
			void CloseWindow()
			{
				window.Close();
				lookupWidget.UnlinkAndDispose();
			}

			void LookupSubmitted()
			{
				if (sectionsCopy.StackCount == 0) {
					CloseWindow();
				}
			}

			void LookupCanceled()
			{
				sectionsCopy.Drop();
				CloseWindow();
			}

			lookupWidget.Submitted += LookupSubmitted;
			lookupWidget.Canceled += LookupCanceled;
			windowWidget.FocusScope.SetDefaultFocus();
		}

		public LookupDialog(LookupSections.SectionType? sectionType = null) : this(out var sections)
		{
			sections.Initialize(sectionType);
			sections.LockNavigationOnLastSection();
		}

		public LookupDialog(Func<LookupSections, IEnumerable<LookupSection>> getSections) : this(out var sections)
		{
			var startSections = getSections(sections);
			if (!startSections.Any()) {
				sections.Initialize();
				return;
			}
			sections.Initialize(startSections.First());
			foreach (var section in startSections.Skip(1)) {
				sections.Push(section);
			}
			sections.LockNavigationOnLastSection();
		}
	}
}
