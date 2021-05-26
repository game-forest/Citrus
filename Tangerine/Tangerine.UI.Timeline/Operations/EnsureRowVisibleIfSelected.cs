using System.Linq;
using Tangerine.Core;
using Tangerine.Core.Components;

namespace Tangerine.UI.Timeline
{
	public class EnsureRowVisibleIfSelected : OperationProcessor<Core.Operations.SetProperty>
	{
		protected override void InternalRedo(Core.Operations.SetProperty op)
		{
			// To allow click on EnterButton when row is partly visible.
			if (Timeline.Instance.RootWidget.Input.IsMousePressed())
				return;
			if (op.Obj is TimelineItemStateComponent s && op.Property.Name == nameof(TimelineItemStateComponent.Selected) && (bool)op.Value) {
				var timeline = Timeline.Instance;
				var item = Document.Current.Rows.FirstOrDefault(i => i.GetTimelineItemState() == s);
				timeline.EnsureRowVisible(item);
			}
		}

		protected override void InternalUndo(Core.Operations.SetProperty op) { }
	}
}
