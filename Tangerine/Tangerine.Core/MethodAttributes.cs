using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Tangerine.Core
{
	public static class MethodAttributes<T>
		where T : Attribute
	{
		private static readonly Dictionary<(Type, string, bool), T> map = new Dictionary<(Type, string, bool), T>();

		public static T Get(MethodInfo method, bool inherit = false)
		{
			return Get(method.DeclaringType, method.Name, inherit);
		}

		public static T Get(Type type, string methodName, bool inherit = false)
		{
			var key = (type, methodName, inherit);
			if (!map.TryGetValue(key, out T a)) {
				var method = type.GetMethods().First(m => m.Name == methodName);
				a = method.GetCustomAttributes(inherit).FirstOrDefault(i => i is T) as T;
				map[key] = a;
			}
			return a;
		}
	}
}
