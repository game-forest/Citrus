using Lime;
using System.Linq;
using Tangerine.Core;
using Tangerine.UI.SceneView.Animesh;

namespace Tangerine.UI.SceneView
{
	public class AnimeshPresenter
	{
		private static bool wasAnimationForced;

		public AnimeshPresenter(SceneView sceneView)
		{
			sceneView.Frame.CompoundPostPresenter.Add(new SyncDelegatePresenter<Widget>(Render));
		}

		private static void Render(Widget canvas)
		{
			Document.Current.Nodes().OfType<Lime.Widgets.Animesh.Animesh>().ToList().ForEach(m => m.Controller());
			if (Document.Current.ExpositionMode || Document.Current.PreviewAnimation) {
				if (!wasAnimationForced) {
					wasAnimationForced = true;
					AnimeshTools.StateBeforeAnimationPreview = AnimeshTools.State;
					AnimeshTools.State = AnimeshTools.ModificationState.Animation;
				}
				return;
			}
			if (wasAnimationForced) {
				wasAnimationForced = false;
				AnimeshTools.State = AnimeshTools.StateBeforeAnimationPreview;
			}
			var meshes = Document.Current.SelectedNodes().Editable().OfType<Lime.Widgets.Animesh.Animesh>().ToList();
			if (meshes.Count == 0) {
				return;
			}
			canvas.PrepareRendererState();
			foreach (var mesh in meshes) {
				mesh.Controller().Render(canvas);
			}
		}
	}
}
