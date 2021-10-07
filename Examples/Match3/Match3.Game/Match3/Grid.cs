using Lime;
using System.Collections;
using System.Collections.Generic;

namespace Match3
{
	public class Grid<T> : IEnumerable<(IntVector2, T)>
	{
		private readonly Dictionary<IntVector2, T> data = new Dictionary<IntVector2, T>();

		public T this[IntVector2 position]
		{
			get
			{
				if (!data.ContainsKey(position)) {
					data.Add(position, default);
				}
				return data[position];
			}

			set
			{
				data[position] = value;
			}
		}

		public IEnumerator<(IntVector2, T)> GetEnumerator()
		{
			foreach (var (k, v) in data) {
				yield return (k, v);
			}
		}

		IEnumerator IEnumerable.GetEnumerator() => data.GetEnumerator();
	}
}

