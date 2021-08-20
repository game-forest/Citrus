using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Orange
{
	/// <summary>
	/// Cache for assemblies and their names in current application domain.
	/// Excludes `system*`, `microsoft*`, `mscorlib` and IsDynamic assemblies.
	/// </summary>
	public class AssemblyTracker : IEnumerable<(string Name, Assembly Assembly)>
	{
		public static AssemblyTracker Instance { get; } = new AssemblyTracker();

		private readonly List<(string Name, Assembly Assembly)> assemblies = null;

		private AssemblyTracker()
		{
			assemblies = AppDomain.CurrentDomain.GetAssemblies()
				.Where(a => !DoesNotSuit(a))
				.Select(a => (a.GetName().Name, a))
				.ToList();
			AppDomain.CurrentDomain.AssemblyLoad += (sender, args) => {
				var a = args.LoadedAssembly;
				try {
					if (!DoesNotSuit(a)) {
						Instance.assemblies.Add((a.GetName().Name, a));
					}
				} catch {
				}
			};
		}

		private static bool DoesNotSuit(Assembly assembly)
		{
			var name = assembly.GetName().Name;
			return name.StartsWith("system", StringComparison.OrdinalIgnoreCase)
				|| name.StartsWith("microsoft", StringComparison.OrdinalIgnoreCase)
				|| name.StartsWith("mscorlib", StringComparison.OrdinalIgnoreCase)
				|| assembly.IsDynamic;
		}

		public Assembly GetAssemblyByName(string assemblyName)
		{
			if (!TryGetAssemblyByName(assemblyName, out var assembly)) {
				throw new Lime.Exception("Assembly '{0}' doesn't exist", assemblyName);
			}
			return assembly;
		}

		public bool TryGetAssemblyByName(string assemblyName, out Assembly assembly)
		{
			return (assembly = assemblies.SingleOrDefault(e => e.Name == assemblyName).Assembly) != null;
		}


		public IEnumerator<(string Name, Assembly Assembly)> GetEnumerator()
		{
			// Assemblies may be loaded while being enumerated.
			// This will allow to enumerate all loaded assemblies.
			int i = 0;
			while (i < assemblies.Count) {
				yield return assemblies[i];
				i++;
			}
		}

		IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();
	}
}
