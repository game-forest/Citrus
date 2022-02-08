using System;
using System.Collections.Generic;
using System.Linq;
using Lime;
using Tangerine.Core;
using Tangerine.Core.Components;

namespace Tangerine.UI.Timeline.Operations
{
	public sealed class ShiftGridSelection : Operation
	{
		public override bool IsChangingDocument => false;

		public readonly IntVector2 Offset;

		public static void Perform(IntVector2 offset)
		{
			DocumentHistory.Current.Perform(new ShiftGridSelection(offset));
		}

		private ShiftGridSelection(IntVector2 offset)
		{
			Offset = offset;
		}

		public sealed class Processor : OperationProcessor<ShiftGridSelection>
		{
			private class Backup { public List<Core.Components.GridSpanList> Spans; }

			protected override void InternalRedo(ShiftGridSelection op)
			{
				ShiftX(op.Offset.X);
				ShiftY(op);
			}

			protected override void InternalUndo(ShiftGridSelection op)
			{
				UnshiftY(op);
				ShiftX(-op.Offset.X);
			}

			private void ShiftX(int offset)
			{
				foreach (var item in Document.Current.VisibleSceneItems) {
					var spans = item.Components.GetOrAdd<GridSpanListComponent>().Spans;
					for (int i = 0; i < spans.Count; i++) {
						var s = spans[i];
						s.A += offset;
						s.B += offset;
						spans[i] = s;
					}
				}
			}

			private void ShiftY(ShiftGridSelection op)
			{
				var b = new Backup {
					Spans = Document.Current.VisibleSceneItems
						.Select(i => i.Components.GetOrAdd<GridSpanListComponent>().Spans)
						.ToList(),
				};
				op.Save(b);
				if (op.Offset.Y != 0) {
					foreach (var item in Document.Current.VisibleSceneItems) {
						var i = item.GetTimelineSceneItemState().Index - op.Offset.Y;
						item.Components.Remove<GridSpanListComponent>();
						item.Components.Add(
							i >= 0 && i < Document.Current.VisibleSceneItems.Count
								? new GridSpanListComponent(b.Spans[i])
								: new GridSpanListComponent()
							);
					}
				}
			}

			private void UnshiftY(ShiftGridSelection op)
			{
				var b = op.Restore<Backup>();
				foreach (var i in Document.Current.VisibleSceneItems) {
					i.Components.Remove<GridSpanListComponent>();
					i.Components.Add(new GridSpanListComponent(b.Spans[i.GetTimelineSceneItemState().Index]));
				}
			}
		}
	}
}
