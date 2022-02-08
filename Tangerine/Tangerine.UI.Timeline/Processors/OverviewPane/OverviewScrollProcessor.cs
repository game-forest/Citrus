using System;
using System.Collections.Generic;
using Lime;
using Tangerine.Core;
using Tangerine.UI.Timeline.Operations;

namespace Tangerine.UI.Timeline
{
	public class OverviewScrollProcessor : Core.ITaskProvider
	{
		private Timeline Timeline => Timeline.Instance;

		public IEnumerator<object> Task()
		{
			var input = Timeline.Overview.RootWidget.Input;
			while (true) {
				if (input.WasMouseReleased(1) || input.WasKeyPressed(Key.Mouse0DoubleClick)) {
					var pos = Timeline.Overview.ContentWidget.LocalMousePosition();
					var offset = pos - Timeline.Grid.Size / 2;
					Timeline.ClampAndSetOffset(offset);
					Document.Current.History.DoTransaction(() => {
						var col = (
							input.MousePosition.X
							/ (TimelineMetrics.ColWidth * Timeline.Overview.ContentWidget.Scale.X)
						).Round();
						SetCurrentColumn.Perform(col);
					});
				}
				if (input.WasMousePressed()) {
					var originalMousePosition = input.MousePosition;
					var scrollPos = Timeline.Offset;
					while (input.IsMousePressed()) {
						var mouseDelta = input.MousePosition - originalMousePosition;
						var scrollDelta = Vector2.Round(mouseDelta / Timeline.Overview.ContentWidget.Scale);
						Timeline.ClampAndSetOffset(scrollPos + scrollDelta);
						yield return null;
					}
				}
				yield return null;
			}
		}
	}
}
