using Lime;
using Tangerine.Core;
using Tangerine.UI;

namespace Tangerine
{
	public class LookupComponentsSection : LookupSection
	{
		private const string PrefixConst = "c";

		public override string Breadcrumb { get; } = "Search Component";
		public override string Prefix { get; } = $"{PrefixConst} ";
		public override string HelpText { get; } = $"Type '{PrefixConst}' to search for components in current document";

		public LookupComponentsSection(LookupSections sections) : base(sections) { }

		public override void FillLookup(LookupWidget lookupWidget)
		{
			if (
				!RequireProjectOrAddAlertItem(lookupWidget, "Open any project to use Go To Component function") ||
				!RequireDocumentOrAddAlertItem(lookupWidget, "Open any document to use Go To Component function")
			) {
				return;
			}
			foreach (var node in Document.Current.RootNodeUnwrapped.SelfAndDescendants) {
				foreach (var component in node.Components) {
					var type = component.GetType();
					if (!type.IsDefined(typeof(TangerineRegisterComponentAttribute), true)) {
						continue;
					}
					var nodeClosed = node;
					lookupWidget.AddItem(new LookupDialogItem(
						lookupWidget,
						component.GetType().Name,
						$"Node: {node}",
						() => {
							LookupNodesSection.NavigateToDocumentNode(nodeClosed, canToogleInspectRootNode: true);
							Sections.Drop();
						}
					));
				}
			}
		}
	}
}
