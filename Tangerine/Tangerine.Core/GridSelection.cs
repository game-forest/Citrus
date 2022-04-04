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
			var items = Document.Current?.SelectedSceneItems().ToList();
			if (items == null || items?.Count == 0) {
				return false;
			}
			var span = SingleSpan(
				items[0].Components.Get<GridSpanListComponent>()
				?.Spans.GetNonOverlappedSpans());
			var index = items[0].GetTimelineSceneItemState().Index;
			if (span == null) {
				return false;
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
					return false;
				}
				span = newSpan;
			}
			result = new IntRectangle {
				Left = Math.Max(span.Value.A, 0),
				Right = span.Value.B,
				Top = items[0].GetTimelineSceneItemState().Index,
				Bottom = index,
			};
			return true;
		}

		public static IEnumerable<(Node AnimationHost, IAnimator Animator)> EnumerateAnimators(IntRectangle boundaries)
		{
			var processed = new HashSet<IAbstractAnimator>();
			var items = Document.Current.VisibleSceneItems.ToList();
			for (int i = boundaries.Top; i <= boundaries.Bottom; ++i) {
				var components = items[i].Components;
				if (components.Get<NodeSceneItem>()?.Node is Node node) {
					foreach (var animator in node.Animators) {
						if (ShouldProcessAnimator(animator)) {
							yield return (node, animator);
						}
					}
				} else {
					var item = components.Get<AnimatorSceneItem>();
					if (item != null && item.Node != null && ShouldProcessAnimator(item.Animator)) {
						yield return (item.Node, item.Animator);
					}
				}
			}

			bool ShouldProcessAnimator(IAnimator animator)
			{
				return processed.Add(animator) && CanAccessAnimator(animator);
			}

			static bool CanAccessAnimator(IAnimator animator)
			{
				return Document.Current.Animation.ValidatedEffectiveAnimatorsPerHost.Contains(animator);
			}
		}
	}
}
