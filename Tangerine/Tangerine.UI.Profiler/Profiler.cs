using System;
using Lime;
using Tangerine.UI.Docking;

namespace Tangerine.UI
{
	public class Profiler
	{
		public static Profiler Instance { get; private set; }

		private readonly Panel panel;
		public readonly Widget RootWidget;

		public Profiler(Panel panel)
		{
			if (Instance != null) {
				throw new InvalidOperationException();
			}
			Instance = this;
			this.panel = panel;
			RootWidget = new Widget {
				Layout = new VBoxLayout {
					Spacing = 6
				}
			};
			panel.ContentWidget.AddNode(RootWidget);
			RootWidget.AddNode(new OverdrawController());
		}
	}
}
