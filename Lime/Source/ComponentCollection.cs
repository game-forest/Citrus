using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

namespace Lime
{
	[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
	public class MutuallyExclusiveDerivedComponentsAttribute : Attribute
	{ }

	[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
	public sealed class AllowMultipleAttribute : Attribute
	{ }

	public class Component
	{
		private static Dictionary<Type, bool> canAddMultiple = new Dictionary<Type, bool>();

		internal static bool CanAddMultiple(Type type)
		{
			lock (canAddMultiple) {
				if (canAddMultiple.TryGetValue(type, out var result)) {
					return result;
				}
				result = type.GetCustomAttribute<AllowMultipleAttribute>() != null;
				canAddMultiple.Add(type, result);
				return result;
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
			if (list == null) {
				return false;
			}
			foreach (var c in list) {
				if (c == component) {
					return true;
				}
			}
			return false;
		}

		public bool Contains<T>() where T : TComponent
		{
			if (list == null) {
				return false;
			}
			for (int i = 0; i < Count; i++) {
				if (list[i] is T) {
					return true;
				}
			}
			return false;
		}
		public bool Contains(Type type)
		{
			if (list == null) {
				return false;
			}
			for (int i = 0; i < Count; i++) {
				if (type.IsInstanceOfType(list[i])) {
					return true;
				}
			}
			return false;
		}

		public TComponent Get(Type type)
		{
			if (list == null) {
				return default;
			}
			foreach (var c in list) {
				if (c == null) {
					return default;
				}
				if (type.IsInstanceOfType(c)) {
					return c;
				}
			}
			return default;
		}

		public T Get<T>() where T : TComponent
		{
			if (list == null) {
				return default;
			}
			foreach (var c in list) {
				if (c == null) {
					return default;
				}
				if (c is T t) {
					return t;
				}
			}
			return default;
		}

		public IEnumerable<T> GetAll<T>() where T : TComponent
		{
			if (list == null) {
				yield break;
			}
			foreach (var c in list) {
				if (c == null) {
					yield break;
				}
				if (c is T t) {
					yield return t;
				}
			}
		}

		public void GetAll<T>(IList<T> result) where T : TComponent
		{
			if (list == null) {
				return;
			}
			foreach (var c in list) {
				if (c == null) {
					return;
				}
				if (c is T t) {
					result.Add(t);
				}
			}
		}

		public IEnumerable<TComponent> GetAll(Type type)
		{
			if (list == null) {
				yield break;
			}
			foreach (var c in list) {
				if (c == null) {
					yield break;
				}
				if (type.IsInstanceOfType(c)) {
					yield return c;
				}
			}
		}

		public void GetAll(Type type, IList<TComponent> result)
		{
			if (list == null) {
				return;
			}
			foreach (var c in list) {
				if (c == null) {
					return;
				}
				if (type.IsInstanceOfType(c)) {
					result.Add(c);
				}
			}
		}

		public bool TryGet<T>(out T result) where T : TComponent
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
			var type = component.GetType();
			if (Contains(component)) {
				throw new InvalidOperationException("Attempt to add a component twice.");
			}
			if (!Component.CanAddMultiple(type) && Contains(type)) {
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

		public bool Remove<T>() where T : TComponent
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
			if (list == null) {
				return false;
			}
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
			public static readonly bool CanAddMultiple = Component.CanAddMultiple(typeof(T));
		}
	}
}
