using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Lime;
using Tangerine.Core;
using Tangerine.UI;

namespace Tangerine
{
	using CategoriesDictionary = Dictionary<string, Dictionary<string, string>>;

	public class ShortcutBinding
	{
		public ICommand Command { get; set; }
		public Shortcut Shortcut { get; set; }
	}

	public static class HotkeyRegistry
	{
		private static readonly List<ShortcutBinding> defaults = new List<ShortcutBinding>();

		private static HotkeyProfile currentProfile;
		public static HotkeyProfile CurrentProfile
		{
			get { return currentProfile; }
			set
			{
				if (currentProfile != null) {
					foreach (var command in currentProfile.Commands) {
						command.Command.Shortcut = new Shortcut();
					}
				}
				currentProfile = value ?? throw new ArgumentNullException(nameof(value));
				foreach (var command in currentProfile.Commands) {
					command.Command.Shortcut = command.Shortcut;
				}
				AppUserPreferences.Instance.CurrentHotkeyProfile = currentProfile.Name;
			}
		}

		public static List<HotkeyProfile> Profiles { get; private set; } = new List<HotkeyProfile>();
		public static string ProfilesDirectory
		{
			get
			{
				return Lime.Environment.GetPathInsideDataDirectory("Tangerine", "HotkeyProfiles");
			}
		}

		public static string DefaultProfileName { get; set; } = "Default";
		public static string CurrentProjectName { get; set; } = null;
		public static Action Reseted { get; set; }

		public static void InitDefaultShortcuts()
		{
			defaults.Clear();
			foreach (var command in CommandRegistry.RegisteredCommands()) {
				defaults.Add(new ShortcutBinding {
					Command = command,
					Shortcut = command.Shortcut,
				});
			}
		}

		public static HotkeyProfile CreateProfile(string profileName)
		{
			var profile = new HotkeyProfile(CommandRegistry.RegisteredCategories(), profileName);
			return profile;
		}

		public static void ResetToDefaults()
		{
			var defaultProfile = new HotkeyProfile(CommandRegistry.RegisteredCategories(), DefaultProfileName);
			foreach (var binding in defaults) {
				binding.Command.Shortcut = binding.Shortcut;
			}
			Profiles.Clear();
			foreach (string file in Directory.EnumerateFiles(ProfilesDirectory)) {
				File.Delete(file);
			}
			currentProfile = CreateProfile(DefaultProfileName);
			currentProfile.Save();
			Profiles.Add(currentProfile);
			Reseted?.Invoke();
		}

		public static void UpdateProfiles()
		{
			string oldCurrentName = CurrentProfile.Name;
			var newProfiles = new List<HotkeyProfile>();
			for (int i = 0; i < Profiles.Count; ++i) {
				var profile = Profiles[i];
				var newProfile = new HotkeyProfile(CommandRegistry.RegisteredCategories(), profile.Name);
				newProfile.Load();
				Profiles[i] = newProfile;
			}
			CurrentProfile = Profiles.FirstOrDefault(i => i.Name == oldCurrentName);
		}
	}

	public class HotkeyProfile
	{
		private const string ProjectDependent = "ProjectDependent";

		/// <summary>
		/// The max number of fail attempts to access the file.
		/// </summary>
		private const int FileAccessAttemptsCount = 4;

		/// <summary>
		/// Time in milliseconds between file access attempts.
		/// </summary>
		private const int AccessFailWaitDuration = 360;

		/// <summary>
		/// File with hotkey settings common to all projects.
		/// </summary>
		public string FilePath => Path.Combine(HotkeyRegistry.ProfilesDirectory, Name);

		public IEnumerable<CommandInfo> Commands => Categories.SelectMany(i => i.Commands.Values);
		public readonly List<CommandCategoryInfo> Categories;

		public readonly string Name;

		internal HotkeyProfile(IEnumerable<CommandCategoryInfo> categories, string name)
		{
			Categories = new List<CommandCategoryInfo>();
			foreach (var categoryInfo in categories) {
				var newCategoryInfo = new CommandCategoryInfo(categoryInfo.Id);
				foreach (var commandInfo in categoryInfo.Commands.Values) {
					var newCommandInfo = new CommandInfo(commandInfo.Command, newCategoryInfo, commandInfo.Id) {
						Shortcut = commandInfo.Command.Shortcut,
						IsProjectSpecific = commandInfo.IsProjectSpecific,
					};
					newCategoryInfo.Commands.Add(newCommandInfo.Id, newCommandInfo);
				}
				Categories.Add(newCategoryInfo);
			}
			Name = name;
		}

		public void Load(string filePath)
		{
			string projectName = HotkeyRegistry.CurrentProjectName ?? string.Empty;
			bool isProjectLoaded = projectName != string.Empty;
			if (!IsNativePath(filePath)) {
				// Copying all project-dependent parts of this profile.
				string srcDirectory = Path.Combine(Path.GetDirectoryName(filePath), ProjectDependent);
				string dstDirectory = Path.Combine(HotkeyRegistry.ProfilesDirectory, ProjectDependent);
				if (Directory.Exists(srcDirectory)) {
					var directories = Directory.GetDirectories(srcDirectory, "*", SearchOption.TopDirectoryOnly);
					string fileName = Path.GetFileNameWithoutExtension(filePath);
					foreach (string projectPath in directories) {
						string srcProfilePath = Path.Combine(projectPath, fileName);
						if (File.Exists(srcProfilePath)) {
							string dstProfilePath = srcProfilePath.Replace(srcDirectory, dstDirectory);
							Directory.CreateDirectory(Path.GetDirectoryName(dstProfilePath));
							SafeFileCopy(srcProfilePath, dstProfilePath, true);
						}
					}
				}
			}
			var loadedCategories = LoadCategories(filePath).AsEnumerable();
			if (isProjectLoaded) {
				var projectProfilePath = GetProjectSpecifiedPath(filePath, projectName);
				if (File.Exists(projectProfilePath)) {
					loadedCategories = loadedCategories.Concat(LoadCategories(projectProfilePath));
				}
			}
			foreach (var i in loadedCategories) {
				var category = Categories.FirstOrDefault(j => j.Id == i.Key);
				if (category != null) {
					foreach (var binding in i.Value) {
						var info = category.Commands.Values.FirstOrDefault(j => j.Id == binding.Key);
						if (info != null) {
							try {
								info.Shortcut = new Shortcut(binding.Value);
							} catch (System.Exception) {
								Debug.Write($"Unknown shortcut: {binding.Value}");
							}
						} else {
							Debug.Write($"Unknown command: {i.Key}.{binding.Key}");
						}
					}
				} else {
					Debug.Write($"Unknown command category: {i.Key}");
				}
			}
			Save();
		}

		public void Load()
		{
			Load(FilePath);
		}

		public void Save(string filePath)
		{
			var commonData = new CategoriesDictionary();
			var projectData = new CategoriesDictionary();
			string projectName = HotkeyRegistry.CurrentProjectName ?? string.Empty;
			bool isProjectLoaded = projectName != string.Empty;
			foreach (var category in Categories) {
				var commonBindings = new Dictionary<string, string>();
				var projectBindings = new Dictionary<string, string>();
				foreach (var info in category.Commands.Values) {
					var shortcut = info.Shortcut.ToString();
					var bindings = (!info.IsProjectSpecific || !isProjectLoaded) ? commonBindings : projectBindings;
					bindings.Add(info.Id, shortcut == "Unknown" ? null : shortcut);
				}
				commonData.Add(category.Id, commonBindings);
				projectData.Add(category.Id, projectBindings);
			}
			if (!IsNativePath(filePath)) {
				// Copying all project-dependent parts of this profile.
				string fileName = Path.GetFileNameWithoutExtension(filePath);
				if (File.Exists(filePath)) {
					filePath = Path.GetDirectoryName(filePath);
				}
				Directory.CreateDirectory(filePath);
				string srcDirectory = Path.Combine(HotkeyRegistry.ProfilesDirectory, ProjectDependent);
				string dstDirectory = Path.Combine(filePath, ProjectDependent);
				filePath = Path.Combine(filePath, fileName);
				var directories = Directory.GetDirectories(srcDirectory, "*", SearchOption.TopDirectoryOnly);
				foreach (string projectPath in directories) {
					string srcProfilePath = Path.Combine(projectPath, fileName);
					if (File.Exists(srcProfilePath)) {
						string dstProfilePath = srcProfilePath.Replace(srcDirectory, dstDirectory);
						Directory.CreateDirectory(Path.GetDirectoryName(dstProfilePath));
						SafeFileCopy(srcProfilePath, dstProfilePath, true);
					}
				}
			}
			SaveCategories(filePath, commonData);
			if (isProjectLoaded) {
				string projectProfilePath = GetProjectSpecifiedPath(filePath, projectName);
				Directory.CreateDirectory(Path.GetDirectoryName(projectProfilePath));
				SaveCategories(projectProfilePath, projectData);
			}
		}

		public void Save()
		{
			Save(FilePath);
		}

		public void Delete()
		{
			string filePath = FilePath;
			File.Delete(filePath);
			string directoryPath = Path.Combine(HotkeyRegistry.ProfilesDirectory, ProjectDependent);
			if (Directory.Exists(directoryPath)) {
				var directories = Directory.GetDirectories(directoryPath, "*", SearchOption.TopDirectoryOnly);
				string fileName = Path.GetFileNameWithoutExtension(filePath);
				foreach (string projectPath in directories) {
					string profilePath = Path.Combine(projectPath, fileName);
					if (File.Exists(profilePath)) {
						File.Delete(profilePath);
					}
				}
			}
			HotkeyRegistry.Profiles.Remove(this);
		}

		private static bool IsNativePath(string path) =>
			path.StartsWith(HotkeyRegistry.ProfilesDirectory);

		private static string GetProjectSpecifiedPath(string commonFilePath, string project)
		{
			var directory = Path.GetDirectoryName(commonFilePath);
			var fileName = Path.GetFileNameWithoutExtension(commonFilePath);
			return Path.Combine(directory, ProjectDependent, project, fileName);
		}

		private static CategoriesDictionary LoadCategories(string filePath)
		{
			// Since there can be several Tangerines open, it is
			// possible that we will not be able to access the file.
			CategoriesDictionary UnsafeLoadCategories(string path) =>
				TangerinePersistence.Instance.ReadFromFile<CategoriesDictionary>(path);
			CategoriesDictionary TryLoadCategories(int nestingLevel)
			{
				try {
					return UnsafeLoadCategories(filePath);
				} catch (IOException) {
					if (nestingLevel == FileAccessAttemptsCount) {
						throw;
					} else {
						Thread.Sleep(AccessFailWaitDuration);
						return TryLoadCategories(nestingLevel + 1);
					}
				}
			}
			return TryLoadCategories(nestingLevel: 0);
		}

		private static void SaveCategories(string filePath, CategoriesDictionary data)
		{
			// Since there can be several Tangerines open, it is
			// possible that we will not be able to access the file.
			void UnsafeSaveCategories() =>
				TangerinePersistence.Instance.WriteToFile(filePath, data, Persistence.Format.Json);
			void TrySaveCategories(int nestingLevel)
			{
				try {
					UnsafeSaveCategories();
				} catch (IOException) {
					if (nestingLevel == FileAccessAttemptsCount) {
						throw;
					} else {
						Thread.Sleep(AccessFailWaitDuration);
						TrySaveCategories(nestingLevel + 1);
					}
				}
			}
			TrySaveCategories(nestingLevel: 0);
		}

		private static void SafeFileCopy(string sourceFileName, string destFileName, bool overwrite)
		{
			void TryCopyFile(int nestingLevel)
			{
				try {
					File.Copy(sourceFileName, destFileName, overwrite);
				} catch (IOException) {
					// Since there can be several Tangerines open, it is
					// possible that we will not be able to access the file.
					if (nestingLevel == FileAccessAttemptsCount) {
						throw;
					} else {
						Thread.Sleep(AccessFailWaitDuration);
						TryCopyFile(nestingLevel + 1);
					}
				}
			}
			TryCopyFile(nestingLevel: 0);
		}
	}
}
