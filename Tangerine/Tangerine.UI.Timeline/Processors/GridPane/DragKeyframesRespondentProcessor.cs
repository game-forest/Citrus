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
							var selected = Document.Current.SelectedRows().Where(r => r.Selected).Select(r => r.Index).ToDictionary(x => x);
							ClearRowSelection.Perform();
							foreach (var row in Document.Current.Rows) {
								if (selected.ContainsKey(row.Index - rq.Offset.Y)) {
									SelectRow.Perform(row, select: true);
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
