using System;
using System.Collections;
using System.Collections.Generic;
using Lime;
using Tangerine.Core.Components;

namespace Tangerine.Core
{
	public class SceneItem
	{
		public string Id
		{
			get => Components.Get<CommonSceneItemData>().Id;
			set => Components.Get<CommonSceneItemData>().Id = value;
		}

		public SceneItem Parent { get; internal set; }

		public readonly SceneItemList SceneItems;
		public readonly ComponentCollection<Component> Components = new ComponentCollection<Component>();

		public SceneItem()
		{
			SceneItems = new SceneItemList(this);
		}

		public void Unlink() => Parent?.SceneItems.Remove(this);

		public bool DescendantOf(SceneItem sceneItem)
		{
			for (var i = Parent; i != null; i = i.Parent) {
				if (i == sceneItem)
					return true;
			}
			return false;
		}

		public bool SameOrDescendantOf(SceneItem sceneItem)
		{
			return sceneItem == this || DescendantOf(sceneItem);
		}

		public TimelineSceneItemStateComponent GetTimelineSceneItemState()
		{
			return Components.GetOrAdd<TimelineSceneItemStateComponent>();
		}

		public Folder.Descriptor GetFolder() => Components.Get<FolderSceneItem>()?.Folder;

		public bool TryGetFolder(out Folder.Descriptor folder)
		{
			folder = GetFolder();
			return folder != null;
		}

		public IAnimator GetAnimator() => Components.Get<AnimatorSceneItem>()?.Animator;

		public bool TryGetAnimator(out IAnimator animator)
		{
			animator = GetAnimator();
			return animator != null;
		}

		public Node GetNode() => Components.Get<NodeSceneItem>()?.Node;

		public bool TryGetNode(out Node node)
		{
			node = GetNode();
			return node != null;
		}

		public Animation GetAnimation() => Components.Get<AnimationSceneItem>()?.Animation;

		public bool TryGetAnimation(out Animation animation)
		{
			animation = GetAnimation();
			return animation != null;
		}

		public Marker GetMarker() => Components.Get<MarkerSceneItem>()?.Marker;

		public bool TryGetMarker(out Marker marker)
		{
			marker = GetMarker();
			return marker != null;
		}

		public AnimationTrack GetAnimationTrack() => Components.Get<AnimationTrackSceneItem>()?.Track;

		public bool TryGetAnimationTrack(out AnimationTrack track)
		{
			track = GetAnimationTrack();
			return track != null;
		}

		public IEnumerable<SceneItem> SelfAndDescendants()
		{
			var s = new Stack<SceneItem>();
			s.Push(this);
			while (s.Count != 0) {
				var i = s.Pop();
				yield return i;
				foreach (var c in i.SceneItems) {
					s.Push(c);
				}
			}
		}
	}

	public class SceneItemList : IList<SceneItem>
	{
		private readonly SceneItem owner;
		private readonly List<SceneItem> list = new List<SceneItem>();
		IEnumerator<SceneItem> IEnumerable<SceneItem>.GetEnumerator() => list.GetEnumerator();

		IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

		public List<SceneItem>.Enumerator GetEnumerator() => list.GetEnumerator();

		public SceneItemList(SceneItem owner) => this.owner = owner;

		public void Add(SceneItem item) => Insert(Count, item);

		public void Clear()
		{
			while (Count > 0) {
				RemoveAt(Count - 1);
			}
		}

		public bool Contains(SceneItem item) => list.Contains(item);

		public void CopyTo(SceneItem[] array, int arrayIndex) => list.CopyTo(array, arrayIndex);

		public bool Remove(SceneItem item)
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

		public int IndexOf(SceneItem item) => list.IndexOf(item);

		public void Insert(int index, SceneItem item)
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

		public SceneItem this[int index]
		{
			get => list[index];
			set => throw new System.NotSupportedException();
		}
	}
}
