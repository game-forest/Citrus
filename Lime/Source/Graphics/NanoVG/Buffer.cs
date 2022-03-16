using System;

namespace Lime.NanoVG
{
	internal class Buffer<T>
	{
		public T[] Array { get; private set; }
		public int Count { get; set; }
		public int Capacity => Array.Length;

		public T this[int index]
		{
			get => Array[index];
			set => Array[index] = value;
		}

		public Buffer(int capacity)
		{
			Array = new T[capacity];
		}

		public void Clear()
		{
			Count = 0;
		}

		public void EnsureSize(int required)
		{
			if (Array.Length >= required) {
				return;
			}
			// Realloc
			var oldData = Array;
			var newSize = Array.Length;
			while (newSize < required) {
				newSize *= 2;
			}
			Array = new T[newSize];
			System.Array.Copy(oldData, Array, oldData.Length);
		}

		public void Add(T item)
		{
			EnsureSize(Count + 1);
			Array[Count] = item;
			++Count;
		}

		public ArraySegment<T> ToArraySegment() => new ArraySegment<T>(Array, 0, Count);
	}
}
