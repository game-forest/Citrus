using Tangerine.Core;

namespace Tangerine.UI.SceneView.Animesh
{
	public static partial class AnimeshModification
	{
		public class Slice : Operation
		{
			public override bool IsChangingDocument => true;

			private readonly Lime.Widgets.Animesh.Animesh mesh;
			private readonly AnimeshController.AnimeshSlice sliceBefore;
			private readonly AnimeshController.AnimeshSlice sliceAfter;
			// First sync is redundant because mesh should already be in correct states.
			// So we skip it in order to ensure mesh operations correctness and to improve performance.
			private bool skipFirstSync;

			private Slice(Lime.Widgets.Animesh.Animesh mesh, AnimeshController.AnimeshSlice sliceBefore,
				AnimeshController.AnimeshSlice sliceAfter, bool skipFirstSync)
			{
				this.mesh = mesh;
				this.sliceBefore = sliceBefore;
				this.sliceAfter = sliceAfter;
				this.skipFirstSync = skipFirstSync;
			}

			public static void Perform(Lime.Widgets.Animesh.Animesh mesh, AnimeshController.AnimeshSlice sliceBefore,
				AnimeshController.AnimeshSlice sliceAfter, bool skipFirstSync = true)
			{
				Document.Current.History.Perform(new Slice(mesh, sliceBefore, sliceAfter, skipFirstSync));
			}

			public class Processor : OperationProcessor<Slice>
			{
				private void Do(Lime.Widgets.Animesh.Animesh mesh, AnimeshController.AnimeshSlice slice, bool skipSync)
				{
					var controller = mesh.Controller();
					AnimeshTools.State = slice.State;
					mesh.Vertices.Clear();
					foreach (var v in slice.Vertices) {
						mesh.Vertices.Add(v);
					}
					mesh.Faces.Clear();
					foreach (var i in slice.IndexBuffer) {
						mesh.Faces.Add(i);
					}
					mesh.ConstrainedEdges.Clear();
					foreach (var cp in slice.ConstrainedVertices) {
						mesh.ConstrainedEdges.Add(cp);
					}
					if (!skipSync) {
						controller.Topology.ConstructFrom(mesh.Vertices, mesh.ConstrainedEdges, mesh.Faces);
					}
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
				}

				protected override void InternalRedo(Slice op)
				{
					Do(op.mesh, op.sliceAfter, op.skipFirstSync);
					op.skipFirstSync = false;
				}

				protected override void InternalUndo(Slice op) =>
					Do(op.mesh, op.sliceBefore, false);
			}
		}
	}
}
