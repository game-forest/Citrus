using Lime.PolygonMesh.Topology;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Tangerine.UI.SceneView.PolygonMesh
{
	public sealed class HalfEdgeTopologyAggregator : ITopologyAggregator
	{
		private readonly HalfEdgeTopology topology;
		private List<ITopologyData> vertexBulkData;
		private List<ITopologyData> edgeBulkData;
		private List<ITopologyData> faceBulkData;

		private List<VertexData> vertexData;
		public List<VertexData> VertexData
		{
			get => vertexData ?? (vertexData = ExtractVertexData());
			private set => vertexData = value;
		}

		private List<EdgeData> edgeData;
		public List<EdgeData> EdgeData
		{
			get => edgeData ?? (edgeData = ExtractEdgeData());
			private set => edgeData = value;
		}

		private List<FaceData> faceData;
		public List<FaceData> FaceData
		{
			get => faceData ?? (faceData = ExtractFaceData());
			private set => faceData = value;
		}

		private Dictionary<int, List<(TopologyDataType Type, int Index)>> vertexAdjacency;
		public Dictionary<int, List<(TopologyDataType Type, int Index)>> VertexAdjacency
		{
			get => vertexAdjacency ?? (vertexAdjacency = ExtractVertexAdjacencyData());
			private set => vertexAdjacency = value;
		}

		private Dictionary<int, List<(TopologyDataType Type, int Index)>> edgeAdjacency;
		public Dictionary<int, List<(TopologyDataType Type, int Index)>> EdgeAdjacency
		{
			get => edgeAdjacency ?? (edgeAdjacency = ExtractEdgeAdjacencyData());
			private set => edgeAdjacency = value;
		}

		private Dictionary<int, List<(TopologyDataType Type, int Index)>> faceAdjacency;
		public Dictionary<int, List<(TopologyDataType Type, int Index)>> FaceAdjacency
		{
			get => faceAdjacency = (faceAdjacency = ExtractFaceAdjacencyData());
			private set => faceAdjacency = value;
		}

		public HalfEdgeTopologyAggregator(HalfEdgeTopology topology)
		{
			this.topology = topology;
		}

		public List<ITopologyData> this[TopologyDataType type]
		{
			get
			{
				switch (type) {
					case TopologyDataType.Vertex:
						return vertexBulkData ?? (vertexBulkData = VertexData.AsBulkData());
					case TopologyDataType.Edge:
						return edgeBulkData ?? (edgeBulkData = EdgeData.AsBulkData());
					case TopologyDataType.Face:
						return faceBulkData ?? (faceBulkData = FaceData.AsBulkData());
					default:
						return null;
				}
			}
		}

		public List<(TopologyDataType Type, int Index)> this[TopologyDataType type, int index]
		{
			get
			{
				switch (type) {
					case TopologyDataType.Vertex:
						return VertexAdjacency[index];
					case TopologyDataType.Edge:
						return EdgeAdjacency[index];
					case TopologyDataType.Face:
						return FaceAdjacency[index];
					default:
						return null;
				}
			}
		}

		public void Invalidate()
		{
			VertexData = null;
			EdgeData = null;
			FaceData = null;

			VertexAdjacency = null;
			EdgeAdjacency = null;
			FaceAdjacency = null;

			vertexBulkData = null;
			edgeBulkData = null;
			faceBulkData = null;
		}

		private List<VertexData> ExtractVertexData()
		{
			var vertices = new List<VertexData>();
			for (var i = 0; i < topology.Vertices.Count; ++i) {
				vertices.Add(new VertexData(i));
			}
			return vertices;
		}

		private List<EdgeData> ExtractEdgeData()
		{
			var edges = new HashSet<EdgeData>();
			for (var i = 0; i < topology.Mesh.Faces.Count; ++i) {
				var face = topology.Mesh.Faces[i];
				edges.Add(new EdgeData(face[0], face[1], topology[face[0], face[1]].Twin == -1, topology[face[0], face[1]].Constrained));
				edges.Add(new EdgeData(face[1], face[2], topology[face[1], face[2]].Twin == -1, topology[face[1], face[2]].Constrained));
				edges.Add(new EdgeData(face[2], face[0], topology[face[2], face[0]].Twin == -1, topology[face[2], face[0]].Constrained));
			}
			return edges.ToList();
		}

		private List<FaceData> ExtractFaceData()
		{
			var faces = new List<FaceData>();
			for (var i = 0; i < topology.Mesh.Faces.Count; ++i) {
				var face = topology.Mesh.Faces[i];
				faces.Add(new FaceData(face[0], face[1], face[2]));
			}
			return faces;
		}

		private Dictionary<int, List<(TopologyDataType Type, int Index)>> ExtractVertexAdjacencyData()
		{
			var dict = new Dictionary<int, List<(TopologyDataType Type, int Index)>>();
			for (var i = 0; i < VertexData.Count; ++i) {
				TryAddAdjacent(dict, VertexData[i], i);
			}
			return dict;
		}

		private Dictionary<int, List<(TopologyDataType Type, int Index)>> ExtractEdgeAdjacencyData()
		{
			var dict = new Dictionary<int, List<(TopologyDataType Type, int Index)>>();
			for (var i = 0; i < EdgeData.Count; ++i) {
				TryAddAdjacent(dict, EdgeData[i], i);
			}
			return dict;
		}

		private Dictionary<int, List<(TopologyDataType Type, int Index)>> ExtractFaceAdjacencyData()
		{
			var dict = new Dictionary<int, List<(TopologyDataType Type, int Index)>>();
			for (var i = 0; i < FaceData.Count; ++i) {
				TryAddAdjacent(dict, FaceData[i], i);
			}
			return dict;
		}

		private int GetVertexOrderIndexByTopologicalIndex(int topologicalIndex)
		{
			for (var i = 0; i < VertexData.Count; ++i) {
				if (VertexData[i].TopologicalIndex == topologicalIndex) {
					return i;
				}
			}
			throw new MemberAccessException();
		}

		private bool TryAddAdjacent(Dictionary<int, List<(TopologyDataType Type, int Index)>> dict, VertexData vertex, int key)
		{
			dict[key] = new List<(TopologyDataType, int)>();
			return TryAddAdjacentEdges(
				TopologyDataType.Vertex,
				dict[key],
				vertex.TopologicalIndex
			);
		}

		private bool TryAddAdjacent(Dictionary<int, List<(TopologyDataType Type, int Index)>> dict, EdgeData edge, int key)
		{
			dict[key] = new List<(TopologyDataType, int)> {
				(TopologyDataType.Vertex, GetVertexOrderIndexByTopologicalIndex(edge.TopologicalIndex0)),
				(TopologyDataType.Vertex, GetVertexOrderIndexByTopologicalIndex(edge.TopologicalIndex1))
			};
			return TryAddAdjacentEdges(
				TopologyDataType.Edge,
				dict[key],
				edge.TopologicalIndex0,
				edge.TopologicalIndex1
			);
		}

		private bool TryAddAdjacent(Dictionary<int, List<(TopologyDataType Type, int Index)>> dict, FaceData face, int key)
		{
			dict[key] = new List<(TopologyDataType, int)> {
				(TopologyDataType.Vertex, GetVertexOrderIndexByTopologicalIndex(face.TopologicalIndex0)),
				(TopologyDataType.Vertex, GetVertexOrderIndexByTopologicalIndex(face.TopologicalIndex1)),
				(TopologyDataType.Vertex, GetVertexOrderIndexByTopologicalIndex(face.TopologicalIndex2))
			};
			return TryAddAdjacentEdges(
				TopologyDataType.Face,
				dict[key],
				face.TopologicalIndex0,
				face.TopologicalIndex1,
				face.TopologicalIndex2
			);
		}

		private bool TryAddAdjacentEdges(TopologyDataType type, List<(TopologyDataType, int)> adjacencyList, params int[] topologicalIndices)
		{
			bool success = false;
			var adjacencySet = new HashSet<(TopologyDataType, int)>();
			for (var i = 0; i < EdgeData.Count; ++i) {
				var edge = EdgeData[i];
				if (
					type == TopologyDataType.Edge &&
					edge.TopologicalIndex0 == topologicalIndices[0] &&
					edge.TopologicalIndex1 == topologicalIndices[1]
				) {
					continue;
				}
				foreach (var index in topologicalIndices) {
					if (index == edge.TopologicalIndex0 || index == edge.TopologicalIndex1) {
						adjacencySet.Add((TopologyDataType.Edge, i));
						success |= true;
					}
				}
			}
			foreach (var item in adjacencySet) {
				adjacencyList.Add(item);
			}
			return success;
		}
	}
}
