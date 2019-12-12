using System;
using System.IO;

namespace Tangerine.UI
{
    public class ThemedIconResource : EmbeddedResource
	{
        public ThemedIconResource(string iconId, string assemblyName) : base(iconId, assemblyName) { }

		public override Stream GetResourceStream()
		{
			foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies()) {
				Stream stream;
				try {
					stream = assembly.GetManifestResourceStream(GetResourceId(themed: true, assembly.GetName().Name));
					stream = stream ?? assembly.GetManifestResourceStream(GetResourceId(themed: false, assembly.GetName().Name));
				} catch (Exception) {
					stream = null;
				}
				if (stream != null) {
					return stream;
				}
			}
			return null;
		}

		private string GetResourceId(bool themed, string assemblyName)
		{
			var theme = themed ? $"{(ColorTheme.Current.IsDark ? "Dark" : "Light")}." : string.Empty;
            return $"{assemblyName}.Resources.Icons.{theme}{ResourceId}.png";
		}
	}
}
