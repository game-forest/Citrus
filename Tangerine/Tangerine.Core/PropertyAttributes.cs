using System.Collections.Generic;
using System.Linq;
using System;
using System.Reflection;

namespace Tangerine.Core
{
	public static class PropertyAttributes<T> where T: Attribute
	{
		private static readonly Dictionary<(Type, string, bool), T[]> map =
			new Dictionary<(Type, string, bool), T[]>();

		public static T Get(System.Reflection.PropertyInfo property, bool inherit = false)
		{
			return Get(property.DeclaringType, property.Name, inherit);
		}

		public static T[] GetAll(Type type, string property, bool inherit = false)
		{
			var key = (type, property, inherit);
			if (!map.TryGetValue(key, out var attr)) {
				// use last part of property path in case it's Animator.PropertyPath
				int index = property.LastIndexOf('.');
				var actualProperty = index == -1
					? property
					: property[(index + 1)..];
				int bracketIndex = actualProperty.IndexOf('[');
				if (bracketIndex != -1) {
					actualProperty = actualProperty.Substring(0, bracketIndex);
				}
				var prop = type.GetProperties().First(p => p.Name == actualProperty);
				// workaround for hidden properties ambiguity (e.g. Layout.Owner vs NodeComponent.Owner)
				map[key] = attr = prop.GetCustomAttributes(inherit).OfType<T>().ToArray();
			}
			return attr;
		}

		public static T Get(Type type, string property, bool inherit = false)
		{
			return GetAll(type, property, inherit).FirstOrDefault();
		}
	}
}
