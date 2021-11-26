using System;
using System.Linq;
using System.Collections.Generic;
using Lime;
using Tangerine.Core.Components;

namespace Tangerine.Core
{
	public static class GridSelection
	{
		private static GridSpan? SingleSpan(GridSpanList spans)
		{
			if (spans == null) {
				return null;
			}
			if (spans.Count != 1) {
				return null;
			} else {
				return spans[0];
			}
		}

		public static bool GetSelectionBoundaries(out IntRectangle result)
		{
			result = new IntRectangle();
			var rows = Document.Current?.SelectedRows().ToList();
			if (rows == null || rows?.Count == 0) {
				return false;
			}
			var span = SingleSpan(
				rows[0].Components.Get<GridSpanListComponent>()
				?.Spans.GetNonOverlappedSpans());
			var index = rows[0].GetTimelineItemState().Index;
			if (span == null) {
				return false;
			}
			for (int i = 1; i < rows.Count; ++i) {
				var newSpan = SingleSpan(
					rows[i].Components.Get<GridSpanListComponent>()
					?.Spans.GetNonOverlappedSpans());
				if (
					newSpan == null ||
					span?.A != newSpan?.A ||
					span?.B != newSpan?.B ||
					++index != rows[i].GetTimelineItemState().Index
				) {
					return false;
				}
				span = newSpan;
			}
			result = new IntRectangle {
				Left = Math.Max(span.Value.A, 0),
				Right = span.Value.B,
				Top = rows[0].GetTimelineItemState().Index,
				Bottom = index
			};
			return true;
		}

		public static IEnumerable<(Node, IAnimator)> EnumerateAnimators(IntRectangle boundaries)
		{
			var processed = new HashSet<IAbstractAnimator>();
			var rows = Document.Current.Rows.ToList();
			for (int i = boundaries.Top; i <= boundaries.Bottom; ++i) {
				var components = rows[i].Components;
				if (components.Get<NodeRow>()?.Node is Node node) {
					foreach (var animator in node.Animators) {
						if (ShouldProcessAnimator(animator)) {
							yield return (node, animator);
						}
					}
				} else {
					var row = components.Get<AnimatorRow>();
					if (row != null && row.Node != null && ShouldProcessAnimator(row.Animator)) {
						yield return (row.Node, row.Animator);
					}
				}
			}
			
			bool ShouldProcessAnimator(IAbstractAnimator animator) =>
				processed.Add(animator) && CanAccessAnimator(animator);
			
			static bool CanAccessAnimator(IAbstractAnimator animator) =>
				Document.Current.Animation.ValidatedEffectiveAnimatorsSet.Contains(animator);
		}
	}
}
