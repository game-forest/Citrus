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
			bool result = _CanInspectProperty(type, property);
			CanInspectPropertyCache[(type, property)] = result;
			return result;
		}

		public static bool CanAnimateProperty(Type type, PropertyInfo property)
		{
			if (CanAnimatePropertyCache.ContainsKey((type, property))) {
				return CanAnimatePropertyCache[(type, property)];
			}
			bool result = _CanAnimateProperty(type, property);
			CanAnimatePropertyCache[(type, property)] = result;
			return result;
		}

		private static bool _CanInspectProperty(Type type, PropertyInfo property)
		{
			if (property.GetIndexParameters().Length > 0) {
				// we don't inspect indexers (they have "Item" name by default)
				return false;
			}
			var yuzuItem = Yuzu.Metadata.Meta.Get(type, InternalPersistence.Instance.YuzuCommonOptions)
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

		private static bool _CanAnimateProperty(Type type, PropertyInfo property)
		{
			bool canInspect = CanInspectProperty(type, property);
			bool isIanimationHost = typeof(IAnimationHost).IsAssignableFrom(type);
			bool isTangerineStatic = PropertyAttributes<TangerineStaticPropertyAttribute>.Get(property) != null;
			bool isInAnimatorRegistry = AnimatorRegistry.Instance.Contains(property.PropertyType);
			return canInspect && isIanimationHost && !isTangerineStatic && isInAnimatorRegistry;
		}
	}
}
