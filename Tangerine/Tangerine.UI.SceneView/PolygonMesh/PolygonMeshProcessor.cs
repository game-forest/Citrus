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

				var meshes = Document.Current.SelectedNodes().Editable().OfType<Lime.Widgets.PolygonMesh.PolygonMesh>().ToList();

				/// Skinning bootstrap
				var bones = Document.Current.SelectedNodes().Editable().OfType<Bone>().ToList();
				if (PolygonMeshTools.State == PolygonMeshTools.ModificationState.Animation) {
					foreach (var mesh in meshes) {
						if (SceneView.Instance.Input.IsKeyPressed(Key.Control)) {
							if (SceneView.Instance.Input.ConsumeKeyPress(Key.Mouse0)) {
								mesh.Controller().TieVertexWithBones(bones);
							} else if (SceneView.Instance.Input.ConsumeKeyPress(Key.Mouse1)) {
								mesh.Controller().UntieVertexFromBones(bones);
							}
						}
					}
				}
				/// Skinning bootstrap

				foreach (var mesh in meshes) {
					if (mesh.Controller().HitTest(sv.MousePosition, sv.Scene.Scale.X)) {
						Utils.ChangeCursorIfDefault(MouseCursor.Hand);
						if (SceneView.Instance.Input.ConsumeKeyPress(Key.Mouse0) && !SceneView.Instance.Input.IsKeyPressed(Key.Control)) {
							switch (PolygonMeshTools.State) {
								case PolygonMeshTools.ModificationState.Animation:
									yield return mesh.Controller().AnimationTask();
									break;
								case PolygonMeshTools.ModificationState.Triangulation:
									yield return mesh.Controller().TriangulationTask();
									break;
								case PolygonMeshTools.ModificationState.Creation:
									yield return mesh.Controller().CreationTask();
									break;
								case PolygonMeshTools.ModificationState.Removal:
									yield return mesh.Controller().RemovalTask();
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
