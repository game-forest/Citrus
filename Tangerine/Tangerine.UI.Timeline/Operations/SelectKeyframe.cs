using Lime;
using Tangerine.Core;
using Tangerine.Core.Components;

namespace Tangerine.UI.Timeline.Operations
{
	public enum SelectKeyframeMode
	{
		Next,
		Previous,
		// Specific
	}

	public static class SelectKeyframe
	{
		public static void Perform(SelectKeyframeMode mode)
		{
			int minFrame = int.MaxValue;
			foreach (var row in Document.Current.SelectedRows()) {
				var animable = row.Components.Get<NodeRow>()?.Node as IAnimationHost;
				if (animable == null) {
					continue;
				}
				foreach (var animator in animable.Animators) {
					if (animator.AnimationId != Document.Current.AnimationId || animator.Keys.Count == 0) {
						continue;
					}
					int current = GetIndexByFrame(animator.Keys, Document.Current.AnimationFrame);
					bool overflow = current == animator.Keys.Count;
					switch (mode) {
						case SelectKeyframeMode.Next:
							current = !overflow && animator.Keys[current].Frame == Document.Current.AnimationFrame
								? current + 1
								: overflow
									? current - 1
									: current;
							break;

						case SelectKeyframeMode.Previous:
							current = current - 1;
							break;
					}
					current = Mathf.Clamp(current, 0, animator.Keys.Count - 1);
					if (animator.Keys[current].Frame < minFrame) {
						minFrame = animator.Keys[current].Frame;
					}
				}
			}
			if (minFrame == int.MaxValue) {
				return;
			}
			var timeline = Timeline.Instance;
			Document.Current.History.DoTransaction(() => {
				timeline.OffsetX = Mathf.Max(0, (minFrame + 1) * TimelineMetrics.ColWidth - timeline.Grid.RootWidget.Width / 2);
				SetCurrentColumn.Perform(minFrame);
				var timelineOffset = Document.Current.Container.Components.GetOrAdd<TimelineOffset>();
				timelineOffset.Offset = timeline.Offset;
			});
		}

		private static int GetIndexByFrame(IKeyframeList keys, int frame)
		{
			int l = 0;
			int r = keys.Count - 1;
			while (l <= r) {
				int m = (l + r) / 2;
				if (keys[m].Frame < frame) {
					l = m + 1;
				} else if (keys[m].Frame > frame) {
					r = m - 1;
				} else {
					return m;
				}
			}
			return l;
		}
	}

	public static class SelectNextKeyframe
	{
		public static void Perform()
		{
			SelectKeyframe.Perform(SelectKeyframeMode.Next);
		}
	}

	public static class SelectPreviousKeyframe
	{
		public static void Perform()
		{
			SelectKeyframe.Perform(SelectKeyframeMode.Previous);
		}
	}
}
