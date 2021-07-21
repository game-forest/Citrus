using System.Collections.Generic;
using System.Linq;
using System;
using System.Reflection;

namespace Tangerine.Core
{
	public static class PropertyAttributes<T> where T: Attribute
	{
		static readonly Dictionary<Type, Dictionary<string, List<T>>> map = new Dictionary<Type, Dictionary<string, List<T>>>();

		public static T Get(System.Reflection.PropertyInfo property)
		{
			return Get(property.DeclaringType, property.Name);
		}

		public static List<T> GetAll(Type type, string property)
		{
			Dictionary<string, List<T>> propMap;
			if (!map.TryGetValue(type, out propMap)) {
				map[type] = propMap = new Dictionary<string, List<T>>();
			}
			List<T> attr;
			if (!propMap.TryGetValue(property, out attr)) {
				// use last part of property path in case it's Animator.PropertyPath
				int index = property.LastIndexOf('.');
				var actualProperty = index == -1
					? property
					: property.Substring(index + 1);
				int bracketIndex = actualProperty.IndexOf('[');
				if (bracketIndex != -1) {
					actualProperty = actualProperty.Substring(0, bracketIndex);
				}
				var prop = type.GetProperties().First(p => p.Name == actualProperty);
				// workaround for hidden properties ambiguity (e.g. Layout.Owner vs NodeComponent.Owner)
				propMap[property] = attr = prop.GetCustomAttributes(false).OfType<T>().ToList();
			}
			return attr;
		}
		
		public static T Get(Type type, string property) => GetAll(type, property).FirstOrDefault();
	}
}
