using Lime;
using Lime.Widgets.PolygonMesh.Topology.HalfEdgeTopology;
using System.Collections.Generic;

namespace Tangerine.UI.SceneView.PolygonMesh
{
	public static class Extensions
	{
		public static TopologyController Controller(this Lime.Widgets.PolygonMesh.PolygonMesh mesh) =>
			mesh.Components.GetOrAdd<TopologyController<HalfEdgeTopology>>();

		public static void Update(this Lime.Widgets.PolygonMesh.PolygonMesh mesh) =>
			mesh.Controller().Topology.EmplaceVertices(mesh.Controller().Vertices as List<Vertex>);
	}
}
