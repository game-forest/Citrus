using System.Collections.Generic;
using System.Linq;
using Tangerine.UI;

namespace Tangerine
{
	public class LookupSections
	{
		private readonly LookupWidget lookupWidget;
		private readonly Stack<LookupSection> stack = new Stack<LookupSection>();

		public readonly LookupInitialSection Initial;
		public readonly LookupHelpSection Help;
		public readonly LookupCommandsSection Commands;
		public readonly LookupSection[] List;

		public int StackCount => stack.Count;

		public LookupSections(LookupWidget lookupWidget)
		{
			this.lookupWidget = lookupWidget;
			lookupWidget.NavigatedBack += NavigatedBack;

			List = new LookupSection[] {
				Initial = new LookupInitialSection(),
				Help = new LookupHelpSection(),
				Commands = new LookupCommandsSection(),
				new LookupFilesSection(),
				new LookupNodesSection(),
				new LookupAnimationMarkersSection(), 
				new LookupDocumentMarkersSection(),
				new LookupAnimationFramesSection(),
				new LookupNodeAnimationsSection(),
				new LookupDocumentAnimationsSection(),
				new LookupComponentsSection(),
			};
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
			stack.Pop();
			SetupSection(stack.Count > 0 ? stack.Peek() : null);
		}

		public void Drop()
		{
			stack.Clear();
			SetupSection(null);
		}

		private void SetupSection(LookupSection section)
		{
			lookupWidget.DataSource = section?.DataSource;
			lookupWidget.Filter = section?.Filter;
			lookupWidget.HintText = section?.HintText;
			lookupWidget.FilterText = null;
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
	}
}
