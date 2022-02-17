using System;
using System.Linq;
using System.Collections.Generic;
using Lime;
using Tangerine.Core;

namespace Tangerine.UI.Timeline
{
	internal class GridMouseScrollProcessor : Core.ITaskProvider
	{
		private Timeline Timeline => Timeline.Instance;

		public IEnumerator<object> Task()
		{
			var widget = Timeline.Grid.RootWidget;
			var userPreferences = CoreUserPreferences.Instance;
			while (true) {
				if (widget.Input.IsMousePressed()) {
					yield return null;
					var cw = TimelineMetrics.ColWidth;
					var p = widget.LocalMousePosition();
					var offset = Timeline.Offset;
					if (p.X > widget.Width - cw / 2) {
						offset.X += cw;
					} else if (p.X < cw / 2) {
						offset.X = Math.Max(0, offset.X - cw);
					}
					if (!userPreferences.LockTimelineCursor) {
						Core.Document.Current.History.DoTransaction(() => {
							Operations.SetCurrentColumn.Perform(Timeline.Ruler.CalcColumnUnderMouse());
						});
					}
					var rh = TimelineMetrics.DefaultRowHeight;
					if (p.Y > widget.Height - rh / 2) {
						offset.Y += rh;
					} else if (p.Y < rh / 2) {
						offset.Y -= rh;
					}
					Timeline.ClampAndSetOffset(offset);
					Window.Current.Invalidate();
				}
				yield return null;
			}
		}
	}
}
