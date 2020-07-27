using System.Collections.Generic;
using Lime;
using Tangerine.Core;
using Tangerine.Core.Operations;
using Tangerine.UI;

namespace Tangerine
{
	public abstract class LookupAnimationsSection : LookupSection
	{
		protected void FillLookupByAnimations(LookupWidget lookupWidget, IEnumerable<Animation> animations, bool navigateToNode = false)
		{
			foreach (var animation in animations) {
				var aClosed = animation;
				navigateToNode &= animation.OwnerNode != Document.Current.Container;
				lookupWidget.AddItem(
					$"Animation: {(animation.IsLegacy ? "[Legacy]" : animation.Id)} in {animation.OwnerNode}",
					() => {
						var a = aClosed;
						if (navigateToNode) {
							var n = LookupNodesSection.NavigateToDocumentNode(aClosed.OwnerNode, asContainer: true);
							if (n == null) {
								return;
							}
							a = n.Animations[aClosed.OwnerNode.Animations.IndexOf(aClosed)];
						}
						var document = Document.Current;
						Document.Current.History.DoTransaction(() => {
							SetProperty.Perform(document, nameof(Document.SelectedAnimation), a, isChangingDocument: false);
						});
						LookupDialog.Sections.Drop();
					}
				);
			}
		}
	}

	public class LookupNodeAnimationsSection : LookupAnimationsSection
	{
		private const string PrefixConst = "a";

		public override string Breadcrumb { get; } = "Search Animation";
		public override string Prefix { get; } = $"{PrefixConst} ";
		public override string HelpText { get; } = $"Type '{PrefixConst}' to search for animation in current node";

		public override void FillLookup(LookupWidget lookupWidget)
		{
			if (Project.Current == null) {
				lookupWidget.AddItem(
					"Open any project to use Go To File function",
					() => {
						new FileOpenProject();
						LookupDialog.Sections.Drop();
					}
				);
				return;
			}
			if (Document.Current == null) {
				lookupWidget.AddItem(
					"Open any document to use Go To Marker function",
					() => {
						new FileOpen();
						LookupDialog.Sections.Drop();
					}
				);
				return;
			}
			var animations = new List<Animation>();
			Document.Current.GetAnimations(animations);
			FillLookupByAnimations(lookupWidget, animations);
		}
	}

	public class LookupDocumentAnimationsSection : LookupAnimationsSection
	{
		private const string PrefixConst = "ad";

		public override string Breadcrumb { get; } = "Search Animation in Document";
		public override string Prefix { get; } = $"{PrefixConst} ";
		public override string HelpText { get; } = $"Type '{PrefixConst}' to search for animation in current document";

		public override void FillLookup(LookupWidget lookupWidget)
		{
			if (Project.Current == null) {
				lookupWidget.AddItem(
					"Open any project to use Go To File function",
					() => {
						new FileOpenProject();
						LookupDialog.Sections.Drop();
					}
				);
				return;
			}
			if (Document.Current == null) {
				lookupWidget.AddItem(
					"Open any document to use Go To Marker function",
					() => {
						new FileOpen();
						LookupDialog.Sections.Drop();
					}
				);
				return;
			}
			foreach (var node in Document.Current.RootNodeUnwrapped.SelfAndDescendants) {
				FillLookupByAnimations(lookupWidget, node.Animations, navigateToNode: true);
			}
		}
	}
}
