using System;
using System.Collections.Generic;
using Lime.Source.Optimizations;
using Yuzu;

namespace Lime.PolygonMesh
{
	public enum GeometryPrimitive
	{
		Vertex,
		Edge,
		Face
	}

	public interface IGeometry
	{
		List<Vertex> Vertices { get; set; }
#if TANGERINE
		List<int> FramingVertices { get; }
#endif
		ITangerineGeometryPrimitive[] this[GeometryPrimitive primitive] { get; }

		void AddVertex(Vertex vertex);
		void RemoveVertex(int index);
		void MoveVertex(int index, Vector2 positionDelta);
		void MoveVertices(int[] indices, Vector2 positionDelta);
		void MoveVertexUv(int index, Vector2 uvDelta);
		void MoveVerticesUv(int[] indices, Vector2 uvDelta);
		int[] Traverse();
	}

#if TANGERINE
	public interface ITangerineGeometryPrimitive
	{
		IGeometry Owner { get; set; }
		int[] VerticeIndices { get; set; }

		bool HitTest(Vector2 position, Matrix32 transform, out float distance, float radius = 1.0f, float scale = 1.0f);
		void Move(Vector2 positionDelta);
		void MoveUv(Vector2 uvDelta);
		void Render(Matrix32 transform, Color4 color, float radius = 1.0f);
		Vector2 InterpolateUv(Vector2 position);
		HashSet<ITangerineGeometryPrimitive> GetAdjacent();
	}

	public struct TangerineVertex : ITangerineGeometryPrimitive
	{
		public IGeometry Owner { get; set; }
		public int[] VerticeIndices { get; set; }

		public TangerineVertex(IGeometry owner, int vertex)
		{
			Owner = owner;
			VerticeIndices = new int[] { vertex };
		}

		public bool HitTest(Vector2 position, Matrix32 transform, out float distance, float radius = 1.0f, float scale = 1.0f)
		{
			var p = transform.TransformVector(Owner.Vertices[VerticeIndices[0]].Pos);
			return PolygonMeshUtils.PointPointIntersection(p, position, radius / scale, out distance);
		}

		public void Move(Vector2 positionDelta)
		{
			Owner.MoveVertex(VerticeIndices[0], positionDelta);
		}

		public void MoveUv(Vector2 uvDelta)
		{
			Owner.MoveVertexUv(VerticeIndices[0], uvDelta);
		}

		public void Render(Matrix32 transform, Color4 color, float radius = 1.0f)
		{
			Renderer.DrawRound(
				transform.TransformVector(Owner.Vertices[VerticeIndices[0]].Pos),
				1.3f * radius,
				64,
				Color4.Black.Lighten(0.1f),
				Color4.Black.Transparentify(0.2f)
			);
			Renderer.DrawRound(
				transform.TransformVector(Owner.Vertices[VerticeIndices[0]].Pos),
				radius,
				32,
				color,
				color
			);
		}
		public Vector2 InterpolateUv(Vector2 position)
		{
			throw new InvalidOperationException();
		}

		public HashSet<ITangerineGeometryPrimitive> GetAdjacent()
		{
			var adjacent = new HashSet<ITangerineGeometryPrimitive>();
			foreach (var edge in Owner[GeometryPrimitive.Edge]) {
				if (
					edge.VerticeIndices[0] == VerticeIndices[0] ||
					edge.VerticeIndices[1] == VerticeIndices[0]
				) {
					adjacent.Add(edge);
				}
			}
			return adjacent;
		}
	}

	public struct TangerineEdge : ITangerineGeometryPrimitive
	{
		public IGeometry Owner { get; set; }
		public int[] VerticeIndices { get; set; }
		public readonly bool IsFraming;

		public TangerineEdge(IGeometry owner, int vertex1, int vertex2, bool isFraming)
		{
			Owner = owner;
			VerticeIndices = new int[] { vertex1, vertex2 };
			IsFraming = isFraming;
		}

		public bool HitTest(Vector2 position, Matrix32 transform, out float distance, float radius = 1.0f, float scale = 1.0f)
		{
			var p1 = transform.TransformVector(Owner.Vertices[VerticeIndices[0]].Pos);
			var p2 = transform.TransformVector(Owner.Vertices[VerticeIndices[1]].Pos);
			return PolygonMeshUtils.PointLineIntersection(position, p1, p2, radius / scale, out distance);
		}

		public void Move(Vector2 positionDelta)
		{
			Owner.MoveVertices(VerticeIndices, positionDelta);
		}

		public void MoveUv(Vector2 uvDelta)
		{
			Owner.MoveVerticesUv(VerticeIndices, uvDelta);
		}

		public void Render(Matrix32 transform, Color4 color, float radius = 1.0f)
		{
			var p1 = transform.TransformVector(Owner.Vertices[VerticeIndices[0]].Pos);
			var p2 = transform.TransformVector(Owner.Vertices[VerticeIndices[1]].Pos);
			if (IsFraming) {
				Renderer.DrawLine(
					p1,
					p2,
					Color4.Black.Transparentify(0.2f),
					radius
				);
				Renderer.DrawLine(
					p1,
					p2,
					color,
					radius / 2.0f
				);
			} else {
				Renderer.DrawDashedLine(
					p1,
					p2,
					color,
					new Vector2(radius * 2.0f, radius / 2.0f)
				);
			}
		}

		public Vector2 InterpolateUv(Vector2 position)
		{
			var v1 = Owner.Vertices[VerticeIndices[0]];
			var v2 = Owner.Vertices[VerticeIndices[1]];
			var len = Vector2.Distance(v1.Pos, v2.Pos);
			var w1 = 1 - Vector2.Distance(position, v1.Pos) / len;
			var w2 = 1 - w1;
			return w1 * v1.UV1 + w2 * v2.UV1;
		}

		public override bool Equals(object obj)
		{
			if (!(obj is TangerineEdge te)) {
				return false;
			}
			var v11 = VerticeIndices[0];
			var v12 = VerticeIndices[1];
			var v21 = te.VerticeIndices[0];
			var v22 = te.VerticeIndices[1];
			return
				v11 == v21 && v12 == v22 ||
				v11 == v22 && v12 == v21;
		}

		public override int GetHashCode()
		{
			return
				(VerticeIndices[0], VerticeIndices[1]).GetHashCode() +
				(VerticeIndices[1], VerticeIndices[0]).GetHashCode();
		}

		public HashSet<ITangerineGeometryPrimitive> GetAdjacent()
		{
			var adjacent = new HashSet<ITangerineGeometryPrimitive>();
			foreach (var index in VerticeIndices) {
				adjacent.Add(Owner[GeometryPrimitive.Vertex][index]);
				adjacent.UnionWith(Owner[GeometryPrimitive.Vertex][index].GetAdjacent());
			}
			return adjacent;
		}
	}

	public struct TangerineFace : ITangerineGeometryPrimitive
	{
		public IGeometry Owner { get; set; }
		public int[] VerticeIndices { get; set; }

		public TangerineFace(IGeometry owner, int vertex1, int vertex2, int vertex3)
		{
			Owner = owner;
			VerticeIndices = new int[] { vertex1, vertex2, vertex3 };
		}

		public bool HitTest(Vector2 position, Matrix32 transform, out float distance, float radius = 1.0f, float scale = 1.0f)
		{
			var p1 = transform.TransformVector(Owner.Vertices[VerticeIndices[0]].Pos);
			var p2 = transform.TransformVector(Owner.Vertices[VerticeIndices[1]].Pos);
			var p3 = transform.TransformVector(Owner.Vertices[VerticeIndices[2]].Pos);

			p1 += radius / (scale * 2.0f) * ((p2 - p1).Normalized + (p3 - p1).Normalized);
			p2 += radius / (scale * 2.0f) * ((p1 - p2).Normalized + (p3 - p2).Normalized);
			p3 += radius / (scale * 2.0f) * ((p2 - p3).Normalized + (p1 - p3).Normalized);

			distance = 0.0f;
			return PolygonMeshUtils.PointTriangleIntersection(position, p1, p2, p3);
		}

		public void Move(Vector2 positionDelta)
		{
			Owner.MoveVertices(VerticeIndices, positionDelta);
		}

		public void MoveUv(Vector2 uvDelta)
		{
			Owner.MoveVerticesUv(VerticeIndices, uvDelta);
		}

		public void Render(Matrix32 transform, Color4 color, float radius = 1.0f)
		{
			var size = 2;
			var texture = new Texture2D();
			var image = new Color4[size * size];
			for (int y = 0; y < size; ++y) {
				for (int x = 0; x < size; ++x) {
					image[y * size + x] = color.Transparentify(0.8f);
				}
			}
			texture.LoadImage(image, size, size);
			var p1 = transform.TransformVector(Owner.Vertices[VerticeIndices[0]].Pos);
			var p2 = transform.TransformVector(Owner.Vertices[VerticeIndices[1]].Pos);
			var p3 = transform.TransformVector(Owner.Vertices[VerticeIndices[2]].Pos);
			var vertices = new[] {
				new Vertex() { Pos = p1, Color = color },
				new Vertex() { Pos = p2, Color = color },
				new Vertex() { Pos = p3, Color = color },
			};
			Renderer.DrawTriangleStrip(texture, vertices, vertices.Length);
		}

		/// <summary>
		/// https://en.wikipedia.org/wiki/Barycentric_coordinate_system#Conversion_between_barycentric_and_Cartesian_coordinates
		/// </summary>
		public Vector2 InterpolateUv(Vector2 position)
		{
			var v1 = Owner.Vertices[VerticeIndices[0]];
			var v2 = Owner.Vertices[VerticeIndices[1]];
			var v3 = Owner.Vertices[VerticeIndices[2]];

			var w1 =
				((v2.Pos.Y - v3.Pos.Y) * (position.X - v3.Pos.X) + (v3.Pos.X - v2.Pos.X) * (position.Y - v3.Pos.Y)) /
				((v2.Pos.Y - v3.Pos.Y) * (v1.Pos.X - v3.Pos.X) + (v3.Pos.X - v2.Pos.X) * (v1.Pos.Y - v3.Pos.Y));

			var w2 =
				((v3.Pos.Y - v1.Pos.Y) * (position.X - v3.Pos.X) + (v1.Pos.X - v3.Pos.X) * (position.Y - v3.Pos.Y)) /
				((v2.Pos.Y - v3.Pos.Y) * (v1.Pos.X - v3.Pos.X) + (v3.Pos.X - v2.Pos.X) * (v1.Pos.Y - v3.Pos.Y));

			var w3 = 1 - w1 - w2;

			return w1 * v1.UV1 + w2 * v2.UV1 + w3 * v3.UV1;
		}

		public HashSet<ITangerineGeometryPrimitive> GetAdjacent()
		{
			var adjacent = new HashSet<ITangerineGeometryPrimitive>();
			foreach (var index in VerticeIndices) {
				adjacent.Add(Owner[GeometryPrimitive.Vertex][index]);
				adjacent.UnionWith(Owner[GeometryPrimitive.Vertex][index].GetAdjacent());
			}
			return adjacent;
		}
	}
#endif

	public class Geometry : IGeometry
	{
		public struct HalfEdge
		{
			[YuzuMember]
			public int Origin;

			[YuzuMember]
			public int Index;

			[YuzuMember]
			public int Twin;

			public HalfEdge(int index, int origin)
			{
				Origin = origin;
				Index = index;
				Twin = -1;
			}

			public HalfEdge(int index, int origin, int twin)
			{
				Origin = origin;
				Index = index;
				Twin = twin;
			}
		}

		[YuzuMember]
		public List<Vertex> Vertices { get; set; }

		[YuzuMember]
		public List<HalfEdge> HalfEdges { get; set; }

		public static HalfEdge DummyHalfEdge => new HalfEdge(-1, -1);

		public static Vertex DummyVertex => new Vertex();

#if TANGERINE
		private List<int> framingVertices;
		public List<int> FramingVertices
		{
			get
			{
				if (framingVertices == null) {
					framingVertices = new List<int>();
					foreach (var edge in HalfEdges) {
						if (edge.Twin == -1) {
							framingVertices.Add(edge.Origin);
						}
					}
				}
				return framingVertices;
			}
			private set
			{
				framingVertices = value;
			}
		}
		private ITangerineGeometryPrimitive[] tangerineVertices;
		private ITangerineGeometryPrimitive[] tangerineEdges;
		private ITangerineGeometryPrimitive[] tangerineFaces;

		public ITangerineGeometryPrimitive[] this[GeometryPrimitive primitive]
		{
			get
			{
				ITangerineGeometryPrimitive[] primitives = null;
				var facesCount = HalfEdges.Count / 3;
				switch (primitive) {
					case GeometryPrimitive.Vertex:
						if (tangerineVertices == null) {
							primitives = new ITangerineGeometryPrimitive[Vertices.Count];
							for (var i = 0; i < Vertices.Count; ++i) {
								primitives[i] = new TangerineVertex(this, i);
							}
							tangerineVertices = primitives;
						} else {
							primitives = tangerineVertices;
						}
						break;
					case GeometryPrimitive.Edge:
						if (tangerineEdges == null) {
							var edges = new HashSet<ITangerineGeometryPrimitive>();
							for (var i = 0; i < HalfEdges.Count; i += 3) {
								var v1v2 = HalfEdges[i];
								var v2v3 = HalfEdges[i + 1];
								var v3v1 = HalfEdges[i + 2];
								edges.Add(
									new TangerineEdge(this, v1v2.Origin, v2v3.Origin, v1v2.Twin == -1));
								edges.Add(
									new TangerineEdge(this, v2v3.Origin, v3v1.Origin, v2v3.Twin == -1));
								edges.Add(
									new TangerineEdge(this, v3v1.Origin, v1v2.Origin, v3v1.Twin == -1));
							}
							primitives = new ITangerineGeometryPrimitive[edges.Count];
							edges.CopyTo(primitives);
							tangerineEdges = primitives;
						} else {
							primitives = tangerineEdges;
						}
						break;
					case GeometryPrimitive.Face:
						if (tangerineFaces == null) {
							primitives = new ITangerineGeometryPrimitive[facesCount];
							for (var i = 0; i < facesCount; ++i) {
								var v1v2 = HalfEdges[3 * i];
								var v2v3 = HalfEdges[3 * i + 1];
								var v3v1 = HalfEdges[3 * i + 2];
								primitives[i] =
									new TangerineFace(this, v1v2.Origin, v2v3.Origin, v3v1.Origin);
							}
							tangerineFaces = primitives;
						} else {
							primitives = tangerineFaces;
						}
						break;
				}
				return primitives;
			}
		}
#endif

		public Geometry()
		{
			Vertices = new List<Vertex>();
			HalfEdges = new List<HalfEdge>();
		}

		public Geometry(List<Vertex> vertices) : this()
		{
			if (vertices.Count != 4) {
				throw new InvalidOperationException();
			}

			Vertices = vertices;
			Connect(0, 1, 2);
			Connect(2, 1, 3);
			MakeTwins(1, 3);
		}

		public bool TryFindHalfEdgeIndexByVertex(Vertex vertex, out int index)
		{
			foreach (var he in HalfEdges) {
				if (Vertices[he.Origin].Equals(vertex)) {
					index = he.Index;
					return true;
				}
			}
			index = -1;
			return false;
		}

		public bool TryFindHalfEdgeIndexByVertexIndex(int vertexIndex, out int halfEdgeIndex)
		{
			foreach (var he in HalfEdges) {
				if (he.Origin == vertexIndex) {
					halfEdgeIndex = he.Index;
					return true;
				}
			}
			halfEdgeIndex = -1;
			return false;
		}

		public bool TryFindVertexIndex(Vertex vertex, out int index)
		{
			for (var i = 0; i < Vertices.Count; ++i) {
				if (Vertices[i].Equals(vertex)) {
					index = i;
					return true;
				}
			}
			index = -1;
			return false;
		}

		public int FindVertexIndex(Vertex vertex)
		{
			if (!TryFindVertexIndex(vertex, out var index)) {
				throw new InvalidOperationException();
			}
			return index;
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
			System.Diagnostics.Debug.Assert(he1.Origin != he2.Origin && he1.Origin == Next(he2).Origin && he2.Origin == Next(he1).Origin);
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

		public void RemoveVertex(int index)
		{
			Triangulator.Instance.RemoveVertex(this, index);
		}

		public void MoveVertex(int index, Vector2 positionDelta)
		{
			var v = Vertices[index];
			v.Pos += positionDelta;
			Vertices[index] = v;
		}

		public void MoveVertexUv(int index, Vector2 uvDelta)
		{
			var v = Vertices[index];
			v.UV1 += uvDelta;
			Vertices[index] = v;
		}

		public void MoveVertices(int[] indices, Vector2 positionDelta)
		{
			foreach (var index in indices) {
				var v = Vertices[index];
				v.Pos += positionDelta;
				Vertices[index] = v;
			}
		}

		public void MoveVerticesUv(int[] indices, Vector2 uvDelta)
		{
			foreach (var index in indices) {
				var v = Vertices[index];
				v.UV1 += uvDelta;
				Vertices[index] = v;
			}
		}

		public int[] Traverse()
		{
			var order = new int[HalfEdges.Count];
			for (var i = 0; i < HalfEdges.Count; i += 3) {
				order[i] = HalfEdges[i].Origin; 
				order[i + 1] = HalfEdges[i + 1].Origin; 
				order[i + 2] = HalfEdges[i + 2].Origin;
			}
			return order;
		}

		public void AddVertex(Vertex vertex)
		{
			Triangulator.Instance.AddVertex(this, vertex);
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
#if TANGERINE
			framingVertices = null;
			tangerineVertices = null;
			tangerineEdges = null;
			tangerineFaces = null;
#endif
#if DEBUG
			foreach (var halfEdge in HalfEdges) {
				System.Diagnostics.Debug.Assert(halfEdge.Twin == -1 || halfEdge.Index == HalfEdges[halfEdge.Twin].Twin);
			}
#endif
		}
	}
}
