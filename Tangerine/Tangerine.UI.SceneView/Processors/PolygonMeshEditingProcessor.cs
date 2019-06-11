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
				if (
					!mesh.HitTest(
						SceneView.Instance.MousePosition,
						mesh.CalcTransitionToSpaceOf(SceneView.Instance.Scene),
						out var target,
						SceneView.Instance.Scene.Scale.X
					)
				) {
					yield return null;
					continue;
				}
				switch (mesh.CurrentState) {
					case PolygonMesh.State.Animate:
						Utils.ChangeCursorIfDefault(MouseCursor.Hand);
						if (SceneView.Instance.Input.ConsumeKeyPress(Key.Mouse0)) {
							yield return Animate(target);
						}
						break;
					case PolygonMesh.State.Modify:
						if (target is TangerineVertex) {
							Utils.ChangeCursorIfDefault(MouseCursor.Hand);
							if (SceneView.Instance.Input.ConsumeKeyPress(Key.Mouse0)) {
								yield return Modify(target);
							}
						}
						break;
					case PolygonMesh.State.Create:
						if (!(target is TangerineVertex)) {
							Utils.ChangeCursorIfDefault(MouseCursor.Hand);
							if (SceneView.Instance.Input.ConsumeKeyPress(Key.Mouse0)) {
								yield return Create(target);
							}
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

		private IEnumerator<object> Animate(ITangerineGeometryPrimitive obj)
		{
			var transform = SceneView.Instance.Scene.CalcTransitionToSpaceOf(mesh);
			var mousePos = SceneView.Instance.MousePosition * transform;
			var cursor = WidgetContext.Current.MouseCursor;
			using (Document.Current.History.BeginTransaction()) {
				while (SceneView.Instance.Input.IsMousePressed()) {
					Utils.ChangeCursorIfDefault(cursor);
					var newMousePos = SceneView.Instance.MousePosition * transform;
					var positionDelta = newMousePos - mousePos;
					var uvDelta = positionDelta / mesh.Size;
					mousePos = newMousePos;
					var isCtrlPressed = SceneView.Instance.Input.IsKeyPressed(Key.Control);
					var isAltPressed = SceneView.Instance.Input.IsKeyPressed(Key.Alt);
					if (isCtrlPressed) {
						obj.MoveUv(uvDelta);
					} else {
						obj.Move(positionDelta);
					}
					yield return null;
				}
				Document.Current.History.CommitTransaction();
			}
			yield return null;
		}

		private IEnumerator<object> Modify(ITangerineGeometryPrimitive obj)
		{
			var transform = SceneView.Instance.Scene.CalcTransitionToSpaceOf(mesh);
			var mousePos = SceneView.Instance.MousePosition * transform;
			var cursor = WidgetContext.Current.MouseCursor;
			using (Document.Current.History.BeginTransaction()) {
				while (SceneView.Instance.Input.IsMousePressed()) {
					Utils.ChangeCursorIfDefault(cursor);
					var newMousePos = SceneView.Instance.MousePosition * transform;
					var positionDelta = newMousePos - mousePos;
					var uvDelta = positionDelta / mesh.Size;
					mousePos = newMousePos;
					var uvOffset =
						mesh.Geometry.Vertices[obj.VerticeIndices[0]].UV1 -
						mesh.Geometry.Vertices[obj.VerticeIndices[0]].Pos / mesh.Size;
					var isCtrlPressed = SceneView.Instance.Input.IsKeyPressed(Key.Control);
					var isAltPressed = SceneView.Instance.Input.IsKeyPressed(Key.Alt);
					if (isCtrlPressed) {
						obj.MoveUv(uvDelta);
					} else {
						var i = obj.VerticeIndices[0];
						mesh.Geometry.MoveVertex(i, positionDelta);
						var v = mesh.Geometry.Vertices[i];
						v.UV1 = uvOffset + v.Pos / mesh.Size;
						mesh.Geometry.Vertices[i] = v;
					}
					yield return null;
				}
				Document.Current.History.CommitTransaction();
			}
			yield return null;
		}

		private IEnumerator<object> Create(ITangerineGeometryPrimitive obj)
		{
			var transform = SceneView.Instance.Scene.CalcTransitionToSpaceOf(mesh);
			var mousePos = SceneView.Instance.MousePosition * transform;
			if (obj is TangerineEdge) {
				var v1 = mesh.Geometry.Vertices[obj.VerticeIndices[0]];
				var v2 = mesh.Geometry.Vertices[obj.VerticeIndices[1]];
				mousePos = PolygonMeshUtils.PointProjectionToLine(mousePos, v1.Pos, v2.Pos, out var isInside);
			}
			var cursor = WidgetContext.Current.MouseCursor;
			using (Document.Current.History.BeginTransaction()) {
				mesh.Geometry.AddVertex(new Vertex() { Pos = mousePos, UV1 = obj.InterpolateUv(mousePos), Color = mesh.GlobalColor });
				Document.Current.History.CommitTransaction();
			}
			Window.Current.Invalidate();
			yield return null;
		}

		private IEnumerator<object> Remove(ITangerineGeometryPrimitive obj)
		{
			var targets = obj.GetAdjacent().Where(v => v is TangerineVertex).ToList();
			if (mesh.Geometry.Vertices.Count - targets.Count - 1 < 3) {
				new AlertDialog("Mesh can't contain less than 3 vertices", "Continue").Show();
				yield return null;
			} else {
				for (var i = 0; i < obj.VerticeIndices.Length; ++i) {
					var current = obj.VerticeIndices[i];
					if (current < mesh.Geometry.Vertices.Count - 1) {
						for (var j = i + 1; j < obj.VerticeIndices.Length; ++j) {
							if (obj.VerticeIndices[j] == mesh.Geometry.Vertices.Count - 1) {
								obj.VerticeIndices[j] = current;
							}
						}
					}
					mesh.Geometry.RemoveVertex(current);
				}
				Window.Current.Invalidate();
				yield return null;
			}
		}
	}
}
