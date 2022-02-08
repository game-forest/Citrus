using System.IO;
using Lime;
using Orange;
using Tangerine.Core;
using Tangerine.UI;

namespace Tangerine.MainMenu
{
	internal class ForceUpdate : CommandHandler
	{
		public override void Execute()
		{
			var result = new AlertDialog("Are you sure? This operation will discard you changes", "Yes", "No").Show();
			if (result == 0) {
				var directory = Path.GetDirectoryName(Project.Current?.CitprojPath ?? string.Empty);
				if (directory == string.Empty) {
					return;
				}
				Git.ForceUpdate(directory);
			}
		}
	}

	internal class Update : CommandHandler
	{
		public override void Execute()
		{
			var directory = Path.GetDirectoryName(Project.Current?.CitprojPath ?? string.Empty);
			if (directory == string.Empty) {
				return;
			}
			Git.Update(directory);
		}
	}
}
