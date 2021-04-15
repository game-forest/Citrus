using System;
using System.Collections.Generic;
using Lime;
using Tangerine.Core;

namespace Tangerine.UI.Timeline.Components
{
	public class GridKeyframesRenderer
	{
		private class Cell
		{
			public BitSet32 Strips;
			public int StripCount;
			public KeyFunction Func1;
			public KeyFunction Func2;

			public void Clear()
			{
				Strips = BitSet32.Empty;
				StripCount = 0;
			}
		}

		private static readonly Stack<Cell> cellPool = new Stack<Cell>();
		private readonly Dictionary<int, Cell> cells = new Dictionary<int, Cell>();

		public void ClearCells()
		{
			foreach (var cell in cells.Values) {
				cell.Clear();
				cellPool.Push(cell);
			}
			cells.Clear();
		}

		public void GenerateCells(Node node, Animation animation)
		{
			var effectiveAnimatorsSet = animation.ValidatedEffectiveAnimatorsSet;
			foreach (var animator in node.Animators) {
				if (!effectiveAnimatorsSet.Contains(animator)) {
					continue;
				}
				for (var j = 0; j < animator.ReadonlyKeys.Count; j++) {
					var key = animator.ReadonlyKeys[j];
					var colorIndex = PropertyAttributes<TangerineKeyframeColorAttribute>.Get(animator.Animable.GetType(), animator.TargetPropertyPath)?.ColorIndex ?? 0;
					if (!cells.TryGetValue(key.Frame, out var cell)) {
						cell = cellPool.Count == 0 ? new Cell() : cellPool.Pop();
						cells.Add(key.Frame, cell);
					}
					if (cell.StripCount == 0) {
						cell.Func1 = key.Function;
					} else if (cell.StripCount == 1) {
						var lastColorIndex = 0;
						for (int i = 0; i < cell.Strips.Count; i++) {
							if (cell.Strips[i]) {
								lastColorIndex = i;
								break;
							}
						}
						if (lastColorIndex < colorIndex) {
							cell.Func2 = key.Function;
						} else {
							var a = cell.Func1;
							cell.Func1 = key.Function;
							cell.Func2 = a;
						}
					}
					cell.Strips[colorIndex] = true;
					cell.StripCount++;
					cells[key.Frame] = cell;
				}
			}
		}

		public void RenderCells(Widget widget)
		{
			widget.PrepareRendererState();
			if (cells.Count > 0) {
				int animatedRange = 0;
				foreach (var frame in cells.Keys) {
					animatedRange = Math.Max(animatedRange, frame);
				}
				Renderer.DrawRect(
					0, 0,
					(animatedRange + 1) * TimelineMetrics.ColWidth, widget.Height,
					ColorTheme.Current.TimelineGrid.AnimatedRangeBackground);
			}
			foreach (var kv in cells) {
				int column = kv.Key;
				var cell = kv.Value;
				var a = new Vector2(column * TimelineMetrics.ColWidth + 1, 0);
				var stripHeight = widget.Height / cell.StripCount;
				if (cell.StripCount <= 2) {
					int color1 = -1;
					int color2 = -1;
					for (int j = 0; j < cell.Strips.Count; j++) {
						if (cell.Strips[j]) {
							if (color1 == -1) {
								color1 = j;
							} else {
								color2 = j;
							}
						}
					}
					if (color2 == -1) {
						color2 = color1;
					}
					var b = a + new Vector2(TimelineMetrics.ColWidth - 1, stripHeight);
					KeyframeFigure.Render(a, b, KeyframePalette.Colors[color1], cell.Func1);
					if (cell.StripCount == 2) {
						a.Y += stripHeight;
						b.Y += stripHeight;
						KeyframeFigure.Render(a, b, KeyframePalette.Colors[color2], cell.Func2);
					}
				} else {
					// Draw strips
					var drawnStripCount = 0;
					for (var colorIndex = 0; colorIndex < cell.Strips.Count; colorIndex++) {
						if (cell.Strips[colorIndex]) {
							var b = a + new Vector2(TimelineMetrics.ColWidth - 1, stripHeight);
							Renderer.DrawRect(a, b, KeyframePalette.Colors[colorIndex]);
							drawnStripCount++;
							if (drawnStripCount == cell.StripCount) break;
							a.Y += stripHeight;
						}
					}
					// Strips of the same color
					if (drawnStripCount < cell.StripCount) {
						int colorIndex;
						for (colorIndex = 0; colorIndex < cell.Strips.Count; colorIndex++) {
							if (cell.Strips[colorIndex]) break;
						}
						for (var j = drawnStripCount; j != cell.StripCount; j++) {
							var b = a + new Vector2(TimelineMetrics.ColWidth - 1, stripHeight);
							Renderer.DrawRect(a, b, KeyframePalette.Colors[colorIndex]);
							a.Y += stripHeight;
						}
					}
				}
			}
		}
	}
}
