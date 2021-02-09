using Lime;
using System.Collections.Generic;
using Tangerine.Core;

namespace Tangerine.UI.Timeline.Components
{
	public class GridNodeView : IGridRowView
	{
		private readonly Node node;
		private Hasher hasher = new Hasher();

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

		private GridKeyframesRenderer keyframesRenderer = new GridKeyframesRenderer();
		private long animatorsVersion = -1;
		private Animation animation;

		protected virtual void Render(Widget widget)
		{
			long v = CalculateAnimatorsTotalVersion();
			if (animatorsVersion != v || animation != Document.Current.Animation) {
				animatorsVersion = v;
				animation = Document.Current.Animation;
				keyframesRenderer.ClearCells();
				keyframesRenderer.GenerateCells(node, animation);
			}
			keyframesRenderer.RenderCells(widget);
		}

		private long CalculateAnimatorsTotalVersion()
		{
			hasher.Begin();
			hasher.Write(node.Animators.Version);
			foreach (var a in node.Animators) {
				hasher.Write(a.Version);
			}
			return hasher.End();
		}
	}
}
