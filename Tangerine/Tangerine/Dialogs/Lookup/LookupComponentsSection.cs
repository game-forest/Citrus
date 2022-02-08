using System.Collections.Generic;
using Lime;
using Tangerine.Core;
using Tangerine.Core.Operations;
using Tangerine.UI;

namespace Tangerine
{
	public class LookupComponentsSection : LookupSectionLimited
	{
		private const string PrefixConst = "c";

		public override string Breadcrumb { get; } = "Search Component";
		public override string Prefix { get; } = $"{PrefixConst}:";
		public override string HelpText { get; } =
			$"Type '{PrefixConst}:' to search for components in current document";

		public LookupComponentsSection(LookupSections sections) : base(sections) { }

		public override void FillLookup(LookupWidget lookupWidget)
		{
			if (
				!RequireProjectOrAddAlertItem(lookupWidget, "Open any project to use Go To Component function") ||
				!RequireDocumentOrAddAlertItem(lookupWidget, "Open any document to use Go To Component function")
			) {
				return;
			}
			var items = new List<LookupItem>(0);
			foreach (var node in Document.Current.RootNodeUnwrapped.SelfAndDescendants) {
				foreach (var component in node.Components) {
					var type = component.GetType();
					if (!type.IsDefined(typeof(TangerineRegisterComponentAttribute), true)) {
						continue;
					}
					var nodeClosed = node;
					items.Add(new LookupDialogItem(
						component.GetType().Name,
						$"Node: {node}",
						() => {
							try {
								NavigateToNode.Perform(
									nodeClosed, enterInto: false, turnOnInspectRootNodeIfNeeded: true
								);
							} catch (System.Exception exception) {
								AlertDialog.Show(exception.Message);
							}
							Sections.Drop();
						}
					));
				}
			}
			MutableItemList = items;
			Active = true;
		}
	}
}
