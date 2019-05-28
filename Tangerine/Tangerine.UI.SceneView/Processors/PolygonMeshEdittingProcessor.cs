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
	public class PolygonMeshEdittingProcessor : ITaskProvider
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
				IPolygonMeshStructureObject target = null;
				foreach (var type in PolygonMesh.Geometry.StructureObjectsTypesArray) {
					foreach (var obj in mesh.Structure[type]) {
						var hitTest = obj.Transform(meshToSceneWidgetTransform).HitTest(SceneView.Instance.MousePosition, SceneView.Instance.Scene.Scale.X);
						obj.InversionTransform();
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
						Utils.ChangeCursorIfDefault(Cursors.DragHandOpen);
						if (SceneView.Instance.Input.ConsumeKeyPress(Key.Mouse0)) {
							yield return Create();
						}
						break;
					case PolygonMesh.State.Remove:
						Utils.ChangeCursorIfDefault(Cursors.DragHandClosed);
						if (SceneView.Instance.Input.ConsumeKeyPress(Key.Mouse0)) {
							yield return Remove(target);
						}
						break;
				}
				yield return null;
			}
		}

		private IEnumerator<object> Modify(IPolygonMeshStructureObject obj)
		{
			var transform = SceneView.Instance.Scene.CalcTransitionToSpaceOf(mesh);
			var mousePos = SceneView.Instance.MousePosition * transform;
			var cursor = WidgetContext.Current.MouseCursor;
			using (Document.Current.History.BeginTransaction()) {
				while (SceneView.Instance.Input.IsMousePressed()) {
					// TO DO: Animator
					Utils.ChangeCursorIfDefault(cursor);
					obj.Move(mousePos, SceneView.Instance.MousePosition * transform, SceneView.Instance.Input.IsKeyPressed(Key.Control));
					mousePos = SceneView.Instance.MousePosition * transform;
					yield return null;
				}
				Document.Current.History.CommitTransaction();
			}
			yield return null;
		}

		private IEnumerator<object> Create()
		{
			yield return null;
		}

		private IEnumerator<object> Remove(IPolygonMeshStructureObject obj)
		{
			yield return null;
		}
	}
}
