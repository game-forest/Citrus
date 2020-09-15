using System;
using System.Collections.Generic;
using System.Reflection;
using Lime;

namespace Tangerine.UI
{
	public static class ComponentIconPool
	{
		private static readonly Dictionary<Type, Icon> map = new Dictionary<Type, Icon>();

		public static Icon GetIcon(Type type)
		{
			if (!map.TryGetValue(type, out var icon)) {
				map[type] = icon = IconPool.GetIcon("Components." + type, "Components.Unknown");
			}
			return icon;
		}

		public static ITexture GetTexture(Type type)
		{
			var attribute = type.GetCustomAttribute<TangerineCustomIconAttribute>();
			return attribute != null ? IconTextureGenerator.GetTexture(type) : GetIcon(type).AsTexture;
		}
	}
}
