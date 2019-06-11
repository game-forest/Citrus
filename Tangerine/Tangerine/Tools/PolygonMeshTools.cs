using Lime.PolygonMesh;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Tangerine.Core;
using Tangerine.UI;
using Tangerine.UI.SceneView;

namespace Tangerine
{
	public class PolygonMeshAnimate : DocumentCommandHandler
	{
		public override void ExecuteTransaction()
		{
			var meshes = Document.Current.SelectedNodes().Editable().OfType<PolygonMesh>().ToList();
			if (meshes.Count != 1) {
				return;
			}
			var mesh = meshes[0];
			mesh.CurrentState = PolygonMesh.State.Animate;
		}
	}

	public class PolygonMeshDeform : DocumentCommandHandler
	{
		public override void ExecuteTransaction()
		{
			var meshes = Document.Current.SelectedNodes().Editable().OfType<PolygonMesh>().ToList();
			if (meshes.Count != 1) {
				return;
			}
			var mesh = meshes[0];
			mesh.CurrentState = PolygonMesh.State.Deform;
		}
	}

	public class PolygonMeshCreate : DocumentCommandHandler
	{
		public override void ExecuteTransaction()
		{
			var meshes = Document.Current.SelectedNodes().Editable().OfType<PolygonMesh>().ToList();
			if (meshes.Count != 1) {
				return;
			}
			var mesh = meshes[0];
			mesh.CurrentState = PolygonMesh.State.Create;
		}
	}

	public class PolygonMeshRemove : DocumentCommandHandler
	{
		public override void ExecuteTransaction()
		{
			var meshes = Document.Current.SelectedNodes().Editable().OfType<PolygonMesh>().ToList();
			if (meshes.Count != 1) {
				return;
			}
			var mesh = meshes[0];
			mesh.CurrentState = PolygonMesh.State.Remove;
		}
	}
}
