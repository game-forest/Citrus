using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

namespace Lime
{
	[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
	/// <summary>
	/// Allow to add multiple components of a type decorated with the attribute and it's derived types.
	/// </summary>
	public class AllowMultipleComponentsAttribute : Attribute { }

	[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
	/// <summary>
	/// Allow to add only one component of a type decorated with the attribute and it's derived types.
	/// </summary>
	public class AllowOnlyOneComponentAttribute : Attribute { }

	public class Component
	{
		private static readonly Dictionary<Type, (Type RuleDeclaringType, bool AllowMultiple)> ruleCache =
			new Dictionary<Type, (Type, bool)>();

		internal static (Type RuleDeclaringType, bool AllowMultiple) GetRule(Type type)
		{
			lock (ruleCache) {
				if (ruleCache.TryGetValue(type, out var result)) {
					return result;
				}
				var t = type;
				while (t != typeof(Component)) {
					var allowMultiple = t.GetCustomAttribute<AllowMultipleComponentsAttribute>(false);
					var allowOnlyOne = t.GetCustomAttribute<AllowOnlyOneComponentAttribute>(false);
					if (allowMultiple != null || allowOnlyOne != null) {
						var rule = (t, allowMultiple != null);
						ruleCache.Add(type, rule);
						return rule;
					}
					t = t.BaseType;
				}
				ruleCache.Add(type, (null, false));
				return (null, false);
			}
		}
	}

	public class ComponentCollection<TComponent> : ICollection<TComponent> where TComponent : Component
	{
		private TComponent[] list;

		public int Count { get; private set; }

		public bool IsReadOnly => false;

		public virtual bool Contains(TComponent component)
		{
			for (int i = 0; i < Count; i++) {
				if (list[i] == component) {
					return true;
				}
			}
			return false;
		}

		public bool Contains<T>()
		{
			for (int i = 0; i < Count; i++) {
				if (list[i] is T) {
					return true;
				}
			}
			return false;
		}

		public bool Contains(Type type)
		{
			for (int i = 0; i < Count; i++) {
				if (type.IsInstanceOfType(list[i])) {
					return true;
				}
			}
			return false;
		}

		public TComponent Get(Type type)
		{
			for (int i = 0; i < Count; i++) {
				var c = list[i];
				if (type.IsInstanceOfType(c)) {
					return c;
				}
			}
			return default;
		}

		public T Get<T>()
		{
			for (int i = 0; i < Count; i++) {
				if (list[i] is T t) {
					return t;
				}
			}
			return default;
		}

		public IEnumerable<T> GetAll<T>()
		{
			for (int i = 0; i < Count; i++) {
				if (list[i] is T t) {
					yield return t;
				}
			}
		}

		public void GetAll<T>(IList<T> result)
		{
			for (int i = 0; i < Count; i++) {
				if (list[i] is T t) {
					result.Add(t);
				}
			}
		}

		public IEnumerable<TComponent> GetAll(Type type)
		{
			for (int i = 0; i < Count; i++) {
				var c = list[i];
				if (type.IsInstanceOfType(c)) {
					yield return c;
				}
			}
		}

		public void GetAll(Type type, IList<TComponent> result)
		{
			for (int i = 0; i < Count; i++) {
				var c = list[i];
				if (type.IsInstanceOfType(c)) {
					result.Add(c);
				}
			}
		}

		public bool TryGet<T>(out T result)
		{
			result = Get<T>();
			return result != null;
		}

		public T GetOrAdd<T>() where T : TComponent, new()
		{
			var c = Get<T>();
			if (c == null) {
				c = new T();
				Add(c);
			}
			return c;
		}

		public virtual void Add(TComponent component)
		{
			if (Contains(component)) {
				throw new InvalidOperationException("Attempt to add the same component twice.");
			}
			var type = component.GetType();
			var (ruleDeclaringType, allowMultiple) = Component.GetRule(type);
			if (
				(ruleDeclaringType == null && ContainsExactType(type))
				|| (ruleDeclaringType != null && !allowMultiple && Contains(ruleDeclaringType))
			) {
				throw new InvalidOperationException(
					$"Attempt to add multiple components of type `{type.FullName}`. " +
					$"Use `{nameof(AllowMultipleComponentsAttribute)}` or " +
					$"`{nameof(AllowOnlyOneComponentAttribute)}` to manage adding multiple components."
				);
			}
			if (list == null) {
				list = new TComponent[4];
				list[0] = component;
				Count = 1;
				return;
			}
			if (Count == list.Length) {
				Array.Resize(ref list, Count * 2);
			}
			list[Count++] = component;
		}

		private bool ContainsExactType(Type type)
		{
			for (int i = 0; i < Count; i++) {
				if (type == list[i].GetType()) {
					return true;
				}
			}
			return false;
		}

		public bool CanAdd(Type type)
		{
			var (ruleDeclaringType, allowMultiple) = Component.GetRule(type);
			return allowMultiple
				|| (ruleDeclaringType == null && !ContainsExactType(type))
				|| (ruleDeclaringType != null && !Contains(ruleDeclaringType));
		}

		public bool CanAdd<T>() where T : TComponent
		{
			var (ruleDeclaringType, allowMultiple) = ComponentRuleResolver<T>.Rule;
			return allowMultiple
				|| (ruleDeclaringType == null && !ContainsExactType(typeof(T)))
				|| (ruleDeclaringType != null && !Contains(ruleDeclaringType));
		}

		public bool Remove<T>()
		{
			var j = 0;
			for (int i = 0; i < Count; i++) {
				var c = list[i];
				if (!(c is T)) {
					list[j] = list[i];
					j++;
				} else {
					OnRemove(list[i]);
				}
			}
			for (int i = j; i < Count; i++) {
				list[i] = null;
			}
			var oldCount = Count;
			Count = j;
			return oldCount != Count;
		}

		public bool Remove(Type type)
		{
			var j = 0;
			for (int i = 0; i < Count; i++) {
				var c = list[i];
				if (!type.IsInstanceOfType(c)) {
					list[j] = list[i];
					j++;
				} else {
					OnRemove(list[i]);
				}
			}
			for (int i = j; i < Count; i++) {
				list[i] = null;
			}
			var oldCount = Count;
			Count = j;
			return oldCount != Count;
		}

		public virtual bool Remove(TComponent component)
		{
			int index = -1;
			for (int i = 0; i < Count; i++) {
				if (index == -1) {
					if (list[i] == component) {
						index = i;
						OnRemove(component);
					}
				} else {
					list[i - 1] = list[i];
				}
			}
			if (index == -1) {
				return false;
			}
			list[--Count] = null;
			return true;
		}

		public bool Replace<T>(T component) where T : TComponent
		{
			var r = Remove<T>();
			Add(component);
			return r;
		}

		protected virtual void OnRemove(TComponent component) { }

		IEnumerator<TComponent> IEnumerable<TComponent>.GetEnumerator() => new Enumerator(list, Count);

		IEnumerator IEnumerable.GetEnumerator() => new Enumerator(list, Count);

		public Enumerator GetEnumerator() => new Enumerator(list, Count);

		public virtual void Clear()
		{
			for (int i = 0; i < Count; i++) {
				OnRemove(list[i]);
				list[i] = null;
			}
			Count = 0;
		}

		public void CopyTo(TComponent[] array, int arrayIndex)
		{
			if (list == null) {
				return;
			}
			for (var i = 0; i < Count; i++) {
				array[arrayIndex++] = list[i];
			}
		}

		public struct Enumerator : IEnumerator<TComponent>
		{
			private readonly TComponent [] list;
			private readonly int length;
			private int index;

			public Enumerator(TComponent[] list, int length)
			{
				this.list = list;
				this.length = length;
				index = -1;
			}

			public TComponent Current => list?[index];
			object IEnumerator.Current => list?[index];

			public bool MoveNext()
			{
				if (list == null) {
					return false;
				}
				if (++index < length) {
					return true;
				}
				return false;
			}

			public void Reset() => index = -1;

			public void Dispose() { }
		}

		private static class ComponentRuleResolver<T> where T : TComponent
		{
			public static readonly (Type RuleDeclaringType, bool AllowMultiple) Rule =
				Component.GetRule(typeof(T));
		}
	}
}
