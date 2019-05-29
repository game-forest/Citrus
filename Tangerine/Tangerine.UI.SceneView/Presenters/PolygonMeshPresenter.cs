using Lime;
using System.Linq;
using Tangerine.Core;
using Lime.PolygonMesh;

namespace Tangerine.UI.SceneView
{
	public class PolygonMeshStructurePresenter
	{
		public PolygonMeshStructurePresenter(SceneView sceneView)
		{
			sceneView.Frame.CompoundPostPresenter.Add(new SyncDelegatePresenter<Widget>(Render));
		}

		private static void Render(Widget canvas)
		{
			if (Document.Current.ExpositionMode || Document.Current.PreviewAnimation) {
				return;
			}
			var selected = Document.Current.SelectedNodes().Editable();
			var meshes = selected.OfType<PolygonMesh>().ToList();
			if (meshes.Count != 1 || meshes.Count != selected.Count()) {
				return;
			}
			var mesh = meshes[0];
			if (mesh.CurrentState == PolygonMesh.State.Display) {
				return;
			}
			var wasHighlighted = false;
			var meshToSceneFrameTransform = mesh.CalcTransitionToSpaceOf(SceneView.Instance.Frame);
			var meshToSceneWidgetTransform = mesh.CalcTransitionToSpaceOf(SceneView.Instance.Scene);
			var mousePos = SceneView.Instance.MousePosition;
			SceneView.Instance.Frame.PrepareRendererState();
			foreach (var primitive in PolygonMesh.Primitives.Reverse()) {
				foreach (var obj in mesh.Geometry[primitive]) {
					var hitTest = obj.HitTest(mousePos, meshToSceneWidgetTransform, radius: 4.0f, scale: SceneView.Instance.Scene.Scale.X);
					var shouldHighlight = hitTest && !wasHighlighted;
					if (primitive == GeometryPrimitive.Face && !shouldHighlight) {
						continue;
					}
					obj.Render(
						meshToSceneFrameTransform,
						shouldHighlight ?
						Color4.Orange.Transparentify(0.2f) :
						Color4.Green.Lighten(0.2f),
						radius: 4.0f
					);
					if (shouldHighlight) {
						wasHighlighted = true;
					}
				}
			}
		}
	}
}
