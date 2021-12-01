using Lime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Tangerine.Core;
using Tangerine.Core.Components;
using Tangerine.Core.Operations;

namespace Tangerine.UI.Timeline
{
	public static class MoveNodesUp
	{
		public static void Perform()
		{
			var items = Document.Current.TopLevelSelectedSceneItems().ToList();
			foreach (var item in items) {
				var parent = item.Parent;
				var index = parent.SceneItems.IndexOf(item);
				if (index > 0) {
					UnlinkSceneItem.Perform(item);
					LinkSceneItem.Perform(parent, new SceneTreeIndex(index - 1), item);
				}
			}
		}
	}

	public static class MoveNodesDown
	{
		public static void Perform()
		{
			var items = Document.Current.TopLevelSelectedSceneItems().ToList();
			foreach (var item in items) {
				var parent = item.Parent;
				var index = parent.SceneItems.IndexOf(item);
				if (index < parent.SceneItems.Count - 1) {
					UnlinkSceneItem.Perform(item);
					LinkSceneItem.Perform(parent, new SceneTreeIndex(index + 1), item);
				}
			}
		}
	}
}
