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
				if (Document.Current.ExpositionMode || Document.Current.PreviewAnimation) {
					yield return null;
					continue;
				}
				if (!SceneView.Instance.InputArea.IsMouseOverThisOrDescendant()) {
					yield return null;
					continue;
				}
				var meshes = Document.Current.SelectedNodes().Editable().OfType<PolygonMesh>().ToList();
				if (meshes.Count != 1) {
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
					case PolygonMesh.State.Deform:
						if (target is TangerineVertex) {
							Utils.ChangeCursorIfDefault(MouseCursor.Hand);
							if (SceneView.Instance.Input.ConsumeKeyPress(Key.Mouse0)) {
								yield return Deform(target);
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
						if (target is TangerineVertex) {
							Utils.ChangeCursorIfDefault(MouseCursor.Hand);
							if (SceneView.Instance.Input.ConsumeKeyPress(Key.Mouse0)) {
								yield return Remove(target);
							}
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
					if (SceneView.Instance.Input.IsKeyPressed(Key.Control)) {
						obj.MoveUv(uvDelta);
					} else {
						obj.Move(positionDelta);
					}
					Core.Operations.SetAnimableProperty.Perform(
						mesh,
						$"{nameof(PolygonMesh.Vertices)}",
						new List<Vertex>(mesh.Vertices),
						createAnimatorIfNeeded: true,
						createInitialKeyframeForNewAnimator: true
					);
					yield return null;
				}
				mesh.Animators.Invalidate();
				Document.Current.History.CommitTransaction();
			}
			yield return null;
		}

		private IEnumerator<object> Deform(ITangerineGeometryPrimitive obj)
		{
			var transform = SceneView.Instance.Scene.CalcTransitionToSpaceOf(mesh);
			var mousePos = SceneView.Instance.MousePosition * transform;
			var cursor = WidgetContext.Current.MouseCursor;
			using (Document.Current.History.BeginTransaction()) {
				var newMousePos = SceneView.Instance.MousePosition * transform;
				var positionDelta = newMousePos - mousePos;
				var uvDelta = positionDelta / mesh.Size;
				while (SceneView.Instance.Input.IsMousePressed()) {
					Utils.ChangeCursorIfDefault(cursor);
					newMousePos = SceneView.Instance.MousePosition * transform;
					positionDelta = newMousePos - mousePos;
					uvDelta = positionDelta / mesh.Size;
					mousePos = newMousePos;
					Core.Operations.PolygonMeshModification.Deform.Perform(mesh, positionDelta, uvDelta, obj.VerticeIndices[0]);
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
				var animatedPos = mousePos;
				var deformedPos = mousePos;
				switch (obj) {
					case TangerineEdge te:
						var w1 =
							PolygonMeshUtils.CalcSegmentRelativeBarycentricCoordinates(
								deformedPos,
								mesh.Geometry.Vertices[obj.VerticeIndices[0]].Pos,
								mesh.Geometry.Vertices[obj.VerticeIndices[1]].Pos
							);
						animatedPos =
							w1[0] * mesh.Vertices[obj.VerticeIndices[0]].Pos +
							w1[1] * mesh.Vertices[obj.VerticeIndices[1]].Pos;
						break;
					case TangerineFace tf:
						var w2 =
							PolygonMeshUtils.CalcTriangleRelativeBarycentricCoordinates(
								deformedPos,
								mesh.Geometry.Vertices[obj.VerticeIndices[0]].Pos,
								mesh.Geometry.Vertices[obj.VerticeIndices[1]].Pos,
								mesh.Geometry.Vertices[obj.VerticeIndices[2]].Pos
							);
						animatedPos =
							w2[0] * mesh.Vertices[obj.VerticeIndices[0]].Pos +
							w2[1] * mesh.Vertices[obj.VerticeIndices[1]].Pos +
							w2[2] * mesh.Vertices[obj.VerticeIndices[2]].Pos;
						break;
				}
				var animatedVertex = new Vertex() { Pos = animatedPos, UV1 = obj.InterpolateUv(mousePos), Color = mesh.GlobalColor };
				var deformedVertex = new Vertex() { Pos = deformedPos, UV1 = obj.InterpolateUv(mousePos), Color = mesh.GlobalColor };
				Core.Operations.PolygonMeshModification.Create.Perform(mesh, animatedVertex, deformedVertex);
				Document.Current.History.CommitTransaction();
			}
			Window.Current.Invalidate();
			yield return null;
		}

		private IEnumerator<object> Remove(ITangerineGeometryPrimitive obj)
		{
			if (mesh.Geometry.Vertices.Count == 4) {
				new AlertDialog("Mesh can't contain less than 3 vertices", "Continue").Show();
				yield return null;
			} else {
				using (Document.Current.History.BeginTransaction()) { 
					Core.Operations.PolygonMeshModification.Remove.Perform(
						mesh,
						mesh.Vertices[obj.VerticeIndices[0]],
						mesh.Geometry.Vertices[obj.VerticeIndices[0]],
						obj.VerticeIndices[0]
					);
					Document.Current.History.CommitTransaction();
				}
				Window.Current.Invalidate();
				yield return null;
			}
		}
	}
}
