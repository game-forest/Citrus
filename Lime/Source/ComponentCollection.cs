using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

namespace Lime
{
	/// <summary>
	/// Allow to add multiple components of a type decorated with the attribute and it's derived types.
	/// </summary>
	[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
	public class AllowMultipleComponentsAttribute : Attribute { }

	/// <summary>
	/// Allow to add only one component of a type decorated with the attribute and it's derived types.
	/// </summary>
	[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
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

#if TANGERINE
		private uint version;

		public uint Version => version;
#endif // TANGERINE
		
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
			if (!CanAdd(type)) {
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
#if TANGERINE
				++version;
#endif // TANGERINE
				return;
			}
			if (Count == list.Length) {
				Array.Resize(ref list, Count * 2);
			}
			list[Count++] = component;
#if TANGERINE
			++version;
#endif // TANGERINE
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

		public bool CanAdd(Type type) => CanAddHelper(type, Component.GetRule(type));

		public bool CanAdd<T>() where T : TComponent => CanAddHelper(typeof(T), ComponentRuleResolver<T>.Rule);

		private bool CanAddHelper(Type type, (Type RuleDeclaringType, bool AllowMultiple) rule)
		{
			return rule.AllowMultiple
				|| (rule.RuleDeclaringType == null && !ContainsExactType(type))
				|| (rule.RuleDeclaringType != null && !Contains(rule.RuleDeclaringType));
		}

		public bool Remove<T>()
		{
			var performedRemove = false;
			while (RemoveOne<T>()) {
				performedRemove = true;
			}
			return performedRemove;
		}

		public bool Remove(Type type)
		{
			var performedRemove = false;
			while (RemoveOne(type)) {
				performedRemove = true;
			}
			return performedRemove;
		}

		private bool RemoveOne(Type type)
		{
			for (var i = Count - 1; i >= 0; i--) {
				if (type.IsInstanceOfType(list[i])) {
					RemoveAt(i);
					return true;
				}
			}
			return false;
		}

		private bool RemoveOne<T>()
		{
			for (int i = Count - 1; i >= 0; i--) {
				if (list[i] is T) {
					RemoveAt(i);
					return true;
				}
			}
			return false;
		}

		public bool Remove(TComponent component)
		{
			for (int i = 0; i < Count; i++) {
				if (list[i] == component) {
					RemoveAt(i);
					return true;
				}
			}
			return false;
		}

		private void RemoveAt(int index)
		{
			var component = list[index];
			for (int i = index + 1; i < Count; i++) {
				list[i - 1] = list[i];
			}
			list[--Count] = null;
			OnRemove(component);
#if TANGERINE
			++version;
#endif // TANGERINE
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

		public void Clear()
		{
#if TANGERINE
			int count = Count;
#endif // TANGERINE
			while (Count > 0) {
				var c = list[--Count];
				list[Count] = null;
				OnRemove(c);
			}
#if TANGERINE
			version += count > 0 ? 1u : 0u;
#endif // TANGERINE
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
			private readonly TComponent[] list;
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
