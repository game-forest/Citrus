using System;
using System.Collections.Generic;
using System.IO;
using Yuzu;
using Lime;

namespace Orange
{
	public class ProjectConfig
	{
		[YuzuOptional]
		public int ActiveTargetIndex;

		/// <summary>
		/// Asset cache mode that will be used  on workspace load
		/// </summary>
		[YuzuOptional]
		public AssetCacheMode AssetCacheMode = AssetCacheMode.Local | AssetCacheMode.Remote;

		/// <summary>
		/// Path to local asset cache that will be used on load
		/// </summary>
		[YuzuOptional]
		public string LocalAssetCachePath = Path.Combine(".orange", "Cache");

		[YuzuOptional]
		public bool BundlePickerVisible = false;
	}

	public class WorkspaceConfig
	{
		[YuzuMember]
		public Vector2 ClientSize;

		[YuzuMember]
		public Vector2 ClientPosition;

		[YuzuOptional]
		public bool BenchmarkEnabled;

		[YuzuOptional]
		public Dictionary<string, ProjectConfig> PerProjectConfig = new Dictionary<string, ProjectConfig>();

		[YuzuOptional]
		public List<string> RecentProjects = new List<string>();

		[YuzuOptional]
		public WindowState WindowState { get; set; } = WindowState.Normal;

		public void RegisterRecentProject(string projectPath)
		{
			if (projectPath == null) {
				return;
			}
			if (!Path.IsPathRooted(projectPath)) {
				throw new InvalidOperationException("Project path must be rooted.");
			}
			var index = RecentProjects.FindIndex((s) =>
				string.Compare(NormalizePath(s), NormalizePath(projectPath), StringComparison.OrdinalIgnoreCase) == 0);
			if (index != -1) {
				RecentProjects.RemoveAt(index);
			}
			RecentProjects.Insert(0, projectPath);
		}

		public ProjectConfig GetProjectConfig(string projectFilePath)
		{
			if (string.IsNullOrEmpty(projectFilePath)) {
				return null;
			}
			projectFilePath = projectFilePath.Replace('\\', '/');
			if (!PerProjectConfig.TryGetValue(projectFilePath, out ProjectConfig projectConfig)) {
				PerProjectConfig.Add(projectFilePath, projectConfig = new ProjectConfig());
			}
			return projectConfig;
		}

		public static string GetDataPath()
		{
			var name = System.Reflection.Assembly.GetExecutingAssembly().GetName().Name;
			return Lime.Environment.GetDataDirectory("Game Forest", name, "1.0");
		}

		private static string GetConfigPath()
		{
			var configPath = Path.Combine(GetDataPath(), ".config");
			return configPath;
		}

		private static readonly Lime.Persistence persistence =
			new Lime.Persistence(new CommonOptions { AllowUnknownFields = true }, null);

		public static WorkspaceConfig Load()
		{
			try {
				return persistence.ReadFromFile<WorkspaceConfig>(GetConfigPath());
			} catch {
				return new WorkspaceConfig();
			}
		}

		public static void Save(WorkspaceConfig config)
		{
			persistence.WriteToFile(GetConfigPath(), config, Persistence.Format.Json);
		}

		public void RemoveRecentProject(string projectPath)
		{
			var index = RecentProjects.FindIndex((s) =>
				string.Compare(NormalizePath(s), NormalizePath(projectPath), StringComparison.OrdinalIgnoreCase) == 0);
			if (index != -1) {
				RecentProjects.RemoveAt(index);
			}
		}

		private string NormalizePath(string path)
		{
			return Path.GetFullPath(new Uri(path).LocalPath)
				.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
				.ToUpperInvariant();
		}
	}
}
