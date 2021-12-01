using System.Linq;
using Tangerine.Core;
using Tangerine.Core.Components;

namespace Tangerine.UI.Timeline
{
	public class EnsureSceneItemVisibleIfSelected : OperationProcessor<Core.Operations.SetProperty>
	{
		protected override void InternalRedo(Core.Operations.SetProperty op)
		{
			// To allow click on EnterButton when scene item is partly visible.
			if (Timeline.Instance.RootWidget.Input.IsMousePressed()) {
				return;
			}
			if (
				op.Obj is TimelineSceneItemStateComponent s
				&& op.Property.Name == nameof(TimelineSceneItemStateComponent.Selected)
				&& (bool)op.Value
			) {
				var timeline = Timeline.Instance;
				var item = Document.Current.VisibleSceneItems.FirstOrDefault(i => i.GetTimelineSceneItemState() == s);
				if (item != null) {
					timeline.EnsureSceneItemVisible(item);
				}
			}
		}

		protected override void InternalUndo(Core.Operations.SetProperty op) { }
	}
}
