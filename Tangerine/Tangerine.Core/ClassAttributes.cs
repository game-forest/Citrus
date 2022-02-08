using System.Collections.Generic;
using System.Linq;
using System;

namespace Tangerine.Core
{
	public static class ClassAttributes<T>
		where T : Attribute
	{
		private static readonly Dictionary<(Type, bool), T> map = new Dictionary<(Type, bool), T>();

		public static T Get(Type type, bool inherit = false)
		{
			var key = (type, inherit);
			if (!map.TryGetValue(key, out T attr)) {
				attr = type.GetCustomAttributes(inherit).FirstOrDefault(i => i is T) as T;
				map[key] = attr;
			}
			return attr;
		}
	}
}
