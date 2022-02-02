using Markdig;
using System;
using System.IO;
using System.Text;
using System.Threading;

namespace Tangerine.UI
{
	public static class Documentation
	{
		public static bool IsHelpModeOn { get; set; } = false;

		public static string MarkdownDocumentationPath { get; set; } =
			Path.Combine(Directory.GetParent(AppDomain.CurrentDomain.BaseDirectory).Parent.Parent.FullName, "Documentation");
		public static string ImagesPath { get; set; } =
			Path.Combine(Directory.GetParent(AppDomain.CurrentDomain.BaseDirectory).Parent.Parent.FullName, "Documentation", "Images");
		public static string HtmlDocumentationPath { get; set; } =
			Lime.Environment.GetPathInsideDataDirectory("Tangerine", "DocumentationCache");
		public static string StyleSheetPath { get; set; } = "file:///" + Path.Combine(MarkdownDocumentationPath, "stylesheet.css");

		public static string PageExtension { get; set; } = ".md";
		public static string DocExtension { get; set; } = ".html";
		public static string StartPageName { get; set; } = "https://game-forest.github.io/Citrus/articles/intro.html";
		public static string ErrorPageName { get; set; } = "ErrorPage";
		public static string ChangelogPageName { get; set; } = "https://game-forest.github.io/Citrus/articles/tangerine/changelog.html";

		public static string GetPagePath(string pageName)
		{
			string path = Path.Combine(MarkdownDocumentationPath, Path.Combine(pageName.Split('.')));
			return path + PageExtension;
		}

		public static string GetDocPath(string pageName)
		{
			string path = Path.Combine(HtmlDocumentationPath, Path.Combine(pageName.Split('.'))).Replace('\\', '/');
			return path + DocExtension;
		}

		public static string GetImagePath(string ImageName) =>
			Path.Combine(ImagesPath, ImageName).Replace('\\', '/');

		public static void Init()
		{
			return;
			// TODO: removing this code will be next step in help refactoring process
			if (!Directory.Exists(HtmlDocumentationPath)) {
				Directory.CreateDirectory(HtmlDocumentationPath);
			}
			if (!Directory.Exists(MarkdownDocumentationPath)) {
				Directory.CreateDirectory(MarkdownDocumentationPath);
			}
			string startPagePath = GetPagePath(StartPageName);
			if (!File.Exists(startPagePath)) {
				File.WriteAllText(startPagePath, "# Start page #");
			}
			string errorPagePath = GetPagePath(ErrorPageName);
			if (!File.Exists(errorPagePath)) {
				File.WriteAllText(errorPagePath, "# Error #\nThis is error page");
			}
			string changelogPagePath = GetPagePath(ChangelogPageName);
			if (!File.Exists(changelogPagePath)) {
				File.WriteAllText(changelogPagePath, "# Error #\nChangelog is empty!");
			}
			Update();
		}

		private static readonly MarkdownPipeline Pipeline =
			MarkdownExtensions
				.Use<CrosslinkExtension>(
					new MarkdownPipelineBuilder()
				).UseAdvancedExtensions().Build();

		private static void Update(string directoryPath = "")
		{
			string source = Path.Combine(MarkdownDocumentationPath, directoryPath);
			var sourceDirectory = new DirectoryInfo(source);

			if (directoryPath == "Images") {
				return;
}

			string destination = Path.Combine(HtmlDocumentationPath, directoryPath);
			if (!Directory.Exists(destination)) {
				Directory.CreateDirectory(destination);
			}
			foreach (var dir in sourceDirectory.GetDirectories()) {
				Update(Path.Combine(directoryPath, dir.Name));
			}
			foreach (var file in sourceDirectory.GetFiles($"*{PageExtension}")) {
				string destPath = Path.Combine(destination, Path.ChangeExtension(file.Name, DocExtension));
				if (!File.Exists(destPath) || File.GetLastWriteTimeUtc(destPath) <= File.GetLastWriteTimeUtc(file.FullName)) {
					using (StreamReader sr = new StreamReader(file.FullName))
					using (StreamWriter sw = new StreamWriter(destPath, false, Encoding.UTF8)) {
						sw.WriteLine(
							"<head>" +
							$"<link rel=\"stylesheet\" type=\"text/css\" href=\"{StyleSheetPath}\">" +
							"</head>"
						);
						Markdown.ToHtml(sr.ReadToEnd(), sw, Pipeline);
					}
				}
			}
			var destinationDirectory = new DirectoryInfo(destination);
			foreach (var dir in destinationDirectory.GetDirectories()) {
				string path = Path.Combine(source, dir.Name);
				if (!Directory.Exists(path)) {
					Directory.Delete(dir.FullName, true);
				}
			}
			foreach (var file in destinationDirectory.GetFiles($"*{DocExtension}")) {
				string path = Path.Combine(source, Path.ChangeExtension(file.Name, PageExtension));
				if (!File.Exists(path)) {
					File.Delete(file.FullName);
				}
			}
		}

		public static void ShowHelp(string pageName)
		{
			// TODO: carefully think if we still need documentation and markdown related code in this file.
			// for example if we'll want to locally render md files into advanced tips windows.
			// Evgeny Polikutin: if help is open in the same thread,
			// weird crashes in GestureManager occur (something changes activeGestures collection).
			// Remove at your own risk
			new Thread(() => {
				Thread.CurrentThread.IsBackground = true;
				Lime.Environment.OpenUrl(pageName);
			}).Start();
		}
	}
}
