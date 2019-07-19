using System;
using System.Collections.Generic;
using System.Linq;
using Lime.Source.Optimizations;

namespace Lime.PolygonMesh
{
	public interface ITopology
	{
		List<Vertex> Vertices { get; set; }
		List<int> IndexBuffer { get; set; }
		//List<(int, int)> ConstrainedPairs { get; set; }
		//List<(int, int)> FramingPairs { get; set; }

		void AddVertex(Vertex vertex);
		void RemoveVertex(int index, bool keepConstrainedEdges = false);
		void TranslateVertex(int index, Vector2 positionDelta);
		void TranslateVertexUV(int index, Vector2 uvDelta);
		void SetConstrain(int index, bool constrained);
	}

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

		public List<Vertex> Vertices { get; set; }
		public List<int> IndexBuffer { get; set; }
		public List<HalfEdge> HalfEdges { get; set; }
		private Triangulator Triangulator { get; }

		public static HalfEdge DummyHalfEdge => new HalfEdge(-1, -1);

		public HalfEdgeTopology()
		{
			Vertices = new List<Vertex>();
			IndexBuffer = new List<int>();
			HalfEdges = new List<HalfEdge>();
			Triangulator = new Triangulator(this);
		}

		public HalfEdgeTopology(List<Vertex> vertices, List<int> indexBuffer, PolygonMesh owner)
		{
			if (vertices.Count != 4) {
				throw new InvalidOperationException();
			}
			Triangulator = new Triangulator(this);
			Vertices = vertices;
			IndexBuffer = indexBuffer;
			HalfEdges = new List<HalfEdge>();

			Connect(0, 1, 2);
			Connect(2, 1, 3);
			MakeTwins(1, 3);
			Traverse();
		}

		public HalfEdge Next(HalfEdge current)
		{
			var next = Next(current.Index);
			if (current.Index == -1 || next < 0 || next >= HalfEdges.Count) {
				throw new InvalidOperationException();
			}
			return HalfEdges[next];
		}

		public int Next(int current)
		{
			if (current == -1) {
				return -1;
			}
			return 3 * (current/ 3) + (current + 1) % 3;
		}

		public HalfEdge Prev(HalfEdge current)
		{
			var prev = Prev(current.Index);
			if (current.Index == -1 || prev < 0 || prev >= HalfEdges.Count) {
				throw new InvalidOperationException();
			}
			return HalfEdges[prev];
		}

		public int Prev(int current)
		{
			if (current == -1) {
				return -1;
			}
			return 3 * (current / 3) + (current + 2) % 3;
		}

		public void MakeTwins(int first, int second)
		{
			var he1 = HalfEdges[first];
			var he2 = HalfEdges[second];
			he1.Twin = second;
			he2.Twin = first;
			HalfEdges[first] = he1;
			HalfEdges[second] = he2;
		}

		public void SetConstrain(int index, bool constrained)
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

		public void Connect(HalfEdge halfEdge, int vertex)
		{
			Connect(halfEdge.Origin, Next(halfEdge).Origin, vertex);
		}

		public void Connect(int v1, int v2, int v3)
		{
			var i = HalfEdges.Count;
			HalfEdges.Add(new HalfEdge(i, v1));
			HalfEdges.Add(new HalfEdge(i + 1, v2));
			HalfEdges.Add(new HalfEdge(i + 2, v3));
		}

		public void Connect(HalfEdge he1, HalfEdge he2, int vertex)
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

		public void RemoveVertex(int index, bool keepConstrainedEdges = false)
		{
			Triangulator.RemoveVertex(this, index);
			Vertices[index] = Vertices[Vertices.Count - 1];
			Vertices.RemoveAt(Vertices.Count - 1);
			if (!keepConstrainedEdges) {
				Triangulator.DoNotKeepConstrainedEdges();
			}
			Invalidate(index);
			System.Diagnostics.Debug.Assert(HalfEdges.Count % 3 == 0);
			System.Diagnostics.Debug.Assert(Triangulator.FullCheck(this));
		}

		public void TranslateVertex(int index, Vector2 positionDelta)
		{
			Triangulator.RemoveVertex(this, index);
			var v = Vertices[index];
			v.Pos += positionDelta;
			Vertices[index] = v;
			Triangulator.AddVertex(this, index);
			Invalidate();
			Traverse();
		}

		public void TranslateVertexUV(int index, Vector2 uvDelta)
		{
			var v = Vertices[index];
			v.UV1 += uvDelta;
			Vertices[index] = v;
		}

		public void AddVertex(Vertex vertex)
		{
			Vertices.Add(vertex);
			Triangulator.AddVertex(this, Vertices.Count - 1);
			Invalidate();
			Traverse();
		}

		public void Traverse()
		{
			IndexBuffer.Clear();
			for (var j = 0; j < HalfEdges.Count; j += 3) {
				IndexBuffer.Add(HalfEdges[j].Origin);
				IndexBuffer.Add(HalfEdges[j + 1].Origin);
				IndexBuffer.Add(HalfEdges[j + 2].Origin);
			}
		}

		public void Invalidate(int removedVertex = -1)
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
						} else {
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

		public void Untwin(int index)
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

		public void RemoveHalfEdge(int index)
		{
			if (index >= 0) {
				var he = HalfEdges[index];
				he.Index = -1;
				HalfEdges[index] = he;
			}
		}

		public void RemoveTriangle(int index)
		{
			RemoveHalfEdge(index);
			RemoveHalfEdge(Next(index));
			RemoveHalfEdge(Prev(index));
		}

		public void InsertConstrainedEdge(int vi1, int vi2)
		{
			Triangulator.InsertConstrainedEdge(this, (vi1, vi2));
			Triangulator.DoNotKeepConstrainedEdges();
			Invalidate();
		}
	}
}
