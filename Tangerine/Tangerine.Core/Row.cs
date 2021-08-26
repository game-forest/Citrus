using System;
using System.Collections;
using System.Collections.Generic;
using Lime;
using Tangerine.Core.Components;

namespace Tangerine.Core
{
	public class Row
	{
		public string Id
		{
			get => Components.Get<CommonRowData>().Id;
			set => Components.Get<CommonRowData>().Id = value;
		}

		public Row Parent { get; internal set; }

		public readonly RowList Rows;
		public readonly ComponentCollection<Component> Components = new ComponentCollection<Component>();

		public Row()
		{
			Rows = new RowList(this);
		}

		public void Unlink() => Parent?.Rows.Remove(this);

		public bool DescendantOf(Row row)
		{
			for (var r = Parent; r != null; r = r.Parent) {
				if (r == row)
					return true;
			}
			return false;
		}

		public bool SameOrDescendantOf(Row row)
		{
			return row == this || DescendantOf(row);
		}

		public TimelineItemStateComponent GetTimelineItemState() => Components.GetOrAdd<TimelineItemStateComponent>();

		public Folder.Descriptor GetFolder() => Components.Get<FolderRow>()?.Folder;

		public bool TryGetFolder(out Folder.Descriptor folder)
		{
			folder = GetFolder();
			return folder != null;
		}

		public IAnimator GetAnimator() => Components.Get<AnimatorRow>()?.Animator;

		public bool TryGetAnimator(out IAnimator animator)
		{
			animator = GetAnimator();
			return animator != null;
		}

		public Node GetNode() => Components.Get<NodeRow>()?.Node;

		public bool TryGetNode(out Node node)
		{
			node = GetNode();
			return node != null;
		}

		public Animation GetAnimation() => Components.Get<AnimationRow>()?.Animation;

		public bool TryGetAnimation(out Animation animation)
		{
			animation = GetAnimation();
			return animation != null;
		}

		public Marker GetMarker() => Components.Get<MarkerRow>()?.Marker;

		public bool TryGetMarker(out Marker marker)
		{
			marker = GetMarker();
			return marker != null;
		}

		public AnimationTrack GetAnimationTrack() => Components.Get<AnimationTrackRow>()?.Track;

		public bool TryGetAnimationTrack(out AnimationTrack track)
		{
			track = GetAnimationTrack();
			return track != null;
		}

		public IEnumerable<Row> SelfAndDescendants()
		{
			var s = new Stack<Row>();
			s.Push(this);
			while (s.Count != 0) {
				var i = s.Pop();
				yield return i;
				foreach (var c in i.Rows) {
					s.Push(c);
				}
			}
		}
	}

	public class RowList : IList<Row>
	{
		private readonly Row owner;
		private readonly List<Row> list = new List<Row>();
		IEnumerator<Row> IEnumerable<Row>.GetEnumerator() => list.GetEnumerator();

		IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

		public List<Row>.Enumerator GetEnumerator() => list.GetEnumerator();

		public RowList(Row owner) => this.owner = owner;

		public void Add(Row item) => Insert(Count, item);

		public void Clear()
		{
			while (Count > 0) {
				RemoveAt(Count - 1);
			}
		}

		public bool Contains(Row item) => list.Contains(item);

		public void CopyTo(Row[] array, int arrayIndex) => list.CopyTo(array, arrayIndex);

		public bool Remove(Row item)
		{
			var i = IndexOf(item);
			if (i < 0) {
				return false;
			}
			RemoveAt(i);
			return true;
		}

		public int Count => list.Count;
		public bool IsReadOnly => false;

		public int IndexOf(Row item) => list.IndexOf(item);

		public void Insert(int index, Row item)
		{
			if (item.Parent != null) {
				throw new InvalidOperationException();
			}
			item.Parent = owner;
			list.Insert(index, item);
		}

		public void RemoveAt(int index)
		{
			var item = list[index];
			item.Parent = null;
			list.RemoveAt(index);
		}

		public Row this[int index]
		{
			get => list[index];
			set => throw new System.NotSupportedException();
		}
	}
}
