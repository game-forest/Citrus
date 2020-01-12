using HalfEdgeTopology = Lime.Widgets.PolygonMesh.Topology.HalfEdgeTopology.HalfEdgeTopology;

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

		public static PolygonMeshController.ModificationState State(this Lime.Widgets.PolygonMesh.PolygonMesh mesh) =>
			mesh.Controller().State;

		public static void Translate(this Lime.PolygonMesh.Topology.ITopologyModificator modificator, ITopologyData data, Lime.Vector2 delta)
		{
			switch (data) {
				case VertexData vd:
					modificator.TranslateVertex(vd.TopologicalIndex, delta, false);
					break;
				case EdgeData ed:
					modificator.TranslateVertex(ed.TopologicalIndex0, delta, false);
					modificator.TranslateVertex(ed.TopologicalIndex1, delta, false);
					break;
				case FaceData fd:
					modificator.TranslateVertex(fd.TopologicalIndex0, delta, false);
					modificator.TranslateVertex(fd.TopologicalIndex1, delta, false);
					modificator.TranslateVertex(fd.TopologicalIndex2, delta, false);
					break;
			}
		}
	}
}
