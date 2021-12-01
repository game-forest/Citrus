using System;
using System.Collections.Generic;
using System.Linq;
using Tangerine.Core;
using Tangerine.Core.Components;

namespace Tangerine.UI.Timeline.Operations
{
	public sealed class DeselectGridSpan : Operation
	{
		public readonly GridSpan Span;
		public readonly int Row;

		public override bool IsChangingDocument => false;

		public static void Perform(int row, int a, int b)
		{
			DocumentHistory.Current.Perform(new DeselectGridSpan(row, a, b));
		}

		private DeselectGridSpan(int row, int a, int b)
		{
			Row = row;
			Span = new GridSpan(a, b);
		}

		public sealed class Processor : OperationProcessor<DeselectGridSpan>
		{
			protected override void InternalRedo(DeselectGridSpan op)
			{
				Document.Current.VisibleSceneItems[op.Row].Components.GetOrAdd<GridSpanListComponent>().Spans.DeselectGridSpan(op.Span);
			}

			protected override void InternalUndo(DeselectGridSpan op)
			{
				Document.Current.VisibleSceneItems[op.Row].Components.GetOrAdd<GridSpanListComponent>().Spans.UndoDeselectGridSpan(op.Span);
			}
		}
	}
}
