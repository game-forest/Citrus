using System;
using System.Linq;
using System.Collections.Generic;
using Lime;
using Tangerine.Core.Components;

namespace Tangerine.Core
{
	public static class GridSelection
	{
		public class RowAnimators
		{
			public IAnimationHost Host;
			public List<IAnimator> Animators;
		}

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

		public static IEnumerable<RowAnimators> EnumerateAnimators(IntRectangle boundaries)
		{
			var rows = Document.Current.Rows.ToList();
			for (int i = boundaries.Top; i <= boundaries.Bottom; ++i) {
				RowAnimators animable = null;
				var components = rows[i].Components;
				if (components.Get<NodeRow>()?.Node is IAnimationHost host) {
					animable = new RowAnimators {
						Host = host,
						Animators = new List<IAnimator>(host.Animators.ToList())
					};
				} else if (components.Get<AnimatorRow>() is AnimatorRow prop && prop.Node is IAnimationHost) {
					animable = new RowAnimators {
						Host = prop.Node,
						Animators = new List<IAnimator>()
					};
					animable.Animators.Add(prop.Animator);
				} else {
					continue;
				}
				yield return animable;
			}
		}
	}
}
