using System;
using System.Collections.Generic;
using Lime;
using Tangerine.Core;
using Tangerine.Core.Components;
using Tangerine.UI.Timeline.Components;

namespace Tangerine.UI.Timeline
{
	public class SelectAndDragKeyframesProcessor : ITaskProvider
	{
		private static Timeline Timeline => Timeline.Instance;
		private static GridPane Grid => Timeline.Instance.Grid;

		private IntRectangle selectionRectangle;
		private IntVector2 lastSelectedCell = IntVector2.Zero;

		public IEnumerator<object> Task()
		{
			var input = Grid.RootWidget.Input;
			Node lastSelectionContainer = null;
			while (true) {
				if (input.WasMousePressed() && !Document.Current.Animation.IsCompound) {
					using (Document.Current.History.BeginTransaction()) {
						if (Grid.IsMouseOverRow()) {
							var initialCell = Grid.CellUnderMouse();
							if (IsCellSelected(initialCell)) {
								yield return DragSelectionTask(initialCell);
							} else {
								var r = new HasKeyframeRequest(initialCell);
								Timeline.Globals.Add(r);
								yield return null;
								Timeline.Globals.Remove<HasKeyframeRequest>();
								var isInMultiselectMode = input.IsKeyPressed(Key.Control);
								var isSelectRangeMode = input.IsKeyPressed(Key.Shift);
								var isSelectingKeyframes = input.IsKeyPressed(Key.Alt);

								if (isSelectRangeMode && lastSelectionContainer == Document.Current.Container) {
									SelectRange(lastSelectedCell, initialCell, isSelectingKeyframes);
									yield return null;
								} else if (!r.Result || isInMultiselectMode) {
									yield return SelectTask(initialCell, isSelectingKeyframes);
									lastSelectedCell = initialCell;
									lastSelectionContainer = Document.Current.Container;
								} else {
									yield return DragSingleKeyframeTask(initialCell);
									lastSelectedCell = initialCell;
									lastSelectionContainer = Document.Current.Container;
								}
							}
						}
						Document.Current.History.CommitTransaction();
					}
				}
				yield return null;
			}
		}

		private static void SelectRange(IntVector2 a, IntVector2 b, bool selectKeyframes)
		{
			Operations.ClearGridSelection.Perform();
			Core.Operations.ClearSceneItemSelection.Perform();
			var r = new IntRectangle {
				A = {
					X = Math.Min(a.X, b.X),
					Y = Math.Min(a.Y, b.Y)
				},
				B = {
					X = Math.Max(a.X, b.X),
					Y = Math.Max(a.Y, b.Y)
				}
			};
			if (selectKeyframes) {
				SelectKeyframes(r);
			} else {
				for (var i = r.A.Y; i <= r.B.Y; i++) {
					Operations.SelectGridSpan.Perform(i, r.A.X, r.B.X + 1);
				}
			}
		}

		private static void SelectKeyframes(IntRectangle bounds)
		{
			foreach (var i in Document.Current.VisibleSceneItems) {
				if (
					i.GetTimelineSceneItemState().Index >= bounds.A.Y && i.GetTimelineSceneItemState().Index <= bounds.B.Y &&
					i.Components.Get<NodeSceneItem>() is NodeSceneItem nodeRow
				) {
					foreach (var animator in nodeRow.Node.Animators) {
						foreach (var key in animator.ReadonlyKeys) {
							if (key.Frame >= bounds.A.X && key.Frame <= bounds.B.X) {
								Operations.SelectGridSpan.Perform(i.GetTimelineSceneItemState().Index, key.Frame, key.Frame + 1);
							}
						}
					}
				}
			}
		}

		private static bool IsCellSelected(IntVector2 cell)
		{
			return Document.Current.VisibleSceneItems[cell.Y].Components.GetOrAdd<GridSpanListComponent>().Spans.IsCellSelected(cell.X);
		}

		private IEnumerator<object> DragSelectionTask(IntVector2 initialCell)
		{
			var input = Grid.RootWidget.Input;
			var offset = IntVector2.Zero;
			void Action(Widget widget) => Timeline.Grid.RenderSelection(widget, offset);
			Grid.OnPostRender += Action;
			float time = 0;

			while (input.IsMousePressed()) {
				time += Lime.Task.Current.Delta;
				offset = Grid.CellUnderMouse() - initialCell;
				Timeline.Ruler.MeasuredFrameDistance = Timeline.CurrentColumn - initialCell.X;

				if (input.IsKeyPressed(Key.Shift) == CoreUserPreferences.Instance.InverseShiftKeyframeDrag) {
					offset.Y = 0;
				}

				Window.Current.Invalidate();
				yield return null;
			}
			if (offset != IntVector2.Zero) {
				Timeline.Globals.Add(new DragKeyframesRequest(offset, !input.IsKeyPressed(Key.Alt)));
				lastSelectedCell = IntVector2.Clamp(
					lastSelectedCell + offset,
					IntVector2.Zero,
					new IntVector2(int.MaxValue, int.MaxValue)
				);
				Timeline.Ruler.MeasuredFrameDistance = 0;
			} else {
				// If a user has clicked with control on a keyframe, try to deselect it [CIT-125].
				var cell = Grid.CellUnderMouse();
				if (input.IsKeyPressed(Key.Control) && time < 0.2f) {
					Operations.DeselectGridSpan.Perform(cell.Y, cell.X, cell.X + 1);
					lastSelectedCell = cell;
				} else if (input.IsKeyPressed(Key.Shift)) {
					Operations.ClearGridSelection.Perform();
					SelectRange(lastSelectedCell, initialCell, input.IsKeyPressed(Key.Alt));
				} else {
					Operations.ClearGridSelection.Perform();
					Operations.SelectGridSpan.Perform(
						cell.Y,
						cell.X,
						cell.X + 1
					);
					lastSelectedCell = cell;
				}
			}
			Grid.OnPostRender -= Action;
			Window.Current.Invalidate();
		}

		private IEnumerator<object> DragSingleKeyframeTask(IntVector2 cell)
		{
			Core.Operations.ClearSceneItemSelection.Perform();
			Operations.ClearGridSelection.Perform();
			Operations.SelectGridSpan.Perform(cell.Y, cell.X, cell.X + 1);
			yield return DragSelectionTask(cell);
		}

		private IEnumerator<object> SelectTask(IntVector2 initialCell, bool selectKeyframes)
		{
			var input = Grid.RootWidget.Input;
			if (!input.IsKeyPressed(Key.Control)) {
				Operations.ClearGridSelection.Perform();
				selectionRectangle = new IntRectangle();
			}
			Grid.OnPostRender += RenderSelectionRect;
			var showMeasuredFrameDistance = false;
			while (input.IsMousePressed()) {
				selectionRectangle.A = initialCell;
				selectionRectangle.B = Grid.CellUnderMouse();
				if (selectionRectangle.Width >= 0) {
					selectionRectangle.B.X++;
				} else {
					selectionRectangle.A.X++;
				}
				if (selectionRectangle.Height >= 0) {
					selectionRectangle.B.Y++;
				} else {
					selectionRectangle.A.Y++;
				}
				selectionRectangle = selectionRectangle.Normalized;
				showMeasuredFrameDistance |= selectionRectangle.Width != 1;
				if (showMeasuredFrameDistance) {
					Timeline.Instance.Ruler.MeasuredFrameDistance = selectionRectangle.Width;
				}
				Window.Current.Invalidate();
				yield return null;
			}
			Timeline.Instance.Ruler.MeasuredFrameDistance = 0;
			Grid.OnPostRender -= RenderSelectionRect;
			var selectedItems = Document.Current.SelectedSceneItems();
			if (!input.IsKeyPressed(Key.Control)) {
				foreach (var i in selectedItems) {
					if (
						i.GetTimelineSceneItemState().Index < selectionRectangle.A.Y
						|| selectionRectangle.B.Y <= i.GetTimelineSceneItemState().Index
					) {
						Core.Operations.ClearSceneItemSelection.Perform();
						break;
					}
				}
			}
			if (selectKeyframes) {
				SelectKeyframes(selectionRectangle);
			} else {
				for (var r = selectionRectangle.A.Y; r < selectionRectangle.B.Y; r++) {
					Operations.SelectGridSpan.Perform(r, selectionRectangle.A.X, selectionRectangle.B.X);
				}
			}
		}

		private void RenderSelectionRect(Widget widget)
		{
			widget.PrepareRendererState();
			var a = Grid.CellToGridCoordinates(selectionRectangle.A);
			var b = Grid.CellToGridCoordinates(selectionRectangle.B);
			Renderer.DrawRect(a, b, ColorTheme.Current.TimelineGrid.Selection);
			Renderer.DrawRectOutline(a, b, ColorTheme.Current.TimelineGrid.SelectionBorder);
		}
	}
}
