using System;
using System.Collections.Generic;
using System.Linq;
using Lime;
using Tangerine.Core.Components;

namespace Tangerine.Core
{
	public class SceneTreeBuilder
	{
		private readonly Func<object, Row> sceneItemFactory;
		public event Action<Row> SceneItemCreated;

		public SceneTreeBuilder(Func<object, Row> sceneItemFactory = null)
		{
			this.sceneItemFactory = sceneItemFactory ?? (o => new Row());
		}

		public Row BuildSceneTreeForNode(Node node)
		{
			var item = GetNodeSceneItem(node);
			int folderIndex = 0;
			int nodeIndex = 0;
			AddSceneItemsForAnimations(item, node);
			AddSceneItemsForAnimatedProperties(item, node);
			BuildFolderTree(item, int.MaxValue, node, ref folderIndex, ref nodeIndex);
			return item;
		}

		void BuildFolderTree(Row parent, int itemCount, Node node, ref int folderIndex, ref int nodeIndex)
		{
			Dictionary<int, Row> boneToSceneItem = null;
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
						boneToSceneItem = boneToSceneItem ?? new Dictionary<int, Row>();
						boneToSceneItem[bone.Index] = nodeSceneItem;
						if (bone.BaseIndex > 0) {
							boneToSceneItem[bone.BaseIndex].Rows.Add(nodeSceneItem);
						} else {
							parent.Rows.Add(nodeSceneItem);
						}
					} else {
						parent.Rows.Add(nodeSceneItem);
					}
				} else if (folder != null) {
					folderIndex++;
					var folderSceneItem = BuildFolderSceneItem(folder);
					if (folderSceneItem.Parent != null) {
						folderSceneItem.Unlink();
					}
					parent.Rows.Add(folderSceneItem);
					BuildFolderTree(folderSceneItem, folder.ItemCount, node, ref folderIndex, ref nodeIndex);
				} else {
					break;
				}
			}
		}

		public Row BuildFolderSceneItem(Folder.Descriptor folder)
		{
			var i = sceneItemFactory(folder);
			i.Components.GetOrAdd<FolderRow>().Folder = folder;
			i.Components.GetOrAdd<CommonFolderRowData>().Folder = folder;
			SceneItemCreated?.Invoke(i);
			return i;
		}

		private Row GetNodeSceneItem(Node node)
		{
			var i = sceneItemFactory(node);
			i.Components.GetOrAdd<NodeRow>().Node = node;
			i.Components.GetOrAdd<CommonNodeRowData>().Node = node;
			if (node is Bone bone) {
				i.Components.GetOrAdd<BoneRow>().Bone = bone;
			}
			SceneItemCreated?.Invoke(i);
			return i;
		}

		private void AddSceneItemsForAnimations(Row parent, Node node)
		{
			var animationComponent = node.Components.Get<AnimationComponent>();
			if (animationComponent != null && animationComponent.Animations.Count > 0) {
				foreach (var animation in animationComponent.Animations) {
					var animationItem = BuildAnimationSceneItem(animation);
					parent.Rows.Add(animationItem);
				}
			}
		}

		private void AddSceneItemsForAnimatedProperties(Row parent, Node node)
		{
			foreach (var animator in node.Animators) {
				var animatorItem = BuildAnimatorSceneItem(animator);
				parent.Rows.Add(animatorItem);
			}
		}

		public Row BuildAnimatorSceneItem(IAnimator animator)
		{
			var i = sceneItemFactory(animator);
			i.Components.GetOrAdd<CommonPropertyRowData>().Animator = animator;
			var component = i.Components.GetOrAdd<PropertyRow>();
			component.Node = (Node)animator.Owner;
			component.Animator = animator;
			SceneItemCreated?.Invoke(i);
			return i;
		}

		public Row BuildAnimationSceneItem(Animation animation)
		{
			var i = sceneItemFactory(animation);
			i.Components.GetOrAdd<AnimationRow>().Animation = animation;
			i.Components.GetOrAdd<CommonAnimationRowData>().Animation = animation;
			foreach (var marker in animation.Markers) {
				i.Rows.Add(BuildMarkerSceneItem(marker));
			}
			foreach (var track in animation.Tracks) {
				i.Rows.Add(BuildAnimationTrackSceneItem(track));
			}
			SceneItemCreated?.Invoke(i);
			return i;
		}

		public Row BuildMarkerSceneItem(Marker marker)
		{
			var i = sceneItemFactory(marker);
			i.Components.GetOrAdd<MarkerRow>().Marker = marker;
			i.Components.GetOrAdd<CommonMarkerRowData>().Marker = marker;
			SceneItemCreated?.Invoke(i);
			return i;
		}

		public Row BuildAnimationTrackSceneItem(AnimationTrack track)
		{
			var i = sceneItemFactory(track);
			i.Components.GetOrAdd<AnimationTrackRow>().Track = track;
			i.Components.GetOrAdd<CommonAnimationTrackRowData>().Track = track;
			SceneItemCreated?.Invoke(i);
			return i;
		}
	}
}
