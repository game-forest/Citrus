using System;
using System.Collections.Generic;
using Lime;
using Tangerine.Core;
using Tangerine.UI.Timeline.Operations;

namespace Tangerine.UI.Timeline
{
	public class OverviewScrollProcessor : Core.ITaskProvider
	{
		private Timeline timeline => Timeline.Instance;

		public IEnumerator<object> Task()
		{
			var input = timeline.Overview.RootWidget.Input;
			while (true) {
				if (input.WasMouseReleased(1) || input.WasKeyPressed(Key.Mouse0DoubleClick)) {
					var pos = timeline.Overview.ContentWidget.LocalMousePosition();
					var offset = pos - timeline.Grid.Size / 2;
					timeline.ClampAndSetOffset(offset);
					Document.Current.History.DoTransaction(() => {
						var col = (input.MousePosition.X / (TimelineMetrics.ColWidth * timeline.Overview.ContentWidget.Scale.X)).Round();
						SetCurrentColumn.Perform(col);
					});
				}
				if (input.WasMousePressed()) {
					var originalMousePosition = input.MousePosition;
					var scrollPos = timeline.Offset;
					while (input.IsMousePressed()) {
						var mouseDelta = input.MousePosition - originalMousePosition;
						var scrollDelta = Vector2.Round(mouseDelta / timeline.Overview.ContentWidget.Scale);
						timeline.ClampAndSetOffset(scrollPos + scrollDelta);
						yield return null;
					}
				}
				yield return null;
			}
		}
	}
}

