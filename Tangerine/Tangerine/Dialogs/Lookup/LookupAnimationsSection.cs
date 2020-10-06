using System.Collections.Generic;
using Lime;
using Tangerine.Core;
using Tangerine.Core.Operations;
using Tangerine.UI;

namespace Tangerine
{
	public abstract class LookupAnimationsSection : LookupSection
	{
		protected LookupAnimationsSection(LookupSections sections) : base(sections) { }

		protected void FillLookupByAnimations(LookupWidget lookupWidget, IEnumerable<Animation> animations, bool navigateToNode = false)
		{
			foreach (var animation in animations) {
				var aClosed = animation;
				var navigateToNodeClosed = navigateToNode && animation.OwnerNode != Document.Current.Container;
				lookupWidget.AddItem(new LookupDialogItem(
					animation.IsLegacy ? "[Legacy]" : animation.Id,
					$"Node: {animation.OwnerNode}",
					() => {
						var a = aClosed;
						if (navigateToNodeClosed) {
							Node node;
							try {
								node = NavigateToNode.Perform(aClosed.OwnerNode, enterInto: true);
							} catch (System.Exception exception) {
								AlertDialog.Show(exception.Message);
								return;
							}
							a = node.Animations[aClosed.OwnerNode.Animations.IndexOf(aClosed)];
						}
						var document = Document.Current;
						Document.Current.History.DoTransaction(() => {
							SetProperty.Perform(document, nameof(Document.SelectedAnimation), a, isChangingDocument: false);
						});
						Sections.Drop();
					}
				));
			}
		}
	}

	public class LookupNodeAnimationsSection : LookupAnimationsSection
	{
		private const string PrefixConst = "a";

		public override string Breadcrumb { get; } = "Search Animation";
		public override string Prefix { get; } = $"{PrefixConst}:";
		public override string HelpText { get; } = $"Type '{PrefixConst}:' to search for animation in current node";

		public LookupNodeAnimationsSection(LookupSections sections) : base(sections) { }

		public override void FillLookup(LookupWidget lookupWidget)
		{
			if (
				!RequireProjectOrAddAlertItem(lookupWidget, "Open any project to use Go To Animation function") ||
				!RequireDocumentOrAddAlertItem(lookupWidget, "Open any document to use Go To Animation function")
			) {
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
		public override string Prefix { get; } = $"{PrefixConst}:";
		public override string HelpText { get; } = $"Type '{PrefixConst}:' to search for animation in current document";

		public LookupDocumentAnimationsSection(LookupSections sections) : base(sections) { }

		public override void FillLookup(LookupWidget lookupWidget)
		{
			if (
				!RequireProjectOrAddAlertItem(lookupWidget, "Open any project to use Go To Animation function") ||
				!RequireDocumentOrAddAlertItem(lookupWidget, "Open any document to use Go To Animation function")
			) {
				return;
			}
			foreach (var node in Document.Current.RootNodeUnwrapped.SelfAndDescendants) {
				FillLookupByAnimations(lookupWidget, node.Animations, navigateToNode: true);
			}
		}
	}
}
