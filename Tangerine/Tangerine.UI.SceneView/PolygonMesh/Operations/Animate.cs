using Tangerine.Core;

namespace Tangerine.UI.SceneView.PolygonMesh
{
	public static partial class PolygonMeshModification
	{
		public class Animate : Operation
		{
			public override bool IsChangingDocument => true;

			private Animate() { }

			public static void Perform() => Document.Current.History.Perform(new Animate());

			public class Processor : OperationProcessor<Animate>
			{
				protected override void InternalRedo(Animate op) => PolygonMeshTools.State = PolygonMeshTools.ModificationState.Animation;
				protected override void InternalUndo(Animate op) => PolygonMeshTools.State = PolygonMeshTools.ModificationState.Animation;
			}
		}
	}
}
