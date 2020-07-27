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
						FillLookupByMenuCommands(menuItem.Menu, text);
					} else if (isPresenterTitle && menuItem is Command command) {
						var action = (Action)command.Issue;
						lookupWidget.AddItem(
							text,
							() => {
								action();
								LookupDialog.Sections.Drop();
							}
						);
					}
				}
			}
			FillLookupByMenuCommands(Application.MainMenu);
		}
	}
}
