using System.Collections.Generic;
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
	}
}
