//using Lime.PolygonMesh;
//using System;
//using System.Collections.Generic;
//using System.Linq;
//using TopologyDataBinding = System.ValueTuple<Tangerine.UI.SceneView.Utils.PolygonMesh.PrimitiveType, int>;

//namespace Tangerine.UI.SceneView.Utils.PolygonMesh
//{
//	using VertexList = List<Vertex>;
//	using EdgeList = List<Edge>;
//	using FaceList = List<Face>;
//	using AdjacencyDictionary = Dictionary<int, List<TopologyDataBinding>>;

//	internal static class TopologyDataListExtenstions
//	{
//		public static List<ITopologyPrimitive> AsPrimitives<T>(this List<T> list) where T : ITopologyPrimitive =>
//			list.Cast<ITopologyPrimitive>().ToList();
//	}

//	internal class TopologyData
//	{
//		public readonly VertexList Vertices;
//		public readonly EdgeList Edges;
//		public readonly FaceList Faces;
//		public readonly AdjacencyDictionary VertexAdjacency;
//		public readonly AdjacencyDictionary EdgeAdjacency;
//		public readonly AdjacencyDictionary FaceAdjacency;

//		public TopologyData(VertexList vertices, EdgeList edges, FaceList faces)
//		{
//			Vertices = vertices;
//			Edges = edges;
//			Faces = faces;
//			VertexAdjacency = new AdjacencyDictionary();
//			EdgeAdjacency = new AdjacencyDictionary();
//			FaceAdjacency = new AdjacencyDictionary();

//			for (var i = 0; i < vertices.Count; ++i) {
//				TryAddAdjacent(vertices[i], i);
//			}
//			for (var i = 0; i < edges.Count; ++i) {
//				TryAddAdjacent(edges[i], i);
//			}
//			for (var i = 0; i < faces.Count; ++i) {
//				TryAddAdjacent(faces[i], i);
//			}
//		}

//		public List<ITopologyPrimitive> this[PrimitiveType type]
//		{
//			get
//			{
//				switch (type) {
//					case PrimitiveType.Vertex:
//						return Vertices.AsPrimitives();
//					case PrimitiveType.Edge:
//						return Edges.AsPrimitives();
//					case PrimitiveType.Face:
//						return Faces.AsPrimitives();
//					default:
//						throw new NotSupportedException();
//				}
//			}
//		}

//		private int GetVertexOrderIndexByTopologicalIndex(int topologicalIndex)
//		{
//			for (var i = 0; i < Vertices.Count; ++i) {
//				if (Vertices[i].TopologicalIndex == topologicalIndex) {
//					return i;
//				}
//			}
//			throw new MemberAccessException();
//		}

//		private bool TryAddAdjacent(Vertex vertex, int key)
//		{
//			VertexAdjacency[key] = new List<TopologyDataBinding>();
//			return TryAddAdjacentEdges(
//				PrimitiveType.Vertex,
//				VertexAdjacency[key],
//				vertex.TopologicalIndex
//			);
//		}

//		private bool TryAddAdjacent(Edge edge, int key)
//		{
//			EdgeAdjacency[key] = new List<TopologyDataBinding> {
//				(PrimitiveType.Vertex, GetVertexOrderIndexByTopologicalIndex(edge.TopologicalIndex0)),
//				(PrimitiveType.Vertex, GetVertexOrderIndexByTopologicalIndex(edge.TopologicalIndex1))
//			};
//			return TryAddAdjacentEdges(
//				PrimitiveType.Edge,
//				EdgeAdjacency[key],
//				edge.TopologicalIndex0,
//				edge.TopologicalIndex1
//			);
//		}

//		private bool TryAddAdjacent(Face face, int key)
//		{
//			FaceAdjacency[key] = new List<TopologyDataBinding> {
//				(PrimitiveType.Vertex, GetVertexOrderIndexByTopologicalIndex(face.TopologicalIndex0)),
//				(PrimitiveType.Vertex, GetVertexOrderIndexByTopologicalIndex(face.TopologicalIndex1)),
//				(PrimitiveType.Vertex, GetVertexOrderIndexByTopologicalIndex(face.TopologicalIndex2))
//			};
//			return TryAddAdjacentEdges(
//				PrimitiveType.Face,
//				FaceAdjacency[key],
//				face.TopologicalIndex0,
//				face.TopologicalIndex1,
//				face.TopologicalIndex2
//			);
//		}

//		private bool TryAddAdjacentEdges(PrimitiveType type, List<TopologyDataBinding> adjacencyList, params int[] topologicalIndices)
//		{
//			bool success = false;
//			for (var i = 0; i < Edges.Count; ++i) {
//				var edge = Edges[i];
//				if (
//					type == PrimitiveType.Edge &&
//					edge.TopologicalIndex0 == topologicalIndices[0] &&
//					edge.TopologicalIndex1 == topologicalIndices[1]
//				) {
//					continue;
//				}
//				foreach (var index in topologicalIndices) {
//					if (index == edge.TopologicalIndex0 || index == edge.TopologicalIndex1) {
//						adjacencyList.Add((PrimitiveType.Edge, i));
//						success |= true;
//					}
//				}
//			}
//			return success;
//		}
//	}

//	//===============================================================
//	// Redundant once Topology Data is stored IN a component
//	internal class TopologyDataProvider
//	{
//		private class TopologyDataKey : IEquatable<TopologyDataKey>
//		{
//			public readonly List<int> IndexBuffer;
//			public readonly int HashCode;

//			public TopologyDataKey(ITopology topology)
//			{
//				IndexBuffer = topology.IndexBuffer;
//				HashCode = topology.IndexBuffer.GetHashCode();
//			}

//			public override int GetHashCode()
//			{
//				unchecked {
//					int hash = (int)251234214123;
//					hash = (hash * 728392183) ^ HashCode;
//					foreach (var index in IndexBuffer) {
//						hash = hash * 101 + index;
//					}
//					return hash;
//				}
//			}

//			public bool Equals(TopologyDataKey other)
//			{
//				if (IndexBuffer.Count == other.IndexBuffer.Count) {
//					for (var i = 0; i < IndexBuffer.Count; ++i) {
//						if (IndexBuffer[i] != other.IndexBuffer[i]) {
//							return false;
//						}
//					}
//					return true;
//				}
//				return false;
//			}
//		}

//		private static readonly Dictionary<TopologyDataKey, TopologyData> dataCache = new Dictionary<TopologyDataKey, TopologyData>();

//		public TopologyData TryGetCachedData(ITopology topology)
//		{
//			var key = new TopologyDataKey(topology);
//			if (!dataCache.TryGetValue(key, out var data)) {
//				data = ExtractData(topology);
//				dataCache[key] = data;
//			}
//			return data;
//		}

//		public TopologyData ExtractData(ITopology topology)
//		{
//			var vertices = new List<Vertex>();
//			var edges = new HashSet<Edge>();
//			var faces = new List<Face>();
//			for (var i = 0; i < topology.Vertices.Count; ++i) {
//				vertices.Add(new Vertex(topology, i));
//			}
//			for (var i = 0; i < topology.IndexBuffer.Count; i += 3) {
//				var ti0 = topology.IndexBuffer[i];
//				var ti1 = topology.IndexBuffer[i + 1];
//				var ti2 = topology.IndexBuffer[i + 2];
//				edges.Add(new Edge(topology, ti0, ti1, false, false));
//				edges.Add(new Edge(topology, ti1, ti2, false, false));
//				edges.Add(new Edge(topology, ti2, ti0, false, false));
//				faces.Add(new Face(topology, ti0, ti1, ti2));
//			}
//			return new TopologyData(vertices, edges.ToList(), faces);
//		}
//	}
//}
