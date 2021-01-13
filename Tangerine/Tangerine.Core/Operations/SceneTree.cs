using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Lime;
using Tangerine.Core.Components;

namespace Tangerine.Core.Operations
{
	public static class UnlinkSceneItem
	{
		public delegate void NodeUnlinkDelegate(Node node, Node previousParent);
		public static event NodeUnlinkDelegate NodeUnlinked;

		public static void Perform(Row item)
		{
			if (!CanUnlink(item)) {
				return;
			}
			PerformHelper(item);
			RemoveFromList<RowList, Row>.Perform(item.Parent.Rows, item.Parent.Rows.IndexOf(item));
		}

		private static void PerformHelper(Row item)
		{
			if (item.GetNode() is Bone || item.GetFolder() != null) {
				foreach (var i in item.Rows.ToList()) {
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
				RemoveFromCollection<AnimatorCollection, IAnimator>.Perform(owner.Animators, animator);
			} else if (item.TryGetAnimationTrack(out var track)) {
				RemoveFromList<AnimationTrackList, AnimationTrack>.Perform(track.Owner.Tracks, track.Owner.Tracks.IndexOf(track));
			}
		}

		private static bool CanUnlink(Row item)
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
		public static void Perform(IEnumerable<Row> items, MemoryStream stream)
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
					if (!container.Animations.TryFind("_", out var animation)) {
						animation = new Animation { IsCompound = true, Id = "_" };
						container.Animations.Add(animation);
					}
					animation.Tracks.Add(Cloner.Clone(t));
				}
			}
			InternalPersistence.Instance.WriteObject(null, stream, container, Persistence.Format.Json);
		}

		private static void CloneFolder(Frame container, Row item, int indexDelta)
		{
			if (item.GetNode() != null) {
				CloneNode(container, item);
			} else if (item.TryGetFolder(out var f)) {
				var folderClone = Cloner.Clone(f);
				folderClone.Index += indexDelta;
				container.Folders.Add(folderClone);
				foreach (var childRow in item.Rows) {
					CloneFolder(container, childRow, indexDelta);
				}
			}
		}

		private static void CloneNode(Frame container, Row item)
		{
			if (item.GetNode() is Bone) {
				AddBoneHierarchy(container, item, 0);
			} else if (item.TryGetNode(out var node)) {
				var clone = Document.CreateCloneForSerialization(node);
				container.Nodes.Add(clone);
			}
		}

		private static void AddBoneHierarchy(Frame container, Row boneItem, int baseIndex)
		{
			var bone = (Bone)Document.CreateCloneForSerialization(boneItem.Components.Get<BoneRow>().Bone);
			bone.BaseIndex = baseIndex;
			bone.Index = BoneUtils.GenerateNewBoneIndex(container);
			container.Nodes.Add(bone);
			foreach (var i in boneItem.Rows) {
				if (i.Components.Contains<BoneRow>()) {
					AddBoneHierarchy(container, i, bone.Index);
				}
			}
		}
	}

	public static class PasteSceneItemsFromStream
	{
		public static bool Perform(MemoryStream stream, Row parent, int index, out List<Row> pastedItems)
		{
			pastedItems = new List<Row>();
			if (!CanPaste(stream, parent)) {
				return false;
			}
			var container = InternalPersistence.Instance.ReadObject<Frame>(null, stream);
			container.LoadExternalScenes();
			foreach (var a in container.Animators) {
				a.AnimationId = Document.Current.AnimationId;
			}
			// Don't use Document.Current.SceneTreeBuilder since we don't want to store an item in the scene item cache.
			var itemsToPaste = Document.Current.SceneTreeBuilder.BuildSceneTreeForNode(container);
			foreach (var i in itemsToPaste.Rows.ToList()) {
				UnlinkSceneItem.Perform(i);
				LinkSceneItem.Perform(parent, index, i);
				index = parent.Rows.IndexOf(i) + 1;
				pastedItems.Add(i);
				DecorateNodes(i);
			}
			if (container.Animations.TryFind("_", out var animation)) {
				foreach (var track in animation.Tracks.ToList()) {
					animation.Tracks.Remove(track);
					LinkSceneItem.Perform(parent, index++, track);
					pastedItems.Add(Document.Current.GetSceneItemForObject(track));
				}
			}
			return true;
		}

		private static void DecorateNodes(Row item)
		{
			if (item.TryGetNode(out var node)) {
				Document.Current.Decorate(node);
			} else if (item.TryGetFolder(out _)) {
				foreach (var i in item.Rows) {
					DecorateNodes(i);
				}
			}
		}

		public static bool CanPaste(MemoryStream stream, Row parent)
		{
			var container = InternalPersistence.Instance.ReadObject<Frame>(null, stream);
			foreach (var a in container.Animators) {
				a.AnimationId = Document.Current.AnimationId;
			}
			// Don't use Document.Current.SceneTreeBuilder since we don't want to store an item in the scene item cache.
			var rowTree = new SceneTreeBuilder().BuildSceneTreeForNode(container);
			foreach (var i in rowTree.Rows) {
				if (!LinkSceneItem.CanLink(parent, i)) {
					return false;
				}
			}
			if (parent.TryGetNode(out var node) && ClassAttributes<TangerineLockChildrenNodeList>.Get(node.GetType()) != null) {
				return false;
			}
			if (container.DefaultAnimation.Tracks.Count > 0 && parent.GetAnimation() == null) {
				return false;
			}
			return true;
		}
	}

	public static class LinkSceneItem
	{
		public static bool CanLink(Row parent, Row item)
		{
			if (item.TryGetAnimator(out var animator)) {
				return CanLink(parent, animator);
			}
			if (item.TryGetNode(out var node)) {
				return CanLink(parent, node);
			}
			if (item.TryGetFolder(out var folder)) {
				return CanLink(parent, folder) &&
				       EnumerateClosestDescendantNodes(item).All(n => CanLink(parent, n));
			}
			if (item.TryGetAnimationTrack(out var track)) {
				return CanLink(parent, track);
			}
			throw new InvalidOperationException();
		}

		public static bool CanLink(Row parent, IAnimator animator)
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
			if (node.Animators.Any(a => a.TargetPropertyPath == animator.TargetPropertyPath)) {
				// The same animator is already exists.
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

		public static bool CanLink(Row parent, Node node)
		{
			if (parent.GetAnimator() != null) {
				// Can't add anything to an animator.
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

		public static bool CanLink(Row parent, AnimationTrack track)
		{
			return parent.GetAnimation() != null;
		}

		public static bool CanLink(Row parent, Folder.Descriptor folder)
		{
			if (parent.GetAnimator() != null) {
				// Can't add anything to an animator.
				return false;
			}
			var parentNode = parent.TryGetFolder(out var f) ? f.Owner : parent.GetNode();
			return NodeCompositionValidator.CanHaveChildren(parentNode.GetType());
		}

		private static IEnumerable<Node> EnumerateClosestDescendantNodes(Row item)
		{
			if (item.TryGetNode(out var node)) {
				yield return node;
			}
			if (item.TryGetFolder(out _)) {
				foreach (var i in item.Rows) {
					foreach (var n in EnumerateClosestDescendantNodes(i)) {
						yield return n;
					}
				}
			}
		}

		public static Row Perform(Row parent, int index, Node node)
		{
			var item = Document.Current.SceneTreeBuilder.BuildSceneTreeForNode(node);
			Perform(parent, index, item);
			return item;
		}

		public static Row Perform(Row parent, int index, Folder.Descriptor folder)
		{
			var item = Document.Current.SceneTreeBuilder.GetFolderSceneItem(folder);
			Perform(parent, index, item);
			return item;
		}

		private static Row Perform(Row parent, IAnimator animator)
		{
			var item = Document.Current.SceneTreeBuilder.GetAnimatorSceneItem(animator);
			Perform(parent, 0, item);
			return item;
		}

		public static Row Perform(Row parent, int index, AnimationTrack track)
		{
			var item = Document.Current.SceneTreeBuilder.GetAnimationTrackItem(track);
			Perform(parent, index, item);
			return item;
		}

		public static void Perform(Row parent, int index, Row item)
		{
			PerformHelper(parent, index, item, addToSceneTree: true);
		}

		private static void PerformHelper(Row parent, int index, Row item, bool addToSceneTree)
		{
			if (item.TryGetNode(out var node)) {
				LinkNodeItem(parent, index, item, addToSceneTree);
				if (node is Bone) {
					// Insert child bones after animators.
					foreach (var subBone in item.Rows.Where(j => j.GetNode() is Bone)) {
						PerformHelper(item, item.Rows.Count, subBone, addToSceneTree: false);
					}
				}
			} else if (item.TryGetFolder(out _)) {
				LinkFolderItem(parent, index, item, addToSceneTree);
				foreach (var i in item.Rows) {
					PerformHelper(item, item.Rows.Count, i, addToSceneTree: false);
				}
			} else if (item.TryGetAnimator(out _)) {
				LinkAnimatorItem(parent, index, item);
			} else if (item.TryGetAnimationTrack(out _)) {
				LinkAnimationTrackItem(parent, index, item);
			} else {
				throw new InvalidOperationException();
			}
		}

		private static void LinkFolderItem(Row parent, int index, Row folderItem, bool addToSceneTree)
		{
			var folder = folderItem.GetFolder();
			if (!CanLink(parent, folder)) {
				throw new InvalidOperationException();
			}
			while (index < parent.Rows.Count && parent.Rows[index].GetAnimator() != null) {
				// Animators should go first.
				index++;
			}
			if (addToSceneTree) {
				InsertIntoList<RowList, Row>.Perform(parent.Rows, index, folderItem);
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

			InsertIntoList<FolderList, Folder.Descriptor>.Perform(ownerNode.Folders, folderIndex, folderItem.GetFolder());

			if (parent.TryGetFolder(out var parentFolder)) {
				SetProperty.Perform(parentFolder, nameof(Folder.Descriptor.ItemCount), parentFolder.ItemCount + 1);
			}
		}

		private static void LinkNodeItem(Row parent, int index, Row nodeItem, bool addToSceneTree)
		{
			var node = nodeItem.GetNode();
			if (!CanLink(parent, node)) {
				throw new InvalidOperationException();
			}
			while (index < parent.Rows.Count && parent.Rows[index].GetAnimator() != null) {
				// Animators should go first.
				index++;
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
				InsertIntoList<RowList, Row>.Perform(parent.Rows, index, nodeItem);
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

		private static void LinkAnimatorItem(Row parent, int index, Row animatorItem)
		{
			var animator = animatorItem.GetAnimator();
			if (!CanLink(parent, animator)) {
				throw new InvalidOperationException();
			}
			var node = parent.GetNode();
			AddIntoCollection<AnimatorCollection, IAnimator>.Perform(node.Animators, animatorItem.GetAnimator());
			index = node.Animators.ToList().IndexOf(animatorItem.GetAnimator());
			InsertIntoList<RowList, Row>.Perform(parent.Rows, index, animatorItem);
		}

		private static void LinkAnimationTrackItem(Row parent, int index, Row item)
		{
			if (!CanLink(parent, item)) {
				throw new InvalidOperationException();
			}
			var track = item.GetAnimationTrack();
			var animation = parent.GetAnimation();
			AddIntoCollection<AnimationTrackList, AnimationTrack>.Perform(animation.Tracks, track);
			InsertIntoList<RowList, Row>.Perform(parent.Rows, index, item);
		}
	}

	public static class SceneTreeUtils
	{
		public static IEnumerable<Row> EnumerateTopSceneItems(IEnumerable<Row> items)
		{
			foreach (var item in items) {
				if (!items.Any(i => i != item && item.DescendantOf(i))) {
					yield return item;
				}
			}
		}

		public static IEnumerable<Row> EnumerateSelectedTopSceneItems()
		{
			return EnumerateTopSceneItems(Document.Current.SelectedRows());
		}

		public static IEnumerable<Row> TraversePreoder(this Row root, bool skipRoot)
		{
			var stack = new Stack<Row>();
			stack.Push(root);
			while (stack.Count > 0) {
				var item = stack.Pop();
				if (!skipRoot || item != root) {
					yield return item;
				}
				for (var i = item.Rows.Count - 1; i >= 0; i--) {
					stack.Push(item.Rows[i]);
				}
			}
		}

		public static void GetSceneItemLinkLocation(
			out Row parent, out int index,
			bool aboveFocused = true,
			Func<Row, bool> raiseThroughHierarchyPredicate = null)
		{
			var focusedItem = Document.Current.RecentlySelectedSceneItem();
			if (focusedItem == null) {
				parent = Document.Current.GetSceneItemForObject(Document.Current.Container);
				index = 0;
				return;
			}
			parent = focusedItem.Parent;
			index = parent.Rows.IndexOf(focusedItem);
			if (!aboveFocused) {
				index++;
			}
			while (raiseThroughHierarchyPredicate?.Invoke(parent) ?? false) {
				index = parent.Parent.Rows.IndexOf(parent);
				if (!aboveFocused) {
					index++;
				}
				parent = parent.Parent;
			}
		}

		public static Row GetOwnerNodeSceneItem(Row item)
		{
			Node ownerNode = null;
			if (item.GetNode() is Bone bone) {
				ownerNode = bone.Parent;
			} else if (item.TryGetNode(out var node)) {
				ownerNode = node;
			} else if (item.TryGetFolder(out var folder)) {
				ownerNode = folder.Owner;
			} else if (item.TryGetAnimator(out var animator)) {
				ownerNode = (Node) animator.Owner;
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
