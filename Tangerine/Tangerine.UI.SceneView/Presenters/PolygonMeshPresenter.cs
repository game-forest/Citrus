using Lime;
using System.Linq;
using Tangerine.Core;
using Lime.PolygonMesh;
using System.Collections;
using System.Collections.Generic;

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
			var defaultColor = Color4.Green.Lighten(0.2f);
			var hoverColor =
				mesh.CurrentState == PolygonMesh.State.Remove ?
				Color4.Red.Transparentify(0.1f) :
				Color4.Orange.Transparentify(0.2f);
			var meshToSceneFrameTransform = mesh.CalcTransitionToSpaceOf(SceneView.Instance.Frame);
			var meshToSceneWidgetTransform = mesh.CalcTransitionToSpaceOf(SceneView.Instance.Scene);
			var renderQueue = new Queue<(ITangerineGeometryPrimitive Primitive, Color4 Color)>();
			SceneView.Instance.Frame.PrepareRendererState();
			mesh.HitTest(
				SceneView.Instance.MousePosition,
				meshToSceneWidgetTransform,
				out var primaryHitTestTarget,
				SceneView.Instance.Scene.Scale.X
			);
			var hitTestTargets = new HashSet<ITangerineGeometryPrimitive>();
			if (mesh.CurrentState == PolygonMesh.State.Modify && !(primaryHitTestTarget is TangerineVertex)) {
				goto render;
			}
			if (primaryHitTestTarget != null) {
				hitTestTargets.Add(primaryHitTestTarget);
				if (primaryHitTestTarget is TangerineFace) {
					renderQueue.Enqueue((primaryHitTestTarget, hoverColor));
				}
				if (mesh.CurrentState == PolygonMesh.State.Remove) {
					hitTestTargets.UnionWith(primaryHitTestTarget.GetAdjacent());
				}
			}

			render:
			foreach (var primitive in new[] { GeometryPrimitive.Edge, GeometryPrimitive.Vertex }) {
				foreach (var obj in mesh.Geometry[primitive]) {
					renderQueue.Enqueue((obj, hitTestTargets.Contains(obj) ? hoverColor : defaultColor));
				}
			}
			foreach (var (Primitive, Color) in renderQueue) {
				Primitive.Render(meshToSceneFrameTransform, Color, radius: 4.0f);
			}
		}
	}
}
