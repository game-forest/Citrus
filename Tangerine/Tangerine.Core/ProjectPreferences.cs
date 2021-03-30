using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Lime;

namespace Tangerine.Core
{
	// TODO: Merge with what Orange reads from project preferences file when refactoring Orange
	public class ProjectPreferences
	{
		public static ProjectPreferences Instance => Project.Current.Preferences;

		private readonly List<ResolutionPreset> resolutions = new List<ResolutionPreset>();
		public IReadOnlyList<ResolutionPreset> Resolutions => resolutions;
		public ResolutionPreset DefaultResolution { get; private set; }
		public bool IsLandscapeDefault { get; private set; }

		public Dictionary<string, RemoteScriptingConfiguration> RemoteScriptingConfigurations =
			new Dictionary<string, RemoteScriptingConfiguration>();

		public RemoteScriptingConfiguration RemoteScriptingCurrentConfiguration;

		public class RemoteScriptingConfiguration
		{
			private readonly List<string> projectReferences = new List<string>();
			private readonly List<string> frameworkReferences = new List<string>();
			public readonly string ScriptsProjectPath;
			public readonly string ScriptsAssemblyName;
			public readonly string EntryPointsClass;
			public readonly string RemoteStoragePath;
			public readonly string BuildTarget;
			public IReadOnlyList<string> FrameworkReferences => frameworkReferences;
			public IReadOnlyList<string> ProjectReferences => projectReferences;

			private static readonly List<string> defaultFrameworkReferences = new List<string> {
				@"mscorlib.dll",
				@"System.dll",
				@"System.Core.dll"
			};

			public RemoteScriptingConfiguration(dynamic json)
			{
				var projectDirectory = Orange.The.Workspace.ProjectDirectory;
				ScriptsProjectPath = AssetPath.CorrectSlashes(Path.Combine(projectDirectory, (string)json.ScriptsProjectPath));
				ScriptsAssemblyName = (string)json.ScriptsAssemblyName;
				var projectReferencesPath = Path.Combine(projectDirectory, (string)json.ProjectReferencesPath);
				var frameworkReferencesPath = Path.Combine(projectDirectory, (string)json.FrameworkReferencesPath);
				var references = new List<string>();
				foreach (string reference in json.FrameworkReferences) {
					references.Add(reference);
				}
				foreach (string reference in defaultFrameworkReferences.Concat(references).Distinct()) {
					frameworkReferences.Add(AssetPath.CorrectSlashes(Path.Combine(frameworkReferencesPath, reference)));
				}
				foreach (string reference in json.ProjectReferences) {
					projectReferences.Add(AssetPath.CorrectSlashes(Path.Combine(projectReferencesPath, reference)));
				}
				EntryPointsClass = (string)json.EntryPointsClass;
				RemoteStoragePath = AssetPath.CorrectSlashes(Path.Combine(projectDirectory, (string)json.RemoteStoragePath));
				BuildTarget = (string)json.BuildTarget;
			}
		}

		public void Initialize()
		{
			InitializeResolutions();
			InitializeRemoteScriptingPreferences();
		}

		private void InitializeResolutions()
		{
			try {
				var projectJson = Orange.The.Workspace.ProjectJson.AsDynamic;
				var resolutionMarkers = new Dictionary<string, ResolutionMarker>();
				foreach (var marker in projectJson.ResolutionSettings.Markers) {
					var name = (string)marker.Name;
					resolutionMarkers.Add(name, new ResolutionMarker((string)marker.PortraitMarker, (string)marker.LandscapeMarker));
				}
				foreach (var resolution in projectJson.ResolutionSettings.Resolutions) {
					var name = (string)resolution.Name;
					var width = (int)resolution.Width;
					var height = (int)resolution.Height;
					var usingResolutionMarkers = new List<ResolutionMarker>();
					foreach (string resolutionMarker in resolution.ResolutionMarkers) {
						usingResolutionMarkers.Add(resolutionMarkers[resolutionMarker]);
					}
					resolutions.Add(new ResolutionPreset(name, width, height, usingResolutionMarkers));
				}
				DefaultResolution = resolutions[0];
				IsLandscapeDefault = (bool)(projectJson.ResolutionSettings.IsLandscapeDefault ?? true);
				Console.WriteLine("Resolution presets was successfully loaded.");
			} catch {
				InitializeDefaultResolutions();
			}
		}

		private void InitializeDefaultResolutions()
		{
			var resolutionMarkers = new[] { new ResolutionMarker("@Portrait", "@Landscape") };
			DefaultResolution = new ResolutionPreset("iPad", 1024, 768, resolutionMarkers);
			resolutions.Clear();
			resolutions.AddRange(new[] {
				DefaultResolution,
				new ResolutionPreset("Wide Screen", 1366, 768, resolutionMarkers),
				new ResolutionPreset("iPhone 4", 960, 640, resolutionMarkers),
				new ResolutionPreset("iPhone 5", 1136, 640, resolutionMarkers),
				new ResolutionPreset("iPhone 6, 7, 8", 1334, 750, resolutionMarkers),
				new ResolutionPreset("iPhone 6, 7, 8 Plus", 1920, 1080, resolutionMarkers),
				new ResolutionPreset("Google Nexus 9 portrait", 976, 768, resolutionMarkers),
				new ResolutionPreset("Google Nexus 9 landscape", 1024, 720, resolutionMarkers),
				new ResolutionPreset("Galaxy S8", 2960, 1440, resolutionMarkers),
				new ResolutionPreset("iPhone X", 2436, 1125, resolutionMarkers),
				new ResolutionPreset("Xperia Z4 Tablet", 2560, 1600, resolutionMarkers),
				new ResolutionPreset("LG G6", 2880, 1440, resolutionMarkers),
			});
			IsLandscapeDefault = true;
			Console.WriteLine("Default resolution presets was loaded.");
		}

		private void InitializeRemoteScriptingPreferences()
		{
			try {
				var projectJson = Orange.The.Workspace.ProjectJson.AsDynamic;

				var remoteScriptingSection = projectJson.RemoteScripting;
				foreach (var kv in remoteScriptingSection) {
					var configuration = new RemoteScriptingConfiguration(kv.Value);
					RemoteScriptingConfigurations.Add(kv.Name, configuration); // configurationName
					RemoteScriptingCurrentConfiguration ??= configuration;
				}
				Console.WriteLine("Remote scripting preferences was successfully loaded.");
			} catch {
				InitializeDefaultRemoteScriptingPreferences();
			}
		}

		private void InitializeDefaultRemoteScriptingPreferences()
		{
			RemoteScriptingConfigurations.Clear();
			RemoteScriptingCurrentConfiguration = null;
		}
	}
}
