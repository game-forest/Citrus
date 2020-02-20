using Lime;
using System.Linq;
using Tangerine.Core;
using Tangerine.UI.SceneView.PolygonMesh;

namespace Tangerine.UI.SceneView
{
	public class PolygonMeshPresenter
	{
		private static bool wasAnimationForced;

		public PolygonMeshPresenter(SceneView sceneView)
		{
			sceneView.Frame.CompoundPostPresenter.Add(new SyncDelegatePresenter<Widget>(Render));
		}

		private static void Render(Widget canvas)
		{
			var meshes = Document.Current.SelectedNodes().Editable().OfType<Lime.Widgets.PolygonMesh.PolygonMesh>().ToList();
			if (meshes.Count == 0) {
				return;
			}
			if (Document.Current.ExpositionMode || Document.Current.PreviewAnimation) {
				if (!wasAnimationForced) {
					wasAnimationForced = true;
					PolygonMeshTools.ControllerStateBeforeAnimationPreview = meshes[0].Controller().State;
					PolygonMeshTools.ChangeState(PolygonMeshController.ModificationState.Animation);
				}
				return;
			}
			if (wasAnimationForced) {
				wasAnimationForced = false;
				PolygonMeshTools.ChangeState(PolygonMeshTools.ControllerStateBeforeAnimationPreview);
			}
			canvas.PrepareRendererState();
			foreach (var mesh in meshes) {
				mesh.Controller().Render(canvas);
			}
		}
	}
}
