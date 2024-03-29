using System;
using System.Collections.Generic;
using System.Linq;
using Lime;
using Tangerine.Core;
using Tangerine.Core.Components;
using Tangerine.Core.Operations;
using Tangerine.UI.Timeline.Components;

namespace Tangerine.UI.Timeline.Operations
{
	public static class DragKeyframes
	{
		public static void Perform(IntVector2 offset, bool removeOriginals)
		{
			var processedKeys = new HashSet<IKeyframe>();
			var operations = new List<Action>();
			foreach (var item in Document.Current.VisibleSceneItems) {
				var spans = item.Components.GetOrAdd<GridSpanListComponent>().Spans.GetNonOverlappedSpans(offset.X > 0);
				foreach (var span in spans) {
					var node = item.Components.Get<NodeSceneItem>()?.Node
						?? item.Components.Get<AnimatorSceneItem>()?.Node;
					if (node == null || node.EditorState().Locked) {
						continue;
					}
					var property = item.Components.Get<AnimatorSceneItem>()?.Animator.TargetPropertyPath;
					var animators = Document.Current.Animation.ValidatedEffectiveAnimators
						.Intersect(node.Animators)
						.OfType<IAnimator>()
						.ToList();
					foreach (var a in animators) {
						if (property != null && a.TargetPropertyPath != property) {
							continue;
						}
						IEnumerable<IKeyframe> keysEnumerable =
							a.Keys.Where(k => k.Frame >= span.A && k.Frame < span.B);
						if (offset.X > 0) {
							keysEnumerable = keysEnumerable.Reverse();
						}
						foreach (var k in keysEnumerable) {
							if (processedKeys.Contains(k)) {
								continue;
							}
							processedKeys.Add(k);
							var destItemIndex = item.GetTimelineSceneItemState().Index + offset.Y;
							if (!CheckSceneItemRange(destItemIndex)) {
								continue;
							}
							var destRowComponents = Document.Current.VisibleSceneItems[destItemIndex].Components;
							var destNode = destRowComponents.Get<NodeSceneItem>()?.Node
								?? destRowComponents.Get<AnimatorSceneItem>()?.Node;
							if (destNode == null || !ArePropertyPathsCompatible(node, destNode, a.TargetPropertyPath)) {
								continue;
							}
							if (k.Frame + offset.X >= 0) {
								var k1 = k.Clone();
								k1.Frame += offset.X;
								// The same logic is used to create keyframes as everywhere, but extended by setting
								// all parameters from a particular keyframe. Yes, this creates some overhead.
								operations.Add(
									() => SetAnimableProperty.Perform(
										destNode, a.TargetPropertyPath, k1.Value, true, false, k1.Frame
									)
								);
								operations.Add(
									() => SetKeyframe.Perform(
										destNode, a.TargetPropertyPath, Document.Current.Animation, k1
									)
								);
							}
							// Order is important. RemoveKeyframe must be after SetKeyframe,
							// to prevent animator clean up if all keys were removed.
							if (removeOriginals) {
								operations.Add(() => RemoveKeyframe.Perform(a, k.Frame));
							}
						}
					}
				}
			}
			foreach (var o in operations) {
				o();
			}
		}

		private static bool CheckSceneItemRange(int sceneItemIndex)
		{
			return sceneItemIndex >= 0 && sceneItemIndex < Document.Current.VisibleSceneItems.Count;
		}

		private static bool ArePropertyPathsCompatible(IAnimationHost object1, IAnimationHost object2, string property)
		{
			var (pd1, _, _) = AnimationUtils.GetPropertyByPath(object1, property);
			var (pd2, _, _) = AnimationUtils.GetPropertyByPath(object2, property);
			return pd1.Info != null && pd1.Info.PropertyType == pd2.Info?.PropertyType;
		}
	}
}
