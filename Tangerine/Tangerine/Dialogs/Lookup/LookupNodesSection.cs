using System;
using System.Collections.Generic;
using Lime;
using Tangerine.Core;
using Tangerine.UI;

namespace Tangerine
{
	public class LookupNodesSection : LookupSection
	{
		private const string PrefixConst = "n";

		public override string Breadcrumb { get; } = "Search Node";
		public override string Prefix { get; } = $"{PrefixConst}:";
		public override string HelpText { get; } = $"Type '{PrefixConst}:' to search for node in the current document";

		public LookupNodesSection(LookupSections sections) : base(sections) { }

		public override void FillLookup(LookupWidget lookupWidget)
		{
			if (
				!RequireProjectOrAddAlertItem(lookupWidget, "Open any project to use Go To Node function") ||
				!RequireDocumentOrAddAlertItem(lookupWidget, "Open any document to use Go To Node function")
			) {
				return;
			}
			foreach (var node in Document.Current.RootNodeUnwrapped.SelfAndDescendants) {
				var nodeClosed = node;
				var nodeType = node.GetType();
				var item = new LookupDialogItem(
					node.Id,
					$"Type: {nodeType.Name}; {(node.Parent != null ? $"Parent: {node.Parent}" : "Root node")}",
					NodeIconPool.GetIcon(nodeType).AsTexture,
					() => {
						NavigateToDocumentNode(nodeClosed, canToogleInspectRootNode: true);
						Sections.Drop();
					}
				);
				if (string.IsNullOrEmpty(node.Id)) {
					item.CreateVisuals();
					item.Header.Enabled = false;
					item.HeaderRichText.Text = RichText.Escape("<Empty Id>");
				}
				lookupWidget.AddItem(item);
			}
		}

		public static Node NavigateToDocumentNode(Node node, bool asContainer = false, bool canToogleInspectRootNode = false)
		{
			if (node.GetRoot() != Document.Current.RootNode) {
				throw new InvalidOperationException();
			}

			var path = new Stack<int>();
			Node externalScene;
			var inspectRootNode = false;
			if (node.Parent == null) {
				asContainer = true;
				inspectRootNode = true;
			}
			if (!asContainer) {
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
				Document externalSceneDocument;
				try {
					externalSceneDocument = Project.Current.OpenDocument(externalScene.ContentsPath);
				} catch (System.Exception e) {
					AlertDialog.Show(e.Message);
					return null;
				}
				externalSceneDocument.SceneNavigatedFrom = currentScenePath;
				node = externalSceneDocument.RootNodeUnwrapped;
				foreach (var i in path) {
					node = node.Nodes[i];
				}
			} else {
				// to ensure TangerineFlags.DisplayContent set by EnterNode is cleared
				Document.Current.History.DoTransaction(Core.Operations.LeaveNode.Perform);
			}
			Document.Current.History.DoTransaction(() => {
				if (asContainer) {
					Core.Operations.EnterNode.Perform(node, selectFirstNode: true);
				} else {
					if (Core.Operations.EnterNode.Perform(node.Parent, selectFirstNode: false)) {
						Core.Operations.SelectNode.Perform(node);
					}
				}
			});
			if (canToogleInspectRootNode) {
				Document.Current.InspectRootNode = inspectRootNode;
			}
			return node;
		}
	}
}
