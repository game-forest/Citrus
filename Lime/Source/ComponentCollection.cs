using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

namespace Lime
{
	[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
	public class AllowMultipleComponentsAttribute : Attribute { }

	[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
	public class AllowOnlyOneComponentAttribute : Attribute { }

	public class Component
	{
		private static Dictionary<Type, (Type TypeOwningAttribute, bool AllowMultiple)> ruleSpreader =
			new Dictionary<Type, (Type, bool)>();

		internal static (Type Type, bool AllowMultiple) GetRuleSpreader(Type type)
		{
			lock (ruleSpreader) {
				if (ruleSpreader.TryGetValue(type, out var result)) {
					return result;
				}
				var t = type;
				while (t != typeof(Component)) {
					var allowMultiple = t.GetCustomAttribute<AllowMultipleComponentsAttribute>(false);
					var allowOnlyOne = t.GetCustomAttribute<AllowOnlyOneComponentAttribute>(false);
					if (allowMultiple != null || allowOnlyOne != null) {
						var rs = (t, allowMultiple != null);
						ruleSpreader.Add(type, rs);
						return rs;
					}
					t = t.BaseType;
				}
				ruleSpreader.Add(type, (null, false));
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
				throw new InvalidOperationException("Attempt to add a component twice.");
			}
			var type = component.GetType();
			var (ruleSpreaderType, allowMultiple) = Component.GetRuleSpreader(type);
			if (
				(ruleSpreaderType == null && ContainsExactType(type))
				|| (ruleSpreaderType != null && !allowMultiple && Contains(ruleSpreaderType))
			) {
				throw new InvalidOperationException("Adding multiple component of this type is not permitted");
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
			var (ruleSpreaderType, allowMultiple) = Component.GetRuleSpreader(type);
			return
				allowMultiple
				|| (ruleSpreaderType == null && !ContainsExactType(type))
				|| (ruleSpreaderType != null && !Contains(ruleSpreaderType));
		}

		public bool CanAdd<T>() where T : TComponent
		{
			var (ruleSpreaderType, allowMultiple) = ComponentInfoResolver<T>.RuleSpreader;
			return
				allowMultiple
				|| (ruleSpreaderType == null && !ContainsExactType(typeof(T)))
				|| (ruleSpreaderType != null && !Contains(ruleSpreaderType));
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

		private static class ComponentInfoResolver<T> where T : TComponent
		{
			public static readonly (Type Type, bool AllowMultiple) RuleSpreader =
				Component.GetRuleSpreader(typeof(T));
		}
	}
}
