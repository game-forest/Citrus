using System;
using System.Collections.Generic;
using Lime.Widgets.PolygonMesh.Topology;

namespace Lime.PolygonMesh.Topology
{
	public interface ITopology
	{
		List<Vertex> Vertices { get; }

		void Sync(List<Vertex> vertices, List<Edge> constrainedEdges, List<Face> faces);
		void Invalidate();
#if TANGERINE
		void EmplaceVertices(List<Vertex> vertices);
#endif

		IEnumerable<Face> Faces { get; }

		event Action<ITopology> OnTopologyChanged;
	}

	public interface ITopologyModificator
	{
		void AddVertex(Vertex vertex);
		void RemoveVertex(int index, bool keepConstrainedEdges = false);
		void TranslateVertex(int index, Vector2 positionDelta, bool modifyStructure);
		void TranslateVertexUV(int index, Vector2 uvDelta);
		void ConstrainEdge(int index0, int index1);
		void Concave(Vector2 position);
	}
}
