using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Lime;

namespace Tangerine.Core
{
	public static class PropertyValidator
	{
		public static IEnumerable<(ValidationResult Result, string Message)> ValidateValue(object owner, object value, Type type, string property)
		{
			var attributes = PropertyAttributes<TangerineValidationAttribute>.GetAll(type, property);
			return attributes.Select(attribute => (attribute.IsValid(owner, value, out var message), message));
		}

		public static IEnumerable<(ValidationResult Result, string Message)> ValidateValue(object owner, object value, PropertyInfo propertyInfo)
		{
			return ValidateValue(owner, value, propertyInfo.DeclaringType, propertyInfo.Name);
		}
	}
}
