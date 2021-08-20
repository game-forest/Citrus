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

		protected Assembly GetAssembly() => Orange.AssemblyTracker.Instance.GetAssemblyByName(AssemblyName);

		public virtual System.IO.Stream GetResourceStream() => GetAssembly().GetManifestResourceStream(ResourceId);

		public byte[] GetResourceBytes()
		{
			using var ms = new System.IO.MemoryStream();
			GetResourceStream().CopyTo(ms);
			return ms.ToArray();
		}
	}
}
