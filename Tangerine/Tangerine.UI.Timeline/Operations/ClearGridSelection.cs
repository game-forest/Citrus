using System;
using System.Collections.Generic;
using System.Linq;
using Lime;
using Tangerine.Core;
using Tangerine.Core.Components;

namespace Tangerine.UI.Timeline.Operations
{
	public sealed class ClearGridSelection : Operation
	{
		public override bool IsChangingDocument => false;

		public static void Perform()
		{
			DocumentHistory.Current.Perform(new ClearGridSelection());
		}

		private ClearGridSelection() {}

		public sealed class Processor : OperationProcessor<ClearGridSelection>
		{
			class Backup { public List<Core.Components.GridSpanList> Spans; }

			protected override void InternalRedo(ClearGridSelection op)
			{
				op.Save(
					new Backup {
						Spans = Document.Current.VisibleSceneItems
							.Select(r => r.Components.GetOrAdd<GridSpanListComponent>().Spans)
							.ToList()
					}
				);
				foreach (var i in Document.Current.VisibleSceneItems) {
					i.Components.Remove<GridSpanListComponent>();
				}
			}

			protected override void InternalUndo(ClearGridSelection op)
			{
				var s = op.Restore<Backup>().Spans;
				foreach (var i in Document.Current.VisibleSceneItems) {
					i.Components.Remove<GridSpanListComponent>();
					i.Components.Add(new GridSpanListComponent(s[i.GetTimelineSceneItemState().Index]));
				}
			}
		}
	}
}
