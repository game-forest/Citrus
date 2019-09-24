using System;
using System.Collections.Generic;
using Edge = Lime.PolygonMesh.PolygonMesh.Edge;
#if DEBUG
using System.Linq;
#endif

namespace Lime.PolygonMesh.Topology
{
	public class HalfEdgeTopology : ITopology
	{
		public struct HalfEdge
		{
			public int Origin;
			public int Index;
			public int Twin;
			public bool Constrained;

			public HalfEdge(int index, int origin)
			{
				Origin = origin;
				Index = index;
				Twin = -1;
				Constrained = false;
			}

			public HalfEdge(int index, int origin, int twin)
			{
				Origin = origin;
				Index = index;
				Twin = twin;
				Constrained = false;
			}
		}

		public PolygonMesh Mesh { get; }
		public List<Vertex> Vertices { get; private set; }

		internal List<HalfEdge> HalfEdges { get; set; }
		internal HalfEdgeTriangulator Triangulator { get; }

		internal static HalfEdge DummyHalfEdge => new HalfEdge(-1, -1);

		public HalfEdge this[int index0, int index1]
		{
			get
			{
				for (var i = 0; i < HalfEdges.Count; ++i) {
					var he = HalfEdges[i];
					if (he.Origin == index0 && Next(he).Origin == index1) {
						return he;
					}
				}
				throw new IndexOutOfRangeException();
			}
		}

		public HalfEdgeTopology(PolygonMesh mesh)
		{
			Mesh = mesh;
			Vertices = mesh.Vertices;
			HalfEdges = new List<HalfEdge>();
			Triangulator = new HalfEdgeTriangulator(this);

			if (Mesh.Faces.Count == 0) {
				Connect(0, 1, 2);
				Connect(2, 1, 3);
				MakeTwins(1, 3);
				Traverse();
			} else {
				Sync();
			}
		}

#if TANGERINE
		public void EmplaceVertices(List<Vertex> vertices) => Vertices = vertices;
#endif

		public void Sync()
		{
			HalfEdges.Clear();
			for (var i = 0; i < Mesh.Faces.Count; ++i) {
				Connect(Mesh.Faces[i]);
			}
			var twinsBuffer = new Dictionary<PolygonMesh.Face, (int[] twins, int count)>();
			for (var i = 0; i < Mesh.Faces.Count; ++i) {
				twinsBuffer[Mesh.Faces[i]] = (new int[3] { -1, -1, -1 }, 0);
			}
			for (var i = 0; i < Mesh.Faces.Count; ++i) {
				var (twins0, count0) = twinsBuffer[Mesh.Faces[i]];
				if (count0 == 3) {
					continue;
				}
				for (var j = 0; j < Mesh.Faces.Count; ++j) {
					var (twins1, count1) = twinsBuffer[Mesh.Faces[j]];
					if (
						i == j ||
						count1 == 3 ||
						twins0[0] == j ||
						twins0[1] == j ||
						twins0[2] == j ||
						twins1[0] == i ||
						twins1[1] == i ||
						twins1[2] == i
					) {
						continue;
					}
					if (InferTwinEdge(Mesh.Faces[i], Mesh.Faces[j])) {
						twins0[count0++] = j;
						twinsBuffer[Mesh.Faces[i]] = (twins0, count0);

						twins1[count1++] = i;
						twinsBuffer[Mesh.Faces[j]] = (twins1, count1);

						if (count0 == 3) {
							break;
						}
					}
				}
			}
			Triangulator.DoNotKeepConstrainedEdges();
			var cache = new List<Edge>(Mesh.ConstrainedVertices);
			foreach (var edge in cache) {
				InsertConstrainedEdge(edge[0], edge[1]);
			}
		}

		internal bool InferTwinEdge(PolygonMesh.Face face0, PolygonMesh.Face face1)
		{
			var e = new List<int>();
			for (var i = 0; i < 3; ++i) {
				for (var j = 0; j < 3; ++j) {
					if (face0[i] == face1[j]) {
						e.Add(face0[i]);
						break;
					}
				}
			}
			if (e.Count == 2) {
				MakeTwins(this[e[0], e[1]].Index, this[e[1], e[0]].Index);
				return true;
			}
			return false;
		}

		internal void Connect(HalfEdge halfEdge, int vertex)
		{
			Connect(halfEdge.Origin, Next(halfEdge).Origin, vertex);
		}

		internal void Connect(PolygonMesh.Face face)
		{
			Connect(face[0], face[1], face[2]);
		}

		internal void Connect(int v1, int v2, int v3)
		{
			var i = HalfEdges.Count;
			HalfEdges.Add(new HalfEdge(i, v1));
			HalfEdges.Add(new HalfEdge(i + 1, v2));
			HalfEdges.Add(new HalfEdge(i + 2, v3));
		}

		internal void Connect(HalfEdge he1, HalfEdge he2, int vertex)
		{
			var i = HalfEdges.Count;
			var new1 = new HalfEdge(i, he1.Origin, he1.Twin);
			var new2 = new HalfEdge(i + 1, he2.Origin, he2.Twin);
			var new3 = new HalfEdge(i + 2, vertex);
			if (he1.Twin != -1) {
				MakeTwins(i, he1.Twin);
			}
			if (he2.Twin != -1) {
				MakeTwins(i + 1, he2.Twin);
			}
			HalfEdges.Add(new1);
			HalfEdges.Add(new2);
			HalfEdges.Add(new3);
		}

		internal HalfEdge Next(HalfEdge current)
		{
			var next = Next(current.Index);
			if (current.Index == -1 || next < 0 || next >= HalfEdges.Count) {
				throw new InvalidOperationException();
			}
			return HalfEdges[next];
		}

		internal int Next(int current)
		{
			if (current == -1) {
				return -1;
			}
			return 3 * (current / 3) + (current + 1) % 3;
		}

		internal HalfEdge Prev(HalfEdge current)
		{
			var prev = Prev(current.Index);
			if (current.Index == -1 || prev < 0 || prev >= HalfEdges.Count) {
				throw new InvalidOperationException();
			}
			return HalfEdges[prev];
		}

		internal int Prev(int current)
		{
			if (current == -1) {
				return -1;
			}
			return 3 * (current / 3) + (current + 2) % 3;
		}

		internal void MakeTwins(int first, int second)
		{
			var he1 = HalfEdges[first];
			var he2 = HalfEdges[second];
			he1.Twin = second;
			he2.Twin = first;
			HalfEdges[first] = he1;
			HalfEdges[second] = he2;
		}

		internal void Traverse()
		{
			Mesh.Faces.Clear();
			Mesh.ConstrainedVertices.Clear();
			for (var j = 0; j < HalfEdges.Count; j += 3) {
				Mesh.Faces.Add(new PolygonMesh.Face {
					Index0 = (ushort)HalfEdges[j].Origin,
					Index1 = (ushort)HalfEdges[j + 1].Origin,
					Index2 = (ushort)HalfEdges[j + 2].Origin
				});
				for (var i = j; i < j + 3; ++i) {
					var pair = new Edge(
						(ushort)HalfEdges[i].Origin,
						(ushort)HalfEdges[i + 1 == j + 3 ? j : i + 1].Origin
					);
					if (HalfEdges[i].Constrained) {
						if (Mesh.ConstrainedVertices.Contains(pair)) {
							continue;
						}
						Mesh.ConstrainedVertices.Add(pair);
					}
				}
			}
		}

		public void Invalidate() => Invalidate(removedVertex: -1);

		internal void Invalidate(int removedVertex)
		{
			var i = 0;
			var edges = new List<HalfEdge>();
			for (var j = 0; j < HalfEdges.Count; j++) {
				var edge = HalfEdges[j];
				if (edge.Index != -1) {
					var halfEdge = edge;
					halfEdge.Index = i++;
					if (removedVertex != -1 && halfEdge.Origin == Vertices.Count) {
						halfEdge.Origin = removedVertex;
					}
					edges.Add(halfEdge);
					if (halfEdge.Twin != -1) {
						if (halfEdge.Twin < halfEdge.Index) {
							var tmp = edges[halfEdge.Twin];
							tmp.Twin = halfEdge.Index;
							edges[halfEdge.Twin] = tmp;
						}
						else {
							var tmp = HalfEdges[halfEdge.Twin];
							tmp.Twin = halfEdge.Index;
							HalfEdges[halfEdge.Twin] = tmp;
						}
					}
				}
			}
			HalfEdges = edges;
			Traverse();
#if DEBUG
			foreach (var halfEdge in HalfEdges) {
				System.Diagnostics.Debug.Assert(halfEdge.Twin == -1 || halfEdge.Index == HalfEdges[halfEdge.Twin].Twin);
			}
			var c = HalfEdges.Count(edge => edge.Constrained && edge.Twin != -1 && !HalfEdges[edge.Twin].Constrained);
			System.Diagnostics.Debug.Assert(c == 0);
#endif
		}

		internal void Untwin(int index)
		{
			if (index < 0) {
				return;
			}
			var he = HalfEdges[index];
			if (he.Twin != -1) {
				var twin = HalfEdges[he.Twin];
				twin.Twin = -1;
				HalfEdges[he.Twin] = twin;
				he.Twin = -1;
				HalfEdges[index] = he;
			}
		}

		internal void RemoveHalfEdge(int index)
		{
			if (index >= 0) {
				var he = HalfEdges[index];
				he.Index = -1;
				HalfEdges[index] = he;
			}
		}

		internal void RemoveTriangle(int index)
		{
			RemoveHalfEdge(index);
			RemoveHalfEdge(Next(index));
			RemoveHalfEdge(Prev(index));
		}

		internal void InsertConstrainedEdge(int vi1, int vi2)
		{
			Triangulator.InsertConstrainedEdge((vi1, vi2));
			Triangulator.DoNotKeepConstrainedEdges();
			Invalidate();
		}

		internal void SetEdgeConstraint(int index, bool constrained)
		{
			var he = HalfEdges[index];
			he.Constrained = constrained;
			if (he.Twin != -1) {
				var twin = HalfEdges[he.Twin];
				twin.Constrained = constrained;
				HalfEdges[he.Twin] = twin;
			}
			HalfEdges[index] = he;
		}
	}
}
