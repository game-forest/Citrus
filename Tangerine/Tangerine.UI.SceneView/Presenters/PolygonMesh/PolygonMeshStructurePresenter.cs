using Lime;
using System.Linq;
using Tangerine.Core;
using Lime.PolygonMesh;

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
			PolygonMeshManager.Instance.SetTargetMesh(meshes[0]);
			PolygonMeshManager.Instance.HitTestTarget();
			PolygonMeshManager.Instance.RenderTarget();
			PolygonMeshManager.Instance.Invalidate();
#if DEBUG
			DebugRender(meshes[0]);
#endif // DEBUG
		}

		private static void DebugRender(PolygonMesh mesh)
		{
			//var transform = mesh.CalcTransitionToSpaceOf(SceneView.Instance.Frame);
			//if (
			//	mesh.CurrentContext == PolygonMesh.Context.Deformation &&
			//	SceneView.Instance.Input.IsKeyPressed(Key.Alt)
			//) {
			//	var g = (HalfEdgeTopology)mesh.Geometry;
			//	foreach (var he in g.HalfEdges) {
			//		var vPos = g.Vertices[he.Origin].Pos;
			//		var vNextPos = g.Vertices[g.Next(he).Origin].Pos;
			//		var offset = new Vector2(vPos.Y - vNextPos.Y, vNextPos.X - vPos.X).Normalized * 12.0f;
			//		var d = Vector2.Distance(vPos, vNextPos);
			//		var a = vPos + 0.2f * d * (vNextPos - vPos).Normalized + offset;
			//		var b = vNextPos + 0.2f * d * (vPos - vNextPos).Normalized + offset;
			//		var c = vNextPos + 0.25f * d * (vPos - vNextPos).Normalized + 1.5f * offset;
			//		Renderer.DrawTextLine(vPos * transform, $"V{he.Origin}", 27.0f, Color4.White, 0.0f);
			//		Renderer.DrawTextLine(vPos * transform, $"V{he.Origin}", 24.0f, Color4.Black, 0.0f);
			//		Renderer.DrawLine(a * transform, b * transform, Color4.Gray, 4.0f);
			//		Renderer.DrawLine(b * transform, c * transform, Color4.Gray, 4.0f);
			//		Renderer.DrawLine(a * transform, b * transform, Color4.Black, 2.0f);
			//		Renderer.DrawLine(b * transform, c * transform, Color4.Black, 2.0f);
			//		Renderer.DrawTextLine((a + b) / 2.0f * transform, $"{he.Index}", 21.0f, Color4.White, 0.0f);
			//		Renderer.DrawTextLine((a + b) / 2.0f * transform, $"{he.Index}", 18.0f, Color4.Black, 0.0f);
			//	}
			//}
		}
	}
}
