using System.IO;
using System.Text;
using Lime;
using Orange;
using Tangerine.Core;

namespace Tangerine.UI
{
	/// <summary>
	/// This class provides information about engine and current project
	/// </summary>
	public static class About
	{
		public static string GetInformation()
		{
			if (!Git.IsGitInPath) {
				return "Git is not in PATH\n";
			}
			var projectDir = Path.GetDirectoryName(Project.Current?.CitprojPath ?? string.Empty);
			StringBuilder stringbBuilder = new StringBuilder();
			var citrusDir = Orange.Toolbox.FindCitrusDirectory();
			Git.Exec(projectDir, "remote -v", stringbBuilder);
			var projectRemoteUri = stringbBuilder.ToString().IsNullOrWhiteSpace() ?
				string.Empty : stringbBuilder.ToString().Split('\n')[0].Split(' ', '\t')[1];
			Git.Exec(projectDir, "rev-parse HEAD", stringbBuilder.Clear());
			var projectLatestCommit = stringbBuilder.ToString();
			Git.Exec(citrusDir, "rev-parse HEAD", stringbBuilder.Clear());
			var citrusLatestCommit = stringbBuilder.ToString();
			string info = $"Project remote url: {projectRemoteUri} \n" +
				$"Project commit: {projectLatestCommit}" +
				$"Citrus commit: {citrusLatestCommit}";
			return info;
		}
		public static void DisplayInformation()
		{
			var info = GetInformation();
			var alertDialog = new AlertDialog(info, "Copy Info", "Close");
			switch (alertDialog.Show()) {
				case 0: {
						// Copy to clipboard
						Clipboard.Text = info;
						break;
					}
			}
		}
	}
}
