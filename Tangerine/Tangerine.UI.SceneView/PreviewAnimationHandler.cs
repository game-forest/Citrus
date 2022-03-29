using Lime;
using Tangerine.Core;

namespace Tangerine.UI.SceneView
{
	public enum PreviewAnimationBehavior
	{
		Inherited,
		StopOnCurrentFrame,
		StopOnStartingFrame,
	}

	public class PreviewAnimationHandler : DocumentCommandHandler
	{
		private PreviewAnimationBehavior behavior;

		public PreviewAnimationHandler(PreviewAnimationBehavior behavior)
		{
			this.behavior = behavior;
		}

		public override void ExecuteTransaction()
		{
			if (behavior != PreviewAnimationBehavior.Inherited) {
				CoreUserPreferences.Instance.StopAnimationOnCurrentFrame =
					behavior == PreviewAnimationBehavior.StopOnCurrentFrame;
			}
			Document.Current.TogglePreviewAnimation();
		}
	}
}
