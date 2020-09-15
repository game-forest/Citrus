using System;
using System.Collections.Generic;
using Lime;

namespace Tangerine.UI
{
	public static class IconPool
	{
		private static readonly Dictionary<string, Icon> icons = new Dictionary<string, Icon>();

		public static ITexture GetTexture(string id, string defaultId = null) => GetIcon(id, defaultId).AsTexture;

		public static Icon GetIcon(string id, string defaultId = null)
		{
			if (!icons.TryGetValue(id, out var icon)) {
				icons[id] = icon = CreateIcon(id, defaultId);
			}
			return icon;
		}

		public static bool TryGetIcon(string id, out Icon icon)
		{
			if (icons.TryGetValue(id, out icon)) {
				return true;
			}
			if (TryCreateIcon(id, out icon)) {
				icons[id] = icon;
				return true;
			}
			return false;
		}

		private static Icon CreateIcon(string id, string defaultId = null)
		{
			if (TryCreateIcon(id, out var icon) || !string.IsNullOrEmpty(defaultId) && TryCreateIcon(defaultId, out icon)) {
				return icon;
			}
			throw new ArgumentException($"Icon '{id}' doesn't exist");
		}

		private static bool TryCreateIcon(string id, out Icon icon)
		{
			icon = null;
			foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies()) {
				try {
					var png = new ThemedIconResource(id, assembly.GetName().Name).GetResourceStream();
					if (png != null) {
						icon = new Icon(new Bitmap(png));
						return true;
					}
				} catch (System.Exception) {
					// suppress
				}
			}
			return false;
		}
	}
}
