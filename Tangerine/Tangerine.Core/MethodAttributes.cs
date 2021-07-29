using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Tangerine.Core
{
	public static class MethodAttributes<T> where T: Attribute
	{
		static readonly Dictionary<Type, Dictionary<string, T>> map = new Dictionary<Type, Dictionary<string, T>>();

		public static T Get(MethodInfo method)
		{
			return Get(method.DeclaringType, method.Name);
		}

		public static T Get(Type type, string methodName)
		{
			Dictionary<string, T> methodMap;
			if (!map.TryGetValue(type, out methodMap)) {
				map[type] = methodMap = new Dictionary<string, T>();
			}
			T attr;
			if (!methodMap.TryGetValue(methodName, out attr)) {
				var method = type.GetMethods().First(m => m.Name == methodName);
				attr = method.GetCustomAttributes(false).FirstOrDefault(i => i is T) as T;
				methodMap[methodName] = attr;
			}
			return attr;
		}
	}
}
