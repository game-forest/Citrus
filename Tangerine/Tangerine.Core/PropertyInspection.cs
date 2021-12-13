using Lime;
using System;
using System.Reflection;

namespace Tangerine.Core
{
	public static class PropertyInspection
	{
		public static bool CanInspectProperty(Type type, PropertyInfo property)
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

		public static bool CanAnimateProperty(Type type, PropertyInfo property)
		{
			bool canInspect = CanInspectProperty(type, property);
			bool isIanimationHost = typeof(IAnimationHost).IsAssignableFrom(type);
			bool isTangerineStatic = PropertyAttributes<TangerineStaticPropertyAttribute>.Get(property) != null;
			bool isInAnimatorRegistry = AnimatorRegistry.Instance.Contains(property.PropertyType);
			return canInspect && isIanimationHost && !isTangerineStatic && isInAnimatorRegistry;
		}
	}
}
