using Lime;
using System;
using System.Linq;
using Tangerine.Core;
using Tangerine.Core.Components;
using Tangerine.Core.Operations;

namespace Tangerine.UI.Timeline.Operations
{
	public static class ReverseKeyframes
	{
		private struct Boundaries
		{
			public int Top;
			public int Bottom;
			public int Left;
			public int Right;
		}

		public static void Perform()
		{
			var Boundaries = GetSelectionBoundaries();
			if (Boundaries == null) {
				AlertDialog.Show("Can't invert animation in a non-rectangular selection. The selection must be a single rectangle.");
				return;
			}
			using (Document.Current.History.BeginTransaction()) {
				for (int i = Boundaries.Value.Top; i <= Boundaries.Value.Bottom; ++i) {
					if (!(Document.Current.VisibleSceneItems[i].Components.Get<NodeSceneItem>()?.Node is IAnimationHost animable)) {
						continue;
					}
					foreach (var animator in animable.Animators.ToList()) {
						var saved = animator.Keys.Where(k =>
							Boundaries.Value.Left <= k.Frame &&
							k.Frame < Boundaries.Value.Right).ToList();
						foreach (var key in saved) {
							RemoveKeyframe.Perform(animator, key.Frame);
						}
						foreach (var key in saved) {
							SetProperty.Perform(key, nameof(IKeyframe.Frame), Boundaries.Value.Left + Boundaries.Value.Right - key.Frame - 1);
							SetKeyframe.Perform(animable, animator.TargetPropertyPath, Document.Current.Animation, key);
						}
					}
				}
				Document.Current.History.CommitTransaction();
			}
		}

		private static Boundaries? GetSelectionBoundaries()
		{
			var items = Document.Current.SelectedSceneItems().ToList();
			if (items.Count == 0) {
				return GetTimelineBoundaries();
			}
			var span = SingleSpan(
				items[0].Components.Get<GridSpanListComponent>()
				?.Spans.GetNonOverlappedSpans());
			var index = items[0].GetTimelineSceneItemState().Index;
			if (span == null) {
				return null;
			}
			for (int i = 1; i < items.Count; ++i) {
				var newSpan = SingleSpan(
					items[i].Components.Get<GridSpanListComponent>()
					?.Spans.GetNonOverlappedSpans());
				if (
					newSpan == null ||
					span?.A != newSpan?.A ||
					span?.B != newSpan?.B ||
					++index != items[i].GetTimelineSceneItemState().Index
				) {
					return null;
				}
				span = newSpan;
			}
			return new Boundaries {
				Left = span.Value.A,
				Right = span.Value.B,
				Top = items[0].GetTimelineSceneItemState().Index,
				Bottom = index
			};
		}

		private static GridSpan? SingleSpan(GridSpanList spans)
		{
			if (spans.Count != 1) {
				return null;
			} else {
				return spans[0];
			}
		}

		private static Boundaries? GetTimelineBoundaries()
		{
			int right = 0;
			foreach (var i in Document.Current.VisibleSceneItems) {
				if (!(i.Components.Get<NodeSceneItem>()?.Node is IAnimationHost animable)) {
					continue;
				}
				foreach (var animator in animable.Animators) {
					foreach (var keyframe in animator.ReadonlyKeys) {
						right = Math.Max(keyframe.Frame, right);
					}
				}
			}
			return new Boundaries {
				Left = 0,
				Right = right,
				Top = 0,
				Bottom = Document.Current.VisibleSceneItems.Last().GetTimelineSceneItemState().Index
			};
		}
	}
}
