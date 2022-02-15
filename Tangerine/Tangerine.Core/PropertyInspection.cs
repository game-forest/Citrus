using Lime;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace Tangerine.Core
{
	public static class PropertyInspection
	{
		private static readonly Dictionary<(Type, PropertyInfo), bool> CanAnimatePropertyCache
			= new Dictionary<(Type, PropertyInfo), bool>();
		private static readonly Dictionary<(Type, PropertyInfo), bool> CanInspectPropertyCache
			= new Dictionary<(Type, PropertyInfo), bool>();

		public static bool CanInspectProperty(Type type, PropertyInfo property)
		{
			if (CanInspectPropertyCache.ContainsKey((type, property))) {
				return CanInspectPropertyCache[(type, property)];
			}
			bool result = CanInspectPropertyHelper(type, property);
			CanInspectPropertyCache[(type, property)] = result;
			return result;
		}

		private static bool CanInspectPropertyHelper(Type type, PropertyInfo property)
		{
			if (property.GetIndexParameters().Length > 0) {
				// we don't inspect indexers (they have "Item" name by default)
				return false;
			}
			var yuzuItem = Yuzu.Metadata.Meta.Get(type, InternalPersistence.Instance.YuzuOptions)
				.Items
				.Find(i => i.PropInfo == property);
			var hasKeyframeColor = PropertyAttributes<TangerineKeyframeColorAttribute>
				.Get(type, property.Name, true) != null;
			var shouldIgnore = PropertyAttributes<TangerineIgnoreAttribute>
				.Get(type, property.Name, true) != null;
			var shouldInspect = PropertyAttributes<TangerineInspectAttribute>
				.Get(type, property.Name, true) != null;
			if (shouldInspect) {
				return true;
			}
			if (shouldIgnore) {
				return false;
			}
			return yuzuItem != null || hasKeyframeColor;
		}

		public static bool CanAnimateProperty(Type type, PropertyInfo property)
		{
			if (CanAnimatePropertyCache.ContainsKey((type, property))) {
				return CanAnimatePropertyCache[(type, property)];
			}
			bool canInspect = CanInspectProperty(type, property);
			bool isIanimationHost = typeof(IAnimationHost).IsAssignableFrom(type);
			bool isTangerineStatic = PropertyAttributes<TangerineStaticPropertyAttribute>.Get(property) != null;
			bool isInAnimatorRegistry = AnimatorRegistry.Instance.Contains(property.PropertyType);
			bool result = canInspect && isIanimationHost && !isTangerineStatic && isInAnimatorRegistry;
			CanAnimatePropertyCache[(type, property)] = result;
			return result;
		}
	}
}
