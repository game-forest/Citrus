using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Text.RegularExpressions;
using Lime;

namespace Orange
{
	public class DictionaryExtractor
	{
		public static Func<DictionaryExtractor> Factory = () => new DictionaryExtractor();

		private static readonly Regex tanTextMatcher = new Regex(
			@"^(\s*""\S*""\s*:)\s*""(?<string>[^""\\]*(?:\\.[^""\\]*)*)"",?\s?$",
			RegexOptions.Compiled | RegexOptions.Multiline);

		private static readonly Regex tanAnimatedTextMatcher = new Regex(
			@"^\s*\[\s*\d+\s*,\s*\d+\s*,\s*""(?<string>[^""\\]*(?:\\.[^""\\]*)*)""\],?\s?$",
			RegexOptions.Compiled | RegexOptions.Multiline);

		private static readonly Regex sourceTextMatcher = new Regex(
			@"(?<prefix>[@$])?""(?<string>[^""\\]*(?:\\.[^""\\]*)*)""", RegexOptions.Compiled);

		private static readonly Regex taggedStringMatcher = new Regex(@"^\[.*\]([^\.]{1}.*)$", RegexOptions.Compiled);

		private LocalizationDictionary dictionary;

		public void ExtractDictionary()
		{
			dictionary = new LocalizationDictionary();
			ExtractTexts();
			foreach (var name in GetFileNames()) {
				CleanupAndSaveDictionary(Path.Combine(GetDictionariesDirectory(), name));
			}
		}

		private static string GetDefaultFileName()
		{
			return Path.ChangeExtension("Dictionary.txt", CreateSerializer().GetFileExtension());
		}

		private static string GetDictionariesDirectory()
		{
			var dictionariesDirectory = Path.Combine(The.Workspace.AssetsDirectory, Localization.DictionariesPath);
			if (Directory.Exists(dictionariesDirectory)) {
				return Localization.DictionariesPath;
			}
			return string.Empty;
		}

		private static IEnumerable<string> GetFileNames()
		{
			var files = Directory.GetFiles(
				Path.Combine(The.Workspace.AssetsDirectory, GetDictionariesDirectory()),
				"*" + CreateSerializer().GetFileExtension(),
				SearchOption.TopDirectoryOnly);
			return files
				.Select(f => Path.GetFileName(f))
				.Where(f => f.StartsWith("Dictionary"));
		}

		private void CleanupAndSaveDictionary(string path)
		{
			var result = new LocalizationDictionary();
			LoadDictionary(result, path);
			var addContext = ShouldAddContextToLocalizedDictionary() ||
			                 path.EndsWith(GetDefaultFileName(), StringComparison.OrdinalIgnoreCase);
			MergeDictionaries(result, dictionary, addContext);
			SaveDictionary(result, path);
		}

		private static void MergeDictionaries(LocalizationDictionary current, LocalizationDictionary modified, bool addContext)
		{
			int added = 0;
			int deleted = 0;
			int updated = 0;
			foreach (var key in modified.Keys.ToList()) {
				if (!current.ContainsKey(key)) {
					Logger.Write("+ " + key);
					added++;
					current[key] = new LocalizationEntry() {
						Text = modified[key].Text,
						Context = modified[key].Context
					};
				} else {
					var currentEntry = current[key];
					var newEntry = modified[key];
					if (currentEntry.Context != newEntry.Context) {
						Logger.Write("Context updated for: " + key);
						updated++;
						currentEntry.Context = newEntry.Context;
					}
				}
				if (!addContext) {
					current[key].Context = "";
				}
			}
			foreach (var key in current.Keys.ToList()) {
				if (!modified.ContainsKey(key) && !LocalizationDictionary.IsComment(key)) {
					Logger.Write("- " + key);
					deleted++;
					current.Remove(key);
				}
			}

			Logger.Write("Added {0}\nDeleted {1}\nContext updated {2}", added, deleted, updated);
		}

		private static void SaveDictionary(LocalizationDictionary dictionary, string path)
		{
			using (new DirectoryChanger(The.Workspace.AssetsDirectory)) {
				using (var stream = new FileStream(path, FileMode.Create)) {
					dictionary.WriteToStream(CreateSerializer(), stream);
				}
			}
		}

		private static ILocalizationDictionarySerializer CreateSerializer() => new LocalizationDictionaryTextSerializer();

		private static void LoadDictionary(LocalizationDictionary dictionary, string path)
		{
			if (AssetBundle.Current.FileExists(path)) {
				using (var stream = AssetBundle.Current.OpenFile(path)) {
					dictionary.ReadFromStream(CreateSerializer(), stream);
				}
			}
		}

		protected virtual void ExtractTexts()
		{
			bool ScanFilter(DirectoryInfo directoryInfo)
			{
				if (directoryInfo.Name == "bin") return false;
				if (directoryInfo.Name == "obj") return false;
				if (directoryInfo.Name == ".svn") return false;
				if (directoryInfo.Name == ".git") return false;
				if (directoryInfo.Name == ".vs") return false;
				if (directoryInfo.Name == "Citrus") return false;
				return true;
			}

			var sourceFiles = new ScanOptimizedFileEnumerator(The.Workspace.ProjectDirectory, ScanFilter);
			using (new DirectoryChanger(The.Workspace.ProjectDirectory)) {
				foreach (var file in sourceFiles.Enumerate(".cs")) {
					var content = File.ReadAllText(file, Encoding.UTF8);
					ProcessSourceFile(file, content);
				}
			}
			foreach (var file in AssetBundle.Current.EnumerateFiles(null, ".json")) {
				// First of all scan lines like this: "[]..."
				var content = AssetBundle.Current.ReadAllText(file, Encoding.UTF8);
				ProcessSourceFile(file, content);
				// Then like this: Text "..."
				if (!ShouldLocalizeOnlyTaggedSceneTexts()) {
					ProcessSceneFile(file, content);
				}
			}
			foreach (var file in AssetBundle.Current.EnumerateFiles(null, ".tan")) {
				var content = AssetBundle.Current.ReadAllText(file, Encoding.UTF8);
				ProcessTanFile(file, content);
			}
		}

		private static bool ShouldLocalizeOnlyTaggedSceneTexts() =>
			The.Workspace.ProjectJson.GetValue("LocalizeOnlyTaggedSceneTexts", false);

		private static bool ShouldAddContextToLocalizedDictionary() =>
			The.Workspace.ProjectJson.GetValue("AddContextToLocalizedDictionary", true);

		private void ProcessSourceFile(string path, string content)
		{
			var context = GetContext(path);
			foreach (var match in sourceTextMatcher.Matches(content)) {
				var m = match as Match;
				var prefix = m.Groups["prefix"].Value;
				var s = m.Groups["string"].Value;
				if (HasAlphabeticCharacters(s) && IsCorrectTaggedString(s)) {
					if (string.IsNullOrEmpty(prefix)) {
						AddToDictionary(s, context);
					} else {
						var type = prefix == "@" ? "verbatim" : "interpolated";
						Logger.Write($"WARNING: Ignoring {type} string: {s}");
					}
				}
			}
		}

		private void ProcessSceneFile(string path, string content)
		{
			const string textPropertiesPattern = @"^(\s*Text)\s""([^""\\]*(?:\\.[^""\\]*)*)""$";
			var context = GetContext(path);
			foreach (var match in Regex.Matches(content, textPropertiesPattern, RegexOptions.Multiline)) {
				var s = ((Match)match).Groups[2].Value;
				if (HasAlphabeticCharacters(s)) {
					AddToDictionary(s, context);
				}
			}
		}

		private void ProcessTanFile(string path, string content)
		{
			var context = GetContext(path);
			var onlyTagged = ShouldLocalizeOnlyTaggedSceneTexts();
			var matches1 = tanTextMatcher.Matches(content);
			var matches2 = tanAnimatedTextMatcher.Matches(content);

			foreach (var match in matches1.OfType<Match>().Concat(matches2.OfType<Match>()).Where(m => m.Success)) {
				var s = match.Groups["string"].Value;
				if (HasAlphabeticCharacters(s) && (!onlyTagged || IsCorrectTaggedString(s))) {
					AddToDictionary(s, context);
				}
			}
		}

		protected static string GetContext(string file) => file;

		private static bool IsCorrectTaggedString(string str) => taggedStringMatcher.Match(str).Success;

		protected void AddToDictionary(string key, string context)
		{
			var match = taggedStringMatcher.Match(key);
			if (match.Success) {
				// The line starts with "[...]..."
				var value = Unescape(match.Groups[1].Value);
				if (key.StartsWith("[]")) {
					key = key.Substring(2);
				}
				AddToDictionaryHelper(Unescape(key), value, context);
			} else {
				// The line has no [] prefix, but still should be localized.
				// E.g. most of texts in scene files.
				AddToDictionaryHelper(Unescape(key), Unescape(key), context);
			}
		}

		protected static bool HasAlphabeticCharacters(string text) => text.Any(c => char.IsLetter(c));

		private void AddToDictionaryHelper(string key, string value, string context)
		{
			var e = dictionary.GetEntry(key);
			e.Text = value;
			var ctx = new List<string>();
			if (!string.IsNullOrWhiteSpace(e.Context)) {
				ctx = e.Context.Split('\n').ToList();
			}
			if (!ctx.Contains(context)) {
				ctx.Add(context);
			}
			e.Context = string.Join("\n", ctx);
		}

		private static string Unescape(string text) =>
			text.Replace("\\n", "\n")
				.Replace("\\\"", "\"")
				.Replace("\\'", "'");
	}
}
