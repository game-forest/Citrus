using Lime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Tangerine.Core;
using Lime.PolygonMesh;
using Lime.PolygonMesh.Structure;

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
			foreach (var type in PolygonMesh.Geometry.StructureObjectsTypesArray.Reverse()) {
				foreach (var obj in mesh.Structure[type]) {
					var hitTest = obj.Transform(meshToSceneWidgetTransform).HitTest(mousePos, SceneView.Instance.Scene.Scale.X);
					obj.InversionTransform();
					var shouldHighlight = hitTest && !wasHighlighted;
					if (type == PolygonMesh.Geometry.StructureObjectsTypes.Face && !shouldHighlight) {
						continue;
					}
					obj.Transform(meshToSceneFrameTransform).Render(
						shouldHighlight ?
						Color4.Orange.Transparentify(0.2f) :
						Color4.Green.Lighten(0.2f)
					);
					obj.InversionTransform();
					if (shouldHighlight) {
						wasHighlighted = true;
					}
				}
			}
		}
	}
}
