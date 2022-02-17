using Lime;
using Tangerine.Core;

namespace Tangerine.UI.Timeline.Components
{
	public class GridPropertyView : IGridRowView
	{
		private readonly Node node;
		private readonly IAnimator animator;

		public Widget GridWidget { get; }
		public Widget OverviewWidget { get; }
		public AwakeBehavior GridWidgetAwakeBehavior => GridWidget.Components.Get<AwakeBehavior>();
		public AwakeBehavior OverviewWidgetAwakeBehavior => OverviewWidget.Components.Get<AwakeBehavior>();

		public GridPropertyView(Node node, IAnimator animator)
		{
			this.node = node;
			this.animator = animator;
			GridWidget = new Widget {
				LayoutCell = new LayoutCell { StretchY = 0 },
				MinHeight = TimelineMetrics.DefaultRowHeight,
				Presenter = new SyncDelegatePresenter<Widget>(Render),
			};
			GridWidget.Components.Add(new AwakeBehavior());
			OverviewWidget = new Widget {
				LayoutCell = new LayoutCell { StretchY = 0 },
				MinHeight = TimelineMetrics.DefaultRowHeight,
				Presenter = new SyncDelegatePresenter<Widget>(Render),
			};
			OverviewWidget.Components.Add(new AwakeBehavior());
		}

		private void Render(Widget widget)
		{
			widget.PrepareRendererState();
			Renderer.DrawRect(Vector2.Zero, widget.ContentSize, ColorTheme.Current.TimelineGrid.PropertyRowBackground);
			var colorIndex = PropertyAttributes<TangerineKeyframeColorAttribute>.Get(
					animator.Animable.GetType(), animator.TargetPropertyPath
				)?.ColorIndex
				?? 0;
			var color = KeyframePalette.Colors[colorIndex];
			for (int i = 0; i < animator.ReadonlyKeys.Count; i++) {
				var key = animator.ReadonlyKeys[i];
				var a = new Vector2(key.Frame * TimelineMetrics.ColWidth + 1, 0);
				var b = a + new Vector2(TimelineMetrics.ColWidth - 1, widget.Height);
				KeyframeFigure.Render(a, b, color, key.Function);
			}
		}
	}
}
