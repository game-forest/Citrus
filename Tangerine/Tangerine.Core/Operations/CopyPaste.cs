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
			var topItems = SceneTreeUtils.EnumerateTopSceneItems(Document.Current.SelectedRows()).ToList();
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

		private static bool CanPaste(MemoryStream stream, Row parent)
		{
			try {
				var container = InternalPersistence.Instance.ReadObject<Frame>(null, stream);
				foreach (var a in container.Animators) {
					a.AnimationId = Document.Current.AnimationId;
				}
				if (parent.TryGetAnimation(out _)) {
					// Allow inserting animation tracks into the compound animation.
					return container.Animations.TryFind(CopySceneItemsToStream.AnimationTracksContainerAnimationId, out _);
				}
				if (container.Animations.Count > 0) {
					// Use animations panel to paste animations.
					return false;
				}
				// Don't use Document.Current.SceneTreeBuilder since we don't want to store an item in the scene item cache.
				var rowTree = new SceneTreeBuilder().BuildSceneTreeForNode(container);
				foreach (var i in rowTree.Rows) {
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

		public static void Perform(out List<Row> pastedItems)
		{
			Row parent;
			int index;
			var data = Clipboard.Text;
			var result = new List<Row>();
			if (!string.IsNullOrEmpty(data)) {
				var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(data));
				var container = InternalPersistence.Instance.ReadObject<Frame>(null, stream);
				// To explicitly determine if animators are selected to paste or not
				if (container.Animators.Count > 0) {
					SceneTreeUtils.GetSceneItemLinkLocation(out parent, out index, typeof(IAnimator));
				} else {
					SceneTreeUtils.GetSceneItemLinkLocation(out parent, out index, typeof(Node));
				}
				Document.Current.History.DoTransaction(() => {
					ClearRowSelection.Perform();
					Perform(stream, parent, index, out result);
					foreach (var i in result) {
						SelectRow.Perform(i);
					}
				});
			}
			pastedItems = result;
		}

		public static bool Perform(MemoryStream stream, Row parent, int index, out List<Row> pastedItems)
		{
			pastedItems = new List<Row>();
			if (!CanPaste(stream, parent)) {
				return false;
			}
			var container = InternalPersistence.Instance.ReadObject<Frame>(null, stream);
			container.LoadExternalScenes();
			foreach (var n in container.Nodes) {
				Document.Decorate(n);
			}
			foreach (var a in container.Animators) {
				a.AnimationId = Document.Current.AnimationId;
			}
			if (container.Animations.TryFind(CopySceneItemsToStream.AnimationTracksContainerAnimationId, out var animation)) {
				foreach (var track in animation.Tracks.ToList()) {
					animation.Tracks.Remove(track);
					LinkSceneItem.Perform(parent, index++, track);
					pastedItems.Add(Document.Current.GetSceneItemForObject(track));
				}
			} else {
				// Don't use Document.Current.SceneTreeBuilder since we don't want to store an item in the scene item cache.
				var itemsToPaste = Document.Current.SceneTreeBuilder.BuildSceneTreeForNode(container);
				foreach (var i in itemsToPaste.Rows.ToList()) {
					UnlinkSceneItem.Perform(i);
					LinkSceneItem.Perform(parent, index, i);
					index = parent.Rows.IndexOf(i) + 1;
					pastedItems.Add(i);
					DecorateNodes(i);
				}
			}
			Pasted?.Invoke();
			return true;
		}

		private static void DecorateNodes(Row item)
		{
			if (item.TryGetNode(out var node)) {
				Document.Decorate(node);
			} else if (item.TryGetFolder(out _)) {
				foreach (var i in item.Rows) {
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
				Row newFocused = null;
				foreach (var item in SceneTreeUtils.EnumerateSelectedTopSceneItems().ToList()) {
					if (!(item.Parent.GetNode() is DistortionMesh)) {
						newFocused = FindFocusedAfterUnlink(item);
						UnlinkSceneItem.Perform(item);
					}
				}
				if (newFocused != null) {
					SelectRow.Perform(newFocused);
				}
			});

			Row FindFocusedAfterUnlink(Row item)
			{
				var i = item.Parent.Rows.IndexOf(item);
				if (i > 0) {
					return item.Parent.Rows[i - 1];
				}
				if (i < item.Parent.Rows.Count - 1) {
					return item.Parent.Rows[i + 1];
				}
				return item.Parent.GetNode() == Document.Current.Container ? null : item.Parent;
			}
		}
	}
}
