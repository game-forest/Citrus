using System.Collections.Generic;
using System.Linq;
using Tangerine.Core;
using Lime;
using Tangerine.UI.SceneView.PolygonMesh;

namespace Tangerine.UI.SceneView
{
	public class PolygonMeshProcessor : ITaskProvider
	{
		private static SceneView sv => SceneView.Instance;

		public IEnumerator<object> Task()
		{
			yield return null;
			while (true) {
				if (Document.Current.ExpositionMode || Document.Current.PreviewAnimation) {
					yield return null;
					continue;
				}
				if (!SceneView.Instance.InputArea.IsMouseOverThisOrDescendant()) {
					yield return null;
					continue;
				}
				var meshes = Document.Current.SelectedNodes().Editable().OfType<Lime.PolygonMesh.PolygonMesh>().ToList();
				foreach (var mesh in meshes) {
					if (mesh.Controller().HitTest(sv.Scene, sv.MousePosition, sv.Scene.Scale.X)) {
						Utils.ChangeCursorIfDefault(MouseCursor.Hand);
						if (SceneView.Instance.Input.ConsumeKeyPress(Key.Mouse0)) {
							switch (mesh.Controller().State) {
								case PolygonMeshController.ModificationState.Animation:
									yield return mesh.Controller().AnimationTask();
									break;
								case PolygonMeshController.ModificationState.Triangulation:
									yield return mesh.Controller().TriangulationTask();
									break;
								case PolygonMeshController.ModificationState.Creation:
									yield return mesh.Controller().CreationTask();
									break;
								case PolygonMeshController.ModificationState.Removal:
									yield return mesh.Controller().RemovalTask();
									break;
								case PolygonMeshController.ModificationState.Concave:
									yield return mesh.Controller().ConcaveTask();
									break;
							}
						}
					}
				}
				yield return null;
			}
		}
	}
}
