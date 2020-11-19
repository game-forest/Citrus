using Tangerine.Core;

namespace Tangerine.UI.Timeline
{
	public class EnsureRowVisibleIfSelected : OperationProcessor<Core.Operations.SetProperty>
	{
		protected override void InternalRedo(Core.Operations.SetProperty op)
		{
			// To allow click on EnterButton when row is partly visible.
			if (Timeline.Instance.RootWidget.Input.IsMousePressed())
				return;
			if (op.Obj is Row sceneItem && op.Property.Name == nameof(Row.Selected) && (bool)op.Value) {
				var timeline = Timeline.Instance;
				timeline.EnsureRowVisible(sceneItem);
			}
		}

		protected override void InternalUndo(Core.Operations.SetProperty op) { }
	}
}
