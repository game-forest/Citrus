using System;
using System.Collections.Generic;
using System.Linq;
using Lime;

namespace Tangerine.Core
{
	public static class NodeExtensions
	{
		public static NodeEditorState EditorState(this Node node)
		{
			if (node.UserData == null) {
				node.UserData = new NodeEditorState(node);
			}
			return (NodeEditorState)node.UserData;
		}

		public static int CollectionIndex(this Node node)
		{
			return node.Parent.Nodes.IndexOf(node);
		}

		public static Folder RootFolder(this Node node)
		{
			return EditorState(node).RootFolder;
		}

		public static void SyncFolderDescriptorsAndNodes(this Node node)
		{
			EditorState(node).RootFolder.SyncDescriptorsAndNodes(node);
		}

		public static bool IsCopyPasteAllowed(this Node node)
		{
			return NodeCompositionValidator.IsCopyPasteAllowed(node.GetType());
		}

		public static string GetRelativePath(this Node node)
		{
			var root = node.GetRoot();
			var bundlePath = root.Components.Get<Node.AssetBundlePathComponent>()?.Path;
			var nodePath = Node.ResolveScenePath(node.ContentsPath);
			return
				nodePath == null ?
					(root == node ? bundlePath : $"{bundlePath} [{node.ToString()}]") :
					$"{nodePath} [{node.ToString()}]";
		}

		public static IEnumerable<Node> DescendantsSkippingNamesakeAnimationOwners(this Node node, string animationId) =>
			new DescendantsSkippingNamesakeAnimationOwnersEnumerable(node, animationId);

		public static int RemoveDanglingAnimators(this Node self)
		{
			var node = self;
			var scope = new Dictionary<string, int>();
			while (node.Parent != null) {
				node = node.Parent;
				FillAnimationsScope(node, scope);
			}
			return self.RemoveDanglingAnimators(scope);
		}

		private static int RemoveDanglingAnimators(this Node self, Dictionary<string, int> scope)
		{
			int result = 0;
			FillAnimationsScope(self, scope);
			var animators = self.Animators.ToList();
			foreach (var animator in animators) {
				if (animator.AnimationId == null || scope.TryGetValue(animator.AnimationId, out var count) && count > 0) {
					continue;
				}
				Operations.RemoveFromCollection<AnimatorCollection, IAnimator>.Perform(self.Animators, animator);
				result += 1;
			}
			foreach (var node in self.Nodes) {
				result += node.RemoveDanglingAnimators(scope);
			}
			RestoreAnimationScope(self, scope);
			return result;
		}

		private static void FillAnimationsScope(Node node, Dictionary<string, int> scope)
		{
			foreach (var animation in node.Animations) {
				if (animation.IsLegacy || animation.Id == null) {
					continue;
				}
				if (scope.TryGetValue(animation.Id, out var count)) {
					scope[animation.Id] = count + 1;
				}
				else {
					scope[animation.Id] = 1;
				}
			}
		}

		private static void RestoreAnimationScope(Node node, Dictionary<string, int> scope)
		{
			foreach (var animation in node.Animations) {
				if (animation.IsLegacy || animation.Id == null) {
					continue;
				}
				scope[animation.Id] -= 1;
			}
		}
	}
}
