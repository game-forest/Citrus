using System;
using System.Collections.Generic;
using Lime;
using System.Linq;

namespace Tangerine.Core.Operations
{
	public static class NavigateToNode
	{
		public static Node Perform(Node node, bool enterInto, bool turnOnInspectRootNodeIfNeeded = false)
		{
			if (node.GetRoot() != Document.Current.RootNode) {
				throw new InvalidOperationException();
			}

			var path = new Stack<int>();
			Node externalScene;
			var inspectRootNode = false;
			if (node.Parent == null) {
				enterInto = true;
				inspectRootNode = true;
			}
			if (!enterInto) {
				path.Push(node.Parent.Nodes.IndexOf(node));
				externalScene = node.Parent;
			} else {
				externalScene = node;
			}
			while (
				externalScene != null &&
				externalScene != Document.Current.RootNode &&
				externalScene != Document.Current.RootNodeUnwrapped &&
				string.IsNullOrEmpty(externalScene.ContentsPath)
			) {
				path.Push(externalScene.Parent.Nodes.IndexOf(externalScene));
				externalScene = externalScene.Parent;
			}
			if (externalScene == Document.Current.RootNode || externalScene == Document.Current.RootNodeUnwrapped) {
				externalScene = null;
			}

			if (externalScene != null) {
				var currentScenePath = Document.Current.Path;
				var externalSceneDocument = Project.Current.OpenDocument(externalScene.ContentsPath);
				externalSceneDocument.SceneNavigatedFrom = currentScenePath;
				node = externalSceneDocument.RootNodeUnwrapped;
				foreach (var i in path) {
					node = node.Nodes[i];
				}
			} else {
				// Ensure TangerineFlags.DisplayContent set by EnterNode is cleared
				var container = Document.Current.Container;
				Document.Current.History.DoTransaction(() => {
					SetProperty.Perform(container, nameof(Node.TangerineFlags), container.TangerineFlags & ~TangerineFlags.DisplayContent, isChangingDocument: false);
				});
			}

			if (enterInto) {
				Document.Current.History.DoTransaction(() => {
					EnterNode.Perform(node, selectFirstNode: true);
				});
			} else {
				Document.Current.History.DoTransaction(() => {
					if (node.Parent == null) {
						EnterNode.Perform(Document.Current.RootNode, selectFirstNode: true);
					} else if (EnterNode.Perform(node.Parent, selectFirstNode: false)) {
						SelectNode.Perform(node);
					}
				});
			}
			if (turnOnInspectRootNodeIfNeeded) {
				Document.Current.InspectRootNode = inspectRootNode;
			}
			return node;
		}
	}

	public interface ISetContainer : IOperation { }

	public static class EnterNode
	{
		private sealed class SetContainer : SetProperty, ISetContainer
		{
			public SetContainer(Node value) : base(Document.Current, nameof(Document.Container), value, false) { }
		}

		public static bool Perform(Node container, bool selectFirstNode = true)
		{
			if (!NodeCompositionValidator.CanHaveChildren(container.GetType())) {
				return false;
			}
			if (!string.IsNullOrEmpty(container.ContentsPath)) {
				if (Project.Current.DocumentExists(container.ContentsPath)) {
					OpenExternalScene(container.ContentsPath);
				} else {
					return false;
				}
			} else {
				ChangeContainer(container, selectFirstNode);
				SetProperty.Perform(container, nameof(Node.TangerineFlags), container.TangerineFlags | TangerineFlags.DisplayContent, isChangingDocument: false);
			}
			return true;
		}

		private static void OpenExternalScene(string path)
		{
			var sceneNavigatedFrom = Document.Current.Path;
			var doc = Project.Current.OpenDocument(path);
			doc.SceneNavigatedFrom = sceneNavigatedFrom;
		}

		private static void ChangeContainer(Node container, bool selectFirstNode)
		{
			ClearRowSelection.Perform();
			DocumentHistory.Current.Perform(new SetContainer(container));
			if (Document.Current.Animation.IsLegacy) {
				SetProperty.Perform(Document.Current, nameof(Document.Animation), container.DefaultAnimation, false);
			}
			if (selectFirstNode && container.Nodes.Count > 0) {
				SelectNode.Perform(container.Nodes[0]);
			}
		}
	}

	public static class LeaveNode
	{
		public static void Perform()
		{
			var doc = Document.Current;
			if (doc.Container == doc.RootNode) {
				var path = doc.SceneNavigatedFrom;
				if (path != null) {
					var document = Project.Current.Documents.FirstOrDefault(i => i.Path == path);
					if (document == null) {
						document = Project.Current.OpenDocument(path);
					}
					document.MakeCurrent();
				}
			} else {
				var container = doc.Container;
				SetProperty.Perform(container, nameof(Node.TangerineFlags), container.TangerineFlags & ~TangerineFlags.DisplayContent, isChangingDocument: false);
				EnterNode.Perform(container.Parent, false);
				SelectNode.Perform(container, true);
			}
		}

		public static bool IsAllowed()
		{
			var doc = Document.Current;
			return doc.Container != doc.RootNode || doc.SceneNavigatedFrom != null;
		}
	}

	public static class NavigateToAnimation
	{
		public static void Perform(Animation animation)
		{
			if (
				animation.IsLegacy && Document.Current.Container != animation.OwnerNode ||
				!animation.IsLegacy && Document.Current.Container.SameOrDescendantOf(animation.OwnerNode)
			) {
				var node = NavigateToNode.Perform(animation.OwnerNode, enterInto: true, turnOnInspectRootNodeIfNeeded: false);
				// Remap animation in case of external scene.
				animation = node.Animations.Find(animation.Id);
			}
			// Wrap into transaction, as the document could be changed in case of an external scene.
			Document.Current.History.DoTransaction(() => {
				SetProperty.Perform(Document.Current, nameof(Document.Animation), animation, isChangingDocument: false);
			});
		}
	}
}
