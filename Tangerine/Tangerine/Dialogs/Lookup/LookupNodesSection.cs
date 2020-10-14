using System.Collections.Generic;
using Lime;
using Tangerine.Core;
using Tangerine.Core.Operations;
using Tangerine.UI;

namespace Tangerine
{
	public class LookupNodesSection : LookupSectionLimited
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
			var items = new List<LookupItem>(0);
			foreach (var node in Document.Current.RootNodeUnwrapped.SelfAndDescendants) {
				var nodeClosed = node;
				var nodeType = node.GetType();
				var item = new LookupDialogItem(
					node.Id,
					$"Type: {nodeType.Name}; {(node.Parent != null ? $"Parent: {node.Parent}" : "Root node")}",
					NodeIconPool.GetTexture(node),
					() => {
						try {
							NavigateToNode.Perform(nodeClosed, enterInto: false, turnOnInspectRootNodeIfNeeded: true);
						} catch (System.Exception exception) {
							AlertDialog.Show(exception.Message);
						}
						Sections.Drop();
					}
				);
				if (string.IsNullOrEmpty(node.Id)) {
					item.CreateVisuals();
					item.Header.Enabled = false;
					item.HeaderRichText.Text = RichText.Escape("<Empty Id>");
				}
				items.Add(item);
			}
			MutableItemList = items;
			Active = true;
		}
	}
}
