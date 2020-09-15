using System;
using System.Collections.Generic;
using Lime;

namespace Tangerine.UI
{
	public static class ComponentIconPool
	{
		private static readonly Dictionary<Type, ITexture> map = new Dictionary<Type, ITexture>();

		public static ITexture GetTexture(Type type)
		{
			if (map.TryGetValue(type, out var texture)) {
				return texture;
			}
			if (IconPool.TryGetIcon($"Components.{type}", out var icon)) {
				map[type] = texture = icon.AsTexture;
				return texture;
			}
			return IconTextureGenerator.GetTexture(type);
		}
	}
}
