using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Lime;
using Environment = System.Environment;

namespace Orange
{
	public static class Toolbox
	{
		private static readonly char[] CmdArgumentDelimiters = { ':' };
		static Toolbox()
		{
		}

		public static string ToWindowsSlashes(string path)
		{
			return path.Replace('/', '\\');
		}

		public static string ToUnixSlashes(string path)
		{
			return path.Replace('\\', '/');
		}

		public static string GetCommandLineArg(string name)
		{
			foreach (var argument in Environment.GetCommandLineArgs()) {
				var parts = argument.Split(CmdArgumentDelimiters, 2);
				if (parts.Length == 2 && parts[0].Equals(name)) {
					return parts[1];
				}
			}
			return null;
		}

		public static bool GetCommandLineFlag(string name)
		{
			return Array.IndexOf(Environment.GetCommandLineArgs(), name) >= 0;
		}

		public static string GetApplicationDirectory()
		{
			var assemblyPath = Assembly.GetExecutingAssembly().GetName().CodeBase;
#if MAC
			if (assemblyPath.StartsWith("file:")) {
				assemblyPath = assemblyPath.Remove(0, 5);
			}
#elif WIN
			if (assemblyPath.StartsWith("file:///")) {
				assemblyPath = assemblyPath.Remove(0, 8);
			}
#endif
			return Path.GetDirectoryName(assemblyPath);
		}

		public static string FindCitrusDirectory()
		{
			var path = Uri.UnescapeDataString((new Uri(Assembly.GetExecutingAssembly().CodeBase)).AbsolutePath);
			while (!File.Exists(Path.Combine(path, CitrusVersion.Filename))) {
				path = Path.GetDirectoryName(path);
				if (string.IsNullOrEmpty(path)) {
					throw new InvalidOperationException("Can't find Citrus directory.");
				}
			}
			path = path.Replace('\\', '/');
			if (!path.EndsWith('/')) {
				path += '/';
			}
			return path;
		}

		/// <summary>
		/// Locates citrus project containing executing assembly. See <see cref="TryFindCitrusProject"/>
		/// </summary>
		public static bool TryFindCitrusProjectForExecutingAssembly(out string projectFilePath)
		{
			return TryFindCitrusProject(Uri.UnescapeDataString(new Uri(Assembly.GetExecutingAssembly().CodeBase).AbsolutePath), out projectFilePath);
		}

		/// <summary>
		/// Locates citrus project up the directory tree, starting from <paramref name="startingPath"/>.
		/// Searches for any <c>*.citproj</c> file until there's no parent directory.
		/// First directory containing <c>*.citproj</c> file is considered a project directory.
		/// </summary>
		/// <param name="startingPath">Path serving as starting point of the search.</param>
		/// <param name="projectFilePath">Path to <c>*.citproj</c> file if found, or <c>null</c> otherwise.</param>
		/// <returns><c>true</c> if project has been found <c>false</c> otherwise.</returns>
		/// <exception cref="InvalidOperationException">If there are multiple <c>*.citproj</c> files in the same directory.</exception>
		public static bool TryFindCitrusProject(string startingPath, out string projectFilePath)
		{
			projectFilePath = null;
			var path = startingPath;
			var directoryInfo = (new DirectoryInfo(path)).Parent;
			while (directoryInfo != null) {
				var citprojFiles = directoryInfo.EnumerateFiles("*.citproj");
				if (citprojFiles.Any()) {
					if (citprojFiles.Count() > 1) {
						throw new InvalidOperationException($"Multiple *.citproj files found in the same directory: {string.Join(",", citprojFiles)}");
					}
					projectFilePath = citprojFiles.First().FullName;
					return true;
				}
				directoryInfo = directoryInfo.Parent;
			}
			return false;
		}

		public static string GetMonoPath()
		{
			return "/Library/Frameworks/Mono.framework/Versions/Current/bin/mono";
		}

		public static IEnumerable<MethodInfo> GetAllMethodsWithAttribute(this Assembly assembly, Type attributeType)
		{
			foreach (var type in assembly.GetTypes()) {
				var allMethods = type.GetMethods(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Public);
				foreach (var method in allMethods
					.Where(m => m.GetCustomAttributes(attributeType, false).Length > 0)
					.ToArray()) {
					yield return method;
				}
			}
		}

		public static string GetTempFilePathWithExtension(string extension)
		{
			var fileName = Guid.NewGuid().ToString() + extension;
			return Path.Combine(Path.GetTempPath(), fileName);
		}

		public static string GetRelativePath(string path, string basePath)
		{
			var baseUri = new Uri(Path.GetFullPath(basePath), UriKind.Absolute);
			var uri = new Uri(Path.GetFullPath(path), UriKind.Absolute);
			return baseUri.MakeRelativeUri(uri).OriginalString;
		}

		public static Node CreateCloneForSerialization(Node node)
		{
			var clone = node.Clone();
			var savedNodesAndTangerineFlags = new List<(Node, TangerineFlags)>();

			foreach (var n in clone.SelfAndDescendants) {
				CleanNode(n);
				SaveAndSetFlags(n);
			}
			clone.Update(0);
			foreach (var (n, nTangerineFlags) in savedNodesAndTangerineFlags) {
				n.TangerineFlags = nTangerineFlags;
				// Make Sure ParticleModifers' animators are at 0.0f AGAIN
				// Since Update(0) could spawn and advance some particle beacuse of ParticleEmitter.TimeShift
				if (n is ParticleModifier pm) {
					pm.Animators.Apply(0.0);
				}
			}
			// Need to clean the clone twice, because in Update() there can be logic that
			// creates some content for external scenes.
			foreach (var n in clone.SelfAndDescendants) {
				CleanNode(n);
			}

			void CleanNode(Node n)
			{
				n.SetTangerineFlag(~TangerineFlags.SerializableMask, false);
				n.RemoveAnimatorsForExternalAnimations();
				foreach (var animation in n.Animations) {
					animation.Frame = 0;
				}
				foreach (var a in n.Animators.ToList()) {
					if (a.ReadonlyKeys.Count == 0 || a.IsZombie) {
						n.Animators.Remove(a);
					}
				}
				if (!string.IsNullOrEmpty(n.ContentsPath)) {
					n.Nodes.Clear();
					n.Animations.Clear();
				}
				if (n.AsWidget?.SkinningWeights?.IsEmpty() ?? false) {
					n.AsWidget.SkinningWeights = null;
				} else if ((n as PointObject)?.SkinningWeights?.IsEmpty() ?? false) {
					(n as PointObject).SkinningWeights = null;
				}
				if (n is ParticleEmitter pe) {
					pe.ClearParticles();
				}
				// Make Sure ParticleModifers' animators are at 0.0f
				if (n is ParticleModifier pm) {
					pm.Animators.Apply(0.0);
				}
			}
			void SaveAndSetFlags(Node n)
			{
				// saving flags and setting all nodes' flags to shown
				// to force globally visible on all nodes to ensure advance animation
				// from Update(0.0f) further will be applied to all nodes
				savedNodesAndTangerineFlags.Add((n, n.TangerineFlags));
				n.SetTangerineFlag(TangerineFlags.Hidden, false);
				n.SetTangerineFlag(TangerineFlags.Shown, true);
			}
			return clone;
		}

		public const string ConfigurationSubstituteToken = "$(CONFIGURATION)";

		/// <summary>
		/// Substitutes engine defined tokens with values.
		/// Tokens are:
		/// "$(CONFIGURATION)" => "Debug"|"Release"
		/// "$(HOST_APPLICATION)" => "Orange"|"Tangerine"
		/// "$(PLATFORM)" => "Win"|"Mac"
		/// </summary>
		/// <param name="string">Assembly path with tokens.</param>
		/// <returns></returns>
		public static string ReplaceCitrusProjectSubstituteTokens(string @string)
		{
			return @string
				.Replace(ConfigurationSubstituteToken,
#if DEBUG
					BuildConfiguration.Debug)
#else
					BuildConfiguration.Release)
#endif
				.Replace("$(PLATFORM)",
#if WIN
					"Win")
#elif MAC
					"Mac")
#endif // WIN
				.Replace("$(HOST_APPLICATION)",
#if TANGERINE
					"Tangerine");
#else
					"Orange");
#endif
		}

		public static List<string> GetListOfAllBundles(
			Target target,
			AssetBundle InputBundle = null,
			Dictionary<string, CookingRules> cookungRules = null
		) {
			InputBundle ??= AssetBundle.Current;
			cookungRules ??= CookingRulesBuilder.Build(InputBundle, target);
			var bundles = new HashSet<string>();
			foreach (var (_, rules) in cookungRules) {
				foreach (var bundle in rules.Bundles) {
					bundles.Add(bundle);
				}
			}
			return bundles.ToList();
		}

		/// <summary>
		/// Returns SHA256 based on the file path and contents.
		/// </summary>
		public static SHA256 ComputeCookingUnitHash(this AssetBundle bundle, string path, CookingRules cookingRules)
		{
			return SHA256.Compute(
				SHA256.Compute(AssetPath.CorrectSlashes(path)),
				bundle.GetFileContentsHash(path),
				cookingRules.Hash
			);
		}
	}
}
