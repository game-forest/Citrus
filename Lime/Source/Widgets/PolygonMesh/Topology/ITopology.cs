using System.Collections.Generic;

namespace Lime.PolygonMesh.Topology
{
	public interface ITopology
	{
		PolygonMesh Mesh { get; }
		List<Vertex> Vertices { get; }

		void Sync();
		void Invalidate();
#if TANGERINE
		void EmplaceVertices(List<Vertex> vertices);
#endif
	}

	public interface ITopologyModificator
	{
		void AddVertex(Vertex vertex);
		void RemoveVertex(int index, bool keepConstrainedEdges = false);
		void TranslateVertex(int index, Vector2 positionDelta, bool modifyStructure);
		void TranslateVertexUV(int index, Vector2 uvDelta);
		void ConstrainEdge(int index0, int index1);
	}
}
