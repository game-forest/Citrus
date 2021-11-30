using System;
using System.Collections.Generic;
using System.Linq;
using Tangerine.UI;

namespace Tangerine
{
	public class LookupSections
	{
		private LookupSection lockedSection;

		private static SubmittedData recentlySubmittedData;

		private readonly LookupWidget lookupWidget;
		private readonly Stack<LookupSection> stack = new Stack<LookupSection>();

		public readonly LookupInitialSection Initial;
		public readonly LookupHelpSection Help;
		public readonly LookupCommandsSection Commands;
		public readonly LookupFilesSection Files;
		public readonly LookupNodesSection Nodes;
		public readonly LookupAnimationMarkersSection AnimationMarkers;
		public readonly LookupDocumentMarkersSection DocumentMarkers;
		public readonly LookupAnimationFramesSection AnimationFrames;
		public readonly LookupNodeAnimationsSection NodeAnimations;
		public readonly LookupDocumentAnimationsSection DocumentAnimations;
		public readonly LookupComponentsSection Components;
		public readonly LookupOpenDocumentsSection OpenDocuments;
		public readonly LookupSection[] List;
		public readonly FuzzyStringSearch FuzzyStringSearch = new FuzzyStringSearch();

		public int StackCount => stack.Count;

		public LookupSections(LookupWidget lookupWidget)
		{
			this.lookupWidget = lookupWidget;
			lookupWidget.NavigatedBack += NavigatedBack;

			List = new LookupSection[] {
				Initial = new LookupInitialSection(this),
				Help = new LookupHelpSection(this),
				Commands = new LookupCommandsSection(this),
				Files = new LookupFilesSection(this),
				Nodes = new LookupNodesSection(this),
				AnimationMarkers = new LookupAnimationMarkersSection(this),
				DocumentMarkers = new LookupDocumentMarkersSection(this),
				AnimationFrames = new LookupAnimationFramesSection(this),
				NodeAnimations = new LookupNodeAnimationsSection(this),
				DocumentAnimations = new LookupDocumentAnimationsSection(this),
				Components = new LookupComponentsSection(this),
				OpenDocuments = new LookupOpenDocumentsSection(this),
			};
		}

		public void Initialize(SectionType? sectionType = null)
		{
			if (sectionType.HasValue) {
				switch (sectionType) {
					case SectionType.Commands: Push(Commands); break;
					case SectionType.Files: Push(Files); break;
					case SectionType.Nodes: Push(Nodes); break;
					case SectionType.AnimationMarkers: Push(AnimationMarkers); break;
					case SectionType.DocumentMarkers: Push(DocumentMarkers); break;
					case SectionType.AnimationFrames: Push(AnimationFrames); break;
					case SectionType.NodeAnimations: Push(NodeAnimations); break;
					case SectionType.DocumentAnimations: Push(DocumentAnimations); break;
					case SectionType.Components: Push(Components); break;
					case SectionType.OpenDocuments: Push(OpenDocuments); break;
					default:
						throw new ArgumentOutOfRangeException(nameof(sectionType), sectionType, null);
				}
			} else if (recentlySubmittedData == null) {
				Push(Initial);
			} else {
				Push(recentlySubmittedData.GetSection(this));
				lookupWidget.FilterText = recentlySubmittedData.FilterText;
				var itemName = recentlySubmittedData.ItemName;

				void SelectRecentlyItem()
				{
					var selectedItem = lookupWidget.FindIndex(i => ((LookupDialogItem)i).Header.Text == itemName);
					if (selectedItem > 0) {
						lookupWidget.SelectItem(selectedItem);
					}
					lookupWidget.FilterApplied -= SelectRecentlyItem;
				}
				lookupWidget.FilterApplied += SelectRecentlyItem;
			}
			recentlySubmittedData = null;
		}

		public void Initialize(LookupSection section)
		{
			Push(section);
			recentlySubmittedData = null;
		}
		
		public void Push(LookupSection section)
		{
			stack.Push(section);
			SetupSection(section);
		}

		public void DropAndPush(LookupSection section)
		{
			Drop();
			Push(section);
		}

		public void Pop()
		{
			if (stack.Count == 0) {
				return;
			}
			var section = stack.Pop();
			section.Dropped();
			SetupSection(stack.Count > 0 ? stack.Peek() : null);
		}

		public void Drop()
		{
			while (stack.Count > 0) {
				var section = stack.Pop();
				section.Dropped();
			}
			SetupSection(null);
		}

		public void LockNavigationOnLastSection()
		{
			if (stack.Count > 0) {
				lockedSection = stack.Peek();
			}
		}

		private void SetupSection(LookupSection section)
		{
			lookupWidget.FilterText = null;
			lookupWidget.DataSource = section?.DataSource;
			lookupWidget.Filter = section?.Filter;
			lookupWidget.HintText = section?.HintText;
			lookupWidget.SetBreadcrumbsNavigation(stack.Select(s => s.Breadcrumb).Reverse().Where(s => !string.IsNullOrEmpty(s)));
		}

		private void NavigatedBack()
		{
			if (stack.Count <= 0 || stack.Peek() == Initial || stack.Peek() == lockedSection) {
				return;
			}
			Pop();
			if (stack.Count == 0) {
				Push(Initial);
			}
		}

		public void SaveRecentlySubmittedData(SectionType sectionType, string itemName)
		{
			recentlySubmittedData = new SubmittedData {
				SectionType = sectionType,
				FilterText = lookupWidget.FilterText,
				ItemName = itemName,
			};
		}

		private class SubmittedData
		{
			public SectionType SectionType;
			public string FilterText;
			public string ItemName;

			public LookupSection GetSection(LookupSections sections)
			{
				switch (SectionType) {
					case SectionType.Commands: return sections.Commands;
					default: throw new ArgumentOutOfRangeException();
				}
			}
		}

		public enum SectionType
		{
			Commands,
			Files,
			Nodes,
			AnimationMarkers,
			DocumentMarkers,
			AnimationFrames,
			NodeAnimations,
			DocumentAnimations,
			Components,
			OpenDocuments,
		}
	}
}
