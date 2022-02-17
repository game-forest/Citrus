using System.Linq;
using Lime;

namespace Tangerine.Core.Operations
{
	public sealed class TimelineHorizontalShift : Operation
	{
		public override bool IsChangingDocument => true;

		private readonly int column;
		private int delta;

		private TimelineHorizontalShift(int column, int delta)
		{
			this.column = column;
			this.delta = delta;
		}

		public static void Perform(int column, int delta)
		{
			Document.Current.History.Perform(new TimelineHorizontalShift(column, delta));
		}

		public sealed class Processor : OperationProcessor<TimelineHorizontalShift>
		{
			protected override void InternalRedo(TimelineHorizontalShift op)
			{
				foreach (var animator in Document.Current.Animation.ValidatedEffectiveAnimators.OfType<IAnimator>()) {
					var keys = op.delta > 0 ? animator.Keys.Reverse() : animator.Keys;
					foreach (var k in keys) {
						if (k.Frame >= op.column) {
							k.Frame += op.delta;
						}
					}
					animator.ResetCache();
					animator.IncreaseVersion();
				}
				var markers = op.delta > 0
					? Document.Current.Animation.Markers.Reverse()
					: Document.Current.Animation.Markers;
				foreach (var m in markers) {
					if (m.Frame >= op.column) {
						m.Frame += op.delta;
					}
				}
			}
			protected override void InternalUndo(TimelineHorizontalShift op)
			{
				op.delta = -op.delta;
				InternalRedo(op);
				op.delta = -op.delta;
			}
		}
	}
}
