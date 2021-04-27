using System;
using Lime;

namespace Tangerine.UI.Timeline.Components
{
	public sealed class RowView : Component
	{
		private IGridRowView gridRowView;
		public bool IsGridRowViewCreated => gridRowView != null;
		public IGridRowView GridRowView => gridRowView ?? (gridRowView = GridRowViewFactory());
		public Func<IGridRowView> GridRowViewFactory { get; set; }
	}

	public interface IGridRowView
	{
		Widget GridWidget { get; }
		Widget OverviewWidget { get; }
		AwakeBehavior GridWidgetAwakeBehavior { get; }
		AwakeBehavior OverviewWidgetAwakeBehavior { get; }
	}
}
