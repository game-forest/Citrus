using System;
using System.Collections.Generic;
using System.Reflection;
using Lime;

namespace Tangerine.Core
{
	public static class PropertyInspection
	{
		private static readonly Dictionary<(Type, PropertyInfo), bool> canAnimatePropertyCache
			= new Dictionary<(Type, PropertyInfo), bool>();
		private static readonly Dictionary<(Type, PropertyInfo), bool> canInspectPropertyCache
			= new Dictionary<(Type, PropertyInfo), bool>();

		public static bool CanInspectProperty(Type type, PropertyInfo property)
		{
			if (canInspectPropertyCache.ContainsKey((type, property))) {
				return canInspectPropertyCache[(type, property)];
			}
			bool result = CanInspectPropertyHelper(type, property);
			canInspectPropertyCache[(type, property)] = result;
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
			if (canAnimatePropertyCache.ContainsKey((type, property))) {
				return canAnimatePropertyCache[(type, property)];
			}
			bool canInspect = CanInspectProperty(type, property);
			bool isIanimationHost = typeof(IAnimationHost).IsAssignableFrom(type);
			bool isTangerineStatic = PropertyAttributes<TangerineStaticPropertyAttribute>.Get(property) != null;
			bool isInAnimatorRegistry = AnimatorRegistry.Instance.Contains(property.PropertyType);
			bool result = canInspect && isIanimationHost && !isTangerineStatic && isInAnimatorRegistry;
			canAnimatePropertyCache[(type, property)] = result;
			return result;
		}
	}
}
