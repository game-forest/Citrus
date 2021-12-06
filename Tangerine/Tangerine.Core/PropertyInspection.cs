using Lime;
using System;
using System.Reflection;

namespace Tangerine.Core
{
	public static class PropertyInspection
	{
		public static bool CanAnimateProperty(Type type, PropertyInfo property)
		{
			var yuzuItem = Yuzu.Metadata.Meta.Get(type, InternalPersistence.Instance.YuzuCommonOptions)
				.Items
				.Find(i => i.PropInfo == property);
			var hasKeyframeColor = PropertyAttributes<TangerineKeyframeColorAttribute>
				.Get(type, property.Name, true) != null;
			var shouldIgnore = PropertyAttributes<TangerineIgnoreAttribute>
				.Get(type, property.Name, true) != null;
			var shouldInspect = PropertyAttributes<TangerineInspectAttribute>
				.Get(type, property.Name, true) != null;
			return shouldInspect || (yuzuItem != null && !shouldIgnore) || (hasKeyframeColor && !shouldIgnore);
		}
	}
}
