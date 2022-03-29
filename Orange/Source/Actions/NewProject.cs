using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Lime;

namespace Orange
{
	public static class NewProject
	{
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
				AllowedFileTypes = new[] { string.Empty },
				Mode = FileDialogMode.SelectFolder,
				Title = "Select source example project",
			};
			bool? dialogCanceled = null;
			// Showing UI must be executed on the UI thread.
			Application.InvokeOnMainThread(() => dialogCanceled = !dialog.RunModal());
			while (!dialogCanceled.HasValue) {
				System.Threading.Thread.Sleep(50);
			}
			if (dialogCanceled.Value) {
				return;
			}
			string sourceDirectory = NormalizePath(dialog.FileName);
			dialog.Title = "Select new project directory. Directory name will be used as project name.";
			dialogCanceled = null;
			Application.InvokeOnMainThread(() => dialogCanceled = !dialog.RunModal());
			while (!dialogCanceled.HasValue) {
				System.Threading.Thread.Sleep(50);
			}
			if (dialogCanceled.Value) {
				return;
			}
			string targetDirectory = NormalizePath(dialog.FileName);
			var isNewExample = Path.GetDirectoryName(Path.GetDirectoryName(sourceDirectory))
				== Path.GetDirectoryName(Path.GetDirectoryName(targetDirectory));
			string newCitrusDirectory = !isNewExample
				? NormalizePath(Path.Combine(targetDirectory, "Citrus"))
				: citrusPath;
			var projectName = Path.GetFileName(Path.GetDirectoryName(targetDirectory));
			if (char.IsDigit(projectName[0])) {
				throw new System.Exception($"Project name '{projectName}' cannot start with a digit");
			}
			foreach (char c in projectName) {
				if (!ValidChars.Contains(c)) {
					throw new System.Exception(
						$"Project name '{projectName}' must contain only Latin letters and digits"
					);
				}
			}
			var sourceProjectName = Path.GetFileName(Path.GetDirectoryName(sourceDirectory));
			var sourceProjectApplicationName = string.Join(" ", Regex.Split(sourceProjectName, @"(?<!^)(?=[A-Z])"));
			var newProjectApplicationName = string.Join(" ", Regex.Split(projectName, @"(?<!^)(?=[A-Z])"));
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
				".ps1",
				".command",
			};
			string newProjectCitprojPath = null;
			using (var dc = new DirectoryChanger(sourceDirectory)) {
				var fe = new FileEnumerator(".");
				foreach (var f in fe.Enumerate()) {
					var targetPath = Path.Combine(targetDirectory, f.Replace("EmptyProject", projectName));
					Console.WriteLine($"Copying: {f} => {targetPath}");
					Directory.CreateDirectory(Path.GetDirectoryName(targetPath));
					System.IO.File.Copy(f, targetPath);
					if (
						textfileExtensions.Contains(
							Path.GetExtension(targetPath).ToLower(CultureInfo.InvariantCulture)
						)
					) {
						string text = File.ReadAllText(targetPath);
						text = text.Replace(sourceProjectName, projectName);
						text = text.Replace(sourceProjectApplicationName, newProjectApplicationName);
						text = text.Replace(sourceProjectName.ToLower(), projectName.ToLower());
						var citrusUri = new Uri(newCitrusDirectory);
						var fileUri = new Uri(targetPath);
						var relativeUri = fileUri.MakeRelativeUri(citrusUri);
						// TODO: apply only to .sln and .csproj file
						text = text.Replace("..\\..\\..", relativeUri.ToString().Replace('/', '\\'));
						if (targetPath.EndsWith(".citproj", StringComparison.OrdinalIgnoreCase)) {
							text = text.Replace(
								"\"CitrusDirectory\": \"../../\",",
								$"\"CitrusDirectory\": \"{relativeUri}\","
							);
							newProjectCitprojPath = targetPath;
						}
						if (targetPath.EndsWith(".ps1", StringComparison.OrdinalIgnoreCase)) {
							text = text.Replace("..\\..", relativeUri.ToString().Replace('/', '\\'));
						}
						if (targetPath.EndsWith(".command", StringComparison.OrdinalIgnoreCase)) {
							text = text.Replace("../..", relativeUri.ToString());
						}
						File.WriteAllText(targetPath, text);
					}
				}
			}
			if (string.IsNullOrEmpty(newProjectCitprojPath)) {
				throw new InvalidOperationException($"Invalid new project citproj path: `{newProjectCitprojPath}`");
			}
			if (isNewExample) {
				// Skip git repository initialization step if new project is being made next to example project.
				return;
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
			Git.Exec(targetDirectory, $"submodule add {relativeSourceCitrusPath} Citrus");
			Git.Exec(targetDirectory, "submodule update --init --recursive");
			Git.Exec(targetDirectory, $"submodule set-url Citrus {citrusRemoteUri}");
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
