using System;
using System.Collections.Generic;
using Lime;
using Tangerine.Core;

namespace Tangerine.UI.Timeline
{
	public class CurveEditorPanProcessor : Core.ITaskProvider
	{
		private readonly Timeline timeline;

		public CurveEditorPanProcessor(Timeline timeline) { this.timeline = timeline; }

		public IEnumerator<object> Task()
		{
			var curveEditor = timeline.CurveEditor;
			var input = curveEditor.MainAreaWidget.Input;
			while (true) {
				if (
					input.IsMousePressed(2)
					|| (input.IsMousePressed(0) && CommonWindow.Current.Input.IsKeyPressed(Key.Space))
				) {
					var prevPosition = input.MousePosition;
					while (input.IsMousePressed(0) || input.IsMousePressed(2)) {
						var delta = input.MousePosition - prevPosition;
						if (delta.X != 0 && timeline.OffsetX - delta.X > 0) {
							timeline.OffsetX -= delta.X;
							Core.Operations.Dummy.Perform(Document.Current.History);
						}
						if (delta.Y != 0) {
							var d = curveEditor.CoordToValue(0) - curveEditor.CoordToValue(delta.Y);
							curveEditor.MinValue += d;
							curveEditor.MaxValue += d;
							Window.Current.Invalidate();
						}
						prevPosition = input.MousePosition;
						yield return null;
					}
				}
				yield return null;
			}
		}
	}
}
