using System;
using System.Collections;
using System.Collections.Generic;

namespace Lime
{
	public class GestureList : IList<Gesture>
	{
		private readonly List<Gesture> gestures = new List<Gesture>();
		private readonly Node owner;

		public int Count => gestures.Count;
		public bool IsReadOnly => false;

		public GestureList(Node owner)
		{
			this.owner = owner;
		}

		public void Add(Gesture gesture)
		{
			CheckOwner(gesture);
			gesture.Owner = owner;
			gestures.Add(gesture);
		}

		public void Insert(int index, Gesture gesture)
		{
			CheckOwner(gesture);
			gesture.Owner = owner;
			gestures.Insert(index, gesture);
		}

		public Gesture this[int index]
		{
			get { return gestures[index]; }
			set
			{
				CheckOwner(value);
				gestures[index].Owner = null;
				gestures[index] = value;
				value.Owner = owner;
			}
		}

		public void RemoveAt(int index)
		{
			gestures[index].Owner = null;
			gestures.RemoveAt(index);
		}

		public bool Remove(Gesture gesture)
		{
			if (gestures.Remove(gesture)) {
				gesture.Owner = null;
				return true;
			}
			return false;
		}

		public void Clear()
		{
			foreach (var g in gestures) {
				g.Owner = null;
			}
			gestures.Clear();
		}

		public int IndexOf(Gesture gesture) => gestures.IndexOf(gesture);
		public bool Contains(Gesture gesture) => gestures.Contains(gesture);
		public void CopyTo(Gesture[] array, int arrayIndex) => gestures.CopyTo(array, arrayIndex);
		public List<Gesture>.Enumerator GetEnumerator() => gestures.GetEnumerator();
		IEnumerator<Gesture> IEnumerable<Gesture>.GetEnumerator() => gestures.GetEnumerator();
		IEnumerator IEnumerable.GetEnumerator() => gestures.GetEnumerator();

		private static void CheckOwner(Gesture gesture)
		{
			if (gesture.Owner != null) {
				throw new InvalidOperationException("Gesture already has an owner.");
			}
		}
	}
}
