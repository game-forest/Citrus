using System;
using System.Collections.Generic;
using System.Linq;
using Lime;
using Tangerine.Core.Components;

namespace Tangerine.Core
{
	public class SceneTreeBuilder
	{
		private readonly Func<object, SceneItem> sceneItemFactory;
		public event Action<SceneItem> SceneItemCreated;

		public SceneTreeBuilder(Func<object, SceneItem> sceneItemFactory = null)
		{
			this.sceneItemFactory = sceneItemFactory ?? (o => new SceneItem());
		}

		public SceneItem BuildSceneTreeForNode(Node node)
		{
			var item = GetNodeSceneItem(node);
			int folderIndex = 0;
			int nodeIndex = 0;
			AddSceneItemsForAnimations(item, node);
			AddSceneItemsForAnimatedProperties(item, node);
			BuildFolderTree(item, int.MaxValue, node, ref folderIndex, ref nodeIndex);
			return item;
		}

		void BuildFolderTree(SceneItem parent, int itemCount, Node node, ref int folderIndex, ref int nodeIndex)
		{
			Dictionary<int, SceneItem> boneToSceneItem = null;
			var folders = node.Folders;
			while (itemCount-- > 0) {
				var folder = folderIndex < folders?.Count ? folders[folderIndex] : null;
				if (nodeIndex < node.Nodes.Count && (folder == null || nodeIndex < folder.Index)) {
					var currentNode = node.Nodes[nodeIndex++];
					var nodeSceneItem = BuildSceneTreeForNode(currentNode);
					if (nodeSceneItem.Parent != null) {
						nodeSceneItem.Unlink();
					}
					if (currentNode is Bone bone) {
						// Link the bone to its base bone.
						boneToSceneItem = boneToSceneItem ?? new Dictionary<int, SceneItem>();
						boneToSceneItem[bone.Index] = nodeSceneItem;
						if (bone.BaseIndex > 0) {
							boneToSceneItem[bone.BaseIndex].SceneItems.Add(nodeSceneItem);
						} else {
							parent.SceneItems.Add(nodeSceneItem);
						}
					} else {
						parent.SceneItems.Add(nodeSceneItem);
					}
				} else if (folder != null) {
					folderIndex++;
					var folderSceneItem = BuildFolderSceneItem(folder);
					if (folderSceneItem.Parent != null) {
						folderSceneItem.Unlink();
					}
					parent.SceneItems.Add(folderSceneItem);
					BuildFolderTree(folderSceneItem, folder.ItemCount, node, ref folderIndex, ref nodeIndex);
				} else {
					break;
				}
			}
		}

		public SceneItem BuildFolderSceneItem(Folder.Descriptor folder)
		{
			var i = sceneItemFactory(folder);
			i.Components.GetOrAdd<FolderSceneItem>().Folder = folder;
			i.Components.GetOrAdd<CommonFolderSceneItemData>().Folder = folder;
			SceneItemCreated?.Invoke(i);
			return i;
		}

		private SceneItem GetNodeSceneItem(Node node)
		{
			var i = sceneItemFactory(node);
			i.Components.GetOrAdd<NodeSceneItem>().Node = node;
			i.Components.GetOrAdd<CommonNodeSceneItemData>().Node = node;
			if (node is Bone bone) {
				i.Components.GetOrAdd<BoneSceneItem>().Bone = bone;
			}
			SceneItemCreated?.Invoke(i);
			return i;
		}

		private void AddSceneItemsForAnimations(SceneItem parent, Node node)
		{
			var animationComponent = node.Components.AnimationComponent;
			if (animationComponent != null && animationComponent.Animations.Count > 0) {
				foreach (var animation in animationComponent.Animations) {
					var animationItem = BuildAnimationSceneItem(animation);
					parent.SceneItems.Add(animationItem);
				}
			}
		}

		private void AddSceneItemsForAnimatedProperties(SceneItem parent, Node node)
		{
			foreach (var animator in node.Animators) {
				var animatorItem = BuildAnimatorSceneItem(animator);
				parent.SceneItems.Add(animatorItem);
			}
		}

		public SceneItem BuildAnimatorSceneItem(IAnimator animator)
		{
			var i = sceneItemFactory(animator);
			i.Components.GetOrAdd<CommonPropertySceneItemData>().Animator = animator;
			var component = i.Components.GetOrAdd<AnimatorSceneItem>();
			component.Node = (Node)animator.Owner;
			component.Animator = animator;
			SceneItemCreated?.Invoke(i);
			return i;
		}

		public SceneItem BuildAnimationSceneItem(Animation animation)
		{
			var i = sceneItemFactory(animation);
			i.Components.GetOrAdd<AnimationSceneItem>().Animation = animation;
			i.Components.GetOrAdd<CommonAnimationSceneItemData>().Animation = animation;
			foreach (var marker in animation.Markers) {
				i.SceneItems.Add(BuildMarkerSceneItem(marker));
			}
			foreach (var track in animation.Tracks) {
				i.SceneItems.Add(BuildAnimationTrackSceneItem(track));
			}
			SceneItemCreated?.Invoke(i);
			return i;
		}

		public SceneItem BuildMarkerSceneItem(Marker marker)
		{
			var i = sceneItemFactory(marker);
			i.Components.GetOrAdd<MarkerSceneItem>().Marker = marker;
			i.Components.GetOrAdd<CommonMarkerSceneItemData>().Marker = marker;
			SceneItemCreated?.Invoke(i);
			return i;
		}

		public SceneItem BuildAnimationTrackSceneItem(AnimationTrack track)
		{
			var i = sceneItemFactory(track);
			i.Components.GetOrAdd<AnimationTrackSceneItem>().Track = track;
			i.Components.GetOrAdd<CommonAnimationTrackSceneItemData>().Track = track;
			SceneItemCreated?.Invoke(i);
			return i;
		}
	}
}
