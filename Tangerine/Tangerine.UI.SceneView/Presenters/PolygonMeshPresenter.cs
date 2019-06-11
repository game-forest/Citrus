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
			var defaultColor = Color4.Green.Lighten(0.2f);
			var hoverColor =
				mesh.CurrentState == PolygonMesh.State.Remove ?
				Color4.Red.Lighten(0.2f) :
				Color4.Orange.Lighten(0.2f);
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
			if (mesh.CurrentState == PolygonMesh.State.Deform && !(primaryHitTestTarget is TangerineVertex)) {
				goto render;
			}
			if (mesh.CurrentState == PolygonMesh.State.Create && primaryHitTestTarget is TangerineVertex) {
				goto render;
			}
			if (mesh.CurrentState == PolygonMesh.State.Remove && !(primaryHitTestTarget is TangerineVertex)) {
				goto render;
			}
			if (primaryHitTestTarget != null) {
				hitTestTargets.Add(primaryHitTestTarget);
				if (primaryHitTestTarget is TangerineFace) {
					renderQueue.Enqueue((primaryHitTestTarget, hoverColor));
				}
				if (mesh.CurrentState == PolygonMesh.State.Remove && primaryHitTestTarget is TangerineVertex) {
					hitTestTargets.UnionWith(primaryHitTestTarget.GetAdjacent());
				}
			}

		render:
			foreach (var primitive in new[] { GeometryPrimitive.Edge, GeometryPrimitive.Vertex }) {
				foreach (var obj in mesh.Geometry[primitive]) {
					renderQueue.Enqueue((obj, hitTestTargets.Contains(obj) ? hoverColor : defaultColor));
				}
			}
			if (mesh.CurrentState == PolygonMesh.State.Create && hitTestTargets.Count > 0) {
				var pos = SceneView.Instance.MousePosition * SceneView.Instance.Scene.CalcTransitionToSpaceOf(mesh);
				if (primaryHitTestTarget is TangerineEdge) {
					var v1 = mesh.Geometry.Vertices[primaryHitTestTarget.VerticeIndices[0]];
					var v2 = mesh.Geometry.Vertices[primaryHitTestTarget.VerticeIndices[1]];
					pos = PolygonMeshUtils.PointProjectionToLine(pos, v1.Pos, v2.Pos, out var isInside);
				}
				mesh.Geometry.Vertices.Add(new Vertex() {
					Pos = pos
				});
				var vertex = new TangerineVertex(mesh.Geometry, mesh.Geometry.Vertices.Count - 1);
				renderQueue.Enqueue((vertex, Color4.Yellow.Lighten(0.5f).Transparentify(0.5f)));
				foreach (var (Primitive, Color) in renderQueue) {
					Primitive.Render(meshToSceneFrameTransform, Color, radius: 4.0f);
				}
				mesh.Geometry.Vertices.RemoveAt(mesh.Geometry.Vertices.Count - 1);
			} else {
				foreach (var (Primitive, Color) in renderQueue) {
					Primitive.Render(meshToSceneFrameTransform, Color, radius: 4.0f);
				}
			}
#if DEBUG
			if (
				mesh.CurrentContext == PolygonMesh.Context.Deformation &&
				SceneView.Instance.Input.IsKeyPressed(Key.Alt)
			) {
				var g = (Geometry)mesh.Geometry;
				foreach (var he in g.HalfEdges) {
					var vPos = g.Vertices[he.Origin].Pos;
					var vNextPos = g.Vertices[g.Next(he).Origin].Pos;
					var offset = new Vector2(vPos.Y - vNextPos.Y, vNextPos.X - vPos.X).Normalized * 12.0f;
					var d = Vector2.Distance(vPos, vNextPos);
					var a = vPos + 0.2f * d * (vNextPos - vPos).Normalized + offset;
					var b = vNextPos + 0.2f * d * (vPos - vNextPos).Normalized + offset;
					var c = vNextPos + 0.25f * d * (vPos - vNextPos).Normalized + 1.5f * offset;
					Renderer.DrawTextLine(vPos * meshToSceneFrameTransform, $"V{he.Origin}", 27.0f, Color4.White, 0.0f);
					Renderer.DrawTextLine(vPos * meshToSceneFrameTransform, $"V{he.Origin}", 24.0f, Color4.Black, 0.0f);
					Renderer.DrawLine(a * meshToSceneFrameTransform, b * meshToSceneFrameTransform, Color4.Gray, 4.0f);
					Renderer.DrawLine(b * meshToSceneFrameTransform, c * meshToSceneFrameTransform, Color4.Gray, 4.0f);
					Renderer.DrawLine(a * meshToSceneFrameTransform, b * meshToSceneFrameTransform, Color4.Black, 2.0f);
					Renderer.DrawLine(b * meshToSceneFrameTransform, c * meshToSceneFrameTransform, Color4.Black, 2.0f);
					Renderer.DrawTextLine((a + b) / 2.0f * meshToSceneFrameTransform, $"{he.Index}", 21.0f, Color4.White, 0.0f);
					Renderer.DrawTextLine((a + b) / 2.0f * meshToSceneFrameTransform, $"{he.Index}", 18.0f, Color4.Black, 0.0f);
				}
			}
#endif
		}
	}
}
