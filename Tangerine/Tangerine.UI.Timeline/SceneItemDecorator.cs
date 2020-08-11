using System;
using Lime;
using Tangerine.Core;
using Tangerine.UI.Timeline.Components;

namespace Tangerine.UI.Timeline
{
	public static class SceneItemDecorator
	{
		public static void Decorate(Row item)
		{
			var view = item.Components.GetOrAdd<RowView>();
			if (view.GridRow != null) {
				return;
			}
			if (item.TryGetNode(out var node)) {
				if (node is Audio audio) {
					view.GridRow = new GridAudioView(audio);
				} else {
					view.GridRow = new GridNodeView(node);
				}
			} else if (item.GetFolder() != null) {
				view.GridRow = new GridFolderView();
			} else if (item.Components.Contains<Core.Components.PropertyRow>()) {
				var propRow = item.Components.Get<Core.Components.PropertyRow>();
				view.GridRow = new GridPropertyView(propRow.Node, propRow.Animator);
			} else if (item.Components.Contains<Core.Components.AnimationTrackRow>()) {
				view.GridRow = new GridAnimationTrackView(item);
			}
		}
	}
}
