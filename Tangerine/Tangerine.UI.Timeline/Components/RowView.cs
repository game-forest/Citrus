using Lime;

namespace Tangerine.UI.Timeline.Components
{
	public sealed class RowView : Component
	{
		public IGridRowView GridRow;
	}

	public interface IGridRowView
	{
		Widget GridWidget { get; }
		Widget OverviewWidget { get; }
		AwakeBehavior GridWidgetAwakeBehavior { get; }
		AwakeBehavior OverviewWidgetAwakeBehavior { get; }
	}
}
