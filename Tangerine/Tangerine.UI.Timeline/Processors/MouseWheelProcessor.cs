using System;
using System.Collections.Generic;
using Lime;
using Tangerine.Core;

namespace Tangerine.UI.Timeline
{
	public class MouseWheelProcessor : Core.ITaskProvider
	{
		private readonly Timeline timeline;

		public MouseWheelProcessor(Timeline timeline) { this.timeline = timeline; }

		public IEnumerator<object> Task()
		{
			var gridHandler = HandleScroll(timeline.Grid.RootWidget.Input);
			var overviewHandler = HandleScroll(timeline.Overview.RootWidget.Input);
			while (true) {
				gridHandler.MoveNext();
				overviewHandler.MoveNext();
				HandleCellsMagnification(timeline.Grid.RootWidget.Input);
				HandleCellsMagnification(timeline.Overview.RootWidget.Input);
				HandleCellsMagnification(timeline.CurveEditor.MainAreaWidget.Input);
				yield return null;
			}
		}

		private IEnumerator<object> HandleScroll(WidgetInput input)
		{
			var prevPosition = Vector2.Zero;
			while (true) {
				var scrollDelta = GetWheelDelta(input);
				if (scrollDelta != 0 && !input.IsKeyPressed(Key.Alt)) {
					timeline.ClampAndSetOffset(
						timeline.Offset + new Vector2(0, 1) * scrollDelta * TimelineMetrics.DefaultRowHeight);
				}
				if (input.IsKeyPressed(Key.Mouse2)) {
					var delta = input.MousePosition - prevPosition;
					if (delta != Vector2.Zero && (timeline.Offset.X - delta.X > 0 || timeline.Offset.Y - delta.Y > 0)) {
						timeline.ClampAndSetOffset(timeline.Offset - delta);
						Core.Operations.Dummy.Perform(Document.Current.History);
					}
				}
				prevPosition = input.MousePosition;
				yield return null;
			}
		}

		private void HandleCellsMagnification(WidgetInput input)
		{
			var delta = GetWheelDelta(input);
			if (delta != 0 && input.IsKeyPressed(Key.Alt)) {
				var prevColWidth = TimelineMetrics.ColWidth;
				TimelineMetrics.ColWidth = (TimelineMetrics.ColWidth + delta).Clamp(5, 30);
				if (prevColWidth != TimelineMetrics.ColWidth) {
					timeline.ClampAndSetOffset(new Vector2(
						timeline.OffsetX + timeline.CurrentColumn * delta,
						timeline.OffsetY));
					Core.Operations.Dummy.Perform(Document.Current.History);
				}
			}
		}

		private int GetWheelDelta(WidgetInput input)
		{
			if (input.WasKeyPressed(Key.MouseWheelDown)) {
				return 1;
			}
			if (input.WasKeyPressed(Key.MouseWheelUp)) {
				return -1;
			}
			return 0;
		}
	}
}
