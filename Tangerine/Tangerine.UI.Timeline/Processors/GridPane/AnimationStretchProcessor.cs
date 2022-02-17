using System;
using System.Collections.Generic;
using System.Linq;
using Lime;
using Tangerine.Core;
using Tangerine.Core.Operations;
using Tangerine.UI.Timeline.Operations;

namespace Tangerine.UI.Timeline
{
	public class AnimationStretchProcessor : ITaskProvider
	{
		private GridPane Grid => Timeline.Instance.Grid;
		private WidgetInput Input => Grid.RootWidget.Input;
		private Dictionary<IKeyframe, double> savedPositions = new Dictionary<IKeyframe, double>();
		private Dictionary<IAnimator, List<IKeyframe>> savedKeyframes = new Dictionary<IAnimator, List<IKeyframe>>();
		private Dictionary<Marker, double> savedMarkerPositions = new Dictionary<Marker, double>();
		private List<Marker> savedMarkers = new List<Marker>();

		public IEnumerator<object> Task()
		{
			while (true) {
				if (!TimelineUserPreferences.Instance.AnimationStretchMode) {
					yield return null;
					continue;
				}
				if (
					!GridSelection.GetSelectionBoundaries(out var boundaries) || boundaries.Right - boundaries.Left < 2
				) {
					yield return null;
					continue;
				}
				var topLeft = Grid.CellToGridCoordinates(boundaries.Top, boundaries.Left);
				var bottomRight = Grid.CellToGridCoordinates(boundaries.Bottom + 1, boundaries.Right);
				var mousePosition = Grid.ContentWidget.LocalMousePosition();
				if (mousePosition.Y < topLeft.Y || mousePosition.Y > bottomRight.Y) {
					yield return null;
					continue;
				}
				if (mousePosition.X - topLeft.X < 0 && mousePosition.X - topLeft.X > -10) {
					Utils.ChangeCursorIfDefault(MouseCursor.SizeWE);
					if (Input.ConsumeKeyPress(Key.Mouse0)) {
						yield return Drag(boundaries, DragSide.Left);
					}
				} else if (mousePosition.X - bottomRight.X > 0 && mousePosition.X - bottomRight.X < 10) {
					Utils.ChangeCursorIfDefault(MouseCursor.SizeWE);
					if (Input.ConsumeKeyPress(Key.Mouse0)) {
						yield return Drag(boundaries, DragSide.Right);
					}
				}
				yield return null;
			}
		}

		private enum DragSide
		{
			Left,
			Right,
		}

		private IEnumerator<object> Drag(IntRectangle boundaries, DragSide side)
		{
			IntVector2? last = null;
			Save(boundaries);
			bool isStretchingMarkers = Input.IsKeyPressed(Key.Control);
			using (Document.Current.History.BeginTransaction()) {
				while (Input.IsMousePressed()) {
					Utils.ChangeCursorIfDefault(MouseCursor.SizeWE);
					var current = Grid.CellUnderMouse();
					if (current == last) {
						yield return null;
						continue;
					}
					Document.Current.History.RollbackTransaction();
					current.X = Math.Max(current.X, 0);
					if (side == DragSide.Left) {
						current.X = Math.Min(current.X, boundaries.Right - 1);
					} else {
						current.X = Math.Max(current.X, boundaries.Left + 1);
					}
					Stretch(boundaries, side, current.X, stretchMarkers: isStretchingMarkers);
					if (side == DragSide.Left) {
						boundaries.Left = current.X;
					} else {
						boundaries.Right = current.X;
					}
					ClearGridSelection.Perform();
					for (int i = boundaries.Top; i <= boundaries.Bottom; ++i) {
						SelectGridSpan.Perform(i, boundaries.Left, boundaries.Right);
					}
					if (side == DragSide.Left) {
						SetCurrentColumn.Perform(boundaries.Left);
					} else {
						SetCurrentColumn.Perform(boundaries.Right);
					}
					last = current;
					yield return null;
				}
				Document.Current.History.CommitTransaction();
			}
			yield return null;
		}

		private void Stretch(IntRectangle boundaries, DragSide side, int newPos, bool stretchMarkers)
		{
			int length;
			if (side == DragSide.Left) {
				length = boundaries.Right - newPos - 1;
			} else {
				length = newPos - boundaries.Left - 1;
			}
			int oldLength = boundaries.Right - boundaries.Left - 1;
			foreach (var (host, animator) in GridSelection.EnumerateAnimators(boundaries)) {
				if (!savedKeyframes.ContainsKey(animator)) {
					continue;
				}
				IEnumerable<IKeyframe> saved = savedKeyframes[animator];
				if (
					side == DragSide.Left && length < oldLength ||
					side == DragSide.Right && length > oldLength
				) {
					saved = saved.Reverse();
				}
				foreach (var key in saved) {
					// Keyframes will be added back so animator doesn't need to be removed even if it is empty.
					// Also, foreach is iterating over collection of animators and removing
					// animator will cause "collection was modified" error.
					RemoveKeyframe.Perform(animator, key.Frame, removeEmptyAnimator: false);
				}
				foreach (var key in saved) {
					double relpos = savedPositions[key];
					int newFrame;
					if (side == DragSide.Left) {
						newFrame = (int)Math.Round(newPos + relpos * length);
					} else {
						newFrame = (int)Math.Round(boundaries.Left + relpos * length);
					}
					var newKey = key.Clone();
					newKey.Frame = newFrame;
					SetAnimableProperty.Perform(
						@object: host,
						propertyPath: animator.TargetPropertyPath,
						value: newKey.Value,
						createAnimatorIfNeeded: true,
						createInitialKeyframeForNewAnimator: false,
						atFrame: newKey.Frame
					);
					SetKeyframe.Perform(host, animator.TargetPropertyPath, Document.Current.Animation, newKey);
				}
			}
			if (stretchMarkers) {
				foreach (var marker in savedMarkers) {
					DeleteMarker.Perform(marker, removeDependencies: false);
				}
				foreach (var marker in savedMarkers) {
					double relpos = savedMarkerPositions[marker];
					int newFrame;
					if (side == DragSide.Left) {
						newFrame = (int)Math.Round(newPos + relpos * length);
					} else {
						newFrame = (int)Math.Round(boundaries.Left + relpos * length);
					}
					var newMarker = marker.Clone();
					newMarker.Frame = newFrame;
					SetMarker.Perform(newMarker, removeDependencies: false);
				}
			}
		}

		private void Save(IntRectangle boundaries)
		{
			savedPositions.Clear();
			savedKeyframes.Clear();
			savedMarkerPositions.Clear();
			savedMarkers.Clear();
			var length = boundaries.Right - boundaries.Left - 1;
			foreach (var (_, animator) in GridSelection.EnumerateAnimators(boundaries)) {
				savedKeyframes.Add(animator, new List<IKeyframe>());
				var keys = animator.Keys.Where(k =>
					boundaries.Left <= k.Frame &&
					k.Frame < boundaries.Right
				);
				foreach (var key in keys) {
					savedPositions.Add(key, ((double)key.Frame - boundaries.Left) / length);
					savedKeyframes[animator].Add(key);
				}
			}
			var markers = Document.Current.Animation.Markers.Where(k =>
				boundaries.Left <= k.Frame &&
				k.Frame < boundaries.Right);
			foreach (var marker in markers) {
				savedMarkerPositions.Add(marker, ((double)marker.Frame - boundaries.Left) / length);
				savedMarkers.Add(marker);
			}
		}
	}
}
