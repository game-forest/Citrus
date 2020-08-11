using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Yuzu;

namespace Lime
{
	public class FolderList : IList<Folder.Descriptor>
	{
		private readonly Node owner;
		private readonly List<Folder.Descriptor> items = new List<Folder.Descriptor>();

		public FolderList(Node owner) => this.owner = owner;
		
		IEnumerator<Folder.Descriptor> IEnumerable<Folder.Descriptor>.GetEnumerator() => items.GetEnumerator();

		IEnumerator IEnumerable.GetEnumerator() => items.GetEnumerator();

		public List<Folder.Descriptor>.Enumerator GetEnumerator() => items.GetEnumerator();

		public void Add(Folder.Descriptor item) => Insert(Count, item);

		public void Clear()
		{
			while (Count > 0) {
				RemoveAt(Count - 1);
			}
		}

		public bool Contains(Folder.Descriptor item) => items.Contains(item);

		public void CopyTo(Folder.Descriptor[] array, int arrayIndex) => items.CopyTo(array, arrayIndex);

		public bool Remove(Folder.Descriptor item)
		{
			if (items.Remove(item)) {
				item.Owner = null;
				return true;
			}
			return false;
		}

		public int Count => items.Count;
		
		public bool IsReadOnly => false;
		
		public int IndexOf(Folder.Descriptor item) => items.IndexOf(item);

		public void Insert(int index, Folder.Descriptor item)
		{
			if (item.Owner != null) {
				throw  new InvalidOperationException();
			}
			item.Owner = owner;
			items.Insert(index, item);
		}

		public void RemoveAt(int index)
		{
			items[index].Owner = null;
			items.RemoveAt(index);
		}

		public Folder.Descriptor this[int index]
		{
			get => items[index];
			set => throw new NotImplementedException();
		}
	}

	public class Folder
	{
		[YuzuDontGenerateDeserializer]
		public class Descriptor
		{
			public Node Owner { get; internal set; }
			
			[YuzuMember]
			public string Id { get; set; }
			
			[YuzuMember]
			public int Index { get; set; }

			[YuzuMember]
			public int ItemCount { get; set; }
			
			public void Unlink() => Owner?.Folders.Remove(this);
		}
	}
}
