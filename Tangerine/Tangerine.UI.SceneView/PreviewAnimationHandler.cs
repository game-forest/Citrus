using Lime;
using Tangerine.Core;

namespace Tangerine.UI.SceneView
{
	public enum PreviewAnimationBehaviour
	{
		Inherited,
		StopOnCurrentFrame,
		StopOnStartingFrame,
	}

	public class PreviewAnimationHandler : DocumentCommandHandler
	{
		private PreviewAnimationBehaviour behaviour;

		public PreviewAnimationHandler(PreviewAnimationBehaviour behaviour)
		{
			this.behaviour = behaviour;
		}

		public override void ExecuteTransaction()
		{
			if (behaviour != PreviewAnimationBehaviour.Inherited) {
				CoreUserPreferences.Instance.StopAnimationOnCurrentFrame =
					behaviour == PreviewAnimationBehaviour.StopOnCurrentFrame;
			}
			Document.Current.TogglePreviewAnimation();
		}
	}
}
