using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Globalization;
using Lime;
using System.Reflection;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Text;

namespace Orange
{
	public static class NewProject
	{
		private static string newCitrusDirectory;
		private static string targetDirectory;
		private static string newProjectCitprojPath;

		[Export(nameof(OrangePlugin.MenuItems))]
		[ExportMetadata("Label", "New Project")]
		[ExportMetadata("Priority", 3)]
		public static void NewProjectAction()
		{
			NewProjectAction(null);
		}

		private static readonly List<char> ValidChars =
			Enumerable.Range(1, 128).Select(i => (char)i).
			Where(char.IsLetterOrDigit).Append('_').ToList();

		public static void NewProjectAction(Func<string, bool> projectOpened)
		{
			var citrusPath = Toolbox.FindCitrusDirectory();
			var dialog = new FileDialog {
				AllowedFileTypes = new [] { "" },
				Mode = FileDialogMode.SelectFolder
			};
			bool? dialogCancelled = null;
			// Showing UI must be executed on the UI thread.
			Application.InvokeOnMainThread(() => dialogCancelled = !dialog.RunModal());
			while (!dialogCancelled.HasValue) {
				System.Threading.Thread.Sleep(50);
			}
			if (dialogCancelled.Value) {
				return;
			}
			targetDirectory = NormalizePath(dialog.FileName);
			newCitrusDirectory = NormalizePath(Path.Combine(targetDirectory, "Citrus"));
			var projectName = Path.GetFileName(Path.GetDirectoryName(targetDirectory));
			if (char.IsDigit(projectName[0])) {
				throw new System.Exception($"Project name '{projectName}' cannot start with a digit");
			}
			foreach (char c in projectName) {
				if (!ValidChars.Contains(c)) {
					throw new System.Exception($"Project name '{projectName}' must contain only Latin letters and digits");
				}
			}
			var newProjectApplicationName = String.Join(" ", Regex.Split(projectName, @"(?<!^)(?=[A-Z])"));
			Console.WriteLine($"New project name is \"{projectName}\"");
			var textfileExtensions = new HashSet<string> {
				".cs",
				".xml",
				".csproj",
				".sln",
				".citproj",
				".projitems",
				".shproj",
				".txt",
				".plist",
				".strings",
				".gitignore",
				".tan",
			};
			using (var dc = new DirectoryChanger(Path.Combine(citrusPath, "Samples/EmptyProject"))) {
				var fe = new FileEnumerator(".");
				foreach (var f in fe.Enumerate()) {
					var targetPath = Path.Combine(targetDirectory, f.Path.Replace("EmptyProject", projectName));
					Console.WriteLine($"Copying: {f.Path} => {targetPath}");
					Directory.CreateDirectory(Path.GetDirectoryName(targetPath));
					System.IO.File.Copy(f.Path, targetPath);
					if (textfileExtensions.Contains(Path.GetExtension(targetPath).ToLower(CultureInfo.InvariantCulture))) {
						string text = File.ReadAllText(targetPath);
						text = text.Replace("EmptyProject", projectName);
						text = text.Replace("Empty Project", newProjectApplicationName);
						text = text.Replace("emptyproject", projectName.ToLower());
						var citrusUri = new Uri(newCitrusDirectory);
						var fileUri = new Uri(targetPath);
						var relativeUri = fileUri.MakeRelativeUri(citrusUri);
						// TODO: apply only to .sln and .csproj file
						text = text.Replace("..\\..\\..", relativeUri.ToString());
						if (targetPath.EndsWith(".citproj", StringComparison.OrdinalIgnoreCase)) {
							text = text.Replace("\"CitrusDirectory\": \"../../\",", $"\"CitrusDirectory\": \"{relativeUri}\",");
							newProjectCitprojPath = targetPath;
						}
						File.WriteAllText(targetPath, text);
					}
				}
			}
#if WIN
// TODO: fix unresponsiveness on mac
			var sb = new StringBuilder();
			Git.Exec(citrusPath, "remote -v", sb);
			// Parsing first <URL> from multiple lines like:
			// `origin <URL> (fetch)`
			var citrusRemoteUri = sb.ToString().Split('\n')[0].Split(' ', '\t')[1];
			var relativeSourceCitrusPath = new Uri(targetDirectory).MakeRelativeUri(new Uri(citrusPath)).ToString();
			Git.Exec(targetDirectory, "init");
			Git.Exec(targetDirectory, "add -A");
			Git.Exec(targetDirectory, $"submodule add {relativeSourceCitrusPath} citrus");
			Git.Exec(targetDirectory, "submodule update --init --recursive");
			Git.Exec(targetDirectory, $"submodule set-url citrus {citrusRemoteUri}");
			Git.Exec(targetDirectory, "submodule sync");
			Git.Exec(targetDirectory, "submodule foreach git submodule sync");
			Git.Exec(targetDirectory, "submodule update --init --recursive");
			Git.Exec(targetDirectory, "add .gitmodules");
			Git.Exec(targetDirectory, "commit -m\"Initial commit.\"");
#endif // WIN
			projectOpened?.Invoke(newProjectCitprojPath);

			static string NormalizePath(string path)
			{
				path = path.Replace('\\', '/');
				if (!path.EndsWith('/')) {
					path += '/';
				}
				return path;
			}
		}
	}
}
