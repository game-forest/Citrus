using Lime;
using System.Collections.Generic;
using System.Linq;
using Tangerine.Core;
using Lime.PolygonMesh;

namespace Tangerine.UI.SceneView
{
	public class PolygonMeshEditingProcessor : ITaskProvider
	{
		private PolygonMesh mesh = null;
		private Matrix32 meshToSceneWidgetTransform = Matrix32.Identity;

		public IEnumerator<object> Task()
		{
			while (true) {
				if (!SceneView.Instance.InputArea.IsMouseOverThisOrDescendant()) {
					yield return null;
					continue;
				}
				var meshes = Document.Current.SelectedNodes().Editable().OfType<PolygonMesh>().ToList();
				if (meshes.Count != 1) {
					yield return null;
					continue;
				}
				if (meshes[0].CurrentState == PolygonMesh.State.Display) {
					yield return null;
					continue;
				}
				mesh = meshes[0];
				meshToSceneWidgetTransform = mesh.CalcTransitionToSpaceOf(SceneView.Instance.Scene);
				ITangerineGeometryPrimitive target = null;
				foreach (var primitive in PolygonMesh.Primitives) {
					foreach (var obj in mesh.Geometry[primitive]) {
						var hitTest = obj.HitTest(SceneView.Instance.MousePosition, meshToSceneWidgetTransform, radius: 4.0f, scale: SceneView.Instance.Scene.Scale.X);
						if (hitTest) {
							target = obj;
							goto skip;
						}
					}
				}
				yield return null;
				continue;
				skip:
				switch (mesh.CurrentState) {
					case PolygonMesh.State.Modify:
						Utils.ChangeCursorIfDefault(MouseCursor.Hand);
						if (SceneView.Instance.Input.ConsumeKeyPress(Key.Mouse0)) {
							yield return Modify(target);
						}
						break;
					case PolygonMesh.State.Create:
						Utils.ChangeCursorIfDefault(MouseCursor.Hand);
						if (SceneView.Instance.Input.ConsumeKeyPress(Key.Mouse0)) {
							yield return Create(target);
						}
						break;
					case PolygonMesh.State.Remove:
						Utils.ChangeCursorIfDefault(MouseCursor.Hand);
						if (SceneView.Instance.Input.ConsumeKeyPress(Key.Mouse0)) {
							yield return Remove(target);
						}
						break;
				}
				yield return null;
			}
		}

		private IEnumerator<object> Modify(ITangerineGeometryPrimitive obj)
		{
			var transform = SceneView.Instance.Scene.CalcTransitionToSpaceOf(mesh);
			var mousePos = SceneView.Instance.MousePosition * transform;
			var cursor = WidgetContext.Current.MouseCursor;
			using (Document.Current.History.BeginTransaction()) {
				while (SceneView.Instance.Input.IsMousePressed()) {
					Utils.ChangeCursorIfDefault(cursor);
					obj.Move(SceneView.Instance.MousePosition * transform - mousePos);
					mousePos = SceneView.Instance.MousePosition * transform;
					yield return null;
				}
				Document.Current.History.CommitTransaction();
			}
			yield return null;
		}

		private IEnumerator<object> Create(ITangerineGeometryPrimitive obj)
		{
			if (!(obj is TangerineFace)) {
				yield return null;
			}
			var transform = SceneView.Instance.Scene.CalcTransitionToSpaceOf(mesh);
			var mousePos = SceneView.Instance.MousePosition * transform;
			var cursor = WidgetContext.Current.MouseCursor;
			mesh.Geometry.AddVertex(new Vertex() { Pos = mousePos * transform });
			yield return null;
		}

		private IEnumerator<object> Remove(ITangerineGeometryPrimitive obj)
		{
			yield return null;
		}
	}
}
