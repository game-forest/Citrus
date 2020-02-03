using System;
using System.Collections.Generic;
using Lime.Widgets.PolygonMesh.Topology;

namespace Lime.PolygonMesh.Topology
{
	public interface ITopology
	{
		List<Vertex> Vertices { get; }

		void Sync(List<Vertex> vertices, List<Edge> constrainedEdges, List<Face> faces);
		IEnumerable<Face> Faces { get; }

		event Action<ITopology> OnTopologyChanged;

		void AddVertex(Vertex vertex);
		void RemoveVertex(int index, bool keepConstrainedEdges = false);
		void TranslateVertex(int index, Vector2 positionDelta, Vector2 uvDelta);
		void ConstrainEdge(int index0, int index1);
		void Concave(Vector2 position);
		IEnumerable<(int, int)> ConstrainedEdges { get; }
	}
}
