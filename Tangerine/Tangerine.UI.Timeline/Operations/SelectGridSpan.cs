using System;
using System.Collections.Generic;
using System.Linq;
using Tangerine.Core;
using Tangerine.Core.Components;

namespace Tangerine.UI.Timeline.Operations
{
	public sealed class SelectGridSpan : Operation
	{
		public readonly GridSpan Span;
		public readonly int Row;

		public override bool IsChangingDocument => false;

		public static void Perform(int row, int a, int b)
		{
			DocumentHistory.Current.Perform(new SelectGridSpan(row, a, b));
			Core.Operations.SelectSceneItem.Perform(Document.Current.VisibleSceneItems[row]);
		}

		private SelectGridSpan(int row, int a, int b)
		{
			Row = row;
			Span = new GridSpan(a, b);
		}

		public sealed class Processor : OperationProcessor<SelectGridSpan>
		{
			protected override void InternalRedo(SelectGridSpan op)
			{
				Document.Current.VisibleSceneItems[op.Row].Components
					.GetOrAdd<GridSpanListComponent>()
					.Spans
					.Add(op.Span);
			}

			protected override void InternalUndo(SelectGridSpan op)
			{
				Document.Current.VisibleSceneItems[op.Row].Components
					.GetOrAdd<GridSpanListComponent>()
					.Spans
					.Remove(op.Span);
			}
		}
	}
}
