using Lime.PolygonMesh.Topology;
using Lime.Widgets.PolygonMesh.Topology.HalfEdgeTopology;

namespace Tangerine.UI.SceneView.PolygonMesh
{
	public static class Extensions
	{
		public static TopologyController Controller(this Lime.Widgets.PolygonMesh.PolygonMesh mesh)
		{
			var controller = mesh.Components.GetOrAdd<TopologyController<HalfEdgeTopology>>();
			controller.Update();
			return controller;
		}
	}
}
