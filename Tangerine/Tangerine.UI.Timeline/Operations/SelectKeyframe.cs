using System.Collections.Generic;
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
			int adjacentFrame = int.MaxValue;
			int currentFrame = Document.Current.AnimationFrame;
			var animators = new HashSet<IAnimator>();
			foreach (var item in Document.Current.SelectedSceneItems()) {
				var animable = item.Components.Get<NodeSceneItem>()?.Node as IAnimationHost;
				if (animable != null) {
					animators.UnionWith(animable.Animators);
					continue;
				}
				var animator = item.Components.Get<AnimatorSceneItem>()?.Animator;
				if (animator != null) {
					animators.Add(animator);
					continue;
				}
				var bone = item.Components.Get<BoneSceneItem>()?.Bone;
				if (bone != null) {
					animators.UnionWith(bone.Animators);
					continue;
				}
			}
			foreach (var animator in animators) {
				if (animator.AnimationId != Document.Current.AnimationId || animator.Keys.Count == 0) {
					continue;
				}
				int sign = 1;
				int current = GetUpperKeyframeIndexByFrame(animator.Keys, currentFrame);
				bool overflow = current == animator.Keys.Count;
				switch (mode) {
					case SelectKeyframeMode.Next:
						current = !overflow && animator.Keys[current].Frame == currentFrame
							? current + 1
							: overflow
								? current - 1
								: current;
						break;

					case SelectKeyframeMode.Previous:
						sign = -1;
						current--;
						break;
				}
				current = Mathf.Clamp(current, 0, animator.Keys.Count - 1);
				if (animator.Keys[current].Frame == currentFrame) {
					continue;
				}
				if (
					Mathf.Sign(animator.Keys[current].Frame - currentFrame) == sign &&
					Mathf.Abs(animator.Keys[current].Frame - currentFrame) < Mathf.Abs(adjacentFrame - currentFrame)
				) {
					adjacentFrame = animator.Keys[current].Frame;
				}
			}
			if (adjacentFrame == int.MaxValue) {
				return;
			}
			var timeline = Timeline.Instance;
			Document.Current.History.DoTransaction(() => {
				timeline.OffsetX = Mathf.Max(0, (adjacentFrame + 1) * TimelineMetrics.ColWidth - timeline.Grid.RootWidget.Width / 2);
				SetCurrentColumn.Perform(adjacentFrame);
				var timelineOffset = Document.Current.Container.Components.GetOrAdd<TimelineOffset>();
				timelineOffset.Offset = timeline.Offset;
			});
		}

		private static int GetUpperKeyframeIndexByFrame(IKeyframeList keys, int frame)
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
