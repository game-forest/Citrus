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

		public override void FillLookup(LookupWidget lookupWidget)
		{
			if (Project.Current == null) {
				lookupWidget.AddItem(
					"Open any project to use Go To Node function",
					() => {
						new FileOpenProject();
						LookupDialog.Sections.Drop();
					}
				);
				return;
			}
			if (Document.Current == null) {
				lookupWidget.AddItem(
					"Open any document to use Go To Node function",
					() => {
						new FileOpen();
						LookupDialog.Sections.Drop();
					}
				);
				return;
			}
			foreach (var node in Document.Current.RootNodeUnwrapped.SelfAndDescendants) {
				foreach (var component in node.Components) {
					var type = component.GetType();
					if (!type.IsDefined(typeof(TangerineRegisterComponentAttribute), true)) {
						continue;
					}
					var nodeClosed = node;
					lookupWidget.AddItem(
						$"Component '{component.GetType().Name}' in {node}",
						() => {
							LookupNodesSection.NavigateToDocumentNode(nodeClosed, canToogleInspectRootNode: true);
							LookupDialog.Sections.Drop();
						}
					);
				}
			}
		}
	}
}
