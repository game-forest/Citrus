using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Lime;
using Tangerine.Core.Components;

namespace Tangerine.Core.Operations
{
	public struct SceneTreeIndex
	{
		public int Value { get; private set; }

		public SceneTreeIndex(int value) => Value = value;

		public static SceneTreeIndex ClampIndex(SceneItem sceneTree, SceneItem itemToLink, SceneTreeIndex index)
		{
			int minIndex, maxIndex;
			if (itemToLink.TryGetAnimation(out _)) {
				minIndex = FromAnimationIndex(sceneTree, 0).Value;
				maxIndex = FromAnimatorIndex(sceneTree, 0).Value;
			} else if (itemToLink.TryGetAnimator(out _)) {
				minIndex = FromAnimatorIndex(sceneTree, 0).Value;
				maxIndex = FromNodeOrFolderIndex(sceneTree, 0).Value;
			} else if (itemToLink.TryGetNode(out _) || itemToLink.TryGetFolder(out _)) {
				minIndex = FromNodeOrFolderIndex(sceneTree, 0).Value;
				maxIndex = sceneTree.SceneItems.Count;
			} else if (itemToLink.TryGetMarker(out _)) {
				minIndex = FromMarkerIndex(sceneTree, 0).Value;
				maxIndex = FromAnimationTrackIndex(sceneTree, 0).Value;
			} else if (itemToLink.TryGetAnimationTrack(out _)) {
				minIndex = FromAnimationTrackIndex(sceneTree, 0).Value;
				maxIndex = sceneTree.SceneItems.Count;
			} else {
				throw new InvalidOperationException();
			}
			return new SceneTreeIndex(Math.Clamp(index.Value, minIndex, maxIndex));
		}

		public static SceneTreeIndex FromAnimationIndex(SceneItem sceneTree, int animationIndex)
		{
			return new SceneTreeIndex(animationIndex);
		}

		public int ToAnimationIndex(SceneItem sceneTree) => Value;

		public static SceneTreeIndex FromMarkerIndex(SceneItem sceneTree, int markerIndex)
		{
			return new SceneTreeIndex(markerIndex);
		}

		public int ToMarkerIndex(SceneItem sceneTree) => Value;

		public static SceneTreeIndex FromAnimationTrackIndex(SceneItem sceneTree, int animationTrackIndex)
		{
			return new SceneTreeIndex(animationTrackIndex + sceneTree.SceneItems.Count(i => i.TryGetMarker(out _)));
		}

		public int ToAnimationTrackIndex(SceneItem sceneTree)
		{
			return Value - sceneTree.SceneItems.Count(i => i.TryGetMarker(out _));
		}

		public static SceneTreeIndex FromAnimatorIndex(SceneItem sceneTree, int animatorIndex)
		{
			return new SceneTreeIndex(animatorIndex + sceneTree.SceneItems.Count(i => i.TryGetAnimation(out _)));
		}

		public int ToAnimatorIndex(SceneItem sceneTree)
		{
			return Value - sceneTree.SceneItems.Count(i => i.TryGetAnimation(out _));
		}

		public static SceneTreeIndex FromNodeOrFolderIndex(SceneItem sceneTree, int nodeIndex)
		{
			return new SceneTreeIndex(
				nodeIndex + sceneTree.SceneItems.Count(i => i.TryGetAnimation(out _) || i.TryGetAnimator(out _))
			);
		}

		public int ToNodeOrFolderIndex(SceneItem sceneTree)
		{
			return Value - sceneTree.SceneItems.Count(i => i.TryGetAnimation(out _) || i.TryGetAnimator(out _));
		}
	}
	public static class UnlinkSceneItem
	{
		public delegate void NodeUnlinkDelegate(Node node, Node previousParent);
		public static event NodeUnlinkDelegate NodeUnlinked;

		public static void Perform(SceneItem item)
		{
			if (!CanUnlink(item)) {
				return;
			}
			PerformHelper(item);
			RemoveFromList<SceneItemList, SceneItem>.Perform(
				item.Parent.SceneItems, item.Parent.SceneItems.IndexOf(item)
			);
		}

		private static void PerformHelper(SceneItem item)
		{
			if (item.GetNode() is Bone || item.GetFolder() != null) {
				foreach (var i in item.SceneItems.ToList()) {
					if (!i.TryGetAnimator(out _)) {
						PerformHelper(i);
					}
				}
			}
			if (item.TryGetNode(out var node)) {
				var ownerItem = SceneTreeUtils.GetOwnerNodeSceneItem(item.Parent);
				// Decrease the parent folder item count
				var parent = item.Parent;
				while (parent.GetNode() is Bone) {
					parent = parent.Parent;
				}
				if (parent.TryGetFolder(out var parentFolder)) {
					SetProperty.Perform(parentFolder, nameof(Folder.Descriptor.ItemCount), parentFolder.ItemCount - 1);
				}
				// Adjust folders indices.
				var owner = ownerItem.GetNode();
				var foldersToBeShifted = ownerItem.TraversePreoder(skipRoot: true)
					.SkipWhile(i => i != item)
					.Where(i => i.TryGetFolder(out var f) && f.Owner == owner).Select(i => i.GetFolder());
				foreach (var f in foldersToBeShifted) {
					SetProperty.Perform(f, nameof(Folder.Descriptor.Index), f.Index - 1);
				}
				RemoveFromList<NodeList, Node>.Perform(node.Parent.Nodes, node.CollectionIndex());
				if (node is Bone bone) {
					SetProperty.Perform(bone, nameof(Bone.BaseIndex), 0);
				}
				NodeUnlinked?.Invoke(node, owner);
			} else if (item.TryGetFolder(out var folder)) {
				var owner = folder.Owner;
				var parent = item.Parent;
				if (parent.TryGetFolder(out var parentFolder)) {
					SetProperty.Perform(parentFolder, nameof(Folder.Descriptor.ItemCount), parentFolder.ItemCount - 1);
				}
				RemoveFromList<FolderList, Folder.Descriptor>.Perform(owner.Folders, folder.CollectionIndex());
			} else if (item.TryGetAnimator(out var animator)) {
				var owner = (Node)animator.Owner;
				RemoveFromList<AnimatorList, IAnimator>.Perform(owner.Animators, animator);
			} else if (item.TryGetAnimation(out var animation)) {
				RemoveFromList<AnimationCollection, Animation>.Perform(
					animation.Owner.Animations, animation.Owner.Animations.IndexOf(animation)
				);
			} else if (item.TryGetAnimationTrack(out var track)) {
				RemoveFromList<AnimationTrackList, AnimationTrack>.Perform(
					track.Owner.Tracks, track.Owner.Tracks.IndexOf(track)
				);
			} else if (item.TryGetMarker(out var marker)) {
				RemoveFromList<MarkerList, Marker>.Perform(marker.Owner.Markers, marker.Owner.Markers.IndexOf(marker));
			} else {
				throw new InvalidOperationException();
			}
		}

		private static bool CanUnlink(SceneItem item)
		{
			// TODO: temporarily ignore TangerineLockChildrenNodeListAttribute
			// because with it DistortionMeshProcessor.RestorePointsIfNeeded is unable to rebuild points
			return true;
			if (item.TryGetNode(out var node)) {
				if (ClassAttributes<TangerineLockChildrenNodeList>.Get(node.Parent.GetType()) != null) {
					return false;
				}
			}
			return true;
		}
	}

	public static class CopySceneItemsToStream
	{
		public static readonly string AnimationTracksContainerAnimationId = "b3598b8c-acde-43be-b1a0-61721cfce355";

		public static void Perform(IEnumerable<SceneItem> items, MemoryStream stream)
		{
			var container = new Frame();
			var topItems = SceneTreeUtils.EnumerateTopSceneItems(items).ToList();
			foreach (var item in topItems) {
				if (item.TryGetNode(out _)) {
					CloneNode(container, item);
				} else if (item.TryGetFolder(out var f)) {
					var i = container.Nodes.Count - f.Index;
					CloneFolder(container, item, i);
				} else if (item.TryGetAnimator(out var a)) {
					container.Animators.Add(Cloner.Clone(a));
				} else if (item.TryGetAnimationTrack(out var t)) {
					if (!container.Animations.TryFind(AnimationTracksContainerAnimationId, out var animation)) {
						animation = new Animation { IsCompound = true, Id = AnimationTracksContainerAnimationId };
						container.Animations.Add(animation);
					}
					animation.Tracks.Add(Cloner.Clone(t));
				}
			}
			InternalPersistence.Instance.WriteToStream(null, stream, container, Persistence.Format.Json);
		}

		private static void CloneFolder(Frame container, SceneItem item, int indexDelta)
		{
			if (item.GetNode() != null) {
				CloneNode(container, item);
			} else if (item.TryGetFolder(out var f)) {
				var folderClone = Cloner.Clone(f);
				folderClone.Index += indexDelta;
				container.Folders.Add(folderClone);
				foreach (var childItem in item.SceneItems) {
					CloneFolder(container, childItem, indexDelta);
				}
			}
		}

		private static void CloneNode(Frame container, SceneItem item)
		{
			if (item.GetNode() is Bone) {
				AddBoneHierarchy(container, item, 0);
			} else if (item.TryGetNode(out var node)) {
				var clone = Document.CreateCloneForSerialization(node);
				container.Nodes.Add(clone);
			}
		}

		private static void AddBoneHierarchy(Frame container, SceneItem boneItem, int baseIndex)
		{
			var bone = (Bone)Document.CreateCloneForSerialization(boneItem.Components.Get<BoneSceneItem>().Bone);
			bone.BaseIndex = baseIndex;
			bone.Index = BoneUtils.GenerateNewBoneIndex(container);
			container.Nodes.Add(bone);
			foreach (var i in boneItem.SceneItems) {
				if (i.Components.Contains<BoneSceneItem>()) {
					AddBoneHierarchy(container, i, bone.Index);
				}
			}
		}
	}

	public static class LinkSceneItem
	{
		public static bool CanLink(SceneItem parent, SceneItem item)
		{
			if (item.TryGetAnimator(out var animator)) {
				return CanLink(parent, animator);
			}
			if (item.TryGetNode(out var node)) {
				return CanLink(parent, node);
			}
			if (item.TryGetFolder(out var folder)) {
				return CanLink(parent, folder)
					&& EnumerateClosestDescendantNodes(item).All(n => CanLink(parent, n));
			}
			if (item.TryGetAnimation(out var animation)) {
				return CanLink(parent, animation);
			}
			if (item.TryGetAnimationTrack(out var track)) {
				return CanLink(parent, track);
			}
			if (item.TryGetMarker(out var marker)) {
				return CanLink(parent, marker);
			}
			throw new InvalidOperationException();
		}

		private static bool CanLink(SceneItem parent, IAnimator animator)
		{
			if (parent.GetAnimator() != null) {
				// Can't add anything to an animator.
				return false;
			}
			var node = parent.GetNode();
			if (node == null) {
				// Can put the animator only into the node.
				return false;
			}
			if (parent.Parent == null) {
				// Can't drag the animator into the root item.
				return false;
			}
			var p = AnimationUtils.GetPropertyByPath(node, animator.TargetPropertyPath);
			if (p.Animable == null) {
				// The given property doesn't exist for the node.
				return false;
			}
			if (p.PropertyData.Info.PropertyType != animator.ValueType) {
				// Property type mismatch.
				return false;
			}
			return true;
		}

		public static bool CanLink(SceneItem parent, Node node)
		{
			if (!parent.TryGetNode(out _) && !parent.TryGetFolder(out _)) {
				return false;
			}
			var parentNode = parent.TryGetFolder(out var f) ? f.Owner : parent.GetNode();
			if (parentNode is Bone) {
				return node is Bone;
			}
			if (!NodeCompositionValidator.CanHaveChildren(parentNode.GetType())) {
				return false;
			}
			return NodeCompositionValidator.Validate(parentNode.GetType(), node.GetType());
		}

		private static bool CanLink(SceneItem parent, Animation animation)
		{
			return parent.TryGetNode(out var node) && node.Animations.All(i => i.Id != animation.Id);
		}

		private static bool CanLink(SceneItem parent, AnimationTrack track)
		{
			return parent.GetAnimation() != null;
		}

		private static bool CanLink(SceneItem parent, Marker marker)
		{
			return parent.GetAnimation() != null && parent.GetAnimation().Markers.All(i => i.Frame != marker.Frame);
		}

		public static bool CanLink(SceneItem parent, Folder.Descriptor folder)
		{
			if (parent.GetAnimator() != null) {
				// Can't add anything to an animator.
				return false;
			}
			var parentNode = parent.TryGetFolder(out var f) ? f.Owner : parent.GetNode();
			return NodeCompositionValidator.CanHaveChildren(parentNode.GetType());
		}

		private static IEnumerable<Node> EnumerateClosestDescendantNodes(SceneItem item)
		{
			if (item.TryGetNode(out var node)) {
				yield return node;
			}
			if (item.TryGetFolder(out _)) {
				foreach (var i in item.SceneItems) {
					foreach (var n in EnumerateClosestDescendantNodes(i)) {
						yield return n;
					}
				}
			}
		}

		public static SceneItem Perform(SceneItem parent, SceneTreeIndex index, Node node, bool clampIndex = true)
		{
			var item = Document.Current.SceneTreeBuilder.BuildSceneTreeForNode(node);
			Perform(parent, index, item, clampIndex);
			return item;
		}

		public static SceneItem Perform(
			SceneItem parent, SceneTreeIndex index, Folder.Descriptor folder, bool clampIndex = true
		) {
			var item = Document.Current.SceneTreeBuilder.BuildFolderSceneItem(folder);
			Perform(parent, index, item, clampIndex);
			return item;
		}

		private static SceneItem Perform(
			SceneItem parent, SceneTreeIndex index, IAnimator animator, bool clampIndex = true
		) {
			var item = Document.Current.SceneTreeBuilder.BuildAnimatorSceneItem(animator);
			Perform(parent, index, item, clampIndex);
			return item;
		}

		public static SceneItem Perform(
			SceneItem parent, SceneTreeIndex index, Animation animation, bool clampIndex = true
		) {
			var item = Document.Current.SceneTreeBuilder.BuildAnimationSceneItem(animation);
			Perform(parent, index, item, clampIndex);
			return item;
		}

		public static SceneItem Perform(
			SceneItem parent, SceneTreeIndex index, AnimationTrack track, bool clampIndex = true
		) {
			var item = Document.Current.SceneTreeBuilder.BuildAnimationTrackSceneItem(track);
			Perform(parent, index, item, clampIndex);
			return item;
		}

		public static void Perform(SceneItem parent, SceneTreeIndex index, SceneItem item, bool clampIndex = true)
		{
			PerformHelper(parent, index, item, addToSceneTree: true, clampIndex);
		}

		private static void PerformHelper(
			SceneItem parent, SceneTreeIndex index, SceneItem item, bool addToSceneTree, bool clampIndex
		) {
			if (clampIndex) {
				index = SceneTreeIndex.ClampIndex(parent, item, index);
			} else if (SceneTreeIndex.ClampIndex(parent, item, index).Value != index.Value) {
				throw new InvalidOperationException("Attempt to link an scene tree item onto a wrong index");
			}
			if (item.TryGetNode(out var node)) {
				LinkNodeItem(parent, index, item, addToSceneTree);
				if (node is Bone) {
					// Insert child bones after animators.
					foreach (var subBone in item.SceneItems.Where(j => j.GetNode() is Bone)) {
						PerformHelper(
							item,
							new SceneTreeIndex(item.SceneItems.Count),
							subBone,
							addToSceneTree: false,
							clampIndex
						);
					}
				}
			} else if (item.TryGetFolder(out _)) {
				LinkFolderItem(parent, index, item, addToSceneTree);
				foreach (var i in item.SceneItems) {
					PerformHelper(
						item,
						new SceneTreeIndex(item.SceneItems.Count),
						i,
						addToSceneTree: false,
						clampIndex
					);
				}
			} else if (item.TryGetAnimator(out _)) {
				LinkAnimatorItem(parent, index, item);
			} else if (item.TryGetAnimation(out _)) {
				LinkAnimationItem(parent, index, item);
			} else if (item.TryGetAnimationTrack(out _)) {
				LinkAnimationTrackItem(parent, index, item);
			} else if (item.TryGetMarker(out _)) {
				LinkMarkerItem(parent, index, item);
			} else {
				throw new InvalidOperationException();
			}
		}

		private static void LinkFolderItem(
			SceneItem parent, SceneTreeIndex index, SceneItem folderItem, bool addToSceneTree
		) {
			var folder = folderItem.GetFolder();
			if (!CanLink(parent, folder)) {
				throw new InvalidOperationException();
			}
			if (addToSceneTree) {
				InsertIntoList<SceneItemList, SceneItem>.Perform(parent.SceneItems, index.Value, folderItem);
			}
			var ownerNodeItem = SceneTreeUtils.GetOwnerNodeSceneItem(parent);
			var ownerNode = ownerNodeItem.GetNode();

			var nodeIndex =
				ownerNodeItem.TraversePreoder(skipRoot: true).TakeWhile(i => i != folderItem)
				.LastOrDefault(i => i.TryGetNode(out var n) && n.Parent == ownerNode)
				?.GetNode().CollectionIndex() + 1 ?? 0;

			SetProperty.Perform(folderItem.GetFolder(), nameof(Folder.Descriptor.Index), nodeIndex);
			SetProperty.Perform(folderItem.GetFolder(), nameof(Folder.Descriptor.ItemCount), 0);

			var folderIndex =
				ownerNodeItem.TraversePreoder(skipRoot: true).TakeWhile(i => i != folderItem)
					.LastOrDefault(i => i.TryGetFolder(out var f) && f.Owner == ownerNode)
					?.GetFolder().CollectionIndex() + 1 ?? 0;

			InsertIntoList<FolderList, Folder.Descriptor>.Perform(
				ownerNode.Folders, folderIndex, folderItem.GetFolder()
			);

			if (parent.TryGetFolder(out var parentFolder)) {
				SetProperty.Perform(parentFolder, nameof(Folder.Descriptor.ItemCount), parentFolder.ItemCount + 1);
			}
		}

		private static void LinkNodeItem(
			SceneItem parent, SceneTreeIndex index, SceneItem nodeItem, bool addToSceneTree
		) {
			var node = nodeItem.GetNode();
			if (!CanLink(parent, node)) {
				throw new InvalidOperationException();
			}
			var ownerNodeItem = SceneTreeUtils.GetOwnerNodeSceneItem(parent);
			var ownerNode = ownerNodeItem.GetNode();
			if (nodeItem.GetNode() is Bone bone) {
				if (parent.GetNode() is Bone parentBone) {
					SetProperty.Perform(bone, nameof(Bone.BaseIndex), parentBone.Index);
				}
				var newIndex = BoneUtils.GenerateNewBoneIndex(ownerNode);
				SetProperty.Perform(bone, nameof(Bone.Index), newIndex);
			}
			if (addToSceneTree) {
				InsertIntoList<SceneItemList, SceneItem>.Perform(parent.SceneItems, index.Value, nodeItem);
			}
			var nodeIndex =
				ownerNodeItem.TraversePreoder(skipRoot: true).TakeWhile(i => i != nodeItem)
				.LastOrDefault(i => i.TryGetNode(out var n) && n.Parent == ownerNode)
				?.GetNode().CollectionIndex() + 1 ?? 0;

			InsertIntoList<NodeList, Node>.Perform(ownerNode.Nodes, nodeIndex, nodeItem.GetNode());
#if DEBUG
			if (nodeItem.GetNode() is Bone bone2) {
				AssertBoneOrder(bone2);
			}
#endif
			// Adjust folders indices.
			var foldersToBeShifted = ownerNodeItem.TraversePreoder(skipRoot: true)
				.SkipWhile(i => i != nodeItem)
				.Where(i => i.TryGetFolder(out var f) && f.Owner == ownerNode)
				.Select(i => i.GetFolder());
			foreach (var f in foldersToBeShifted) {
				SetProperty.Perform(f, nameof(Folder.Descriptor.Index), f.Index + 1);
			}
			// Increase the parent folder item count.
			while (parent.GetNode() is Bone) {
				parent = parent.Parent;
			}
			if (parent.TryGetFolder(out var parentFolder)) {
				SetProperty.Perform(parentFolder, nameof(Folder.Descriptor.ItemCount), parentFolder.ItemCount + 1);
			}
		}

		private static void AssertBoneOrder(Bone bone)
		{
			if (bone.BaseIndex != 0) {
				var nodes = bone.Parent.Nodes;
				var baseBone = nodes.First(i => (i is Bone b) && b.Index == bone.BaseIndex);
				if (nodes.IndexOf(baseBone) > nodes.IndexOf(bone)) {
					throw new InvalidOperationException($"Bone {bone} added on the place before its base bone");
				}
			}
		}

		private static void LinkAnimatorItem(SceneItem parent, SceneTreeIndex index, SceneItem animatorItem)
		{
			var animator = animatorItem.GetAnimator();
			if (!CanLink(parent, animator)) {
				throw new InvalidOperationException();
			}
			var existedAnimatorItem = parent.SceneItems.FirstOrDefault(i =>
				i.TryGetAnimator(out var a)
				&& a.TargetPropertyPath == animator.TargetPropertyPath
				&& a.AnimationId == animator.AnimationId
			);
			if (existedAnimatorItem != null) {
				if (index.Value > new SceneTreeIndex(parent.SceneItems.IndexOf(existedAnimatorItem)).Value) {
					index = new SceneTreeIndex(index.Value - 1);
				}
				UnlinkSceneItem.Perform(existedAnimatorItem);
			}
			var node = parent.GetNode();
			InsertIntoList<AnimatorList, IAnimator>.Perform(node.Animators, index.ToAnimatorIndex(parent), animator);
			SetProperty.Perform(animatorItem.Components.Get<AnimatorSceneItem>(), nameof(AnimatorSceneItem.Node), node);
			InsertIntoList<SceneItemList, SceneItem>.Perform(parent.SceneItems, index.Value, animatorItem);
			if (existedAnimatorItem != null) {
				var existedAnimator = existedAnimatorItem.GetAnimator();
				foreach (var key in existedAnimator.Keys) {
					if (animator.Keys.All(i => i.Frame != key.Frame)) {
						SetKeyframe.Perform(animator, Document.Current.Animation, key.Clone());
					}
				}
			}
		}

		private static void LinkAnimationItem(SceneItem parent, SceneTreeIndex index, SceneItem item)
		{
			if (!CanLink(parent, item)) {
				throw new InvalidOperationException();
			}
			var animation = item.GetAnimation();
			var node = parent.GetNode();
			bool hasLowPriorityAnimation = node.Ancestors.Any(n => n.Animations.Any(a => a.Id == animation.Id));
			if (hasLowPriorityAnimation) {
				DelegateOperation.Perform(redo: null, undo: AnimatorCollectionChanged, isChangingDocument: false);
			}
			InsertIntoList<AnimationCollection, Animation>.Perform(
				node.Animations, index.ToAnimationIndex(parent), animation
			);
			InsertIntoList<SceneItemList, SceneItem>.Perform(parent.SceneItems, index.Value, item);
			if (hasLowPriorityAnimation) {
				DelegateOperation.Perform(redo: AnimatorCollectionChanged, undo: null, isChangingDocument: false);
			}
			void AnimatorCollectionChanged() => ((IAnimationHost)node).OnAnimatorCollectionChanged();
		}

		private static void LinkAnimationTrackItem(SceneItem parent, SceneTreeIndex index, SceneItem item)
		{
			if (!CanLink(parent, item)) {
				throw new InvalidOperationException();
			}
			var track = item.GetAnimationTrack();
			var animation = parent.GetAnimation();
			InsertIntoList<AnimationTrackList, AnimationTrack>.Perform(
				animation.Tracks,
				index.ToAnimationTrackIndex(parent),
				track
			);
			InsertIntoList<SceneItemList, SceneItem>.Perform(parent.SceneItems, index.Value, item);
		}

		private static void LinkMarkerItem(SceneItem parent, SceneTreeIndex index, SceneItem item)
		{
			if (!CanLink(parent, item)) {
				throw new InvalidOperationException();
			}
			var marker = item.GetMarker();
			var animation = parent.GetAnimation();
			InsertIntoList<MarkerList, Marker>.Perform(animation.Markers, index.ToMarkerIndex(parent), marker);
			InsertIntoList<SceneItemList, SceneItem>.Perform(parent.SceneItems, index.Value, item);
		}
	}

	public static class SceneTreeUtils
	{
		public static IEnumerable<SceneItem> EnumerateTopSceneItems(IEnumerable<SceneItem> items)
		{
			foreach (var item in items) {
				if (!items.Any(i => i != item && item.DescendantOf(i))) {
					yield return item;
				}
			}
		}

		public static IEnumerable<SceneItem> EnumerateSelectedTopSceneItems()
		{
			return EnumerateTopSceneItems(Document.Current.SelectedSceneItems());
		}

		public static IEnumerable<SceneItem> TraversePreoder(this SceneItem root, bool skipRoot)
		{
			var stack = new Stack<SceneItem>();
			stack.Push(root);
			while (stack.Count > 0) {
				var item = stack.Pop();
				if (!skipRoot || item != root) {
					yield return item;
				}
				for (var i = item.SceneItems.Count - 1; i >= 0; i--) {
					stack.Push(item.SceneItems[i]);
				}
			}
		}

		public static bool TryGetSceneItemLinkLocation(
			out SceneItem parent,
			out SceneTreeIndex index,
			Type insertingType,
			bool aboveFocused = true,
			Func<SceneItem, bool> raiseThroughHierarchyPredicate = null
		) {
			var focusedItem = Document.Current.RecentlySelectedSceneItem();
			if (focusedItem == null) {
				parent = Document.Current.GetSceneItemForObject(Document.Current.Container);
				index = new SceneTreeIndex(0);
			} else {
				bool isAnimatorItem = insertingType.IsAssignableFrom(typeof(IAnimator));
				bool isAnimatorFocused = focusedItem.TryGetAnimator(out _);
				if (!isAnimatorItem && isAnimatorFocused) {
					parent = focusedItem.Parent.Parent;
					index = new SceneTreeIndex(parent.SceneItems.IndexOf(focusedItem.Parent));
				} else if (isAnimatorItem && !isAnimatorFocused) {
					parent = focusedItem;
					index = new SceneTreeIndex(parent.SceneItems.Count);
				} else {
					parent = focusedItem.Parent;
					index = new SceneTreeIndex(parent.SceneItems.IndexOf(focusedItem));
				}
				if (!aboveFocused) {
					index = new SceneTreeIndex(index.Value + 1);
				}
			}
			if (raiseThroughHierarchyPredicate != null) {
				while (raiseThroughHierarchyPredicate.Invoke(parent)) {
					if (parent.Parent == null) {
						return false;
					}
					index = new SceneTreeIndex(parent.Parent.SceneItems.IndexOf(parent));
					if (!aboveFocused) {
						index = new SceneTreeIndex(index.Value + 1);
					}
					parent = parent.Parent;
				}
			}
			return true;
		}

		public static SceneItem GetOwnerNodeSceneItem(SceneItem item)
		{
			Node ownerNode = null;
			if (item.GetNode() is Bone bone) {
				ownerNode = bone.Parent;
			} else if (item.TryGetNode(out var node)) {
				ownerNode = node;
			} else if (item.TryGetFolder(out var folder)) {
				ownerNode = folder.Owner;
			} else if (item.TryGetAnimator(out var animator)) {
				ownerNode = (Node)animator.Owner;
			} else {
				throw new InvalidOperationException();
			}
			for (var i = item; i != null; i = i.Parent) {
				if (i.TryGetNode(out var node) && node == ownerNode) {
					return i;
				}
			}
			throw new InvalidOperationException();
		}

		public static int CollectionIndex(this Folder.Descriptor folder)
		{
			return folder.Owner.Folders.IndexOf(folder);
		}
	}
}
