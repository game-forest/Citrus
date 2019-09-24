using Lime.PolygonMesh;
using System.Linq;
using Tangerine.Core;
using Tangerine.UI;
using Tangerine.UI.SceneView.PolygonMesh;

namespace Tangerine
{
	public static class PolygonMeshTools
	{
		public static PolygonMeshController.ModificationState ControllerStateBeforeClone { get; set; }

		public static void ChangeState(PolygonMeshController.ModificationState state)
		{
			foreach (var mesh in Document.Current.Nodes().OfType<PolygonMesh>().ToList()) {
				mesh.Controller().State = state;
			}
		}

		public class Animate : DocumentCommandHandler
		{
			public override void ExecuteTransaction() =>
				ChangeState(PolygonMeshController.ModificationState.Animation);
		}

		public class Triangulate : DocumentCommandHandler
		{
			public override void ExecuteTransaction() =>
				ChangeState(PolygonMeshController.ModificationState.Triangulation);
		}

		public class Create : DocumentCommandHandler
		{
			public override void ExecuteTransaction() =>
				ChangeState(PolygonMeshController.ModificationState.Creation);
		}

		public class Remove : DocumentCommandHandler
		{
			public override void ExecuteTransaction() =>
				ChangeState(PolygonMeshController.ModificationState.Removal);
		}
	}



}
