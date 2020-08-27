using System;
using System.Collections.Generic;
using System.Linq;
using Tangerine.UI;

namespace Tangerine
{
	public class LookupSections
	{
		private static SubmittedData recentlySubmittedData;

		private readonly LookupWidget lookupWidget;
		private readonly Stack<LookupSection> stack = new Stack<LookupSection>();

		public readonly LookupInitialSection Initial;
		public readonly LookupHelpSection Help;
		public readonly LookupCommandsSection Commands;
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
				new LookupFilesSection(this),
				new LookupNodesSection(this),
				new LookupAnimationMarkersSection(this), 
				new LookupDocumentMarkersSection(this),
				new LookupAnimationFramesSection(this),
				new LookupNodeAnimationsSection(this),
				new LookupDocumentAnimationsSection(this),
				new LookupComponentsSection(this),
			};
		}

		public void Initialize()
		{
			if (recentlySubmittedData == null) {
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

				recentlySubmittedData = null;
			}
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
			if (stack.Count <= 0 || stack.Peek() == Initial) {
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
					case SectionType.Command: return sections.Commands;
					default: throw new ArgumentOutOfRangeException();
				}
			}
		}

		public enum SectionType
		{
			Command,
		}
	}
}
