using Lime;
using System.Linq;
using Tangerine.Core;
using Lime.PolygonMesh;
using System.Collections;
using System.Collections.Generic;
using System;

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
			SceneView.Instance.Frame.PrepareRendererState();
			FillRenderChain(meshes[0], out var renderChain);
			foreach (var action in renderChain) {
				action.Invoke();
			}
#if DEBUG
			DebugRender(meshes[0]);
#endif
		}

		private static void FillRenderChain(PolygonMesh mesh, out Queue<Action> renderChain)
		{
			renderChain = new Queue<Action>();
			var meshToSceneFrameTransform = mesh.CalcTransitionToSpaceOf(SceneView.Instance.Frame);
			var meshToSceneWidgetTransform = mesh.CalcTransitionToSpaceOf(SceneView.Instance.Scene);
			mesh.HitTest(
				SceneView.Instance.MousePosition,
				meshToSceneWidgetTransform,
				out var primaryHitTestTarget,
				SceneView.Instance.Scene.Scale.X
			);
			var hitTestTargets = new HashSet<ITangerineGeometryPrimitive> { primaryHitTestTarget };
			switch (mesh.CurrentState) {
				case PolygonMesh.State.Deform:
					if (!(primaryHitTestTarget is TangerineVertex)) {
						hitTestTargets.Clear();
					}
					break;
				case PolygonMesh.State.Create:
					if (primaryHitTestTarget is TangerineVertex) {
						hitTestTargets.Clear();
					} else if (primaryHitTestTarget is TangerineFace) {
						renderChain.Enqueue(() => primaryHitTestTarget.RenderHovered(meshToSceneFrameTransform));
					}
					break;
				case PolygonMesh.State.Remove:
					if (!(primaryHitTestTarget is TangerineVertex)) {
						hitTestTargets.Clear();
					} else {
						hitTestTargets.UnionWith(primaryHitTestTarget.GetAdjacent());
					}
					break;
				case PolygonMesh.State.Animate:
					if (primaryHitTestTarget is TangerineFace) {
						renderChain.Enqueue(() => primaryHitTestTarget.RenderHovered(meshToSceneFrameTransform));
					}
					break;
			}
			foreach (var primitive in PolygonMesh.PrimitivesRenderChain) {
				foreach (var obj in mesh.Geometry[primitive]) {
					renderChain.Enqueue(() => {
						if (hitTestTargets.Contains(obj)) {
							obj.RenderHovered(
								meshToSceneFrameTransform,
								mesh.CurrentState == PolygonMesh.State.Remove
							);
						} else {
							obj.Render(meshToSceneFrameTransform);
						}
					});
				}
			}
			if (mesh.CurrentState == PolygonMesh.State.Create && hitTestTargets.Count > 0 && hitTestTargets.First() != null) {
				var pos = SceneView.Instance.MousePosition * SceneView.Instance.Scene.CalcTransitionToSpaceOf(mesh);
				if (primaryHitTestTarget is TangerineEdge) {
					var v1 = mesh.Geometry.Vertices[primaryHitTestTarget.VerticeIndices[0]];
					var v2 = mesh.Geometry.Vertices[primaryHitTestTarget.VerticeIndices[1]];
					pos = PolygonMeshUtils.PointProjectionToLine(pos, v1.Pos, v2.Pos, out var isInside);
				}
				pos = meshToSceneFrameTransform.TransformVector(pos);
				renderChain.Enqueue(() => {
					PolygonMeshUtils.RenderVertex(
						pos,
						Theme.Metrics.PolygonMeshBackgroundVertexRadius,
						Theme.Metrics.PolygonMeshVertexRadius,
						Color4.White.Transparentify(0.5f),
						Color4.Yellow.Lighten(0.5f).Transparentify(0.5f)
					);
				});
			}
		}

		private static void DebugRender(PolygonMesh mesh)
		{
			var transform = mesh.CalcTransitionToSpaceOf(SceneView.Instance.Frame);
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
					Renderer.DrawTextLine(vPos * transform, $"V{he.Origin}", 27.0f, Color4.White, 0.0f);
					Renderer.DrawTextLine(vPos * transform, $"V{he.Origin}", 24.0f, Color4.Black, 0.0f);
					Renderer.DrawLine(a * transform, b * transform, Color4.Gray, 4.0f);
					Renderer.DrawLine(b * transform, c * transform, Color4.Gray, 4.0f);
					Renderer.DrawLine(a * transform, b * transform, Color4.Black, 2.0f);
					Renderer.DrawLine(b * transform, c * transform, Color4.Black, 2.0f);
					Renderer.DrawTextLine((a + b) / 2.0f * transform, $"{he.Index}", 21.0f, Color4.White, 0.0f);
					Renderer.DrawTextLine((a + b) / 2.0f * transform, $"{he.Index}", 18.0f, Color4.Black, 0.0f);
				}
			}
		}
	}
}
