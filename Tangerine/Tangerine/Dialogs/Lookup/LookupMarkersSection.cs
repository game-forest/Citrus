using System.Collections.Generic;
using Lime;
using Tangerine.Core;
using Tangerine.Core.Operations;
using Tangerine.UI;
using Tangerine.UI.Timeline.Operations;

namespace Tangerine
{
	public abstract class LookupMarkersSection : LookupSection
	{
		private static readonly Dictionary<MarkerAction, Icon> markerActionsIcons;

		static LookupMarkersSection()
		{
			markerActionsIcons = new Dictionary<MarkerAction, Icon> {
				{ MarkerAction.Play, IconPool.GetIcon("Lookup.MarkerPlayAction") },
				{ MarkerAction.Jump, IconPool.GetIcon("Lookup.MarkerJumpAction") },
				{ MarkerAction.Stop, IconPool.GetIcon("Lookup.MarkerStopAction") },
			};
		}

		protected LookupMarkersSection(LookupSections sections) : base(sections) { }

		protected void FillLookupByAnimationMarkers(LookupWidget lookupWidget, Animation animation, bool navigateToNode = true)
		{
			foreach (var m in animation.Markers) {
				var mClosed = m;
				var item = new LookupDialogItem(
					lookupWidget,
					m.Id,
					$"{m.Action} Marker at Frame: {m.Frame}; Animation: {(animation.IsLegacy ? "[Legacy]" : animation.Id)}; Node: {animation.OwnerNode}",
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
						Sections.Drop();
					}
				) {
					IconTexture = markerActionsIcons[m.Action].AsTexture,
				};
				if (string.IsNullOrEmpty(m.Id)) {
					item.HeaderSimpleText.Text = "<No Name>";
				}
				lookupWidget.AddItem(item);
			}
		}
	}

	public class LookupAnimationMarkersSection : LookupMarkersSection
	{
		private const string PrefixConst = "m";

		public override string Breadcrumb { get; } = "Search Marker";
		public override string Prefix { get; } = $"{PrefixConst} ";
		public override string HelpText { get; } = $"Type '{PrefixConst}' to search for marker in current animation";

		public LookupAnimationMarkersSection(LookupSections sections) : base(sections) { }

		public override void FillLookup(LookupWidget lookupWidget)
		{
			if (
				!RequireProjectOrAddAlertItem(lookupWidget, "Open any project to use Go To Marker function") ||
				!RequireDocumentOrAddAlertItem(lookupWidget, "Open any document to use Go To Marker function") ||
				!RequireAnimationWithMarkersOrAddAlertItem(lookupWidget, "Select any node with markers to use Go To Marker function")
			) {
				return;
			}
			FillLookupByAnimationMarkers(lookupWidget, Document.Current.Animation);
		}

		private bool RequireAnimationWithMarkersOrAddAlertItem(LookupWidget lookupWidget, string alertText)
		{
			if (Document.Current.Animation.Markers.Count > 0) {
				return true;
			}
			lookupWidget.AddItem(new LookupDialogItem(
				lookupWidget,
				alertText,
				null,
				Sections.Drop
			));
			return false;
		}
	}

	public class LookupDocumentMarkersSection : LookupMarkersSection
	{
		private const string PrefixConst = "md";

		public override string Breadcrumb { get; } = "Search Marker in Document";
		public override string Prefix { get; } = $"{PrefixConst} ";
		public override string HelpText { get; } = $"Type '{PrefixConst}' to search for marker in current document";

		public LookupDocumentMarkersSection(LookupSections sections) : base(sections) { }

		public override void FillLookup(LookupWidget lookupWidget)
		{
			if (
				!RequireProjectOrAddAlertItem(lookupWidget, "Open any project to use Go To Marker function") ||
				!RequireDocumentOrAddAlertItem(lookupWidget, "Open any document to use Go To Marker function")
			) {
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
