using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Lime;

namespace Tangerine.Core.Operations
{
	public static class Copy
	{
		public static void CopyToClipboard()
		{
			if (Document.Current == null) {
				return;
			}
			Clipboard.Text = CopyToString();
		}

		private static string CopyToString()
		{
			var stream = new System.IO.MemoryStream();
			var topItems = SceneTreeUtils.EnumerateTopSceneItems(Document.Current.SelectedSceneItems()).ToList();
			CopySceneItemsToStream.Perform(topItems, stream);
			var text = System.Text.Encoding.UTF8.GetString(stream.ToArray());
			return text;
		}
	}

	public static class Cut
	{
		public static void Perform()
		{
			Copy.CopyToClipboard();
			Delete.Perform();
		}
	}

	public static class Paste
	{
		public static event Action Pasted;

		private static bool CanPaste(MemoryStream stream, SceneItem parent)
		{
			try {
				var container = InternalPersistence.Instance.ReadFromStream<Frame>(null, stream);
				foreach (var a in container.Animators) {
					a.AnimationId = Document.Current.AnimationId;
				}
				if (parent.TryGetAnimation(out _)) {
					// Allow inserting animation tracks into the compound animation.
					return container.Animations.TryFind(
						CopySceneItemsToStream.AnimationTracksContainerAnimationId, out _
					);
				}
				if (container.Animations.Count > 0) {
					// Use animations panel to paste animations.
					return false;
				}
				// Don't use Document.Current.SceneTreeBuilder since we don't
				// want to store an item in the scene item cache.
				var sceneTree = new SceneTreeBuilder().BuildSceneTreeForNode(container);
				foreach (var i in sceneTree.SceneItems) {
					if (!LinkSceneItem.CanLink(item: i, parent: parent)) {
						return false;
					}
				}
				if (parent.TryGetNode(out var node) &&
					ClassAttributes<TangerineLockChildrenNodeList>.Get(node.GetType()) != null) {
					return false;
				}
				if (container.DefaultAnimation.Tracks.Count > 0 && parent.GetAnimation() == null) {
					return false;
				}
				return true;
			} catch {
				return false;
			}
		}

		public static void Perform(out List<SceneItem> pastedItems)
		{
			SceneItem parent;
			SceneTreeIndex index;
			var data = Clipboard.Text;
			var result = new List<SceneItem>();
			if (!string.IsNullOrEmpty(data)) {
				var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(data));
				var container = InternalPersistence.Instance.ReadFromStream<Frame>(null, stream);
				// To explicitly determine if animators are selected to paste or not
				if (container.Animators.Count > 0) {
					SceneTreeUtils.TryGetSceneItemLinkLocation(out parent, out index, typeof(IAnimator));
				} else {
					SceneTreeUtils.TryGetSceneItemLinkLocation(out parent, out index, typeof(Node));
				}
				Document.Current.History.DoTransaction(() => {
					ClearSceneItemSelection.Perform();
					Perform(stream, parent, index, out result);
					foreach (var i in result) {
						SelectSceneItem.Perform(i);
					}
				});
			}
			pastedItems = result;
		}

		public static bool Perform(
			MemoryStream stream,
			SceneItem parent,
			SceneTreeIndex index,
			out List<SceneItem> pastedItems
		) {
			pastedItems = new List<SceneItem>();
			if (!CanPaste(stream, parent)) {
				return false;
			}
			var container = InternalPersistence.Instance.ReadFromStream<Frame>(null, stream);
			container.LoadExternalScenes();
			foreach (var n in container.Nodes) {
				Document.Decorate(n);
			}
			foreach (var a in container.Animators) {
				a.AnimationId = Document.Current.AnimationId;
			}
			if (
				container.Animations.TryFind(
					CopySceneItemsToStream.AnimationTracksContainerAnimationId, out var animation
				)
			) {
				foreach (var track in animation.Tracks.ToList()) {
					animation.Tracks.Remove(track);
					LinkSceneItem.Perform(parent, index, track);
					index = new SceneTreeIndex(index.Value + 1);
					pastedItems.Add(Document.Current.GetSceneItemForObject(track));
				}
			} else {
				// Don't use Document.Current.SceneTreeBuilder since
				// we don't want to store an item in the scene item cache.
				var itemsToPaste = Document.Current.SceneTreeBuilder.BuildSceneTreeForNode(container);
				foreach (var i in itemsToPaste.SceneItems.ToList()) {
					UnlinkSceneItem.Perform(i);
					LinkSceneItem.Perform(parent, index, i);
					index = new SceneTreeIndex(parent.SceneItems.IndexOf(i) + 1);
					pastedItems.Add(i);
					DecorateNodes(i);
				}
			}
			Pasted?.Invoke();
			return true;
		}

		private static void DecorateNodes(SceneItem item)
		{
			if (item.TryGetNode(out var node)) {
				Document.Decorate(node);
			} else if (item.TryGetFolder(out _)) {
				foreach (var i in item.SceneItems) {
					DecorateNodes(i);
				}
			}
		}
	}

	public static class Delete
	{
		public static void Perform()
		{
			var doc = Document.Current;
			doc.History.DoTransaction(() => {
				SceneItem newFocused = null;
				foreach (var item in SceneTreeUtils.EnumerateSelectedTopSceneItems().ToList()) {
					if (!(item.Parent.GetNode() is DistortionMesh)) {
						newFocused = FindFocusedAfterUnlink(item);
						UnlinkSceneItem.Perform(item);
					}
				}
				if (newFocused != null) {
					SelectSceneItem.Perform(newFocused);
				}
			});

			SceneItem FindFocusedAfterUnlink(SceneItem item)
			{
				var i = item.Parent.SceneItems.IndexOf(item);
				if (i > 0) {
					return item.Parent.SceneItems[i - 1];
				}
				if (i < item.Parent.SceneItems.Count - 1) {
					return item.Parent.SceneItems[i + 1];
				}
				return item.Parent.GetNode() == Document.Current.Container ? null : item.Parent;
			}
		}
	}
}
