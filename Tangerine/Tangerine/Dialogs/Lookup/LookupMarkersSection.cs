using System.Collections.Generic;
using Lime;
using Tangerine.Core;
using Tangerine.Core.Operations;
using Tangerine.UI;
using Tangerine.UI.Timeline.Operations;

namespace Tangerine
{
	public static class LookupMarkersSection
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

		public static void FillLookupItemsListByAnimationMarkers(LookupSections sections, List<LookupItem> items, Animation animation, bool navigateToNode = true)
		{
			foreach (var m in animation.Markers) {
				var mClosed = m;
				var item = new LookupDialogItem(
					m.Id,
					$"{m.Action} Marker at Frame: {m.Frame}; Animation: {(animation.IsLegacy ? "[Legacy]" : animation.Id)}; Node: {animation.OwnerNode}",
					markerActionsIcons[m.Action].AsTexture,
					() => {
						var a = animation;
						if (navigateToNode) {
							var animationIndex = animation.OwnerNode.Animations.IndexOf(animation);
							Node node;
							try {
								node = NavigateToNode.Perform(animation.OwnerNode, enterInto: true, turnOnInspectRootNodeIfNeeded: true);
							} catch (System.Exception exception) {
								AlertDialog.Show(exception.Message);
								return;
							}
							a = node.Animations[animationIndex];
						}
						var document = Document.Current;
						document.History.DoTransaction(() => {
							if (navigateToNode) {
								SetProperty.Perform(document, nameof(Document.Animation), a, isChangingDocument: false);
							}
							SetCurrentColumn.Perform(mClosed.Frame, a);
							CenterTimelineOnCurrentColumn.Perform();
						});
						sections.Drop();
					}
				);
				if (string.IsNullOrEmpty(m.Id)) {
					item.CreateVisuals();
					item.Header.Enabled = false;
					item.HeaderRichText.Text = RichText.Escape("<No Name>");
				}
				items.Add(item);
			}
		}
	}

	public class LookupAnimationMarkersSection : LookupSection
	{
		private const string PrefixConst = "m";

		public override string Breadcrumb { get; } = "Search Marker";
		public override string Prefix { get; } = $"{PrefixConst}:";
		public override string HelpText { get; } = $"Type '{PrefixConst}:' to search for marker in current animation";

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
			var items = new List<LookupItem>(0);
			LookupMarkersSection.FillLookupItemsListByAnimationMarkers(Sections, items, Document.Current.Animation, navigateToNode: false);
			lookupWidget.AddRange(items);
		}

		private bool RequireAnimationWithMarkersOrAddAlertItem(LookupWidget lookupWidget, string alertText)
		{
			if (Document.Current.Animation.Markers.Count > 0) {
				return true;
			}
			lookupWidget.AddItem(new LookupDialogItem(
				alertText,
				null,
				Sections.Drop
			));
			return false;
		}
	}

	public class LookupDocumentMarkersSection : LookupSectionLimited
	{
		private const string PrefixConst = "md";

		public override string Breadcrumb { get; } = "Search Marker in Document";
		public override string Prefix { get; } = $"{PrefixConst}:";
		public override string HelpText { get; } = $"Type '{PrefixConst}:' to search for marker in current document";

		public LookupDocumentMarkersSection(LookupSections sections) : base(sections) { }

		public override void FillLookup(LookupWidget lookupWidget)
		{
			if (
				!RequireProjectOrAddAlertItem(lookupWidget, "Open any project to use Go To Marker function") ||
				!RequireDocumentOrAddAlertItem(lookupWidget, "Open any document to use Go To Marker function")
			) {
				return;
			}
			var items = new List<LookupItem>(0);
			foreach (var node in Document.Current.RootNodeUnwrapped.SelfAndDescendants) {
				foreach (var animation in node.Animations) {
					LookupMarkersSection.FillLookupItemsListByAnimationMarkers(Sections, items, animation, navigateToNode: node != Document.Current.Container);
				}
			}
			MutableItemList = items;
			Active = true;
		}
	}
}
