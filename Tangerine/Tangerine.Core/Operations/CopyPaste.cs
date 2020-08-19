using System;
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

		public static void Perform(Vector2? mousePosition = null)
		{
			Row parent;
			int index;
			if (Document.Current.Animation.IsCompound) {
				GetAnimationTrackLinkLocation(out parent, out index);
			} else {
				SceneTreeUtils.GetSceneItemLinkLocation(out parent, out index);
			}
			var data = Clipboard.Text;
			if (!string.IsNullOrEmpty(data)) {
				var stream = new System.IO.MemoryStream(System.Text.Encoding.UTF8.GetBytes(data));
				Document.Current.History.DoTransaction(() => {
					ClearRowSelection.Perform();
					PasteSceneItemsFromStream.Perform(stream, parent, index, mousePosition, out var pastedItems);
					foreach (var i in pastedItems) {
						SelectRow.Perform(i);
					}
				});
			}
		}

		private static void GetAnimationTrackLinkLocation(out Row parent, out int index)
		{
			var focusedItem = Document.Current.RecentlySelectedSceneItem();
			parent = Document.Current.AnimationTree;
			index = focusedItem == null ? 0 : Document.Current.AnimationTree.Rows.IndexOf(focusedItem);
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
					newFocused = FindFocusedAfterUnlink(item);
					UnlinkSceneItem.Perform(item);
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
