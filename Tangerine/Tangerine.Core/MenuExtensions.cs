using Lime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Tangerine.Core
{
	public static class MenuExtensions
	{
		/// <summary>
		/// Inserts command into menu, creating intermediate nested menus as specified by path.
		/// </summary>
		/// <param name="menu">Target menu.</param>
		/// <param name="command">Command to insert.</param>
		/// <param name="path"><see cref="Lime.TangerineMenuPathAttribute"/></param>
		public static void InsertCommandAlongPath(this IMenu menu, ICommand command, string path)
		{
			IMenu nextMenu = menu;
			bool endsWithSlash = path.EndsWith("/");
			var parts = path.Split(new[] { "/" }, StringSplitOptions.RemoveEmptyEntries);
			for (int i = 0; i < parts.Length - (endsWithSlash ? 0 : 1); i++) {
				ICommand nestedMenuCommand = nextMenu.FirstOrDefault(c => c.Text == parts[i]);
				if (nestedMenuCommand == null) {
					nestedMenuCommand = new Command(parts[i], new Menu());
					nextMenu.Add(nestedMenuCommand);
				}
				nextMenu = nestedMenuCommand.Menu;
			}
			if (!endsWithSlash) {
				command.Text = parts[parts.Length - 1];
			}
			nextMenu.Add(command);
		}

		public static void SortTypesByMenuPath(List<Type> types)
		{
			types.Sort((a, b) => {
				string aPath = ClassAttributes<TangerineMenuPathAttribute>.Get(a)?.Path ?? a.Name;
				string bPath = ClassAttributes<TangerineMenuPathAttribute>.Get(b)?.Path ?? b.Name;
				int aRank = aPath.Count(c => c == '/');
				int bRank = bPath.Count(c => c == '/');
				int r = (bRank).CompareTo(aRank);
				if (r == 0) {
					if (aPath.EndsWith("/")) {
						aPath += a.Name;
					}
					if (bPath.EndsWith("/")) {
						bPath += b.Name;
					}
					r = aPath.CompareTo(bPath);
				}
				return r;
			});
		}
	}
}
