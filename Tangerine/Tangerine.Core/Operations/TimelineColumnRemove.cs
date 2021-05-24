using System.Collections.Generic;
using System.Linq;
using Lime;

namespace Tangerine.Core.Operations
{
	public sealed class TimelineColumnRemove : Operation
	{
		public override bool IsChangingDocument => true;

		private readonly int column;
		private readonly Dictionary<IAnimator, int> keyRemoveAt = new Dictionary<IAnimator, int>();
		private int markerRemoveAt = -1;

		private TimelineColumnRemove(int column)
		{
			this.column = column;
		}

		public static void Perform(int column)
		{
			Document.Current.History.Perform(new TimelineColumnRemove(column));
		}

		public sealed class Processor : OperationProcessor<TimelineColumnRemove>
		{
			protected override void InternalRedo(TimelineColumnRemove op)
			{
				op.keyRemoveAt.Clear();
				RemoveTimelineColumn(op);
				var markers = Document.Current.Animation.Markers;
				if (markers.Count == 0) {
					return;
				}
				if (op.markerRemoveAt == -1) {
					var markersOccupied = new HashSet<int>();
					foreach (var m in markers) {
						markersOccupied.Add(m.Frame);
					}
					for (var i = op.column; ; ++i) {
						if (!markersOccupied.Contains(i)) {
							op.markerRemoveAt = i;
							break;
						}
					}
				}
				foreach (var m in markers) {
					if (m.Frame != 0 && m.Frame >= op.markerRemoveAt) {
						m.Frame -= 1;
					}
				}
			}

			protected override void InternalUndo(TimelineColumnRemove op)
			{
				foreach (var animator in Document.Current.Animation.ValidatedEffectiveAnimators.OfType<IAnimator>()) {
					foreach (var k in animator.ReadonlyKeys.Reverse()) {
						if (k.Frame >= op.keyRemoveAt[animator]) {
							k.Frame += 1;
						}
					}
					animator.ResetCache();
					animator.IncreaseVersion();
				}
				foreach (var m in Document.Current.Animation.Markers.Reverse()) {
					if (m.Frame >= op.markerRemoveAt) {
						m.Frame += 1;
					}
				}
			}

			private void RemoveTimelineColumn(TimelineColumnRemove op)
			{
				var occupied = new HashSet<int>();
				foreach (var animator in Document.Current.Animation.ValidatedEffectiveAnimators.OfType<IAnimator>()) {
					occupied.Clear();
					foreach (var k in animator.ReadonlyKeys) {
						occupied.Add(k.Frame);
					}
					for (var i = op.column; ; ++i) {
						if (!occupied.Contains(i)) {
							op.keyRemoveAt[animator] = i;
							break;
						}
					}
					foreach (var k in animator.ReadonlyKeys) {
						if (k.Frame != 0 && k.Frame >= op.keyRemoveAt[animator]) {
							k.Frame -= 1;
						}
					}
					animator.ResetCache();
					animator.IncreaseVersion();
				}
			}
		}
	}
}
