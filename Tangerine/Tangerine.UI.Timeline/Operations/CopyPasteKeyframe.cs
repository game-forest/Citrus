using System.Collections.Generic;
using System.Linq;
using Lime;
using Tangerine.Core;
using Tangerine.Core.Components;
using Tangerine.Core.Operations;
using Tangerine.UI.Timeline.Components;
using Yuzu;

namespace Tangerine.UI.Timeline.Operations
{
	internal static class KeyframeClipboard
	{
		public static List<AnimationHostKeyBinding> Keys;
	}

	public static class CopyKeyframes
	{
		public static void Perform()
		{
			KeyframeClipboard.Keys = GetKeyframes();
		}

		private static List<AnimationHostKeyBinding> GetKeyframes()
		{
			var list = new List<AnimationHostKeyBinding>();

			int startItemIndex = Document.Current.TopLevelSelectedSceneItems()
				.First()
				.GetTimelineSceneItemState().Index;
			var spans = Document.Current.VisibleSceneItems[startItemIndex].Components
				.Get<GridSpanListComponent>()?.Spans;
			if (spans == null || !spans.Any()) {
				return list;
			}
			int startCol = spans.First().A;
			int animationHostIndex = -1;
			IAnimationHost previousAnimationHost = null;
			var effectiveAnimatorsPerHost = Document.Current.Animation.ValidatedEffectiveAnimatorsPerHost;

			foreach (var item in Document.Current.SelectedSceneItems()) {
				spans = item.Components.Get<GridSpanListComponent>()?.Spans;
				if (spans == null) {
					continue;
				}
				if (item.TryGetAnimator(out var animator)) {
					if (previousAnimationHost != animator.Owner) {
						previousAnimationHost = animator.Owner;
						animationHostIndex++;
					}
					ProcessAnimator(animator);
				} else if (item.TryGetNode(out var node)) {
					animationHostIndex++;
					previousAnimationHost = node;
					foreach (var a in node.Animators) {
						ProcessAnimator(a);
					}
				}
				void ProcessAnimator(IAnimator animator)
				{
					if (!effectiveAnimatorsPerHost.Contains(animator)) {
						return;
					}
					foreach (var keyframe in animator.Keys.Where(i => spans.Any(j => j.Contains(i.Frame)))) {
						list.Add(new AnimationHostKeyBinding {
							Frame = keyframe.Frame - startCol,
							Property = animator.TargetPropertyPath,
							AnimationHostOrderIndex = animationHostIndex,
							Keyframe = keyframe,
						});
					}
				}
			}
			return list;
		}
	}

	internal class AnimationHostKeyBinding
	{
		[YuzuMember]
		public int AnimationHostOrderIndex { get; set; }

		[YuzuMember]
		public int Frame { get; set; }

		[YuzuMember]
		public string Property { get; set; }

		[YuzuMember]
		public IKeyframe Keyframe { get; set; }
	}

	public static class CutKeyframes
	{
		public static void Perform()
		{
			CopyKeyframes.Perform();
			DeleteKeyframes.Perform();
		}
	}

	public static class PasteKeyframes
	{
		public static void Perform()
		{
			var keys = KeyframeClipboard.Keys;
			if (keys == null || !Document.Current.TopLevelSelectedSceneItems().Any()) {
				return;
			}
			int startItemIndex = Document.Current.TopLevelSelectedSceneItems()
				.First()
				.GetTimelineSceneItemState().Index;
			var spans = Document.Current.VisibleSceneItems[startItemIndex].Components
				.Get<GridSpanListComponent>()?.Spans;
			if (spans == null || !spans.Any()) {
				return;
			}
			int startCol = spans.First().A;
			Document.Current.History.DoTransaction(() => {
				var items = Document.Current.VisibleSceneItems;
				if (items[startItemIndex].TryGetAnimator(out _)) {
					startItemIndex = items.IndexOf(Document.Current.VisibleSceneItems[startItemIndex].Parent);
				}
				int itemIndex = startItemIndex;
				int animationHostIndex = 0;
				IAnimationHost animationHost = null;
				Node node = null;

				foreach (var key in keys) {
					int colIndex = startCol + key.Frame;
					if (itemIndex >= Document.Current.VisibleSceneItems.Count || colIndex < 0) {
						continue;
					}
					while (itemIndex < items.Count) {
						node = items[itemIndex].Components.Get<NodeSceneItem>()?.Node;
						animationHost = node;
						if (animationHost != null) {
							if (animationHostIndex == key.AnimationHostOrderIndex) {
								break;
							}
							animationHostIndex++;
						}
						++itemIndex;
					}
					if (itemIndex >= items.Count) {
						break;
					}
					if (node.EditorState().Locked) {
						continue;
					}
					var (pd, _, _) = AnimationUtils.GetPropertyByPath(animationHost, key.Property);
					if (pd.Info == null) {
						continue;
					}
					var keyframe = key.Keyframe.Clone();
					keyframe.Frame = colIndex;
					SetKeyframe.Perform(animationHost, key.Property, Document.Current.Animation, keyframe);
				}
			});
		}
	}

	public static class DeleteKeyframes
	{
		public static void Perform()
		{
			Document.Current.History.DoTransaction(() => {
				foreach (var item in Document.Current.SelectedSceneItems().ToList()) {
					var spans = item.Components.Get<GridSpanListComponent>()?.Spans;
					if (spans == null) {
						continue;
					}
					var node = item.Components.Get<NodeSceneItem>()?.Node
						?? item.Components.Get<AnimatorSceneItem>()?.Node;
					if (node.EditorState().Locked) {
						continue;
					}
					var animable = (IAnimationHost)node;
					if (animable == null) {
						continue;
					}
					foreach (var animator in animable.Animators.ToList()) {
						if (animator.AnimationId != Document.Current.AnimationId) {
							continue;
						}
						var keysWithinSpan = animator.Keys.Where(i => spans.Any(j => j.Contains(i.Frame))).ToList();
						foreach (var keyframe in keysWithinSpan) {
							RemoveKeyframe.Perform(animator, keyframe.Frame);
						}
					}
				}
			});
		}
	}
}
