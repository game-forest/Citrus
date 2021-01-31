using Lime;
using System;
using System.Linq;

namespace Tangerine.Core
{
	public static class MenuExtensions
	{
		public static void InsertCommandAlongPath(this IMenu menu, ICommand command, string path)
		{
			IMenu nextMenu = menu;
			var parts = path.Split(new[] { "/" }, StringSplitOptions.RemoveEmptyEntries);
			for (int i = 0; i < parts.Length - 1; i++) {
				ICommand nestedMenuCommand = nextMenu.FirstOrDefault(c => c.Text == parts[i]);
				if (nestedMenuCommand == null) {
					nestedMenuCommand = new Command(parts[i], new Menu());
					nextMenu.Add(nestedMenuCommand);
				}
				nextMenu = nestedMenuCommand.Menu;
			}
			command.Text = parts[parts.Length - 1];
			nextMenu.Add(command);
		}
	}
}
