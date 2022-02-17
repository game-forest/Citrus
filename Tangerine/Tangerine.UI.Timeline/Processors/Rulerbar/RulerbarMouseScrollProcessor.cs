using System;
using System.Collections.Generic;
using System.Linq;
using Lime;
using Tangerine.Core;
using Tangerine.Core.Operations;
using Tangerine.UI.Docking;
using Tangerine.UI.Timeline.Operations;

namespace Tangerine.UI.Timeline
{
	public class RulerbarMouseScrollProcessor : ITaskProvider
	{
		private static Timeline Timeline => Timeline.Instance;

		public IEnumerator<object> Task()
		{
			var rulerWidget = Timeline.Ruler.RootWidget;
			var input = rulerWidget.Input;
			while (true) {
				if (input.WasMousePressed()) {
					using (Document.Current.History.BeginTransaction()) {
						int initialCurrentColumn = Timeline.Ruler.CalcColumnUnderMouse();
						Document.Current.AnimationFrame = initialCurrentColumn;
						var saved = CoreUserPreferences.Instance.StopAnimationOnCurrentFrame;
						// Dirty hack: prevent creating RestoreAnimationsTimesComponent
						// in order to stop running animation on clicked frame (RBT-2887)
						CoreUserPreferences.Instance.StopAnimationOnCurrentFrame = true;
						SetCurrentColumn.Perform(initialCurrentColumn);
						CoreUserPreferences.Instance.StopAnimationOnCurrentFrame = saved;
						int previousColumn = -1;
						var marker = Document.Current.Animation.Markers.GetByFrame(initialCurrentColumn);
						bool isShifting = false;
						while (input.IsMousePressed()) {
							bool isEditing = input.IsKeyPressed(Key.Control);
							bool startShifting = isEditing && input.IsKeyPressed(Key.Shift);
							isShifting = isShifting && startShifting;

							var cw = TimelineMetrics.ColWidth;
							var mp = rulerWidget.LocalMousePosition().X;
							if (mp > rulerWidget.Width - cw / 2) {
								Timeline.OffsetX += cw;
							} else if (mp < cw / 2) {
								Timeline.OffsetX = Math.Max(0, Timeline.OffsetX - cw);
							}
							int newColumn = Timeline.Ruler.CalcColumnUnderMouse();
							if (newColumn == previousColumn) {
								yield return null;
								continue;
							}
							// Evgenii Polikutin: don't Undo to avoid animation cache invalidation when just scrolling
							// Evgenii Polikutin: yet we have to sacrifice performance when editing document
							SetCurrentColumn.IsFrozen = !isEditing;
							SetCurrentColumn.RollbackHistoryWithoutScrolling();
							SetCurrentColumn.IsFrozen = false;

							if (isEditing) {
								if (isShifting) {
									ShiftTimeline(newColumn);
								} else if (startShifting && newColumn == initialCurrentColumn) {
									isShifting = true;
								} else if (!startShifting && marker != null) {
									DragMarker(marker, newColumn);
								}
							}
							// Evgenii Polikutin: we need operation to backup the value we need, not the previous one
							Document.Current.AnimationFrame = initialCurrentColumn;
							if (newColumn != initialCurrentColumn) {
								SetCurrentColumn.Perform(newColumn);
							}
							Timeline.Ruler.MeasuredFrameDistance = Timeline.CurrentColumn - initialCurrentColumn;
							previousColumn = newColumn;
							DockHierarchy.Instance.InvalidateWindows();
							yield return null;
						}
						Document.Current.History.CommitTransaction();
						Timeline.Ruler.MeasuredFrameDistance = 0;
					}
				}
				yield return null;
			}
		}

		private void ShiftTimeline(int destColumn)
		{
			var delta = destColumn - Timeline.CurrentColumn;
			if (delta > 0) {
				TimelineHorizontalShift.Perform(Timeline.CurrentColumn, delta);
			} else if (delta < 0) {
				foreach (
					var animator
					in Document.Current.Animation.ValidatedEffectiveAnimators.OfType<IAnimator>().ToList()
				) {
					RemoveKeyframeRange.Perform(animator, destColumn, Timeline.CurrentColumn - 1);
				}
				foreach (
					var marker
					in Document.Current.Animation.Markers
						.Where(m => m.Frame >= destColumn && m.Frame < Timeline.CurrentColumn)
						.ToList()
				) {
					DeleteMarker.Perform(marker, removeDependencies: false);
				}
				TimelineHorizontalShift.Perform(destColumn, delta);
			}
		}

		private void DragMarker(Marker marker, int destColumn)
		{
			var markerToRemove = Document.Current.Animation.Markers.FirstOrDefault(m => m.Frame == destColumn);
			if (marker.Frame != destColumn && markerToRemove != null) {
				DeleteMarker.Perform(markerToRemove, false);
			}
			// Delete and add marker again, because we want to maintain the markers order.
			DeleteMarker.Perform(marker, false);
			SetProperty.Perform(marker, "Frame", destColumn);
			SetMarker.Perform(marker, true);
		}
	}
}
