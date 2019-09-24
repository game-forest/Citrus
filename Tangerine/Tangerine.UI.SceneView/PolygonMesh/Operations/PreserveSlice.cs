using Tangerine.Core;
using PolygonMeshSlice = Tangerine.UI.SceneView.PolygonMesh.PolygonMeshController.PolygonMeshSlice;

namespace Tangerine.UI.SceneView.PolygonMesh
{
	public static partial class PolygonMeshModification
	{
		public class Slice : Operation
		{
			public override bool IsChangingDocument => true;

			private Lime.PolygonMesh.PolygonMesh mesh;
			private PolygonMeshSlice sliceBefore;
			private PolygonMeshSlice sliceAfter;

			private Slice(Lime.PolygonMesh.PolygonMesh mesh, PolygonMeshSlice sliceBefore, PolygonMeshSlice sliceAfter)
			{
				this.mesh = mesh;
				this.sliceBefore = sliceBefore;
				this.sliceAfter = sliceAfter;
			}

			public static void Perform(Lime.PolygonMesh.PolygonMesh mesh, PolygonMeshSlice sliceBefore, PolygonMeshSlice sliceAfter)
			{
				Document.Current.History.Perform(new Slice(mesh, sliceBefore, sliceAfter));
			}

			public class Processor : OperationProcessor<Slice>
			{
				private void Do(Lime.PolygonMesh.PolygonMesh mesh, PolygonMeshSlice slice)
				{
					var controller = mesh.Controller();
					controller.State = slice.State;
					mesh.Vertices.Clear();
					foreach (var v in slice.Vertices) {
						mesh.Vertices.Add(v);
					}
					mesh.Faces.Clear();
					foreach (var i in slice.IndexBuffer) {
						mesh.Faces.Add(i);
					}
					mesh.ConstrainedVertices.Clear();
					foreach (var cp in slice.ConstrainedVertices) {
						mesh.ConstrainedVertices.Add(cp);
					}
					controller.Topology.Sync();
					if (mesh.Animators.TryFind(nameof(mesh.TransientVertices), out var animator)) {
						animator.Keys.Clear();
						foreach (var key in slice.Keyframes) {
							animator.Keys.AddOrdered(key);
							animator.ResetCache();
						}
						mesh.Animators.Invalidate();
					} else {
						mesh.TransientVertices.Clear();
						foreach (var v in slice.Vertices) {
							mesh.TransientVertices.Add(v);
						}
					}
					controller.TopologyAggregator.Invalidate();
				}

				protected override void InternalRedo(Slice op) =>
					Do(op.mesh, op.sliceAfter);

				protected override void InternalUndo(Slice op) =>
					Do(op.mesh, op.sliceBefore);
			}
		}
	}
}
