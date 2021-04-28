using System;
using Lime;
using Tangerine.UI;

namespace Tangerine
{
	public class LookupCommandsSection : LookupSection
	{
		private const string PrefixConst = ">";

		public override string Breadcrumb { get; } = "Search Command";
		public override string Prefix { get; } = PrefixConst;
		public override string HelpText { get; } = $"Type '{PrefixConst}' to search for command in the current workspace";

		private readonly IMenu startMenu;

		public LookupCommandsSection(LookupSections sections) : base(sections) { }

		public LookupCommandsSection(LookupSections sections, IMenu menu, string breadcrumb) : base(sections)
		{
			startMenu = menu;
			Breadcrumb = !string.IsNullOrEmpty(breadcrumb) ? breadcrumb : throw new InvalidOperationException();
		}

		public override void FillLookup(LookupWidget lookupWidget)
		{
			void FillLookupByMenuCommands(IMenu menu, string prefix = null)
			{
				foreach (var menuItem in menu) {
					if (!menuItem.Enabled) {
						continue;
					}
					var isPresenterTitle = !string.IsNullOrEmpty(menuItem.Text);
					var text =
						isPresenterTitle ?
							!string.IsNullOrEmpty(prefix) ? $"{prefix}\\{menuItem.Text}" : menuItem.Text :
							prefix;
					if (menuItem.Menu != null) {
						var menuCopy = menuItem.Menu;
						lookupWidget.AddItem(new LookupDialogItem(
							headerText: text, 
							text: null, 
							label: "Menu", 
							() => Sections.Push(new LookupCommandsSection(Sections, menuCopy, text))
						));
						FillLookupByMenuCommands(menuItem.Menu, text);
					} else if (isPresenterTitle && menuItem is Command command) {
						var action = (Action)command.Issue;
						lookupWidget.AddItem(new LookupDialogItem(
							headerText: text,
							text: null,
							command.Shortcut,
							() => {
								action();
								Sections.SaveRecentlySubmittedData(LookupSections.SectionType.Commands, text);
								Sections.Drop();
							}
						));
					}
				}
			}
			FillLookupByMenuCommands(startMenu ?? Application.MainMenu);
		}
	}
}
