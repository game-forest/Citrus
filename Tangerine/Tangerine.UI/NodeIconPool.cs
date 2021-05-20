using System;
using System.Collections.Generic;
using System.Reflection;
using Lime;

namespace Tangerine.UI
{
	public static class NodeIconPool
	{
		private static readonly Dictionary<Type, Icon> iconMap = new Dictionary<Type, Icon>();
		private static readonly Dictionary<Type, ITexture> textureMap = new Dictionary<Type, ITexture>();

		public delegate void IconCreatedDelegate(Icon icon);

		public static Icon DefaultIcon { get; } = IconPool.GetIcon("Nodes.Unknown");

		[NodeComponentDontSerialize]
		public class CachedNodeIconTextureComponent : NodeComponent
		{
			public ITexture Texture;
		}

		public static ITexture GetTexture(Node node)
		{
			var c = node.Components.GetOrAdd<CachedNodeIconTextureComponent>();
			if (c.Texture != null) {
				return c.Texture;
			}
			var isNodeType = true;
			var type = node.GetType();
			var attribute = type.GetCustomAttribute<TangerineCustomIconAttribute>();
			foreach (var component in node.Components) {
				var componentType = component.GetType();
				var componentAttribute = componentType.GetCustomAttribute<TangerineCustomIconAttribute>();
				if (componentAttribute != null && componentAttribute.Priority > (attribute?.Priority ?? 0)) {
					isNodeType = false;
					type = componentType;
					attribute = componentAttribute;
				}
			}
			var t = isNodeType ? GetTexture(type) : ComponentIconPool.GetTexture(type);
			return c.Texture = t;
		}

		public static ITexture GetTexture(Type type)
		{
			if (textureMap.TryGetValue(type, out var texture)) {
				return texture;
			}
			if (TryGetIcon(type, out var icon)) {
				return icon.AsTexture;
			}
			textureMap[type] = texture = IconTextureGenerator.GetTexture(type);
			return texture;
		}

		public static bool TryGetIcon(Type type, out Icon icon)
		{
			if (iconMap.TryGetValue(type, out icon)) {
				return true;
			}
			if (IconPool.TryGetIcon($"Nodes.{type}", out icon)) {
				iconMap[type] = icon;
				return true;
			}
			return false;
		}

		public static void GenerateIcon(Type type, IconCreatedDelegate iconCreated)
		{
			IconTextureGenerator.GetTexture(
				type,
				bitmap => {
					var icon = new Icon(bitmap);
					iconMap[type] = icon;
					iconCreated?.Invoke(icon);
				}
			);
		}
	}
}
