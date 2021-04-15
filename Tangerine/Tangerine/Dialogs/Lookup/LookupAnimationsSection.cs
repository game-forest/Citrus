using System;
using System.Collections.Generic;
using Lime;
using Tangerine.Core;
using Tangerine.Core.Operations;
using Tangerine.UI;

namespace Tangerine
{
	public static class LookupAnimationsSection
	{
		public static void FillLookupByAnimations(LookupSections sections, List<LookupItem> items, IEnumerable<Animation> animations, bool navigateToNode = false)
		{
			foreach (var animation in animations) {
				var aClosed = animation;
				var navigateToNodeClosed = navigateToNode && animation.OwnerNode != Document.Current.Container;
				items.Add(new LookupDialogItem(
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
							SetProperty.Perform(document, nameof(Document.Animation), a, isChangingDocument: false);
						});
						sections.Drop();
					}
				));
			}
		}
	}

	public class LookupNodeAnimationsSection : LookupSection
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
			var animations = GetAnimations();
			var items = new List<LookupItem>(0);
			LookupAnimationsSection.FillLookupByAnimations(Sections, items, animations);
			lookupWidget.AddRange(items);
		}

		private List<Animation> GetAnimations()
		{
			var animations = new List<Animation>();
			GetAnimationsHelper(animations);
			animations.Sort(AnimationsComparer.Instance);
			animations.Insert(0, Document.Current.Container.DefaultAnimation);
			return animations;
		}

		private void GetAnimationsHelper(List<Animation> animations)
		{
			var usedAnimations = new HashSet<string>();
			var ancestor = Document.Current.Container;
			lock (usedAnimations) {
				usedAnimations.Clear();
				while (true) {
					foreach (var a in ancestor.Animations) {
						if (!a.IsLegacy && usedAnimations.Add(a.Id)) {
							animations.Add(a);
						}
					}
					if (ancestor == Document.Current.RootNode) {
						return;
					}
					ancestor = ancestor.Parent;
				}
			}
		}

		class AnimationsComparer : IComparer<Animation>
		{
			public static readonly AnimationsComparer Instance = new AnimationsComparer();

			public int Compare(Animation x, Animation y)
			{
				return x.Id.CompareTo(y.Id);
			}
		}
	}

	public class LookupDocumentAnimationsSection : LookupSectionLimited
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
			var items = new List<LookupItem>(0);
			foreach (var node in Document.Current.RootNodeUnwrapped.SelfAndDescendants) {
				LookupAnimationsSection.FillLookupByAnimations(Sections, items, node.Animations, navigateToNode: true);
			}
			MutableItemList = items;
			Active = true;
		}
	}
}
