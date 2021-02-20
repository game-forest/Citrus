using Lime;
using System.Collections.Generic;
using Tangerine.Core;

namespace Tangerine.UI.Timeline.Components
{
	public class GridNodeView : IGridRowView
	{
		private readonly Node node;

		public Widget GridWidget { get; }
		public Widget OverviewWidget { get; }
		public AwakeBehavior GridWidgetAwakeBehavior => GridWidget.Components.Get<AwakeBehavior>();
		public AwakeBehavior OverviewWidgetAwakeBehavior => OverviewWidget.Components.Get<AwakeBehavior>();

		public GridNodeView(Node node)
		{
			this.node = node;
			GridWidget = new Widget {
				LayoutCell = new LayoutCell {StretchY = 0},
				MinHeight = TimelineMetrics.DefaultRowHeight,
				Presenter = new SyncDelegatePresenter<Widget>(Render)
			};
			GridWidget.Components.Add(new AwakeBehavior());
			OverviewWidget = new Widget {
				LayoutCell = new LayoutCell {StretchY = 0},
				MinHeight = TimelineMetrics.DefaultRowHeight,
				Presenter = new SyncDelegatePresenter<Widget>(Render)
			};
			OverviewWidget.Components.Add(new AwakeBehavior());
		}

		private readonly GridKeyframesRenderer keyframesRenderer = new GridKeyframesRenderer();
		private int effectiveAnimatorsVersionHashCode;

		protected virtual void Render(Widget widget)
		{
			var hc = CalculateEffectiveAnimatorsVersionHashCode();
			if (effectiveAnimatorsVersionHashCode != hc) {
				effectiveAnimatorsVersionHashCode = hc;
				keyframesRenderer.ClearCells();
				keyframesRenderer.GenerateCells(node, Document.Current.Animation);
			}
			keyframesRenderer.RenderCells(widget);
		}

		private int CalculateEffectiveAnimatorsVersionHashCode()
		{
			var hashCode = 1568519108;
			unchecked {
				var effectiveAnimatorsSet = Document.Current.Animation.ValidatedEffectiveAnimatorsSet;
				foreach (var animator in node.Animators) {
					if (effectiveAnimatorsSet.Contains(animator)) {
						hashCode = hashCode * -1521134295 + animator.GetHashCode();
						hashCode = hashCode * -1521134295 + animator.Version;
					}
				}
			}
			return hashCode;
		}
	}
}
