using System;
using System.Collections.Generic;
using System.Reflection;
using Lime;

namespace Tangerine.UI
{
	public static class NodeIconPool
	{
		private static readonly Dictionary<Type, Icon> map = new Dictionary<Type, Icon>();

		public static Icon GetIcon(Type nodeType)
		{
			if (!map.TryGetValue(nodeType, out var icon)) {
				map[nodeType] = icon = IconPool.GetIcon("Nodes." + nodeType, "Nodes.Unknown");
			}
			return icon;
		}

		public static ITexture GetTexture(Type nodeType) => GetIcon(nodeType).AsTexture;

		public static ITexture GetTexture(Node node)
		{
			var type = node.GetType();
			var attribute = type.GetCustomAttribute<TangerineCustomIconAttribute>();
			foreach (var component in node.Components) {
				var componentType = component.GetType();
				var componentAttribute = componentType.GetCustomAttribute<TangerineCustomIconAttribute>();
				if (componentAttribute != null && componentAttribute.Priority > (attribute?.Priority ?? 0)) {
					type = componentType;
					attribute = componentAttribute;
				}
			}
			return attribute != null ? IconTextureGenerator.GetTexture(type) : GetTexture(node.GetType());
		}
	}
}
