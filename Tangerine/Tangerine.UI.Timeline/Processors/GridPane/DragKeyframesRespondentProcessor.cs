using System;
using System.Linq;
using System.Collections.Generic;
using Lime;
using Tangerine.Core;
using Tangerine.Core.Components;
using Tangerine.Core.Operations;
using Tangerine.UI.Timeline.Components;

namespace Tangerine.UI.Timeline
{
	public class DragKeyframesRespondentProcessor : ITaskProvider
	{
		public IEnumerator<object> Task()
		{
			var g = Timeline.Instance.Globals;
			while (true) {
				var rq = g.Get<DragKeyframesRequest>();
				if (rq != null) {
					Document.Current.History.DoTransaction(() => {
						g.Remove<DragKeyframesRequest>();
						Operations.DragKeyframes.Perform(rq.Offset, rq.RemoveOriginals);
						Operations.ShiftGridSelection.Perform(rq.Offset);
						if (rq.Offset.Y != 0) {
							var selected =
								Document.Current.SelectedSceneItems().
								Where(r => r.GetTimelineSceneItemState().Selected).
								Select(r => r.GetTimelineSceneItemState().Index).ToDictionary(x => x);
							ClearSceneItemSelection.Perform();
							foreach (var i in Document.Current.VisibleSceneItems) {
								if (selected.ContainsKey(i.GetTimelineSceneItemState().Index - rq.Offset.Y)) {
									SelectSceneItem.Perform(i, select: true);
								}
							}
						}
					});
				}
				yield return null;
			}
		}
	}
}
