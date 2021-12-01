using System;
using System.Linq;
using System.Collections.Generic;
using Lime;
using Tangerine.Core;
using Tangerine.Core.Components;
using Tangerine.UI.Timeline.Components;

namespace Tangerine.UI.Timeline
{
	class HasKeyframeRespondentProcessor : Core.ITaskProvider
	{
		public IEnumerator<object> Task()
		{
			var g = Timeline.Instance.Globals;
			while (true) {
				var r = g.Get<HasKeyframeRequest>();
				if (r != null) {
					r.Result = HasKeyframeOnCell(r.Cell);
				}
				yield return null;
			}
		}

		bool HasKeyframeOnCell(IntVector2 cell)
		{
			var item = Document.Current.VisibleSceneItems[cell.Y];
			var nodeData = item.Components.Get<NodeSceneItem>();
			if (nodeData != null) {
				var hasKey = nodeData.Node.Animators.Any(i => i.Keys.Any(k => k.Frame == cell.X));
				return hasKey;
			}
			var ai = item.Components.Get<AnimatorSceneItem>();
			return ai != null && ai.Animator.Keys.Any(k => k.Frame == cell.X);
		}
	}
}
