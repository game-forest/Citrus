using System.Linq;
using Lime;
using Tangerine.Core;

namespace Tangerine.UI.AnimeshEditor
{
	public class AnimeshPresenter
	{
		private static bool wasAnimationForced;

		public AnimeshPresenter(ISceneView sceneView)
		{
			sceneView.Frame.CompoundPostPresenter.Add(
				new SyncDelegatePresenter<Widget>(widget => Render(widget, sceneView))
			);
		}

		private static void Render(Widget canvas, ISceneView sv)
		{
			Document.Current.Nodes().OfType<Lime.Animesh>().ToList().ForEach(m => m.Controller());
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
			var meshes = Document.Current.SelectedNodes().Editable().OfType<Lime.Animesh>().ToList();
			if (meshes.Count == 0) {
				return;
			}
			if (AnimeshTools.State != AnimeshTools.ModificationState.Transformation) {
				canvas.PrepareRendererState();
				foreach (var mesh in meshes) {
					mesh.Controller(sv).Render(canvas);
				}
			}
		}
	}
}
