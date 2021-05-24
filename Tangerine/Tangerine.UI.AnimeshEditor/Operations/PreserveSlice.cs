using Tangerine.Core;

namespace Tangerine.UI.AnimeshEditor.Operations
{
	public static partial class AnimeshModification
	{
		public sealed class Slice : Operation
		{
			public override bool IsChangingDocument => true;

			private readonly Lime.Animesh mesh;
			private readonly AnimeshSlice sliceBefore;
			private readonly AnimeshSlice sliceAfter;
			// First sync is redundant because mesh should already be in correct states.
			// So we skip it in order to ensure mesh operations correctness and to improve performance.
			private bool skipFirstSync;

			private Slice(Lime.Animesh mesh, AnimeshSlice sliceBefore,
				AnimeshSlice sliceAfter, bool skipFirstSync)
			{
				this.mesh = mesh;
				this.sliceBefore = sliceBefore;
				this.sliceAfter = sliceAfter;
				this.skipFirstSync = skipFirstSync;
			}

			public static void Perform(Lime.Animesh mesh, AnimeshSlice sliceBefore,
				AnimeshSlice sliceAfter, bool skipFirstSync = true)
			{
				Document.Current.History.Perform(new Slice(mesh, sliceBefore, sliceAfter, skipFirstSync));
			}

			public sealed class Processor : OperationProcessor<Slice>
			{
				private void Do(Lime.Animesh mesh, AnimeshSlice slice, bool skipSync)
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
							animator.IncreaseVersion();
						}
					} else {
						mesh.TransientVertices.Clear();
						foreach (var v in slice.Vertices) {
							mesh.TransientVertices.Add(v);
						}
					}
					mesh.Invalidate();
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
