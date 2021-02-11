using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Hosting;
using System.ComponentModel.Composition.Primitives;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Lime;

namespace Orange
{
	public interface IAtlasPackerMetadata
	{
		string Id { get; }
	}

	public interface IMenuItemMetadata
	{
		[DefaultValue("Unspecified label")]
		string Label { get; }

		[DefaultValue(int.MaxValue)]
		int Priority { get; }

		[DefaultValue(false)]
		bool ApplicableToBundleSubset { get; }
	}

	public class OrangePlugin
	{
		[ImportMany(nameof(Initialize), AllowRecomposition = true)]
		public IEnumerable<Action> Initialize;

		[Import(nameof(BuildUI), AllowRecomposition = true, AllowDefault = true)]
		public Action<IPluginUIBuilder> BuildUI;

		[Import(nameof(Finalize), AllowRecomposition = true, AllowDefault = true)]
		public Action Finalize;

		[Import(nameof(GetRequiredAssemblies), AllowRecomposition = true, AllowDefault = true)]
		public Func<string[]> GetRequiredAssemblies;

		[ImportMany(nameof(AtlasPackers), AllowRecomposition = true)]
		public IEnumerable<Lazy<Func<string, List<TextureTools.AtlasItem>, int, int>, IAtlasPackerMetadata>> AtlasPackers { get; set; }

		[ImportMany(nameof(BeforeBundlesCooking), AllowRecomposition = true)]
		public IEnumerable<Action> BeforeBundlesCooking { get; set; }

		[ImportMany(nameof(AfterAssetUpdated), AllowRecomposition = true)]
		public IEnumerable<Action<Lime.AssetBundle, CookingRules, string>> AfterAssetUpdated { get; set; }

		[ImportMany(nameof(AfterAssetsCooked), AllowRecomposition = true)]
		public IEnumerable<Action<string>> AfterAssetsCooked { get; set; }

		[Import(nameof(AfterBundlesCooked), AllowRecomposition = true, AllowDefault = true)]
		public Action<IReadOnlyCollection<string>> AfterBundlesCooked;

		[ImportMany(nameof(CommandLineArguments), AllowRecomposition = true)]
		public IEnumerable<Func<string>> CommandLineArguments { get; set; }

		[ImportMany(nameof(MenuItems), AllowRecomposition = true)]
		public IEnumerable<Lazy<Action, IMenuItemMetadata>> MenuItems { get; set; }

		/// <summary>
		/// Used with and as MenuItems but should return null on success or a textual info about error on error
		/// </summary>
		[ImportMany(nameof(MenuItemsWithErrorDetails), AllowRecomposition = true)]
		public IEnumerable<Lazy<Func<string>, IMenuItemMetadata>> MenuItemsWithErrorDetails { get; set; }

		[ImportMany(nameof(TangerineProjectOpened), AllowRecomposition = true)]
		public IEnumerable<Action> TangerineProjectOpened;

		[ImportMany(nameof(TangerineProjectClosing), AllowRecomposition = true)]
		public IEnumerable<Action> TangerineProjectClosing;
	}

	public static class PluginLoader
	{
		public static OrangePlugin CurrentPlugin = new OrangePlugin();
		private static CompositionContainer compositionContainer;
		private static readonly AggregateCatalog catalog;
		private static readonly List<ComposablePartCatalog> registeredCatalogs = new List<ComposablePartCatalog>();
		/// <summary>
		/// List of already handled actions in OrangePlugin.Initialize.
		/// It is needed to call OrangePlugin.Initialize for each loading plugin exactly after it was loaded.
		/// Because first plugin in the list in his Initialize() can compile other plugins (dlls).
		/// And MEF works so that in OrangePlugin.Initialize list all Actions are present -
		/// already called and not.
		///
		/// Этот список нужен для того, чтобы вызывать метод OrangePlugin.Initialize для каждого
		/// подключенного плагина точно в тот момент, когда он загрузился (а не для все сразу после всей загрузки).
		/// Это нужно, чтобы первый плагин мог в своём Initialize() скомпиллировать остальные плагины.
		///
		/// Хорошо бы переделать эту множественную инициализацию с компиляцией на задачу CIT-1375.
		/// </summary>
		private static readonly HashSet<Action> handledInitializers = new HashSet<Action>();
		private static readonly Regex ignoredAssemblies = new Regex(
			"^(Lime|System.*|mscorlib.*|Microsoft.*)",
			RegexOptions.Compiled
		);
		private const string PluginsField = "PluginAssemblies";
		private const string OrangeAndTangerineField = "OrangeAndTangerine";
		private const string OrangeField = "Orange";
		private const string TangerineField = "Tangerine";

		static PluginLoader()
		{
			AppDomain.CurrentDomain.AssemblyResolve += AssemblyResolve;
			catalog = new AggregateCatalog();
			RegisterAssembly(typeof(PluginLoader).Assembly);
			ResetPlugins();
		}

		private static Assembly AssemblyResolve(object sender, ResolveEventArgs args)
		{
			var i = args.Name.IndexOf(',');
			var assemblyName = i > 0 ? args.Name.Substring(0, i) : args.Name;
			// For some reason first call of Type.GetType on custom types doesn't find already
			// proper loaded assembly and calls AssemblyResolve. Returning already loaded assembly
			// solves the problem but looks like bad workaround. Need to go deeper in investigation (see CIT-1647).
			foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies()) {
				if (assembly.GetName().Name == assemblyName) {
					return assembly;
				}
			}
			foreach (var path in EnumerateCurrentApplicationPluginAssemblyPaths()) {
				if (Path.GetFileNameWithoutExtension(Toolbox.ReplaceCitrusProjectSubstituteTokens(path)) == assemblyName) {
					return TryLoadAssembly(path);
				}
			}
			return null;
		}

		private static void ResetPlugins()
		{
			handledInitializers.Clear();
			catalog.Catalogs.Clear();
			foreach (var additionalCatalog in registeredCatalogs) {
				catalog.Catalogs.Add(additionalCatalog);
			}
			compositionContainer = new CompositionContainer(catalog);
			try {
				compositionContainer.ComposeParts(CurrentPlugin);
			} catch (CompositionException compositionException) {
				Console.WriteLine(compositionException.ToString());
			}
		}

		public static void RegisterAssembly(Assembly assembly)
		{
			registeredCatalogs.Add(new AssemblyCatalog(assembly));
			ResetPlugins();
		}

		public static void ScanForPlugins()
		{
			CurrentPlugin?.Finalize?.Invoke();
			The.UI.DestroyPluginUI();
			BuildTargets();
			CurrentPlugin = new OrangePlugin();
			resolvedAssemblies.Clear();
			ResetPlugins();
			try {
				foreach (var path in EnumerateCurrentApplicationPluginAssemblyPaths()) {
					TryLoadAssembly(path);
				}
				ValidateComposition();
			} catch (BadImageFormatException e) {
				Console.WriteLine(e.Message);
			} catch (System.Exception e) {
				The.UI.ShowError(e.Message);
				Console.WriteLine(e.Message);
			}
			ProcessPluginInitializeActions();
			var uiBuilder = The.UI.GetPluginUIBuilder();
			try {
				if (uiBuilder != null) {
					CurrentPlugin?.BuildUI?.Invoke(uiBuilder);
					The.UI.CreatePluginUI(uiBuilder);
				}
			} catch (System.Exception e) {
				Orange.UserInterface.Instance.ShowError($"Failed to build Orange Plugin UI with an error: {e.Message}\n{e.StackTrace}");
			}
			The.MenuController.CreateAssemblyMenuItems();
		}

		private static void BuildTargets()
		{
			string buildTargetsSuffix = "BuildTargets";
			var targetNames = EnumeratePluginAssemblySubfieldElements(OrangeAndTangerineField + buildTargetsSuffix)
#if TANGERINE
				.Concat(EnumeratePluginAssemblySubfieldElements(TangerineField + buildTargetsSuffix));
#else // TANGERINE
				.Concat(EnumeratePluginAssemblySubfieldElements(OrangeField + buildTargetsSuffix));
#endif  // TANGERINE

			foreach (var target in The.Workspace.Targets.Join(targetNames, t => t.Name, tn => tn, (t, tn) => t)) {
				var builder = new SolutionBuilder(target);
				if (target.CleanBeforeBuild == true) {
					builder.Clean();
				}
				if (!builder.Build()) {
					UserInterface.Instance.ExitWithErrorIfPossible();
				}
			}
		}

		public static IEnumerable<Assembly> EnumerateOrangeAndTangerinePluginAssemblies() =>
			EnumeratePluginAssemblySubfieldElements(OrangeAndTangerineField)
			.Select(TryLoadAssembly);

		private static IEnumerable<string> EnumeratePluginAssemblySubfieldElements(string subfieldName)
		{
			var array = The.Workspace.ProjectJson.GetArray<string>($"{PluginsField}/{subfieldName}");
			if (array == null) {
				yield break;
			}
			foreach (var v in array) {
				yield return v;
			}
		}

		private static IEnumerable<string> EnumerateCurrentApplicationPluginAssemblyPaths()
		{
			var paths = EnumeratePluginAssemblySubfieldElements(OrangeAndTangerineField);
#if TANGERINE
			paths = paths.Concat(EnumeratePluginAssemblyPaths(TangerineField));
#else
			paths = paths.Concat(EnumeratePluginAssemblySubfieldElements(OrangeField));
#endif
			return paths;
		}

		private static Assembly TryLoadAssembly(string assemblyPath)
		{
			if (!assemblyPath.Contains(Toolbox.ConfigurationSubstituteToken)) {
				Console.WriteLine(
					$"Warning: Using '{Toolbox.ConfigurationSubstituteToken}' instead of 'Debug' or 'Release' in dll path" +
					$" is strictly recommended ('{Toolbox.ConfigurationSubstituteToken}' line not found in {assemblyPath}");
			}
			var absPath = Path.Combine(The.Workspace.ProjectDirectory, Toolbox.ReplaceCitrusProjectSubstituteTokens(assemblyPath));
			if (!File.Exists(absPath)) {
				throw new FileNotFoundException("File not found on attempt to import PluginAssemblies: " + absPath);
			}
			var domainAssemblies = AppDomain.CurrentDomain.GetAssemblies();
			if (!TryFindDomainAssembliesByPath(domainAssemblies, absPath, out var assembly)) {
				var assemblyName = AssemblyName.GetAssemblyName(absPath);
				TryFindDomainAssembliesByName(domainAssemblies, assemblyName.Name, out assembly);
			}
			try {
				if (assembly == null) {
					assembly = LoadAssembly(absPath);
				}
				if (!resolvedAssemblies.ContainsKey(assembly.GetName().Name)) {
					catalog.Catalogs.Add(new AssemblyCatalog(assembly));
				}
			} catch (ReflectionTypeLoadException e) {
				var msg = "Failed to import OrangePluginAssemblies: " + absPath;
				foreach (var loaderException in e.LoaderExceptions) {
					msg += $"\n{loaderException}";
				}
				throw new System.Exception(msg);
			} catch (System.Exception e) {
				var msg = $"Unhandled exception while importing OrangePluginAssemblies: {absPath}\n{e}";
				throw new System.Exception(msg);
			}
			resolvedAssemblies[assembly.GetName().Name] = assembly;
			ProcessPluginInitializeActions();
			return assembly;
		}

		private static void ProcessPluginInitializeActions()
		{
			if (CurrentPlugin != null) {
				foreach (var initAction in CurrentPlugin.Initialize) {
					if (handledInitializers.Add(initAction)) {
						initAction();
					}
				}
			}
		}

		public static void BeforeBundlesCooking()
		{
			foreach (var action in CurrentPlugin.BeforeBundlesCooking) {
				action();
			}
		}

		public static void AfterAssetUpdated(Lime.AssetBundle bundle, CookingRules cookingRules, string path)
		{
			foreach (var i in CurrentPlugin.AfterAssetUpdated) {
				i(bundle, cookingRules, path);
			}
		}

		public static void AfterAssetsCooked(string bundleName)
		{
			foreach (var i in CurrentPlugin.AfterAssetsCooked) {
				i(bundleName);
			}
		}

		public static void AfterBundlesCooked(IReadOnlyCollection<string> bundles)
		{
			CurrentPlugin.AfterBundlesCooked?.Invoke(bundles);
		}

		public static string GetCommandLineArguments()
		{
			string result = "";
			if (CurrentPlugin != null) {
				result = GetPluginCommandLineArgumets();
			}
			return result;
		}

		private static string GetPluginCommandLineArgumets()
		{
			return CurrentPlugin.CommandLineArguments.Aggregate("", (current, i) => current + i());
		}

		private static void ValidateComposition()
		{
			var exportedCount = catalog.Parts.SelectMany(p => p.ExportDefinitions).Count();
			var importedCount = 0;

			Func<MemberInfo, bool> isImportMember = (m) =>
				Attribute.IsDefined(m, typeof(ImportAttribute)) ||
				Attribute.IsDefined(m, typeof(ImportManyAttribute));

			foreach (
				var member in typeof(OrangePlugin).GetMembers()
					.Where(m => m is PropertyInfo || m is FieldInfo)
					.Where(m => isImportMember(m))
				) {
				if (member is PropertyInfo) {
					var property = member as PropertyInfo;
					if (property.PropertyType.GetInterfaces().Contains(typeof(IEnumerable))) {
						importedCount += ((ICollection)property.GetValue(CurrentPlugin)).Count;
					} else if (property.GetValue(CurrentPlugin) != null) {
						importedCount++;
					}
				} else if (member is FieldInfo) {
					var field = member as FieldInfo;
					if (field.FieldType.GetInterfaces().Contains(typeof(IEnumerable))) {
						importedCount += ((ICollection)field.GetValue(CurrentPlugin)).Count;
					} else if (field.GetValue(CurrentPlugin) != null ){
						importedCount++;
					}
				}
			}

			if (exportedCount != importedCount) {
				Console.WriteLine(
					$"WARNING: Plugin composition mismatch found.\nThe given assemblies defines [{exportedCount}] " +
					$"exports, but only [{importedCount}] has been imported.\nPlease check export contracts.\n");
			}
		}

		private static readonly Dictionary<string, Assembly> resolvedAssemblies = new Dictionary<string, Assembly>();
		public static IEnumerable<Type> EnumerateTangerineExportedTypes()
		{
			// TODO: use only citproj as source of assemblies with exported types?
			var requiredAssemblies = CurrentPlugin?.GetRequiredAssemblies;
			if (requiredAssemblies != null) {
				foreach (var name in requiredAssemblies()) {
					AssemblyResolve(null, new ResolveEventArgs(name, null));
				}
			}

			foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies()) {
				var assemblyName = assembly.GetName().Name;
				if (ignoredAssemblies.IsMatch(assemblyName)) {
					continue;
				}

				Type[] exportedTypes = null;
				try {
					// dynamic assemblies don't support GetExportedTypes()
					if (!assembly.IsDynamic) {
						exportedTypes = assembly.GetExportedTypes();
					}
				} catch (System.Exception) {
					exportedTypes = null;
				}
				if (exportedTypes != null) {
					foreach (var t in exportedTypes) {
						if (t.GetCustomAttributes(false).Any(i =>
							i is TangerineRegisterNodeAttribute || i is TangerineRegisterComponentAttribute)
						) {
							yield return t;
						}
					}
				}
			}
		}

		private static bool TryFindDomainAssembliesByPath(Assembly[] domainAssemblies, string path, out Assembly assembly)
		{
			assembly = domainAssemblies.FirstOrDefault(i => {
				try {
					return !i.IsDynamic && !string.IsNullOrEmpty(i.Location) && string.Equals(Path.GetFullPath(i.Location), Path.GetFullPath(path), StringComparison.CurrentCultureIgnoreCase);
				} catch {
					return false;
				}
			});
			return assembly != null;
		}

		private static bool TryFindDomainAssembliesByName(Assembly[] domainAssemblies, string name, out Assembly assembly)
		{
			assembly = domainAssemblies.FirstOrDefault(i => {
				try {
					return string.Equals(i.GetName().Name, name, StringComparison.CurrentCultureIgnoreCase);
				} catch {
					return false;
				}
			});
			return assembly != null;
		}

		private static Assembly LoadAssembly(string path)
		{
			return Assembly.LoadFrom(path);
		}
	}
}
