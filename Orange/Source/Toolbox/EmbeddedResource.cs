using System;
using System.Linq;

namespace Orange
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

		public System.IO.Stream GetResourceStream()
		{
			if (!AssemblyTracker.Instance.TryGetAssemblyByName(AssemblyName, out var assembly)) {
				throw new Lime.Exception("Assembly '{0}' doesn't exist", AssemblyName);
			}
			return assembly.GetManifestResourceStream(ResourceId);
		}

		public byte[] GetResourceBytes()
		{
			using var ms = new System.IO.MemoryStream();
			GetResourceStream().CopyTo(ms);
			return ms.ToArray();
		}
	}

}
