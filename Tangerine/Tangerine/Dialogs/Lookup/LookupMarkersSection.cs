using Lime;
using Tangerine.Core;
using Tangerine.Core.Operations;
using Tangerine.UI;
using Tangerine.UI.Timeline.Operations;

namespace Tangerine
{
	public abstract class LookupMarkersSection : LookupSection
	{
		protected void FillLookupByAnimationMarkers(LookupWidget lookupWidget, Animation animation, bool navigateToNode = true)
		{
			foreach (var m in animation.Markers) {
				var mClosed = m;
				lookupWidget.AddItem(
					$"{m.Action} Marker '{m.Id}' at Frame: {m.Frame} in Animation: {(animation.IsLegacy ? "[Legacy]" : animation.Id)} in {animation.OwnerNode}",
					() => {
						var a = animation;
						if (navigateToNode) {
							var node = LookupNodesSection.NavigateToDocumentNode(animation.OwnerNode, asContainer: true);
							if (node == null) {
								return;
							}
							a = node.Animations[animation.OwnerNode.Animations.IndexOf(animation)];
						}
						var document = Document.Current;
						Document.Current.History.DoTransaction(() => {
							if (navigateToNode) {
								SetProperty.Perform(document, nameof(Document.SelectedAnimation), a, isChangingDocument: false);
							}
							SetCurrentColumn.Perform(mClosed.Frame, a);
							CenterTimelineOnCurrentColumn.Perform();
						});
						LookupDialog.Sections.Drop();
					}
				);
			}
		}
	}

	public class LookupAnimationMarkersSection : LookupMarkersSection
	{
		private const string PrefixConst = "m";

		public override string Breadcrumb { get; } = "Search Marker";
		public override string Prefix { get; } = $"{PrefixConst} ";
		public override string HelpText { get; } = $"Type '{PrefixConst}' to search for marker in current animation";

		public override void FillLookup(LookupWidget lookupWidget)
		{
			if (Project.Current == null) {
				lookupWidget.AddItem(
					"Open any project to use Go To Marker function",
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
			if (Document.Current.Animation.Markers.Count == 0) {
				lookupWidget.AddItem("Select any node with markers to use Go To Marker function", LookupDialog.Sections.Drop);
				return;
			}
			FillLookupByAnimationMarkers(lookupWidget, Document.Current.Animation);
		}
	}

	public class LookupDocumentMarkersSection : LookupMarkersSection
	{
		private const string PrefixConst = "md";

		public override string Breadcrumb { get; } = "Search Marker in Document";
		public override string Prefix { get; } = $"{PrefixConst} ";
		public override string HelpText { get; } = $"Type '{PrefixConst}' to search for marker in current document";

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
				foreach (var animation in node.Animations) {
					FillLookupByAnimationMarkers(lookupWidget, animation, navigateToNode: node != Document.Current.Container);
				}
			}
		}
	}
}
