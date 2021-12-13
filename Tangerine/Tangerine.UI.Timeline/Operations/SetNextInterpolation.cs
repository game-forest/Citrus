using System.Collections.Generic;
using System.Linq;
using Lime;
using Tangerine.Core;
using Tangerine.Core.Components;
using Tangerine.Core.Operations;

namespace Tangerine.UI.Timeline.Operations
{
	public static class SetNextInterpolation
	{
		public static void Perform()
		{
			if (!Timeline.Instance.RootWidget.IsMouseOverThisOrDescendant()) {
				return;
			}
			Document.Current?.History.DoTransaction(() => {
				var processedKeyframes = new Dictionary<IKeyframe, (IAnimator, Node)>();
				if (!Timeline.Instance.Grid.RootWidget.DescendantOf(Widget.Focused)) {
					foreach (var treeViewItem in Timeline.Instance.Roll.TreeView.SelectedItems) {
						var item = ((ISceneItemHolder)treeViewItem).SceneItem;
						var node = 
							item.Components.Get<NodeSceneItem>()?.Node ?? 
							item.Components.Get<AnimatorSceneItem>()?.Node;
						if (node == null || node.EditorState().Locked) {
							continue;
						}
						var property = item.Components.Get<AnimatorSceneItem>()?.Animator.TargetPropertyPath;
						var animators = ValidatedEffectiveAnimators.Intersect(node.Animators);
						foreach (var animator in animators) {
							if (property != null && animator.TargetPropertyPath != property) {
								continue;
							}
							foreach (var key in animator.Keys) {
								processedKeyframes.TryAdd(key, (animator, node));
							}
						}
					}
				} else {
					foreach (var item in Document.Current.VisibleSceneItems) {
						var spans = item.Components.GetOrAdd<GridSpanListComponent>().Spans.GetNonOverlappedSpans();
						foreach (var span in spans) {
							var node = 
								item.Components.Get<NodeSceneItem>()?.Node ?? 
								item.Components.Get<AnimatorSceneItem>()?.Node;
							if (node == null || node.EditorState().Locked) {
								continue;
							}
							var property = item.Components.Get<AnimatorSceneItem>()?.Animator.TargetPropertyPath;
							var animators = ValidatedEffectiveAnimators.Intersect(node.Animators);
							foreach (var animator in animators) {
								if (property != null && animator.TargetPropertyPath != property) {
									continue;
								}
								bool IsKeyframeInSpan(IKeyframe keyframe) =>
									keyframe.Frame >= span.A && keyframe.Frame < span.B;
								foreach (var key in animator.Keys.Where(IsKeyframeInSpan)) {
									processedKeyframes.TryAdd(key, (animator, node));
								}
							}
						}
					}
				}
				var function = GetKeyFunction(processedKeyframes.Keys);
				foreach (var (keyframe, (animator, node)) in processedKeyframes) {
					var keyframeClone = keyframe.Clone();
					keyframeClone.Function = function;
					SetKeyframe.Perform(node, animator.TargetPropertyPath, Document.Current.Animation, keyframeClone);
				}
			});
		}

		private static IEnumerable<IAnimator> ValidatedEffectiveAnimators =>
			Document.Current.Animation.ValidatedEffectiveAnimators.OfType<IAnimator>();

		private static KeyFunction GetKeyFunction(IEnumerable<IKeyframe> keyframes)
		{
			KeyFunction? function = null;
			foreach (var keyframe in keyframes) {
				if (function == null) {
					function = keyframe.Function;
				} else if (function != keyframe.Function) {
					function = null;
					break;
				}
			}
			return function == null ? KeyFunction.Linear : GetNextKeyFunction(function.Value);
		}
		
		private static readonly KeyFunction[] nextKeyFunction = {
			KeyFunction.Steep,
			KeyFunction.Spline,
			KeyFunction.ClosedSpline,
			KeyFunction.Linear,
		};

		public static KeyFunction GetNextKeyFunction(KeyFunction value) => nextKeyFunction[(int)value];
	}
}
