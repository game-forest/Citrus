using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Tangerine.UI
{
	public class EmbeddedResource
	{
		public readonly string ResourceId;
		public readonly string AssemblyName;

		public EmbeddedResource(string resourceId, string assemblyName)
		{
			ResourceId = resourceId;
			AssemblyName = assemblyName;
		}

		static EmbeddedResource()
		{
			assemblies = AppDomain.CurrentDomain.GetAssemblies().Select(a => (a.GetName().Name, a)).ToList();
			AppDomain.CurrentDomain.AssemblyLoad += (sender, args) => {
				assemblies.Add((args.LoadedAssembly.GetName().Name, args.LoadedAssembly));
			};
		}

		private static List<(string Name, Assembly Assembly)> assemblies;

		protected Assembly GetAssembly()
		{
			var resourcesAssembly = assemblies.SingleOrDefault(e => e.Name == AssemblyName).Assembly;
			if (resourcesAssembly == null) {
				throw new Lime.Exception("Assembly '{0}' doesn't exist", AssemblyName);
			}
			return resourcesAssembly;
		}

		public virtual System.IO.Stream GetResourceStream()
		{
			return GetAssembly().GetManifestResourceStream(ResourceId);
		}

		public byte[] GetResourceBytes()
		{
			using (var ms = new System.IO.MemoryStream()) {
				GetResourceStream().CopyTo(ms);
				return ms.ToArray();
			}
		}
	}
}
