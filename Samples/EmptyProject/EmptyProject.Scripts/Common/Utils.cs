using System;
using System.Reflection;

namespace EmptyProject.Scripts
{
	public static class Utils
	{
		public static T2 GetInstanceField<T1, T2>(T1 instance, string fieldName) => (T2)GetInstanceField(typeof(T1), instance, fieldName);

		private static object GetInstanceField(Type type, object instance, string fieldName)
		{
			const BindingFlags BindFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
			var fieldInfo = type.GetField(fieldName, BindFlags);
			if (fieldInfo == null) {
				throw new Exception($"Field \"{fieldName}\" was not found in {type.Name}");
			}
			return fieldInfo.GetValue(instance);
		}

		public static void SetInstanceField(Type type, object instance, string fieldName, object value)
		{
			const BindingFlags BindFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
			var fieldInfo = type.GetField(fieldName, BindFlags);
			if (fieldInfo == null) {
				throw new Exception($"Field \"{fieldName}\" was not found in {type.Name}");
			}
			fieldInfo.SetValue(instance, value);
		}

		public static void InvokeInstanceMethod<T>(T instance, string methodName, Type[] parametersTypes, params object[] parameters)
		{
			InvokeInstanceMethod(typeof(T), instance, methodName, parametersTypes, parameters);
		}

		public static void InvokeInstanceMethod<T>(T instance, string methodName, params object[] parameters)
		{
			InvokeInstanceMethod(typeof(T), instance, methodName, null, parameters);
		}

		public static T2 InvokeInstanceMethod<T1, T2>(T1 instance, string methodName, Type[] parametersTypes, params object[] parameters)
		{
			return (T2)InvokeInstanceMethod(typeof(T1), instance, methodName, parametersTypes, parameters);
		}

		public static T2 InvokeInstanceMethod<T1, T2>(T1 instance, string methodName, params object[] parameters)
		{
			return (T2)InvokeInstanceMethod(typeof(T1), instance, methodName, null, parameters);
		}

		private static object InvokeInstanceMethod(Type type, object instance, string methodName, Type[] parametersTypes = null, params object[] parameters)
		{
			const BindingFlags BindingFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
			var methodInfo =
				parametersTypes == null ?
				type.GetMethod(methodName, BindingFlags) :
				type.GetMethod(methodName, BindingFlags, null, parametersTypes, null);
			if (methodInfo == null) {
				throw new Exception($"Method \"{methodName}\" was not found in {type.Name}");
			}
			return methodInfo.Invoke(instance, parameters);
		}
	}
}
