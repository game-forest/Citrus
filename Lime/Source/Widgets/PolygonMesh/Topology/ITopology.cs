using System;
using System.Collections.Generic;
using Lime.Widgets.PolygonMesh.Topology;
using Vertex = Lime.Vertex;

namespace Lime.PolygonMesh.Topology
{
	public class TopologyHitTestResult
	{
		public ITopologyPrimitive Target;
		public object Info;
	}

	public interface ITopology
	{
		List<Vertex> Vertices { get; }

		void Sync(List<Vertex> vertices, List<Edge> constrainedEdges, List<Face> faces);
		IEnumerable<Face> Faces { get; }
#if TANGERINE
		/// <summary>
		/// We need private information in Tangerine in order to show them to end user.
		/// In any other application there is no need to know inner information about topology.
		/// </summary>
		IEnumerable<(Face, Face.FaceInfo)> FacesWithInfo { get; }
#endif

		event Action<ITopology> OnTopologyChanged;

		void AddVertex(Vertex vertex);
		void RemoveVertex(int index, bool keepConstrainedEdges = false);
		void TranslateVertex(int index, Vector2 positionDelta, Vector2 uvDelta);
		void ConstrainEdge(int index0, int index1);
		void Concave(Vector2 position);
		IEnumerable<(int, int)> ConstrainedEdges { get; }
		bool HitTest(Vector2 position, out TopologyHitTestResult result);
	}
}
