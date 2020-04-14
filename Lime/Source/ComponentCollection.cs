using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

namespace Lime
{
	[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
	public class MutuallyExclusiveDerivedComponentsAttribute : Attribute
	{ }

	public class Component
	{
		private static Dictionary<Type, int> keyMap = new Dictionary<Type, int>();
		private static int keyCounter;

		internal static int GetKeyForType(Type type)
		{
			lock (keyMap) {
				if (keyMap.TryGetValue(type, out int key)) {
					return key;
				}
				Type t = type;
				while (t != null) {
					if (t.GetCustomAttribute<MutuallyExclusiveDerivedComponentsAttribute>(false) != null) {
						break;
					}
					t = t.BaseType;
				}
				t = t ?? type;
				if (!keyMap.TryGetValue(t, out key)) {
					key = ++keyCounter;
					keyMap.Add(t, key);
				}
				if (!keyMap.TryGetValue(type, out _)) {
					keyMap.Add(type, key);
				}
				return key;
			}
		}

		internal int GetKey() => GetKeyForType(GetType());
	}

	public class ComponentCollection<TComponent> : ICollection<TComponent> where TComponent : Component
	{
		private (int Key, TComponent Component)[] list;

		public int Count { get; private set; }

		public bool IsReadOnly => false;

		public virtual bool Contains(TComponent component) => ContainsKey(component.GetKey());
		public bool Contains<T>() where T : TComponent => ContainsKey(ComponentKeyResolver<T>.Key);
		public bool Contains(Type type) => ContainsKey(Component.GetKeyForType(type));

		private bool ContainsKey(int key)
		{
			if (list == null) {
				return false;
			}
			foreach (var (k, c) in list) {
				if (k == key) {
					return true;
				}
				if (c == null) {
					return false;
				}
			}
			return false;
		}

		private TComponent Get(int key)
		{
			if (list == null) {
				return default;
			}
			foreach (var (k, c) in list) {
				if (c == null) {
					return default;
				}
				if (k == key) {
					return c;
				}
			}
			return default;
		}

		public TComponent Get(Type type) => Get(Component.GetKeyForType(type));

		public T Get<T>() where T : TComponent => Get(ComponentKeyResolver<T>.Key) as T;

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
			if (Contains(component.GetType())) {
				throw new InvalidOperationException("Attempt to add a component twice.");
			}
			if (list == null) {
				list = new (int, TComponent)[4];
				list[0] = (component.GetKey(), component);
				Count = 1;
				return;
			}
			if (Count == list.Length) {
				Array.Resize(ref list, Count * 2);
			}
			list[Count++] = (component.GetKey(), component);
		}

		public bool Remove<T>() where T : TComponent
		{
			var c = Get<T>();
			return c != null && Remove(c);
		}

		public bool Remove(Type type)
		{
			var c = Get(type);
			return c != null && Remove(c);
		}

		public virtual bool Remove(TComponent component)
		{
			if (list == null) {
				return false;
			}
			int index = -1;
			for (int i = 0; i < Count; i++) {
				if (index == -1) {
					if (list[i].Component == component) {
						index = i;
					}
				} else {
					list[i - 1] = list[i];
				}
			}
			if (index == -1) {
				return false;
			}
			list[--Count] = (-1, null);
			return true;
		}

		public bool Replace<T>(T component) where T : TComponent
		{
			var r = Remove<T>();
			Add(component);
			return r;
		}

		IEnumerator<TComponent> IEnumerable<TComponent>.GetEnumerator() => new Enumerator(list, Count);

		IEnumerator IEnumerable.GetEnumerator() => new Enumerator(list, Count);

		public Enumerator GetEnumerator() => new Enumerator(list, Count);

		public virtual void Clear()
		{
			for (int i = 0; i < Count; i++) {
				list[i] = (-1, null);
			}
			Count = 0;
		}

		public void CopyTo(TComponent[] array, int arrayIndex)
		{
			if (list == null) {
				return;
			}
			for (var i = 0; i < Count; i++) {
				array[arrayIndex++] = list[i].Component;
			}
		}

		public struct Enumerator : IEnumerator<TComponent>
		{
			private readonly (int Key, TComponent Component)[] list;
			private readonly int length;
			private int index;

			public Enumerator((int, TComponent)[] list, int length)
			{
				this.list = list;
				this.length = length;
				index = -1;
			}

			public TComponent Current => list?[index].Component;
			object IEnumerator.Current => list?[index].Component;

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

		private static class ComponentKeyResolver<T> where T : TComponent
		{
			public static readonly int Key = Component.GetKeyForType(typeof(T));
		}
	}
}
