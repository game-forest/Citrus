using System;
using System.Collections.Generic;
using System.Linq;
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

	public interface IGeometry : IAnimable
	{
		List<Vertex> Vertices { get; set; }
		List<int> IndexBuffer { get; set; }
#if TANGERINE
		List<int> FramingVertices { get; }
#endif
		ITangerineGeometryPrimitive[] this[GeometryPrimitive primitive] { get; }

		void AddVertex(Vertex vertex);
		void RemoveVertex(int index, bool keepConstrainedEdges = false);
		void MoveVertex(int index, Vector2 positionDelta);
		void MoveVertices(int[] indices, Vector2 positionDelta);
		void MoveVertexUv(int index, Vector2 uvDelta);
		void MoveVerticesUv(int[] indices, Vector2 uvDelta);
		void SetConstrain(int index, bool constrained);
#if TANGERINE
		void ResetCache();
#endif
	}

#if TANGERINE
	public interface ITangerineGeometryPrimitive
	{
		IGeometry Owner { get; set; }
		int[] VerticeIndices { get; set; }

		bool HitTest(Vector2 position, Matrix32 transform, out float distance, float scale = 1.0f);
		void Move(Vector2 positionDelta);
		void MoveUv(Vector2 uvDelta);
		void Render(Matrix32 transform);
		void RenderHovered(Matrix32 transform, bool isRemoving = false);
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

		public bool HitTest(Vector2 position, Matrix32 transform, out float distance, float scale = 1.0f)
		{
			var p = transform.TransformVector(Owner.Vertices[VerticeIndices[0]].Pos);
			return PolygonMeshUtils.PointPointIntersection(
				p, position, Theme.Metrics.PolygonMeshVertexHitTestRadius / scale, out distance
			);
		}

		public void Move(Vector2 positionDelta)
		{
			var v = Owner.Vertices[VerticeIndices[0]];
			v.Pos += positionDelta;
			Owner.Vertices[VerticeIndices[0]] = v;
		}

		public void MoveUv(Vector2 uvDelta)
		{
			Owner.MoveVertexUv(VerticeIndices[0], uvDelta);
		}

		public void Render(Matrix32 transform)
		{
			PolygonMeshUtils.RenderVertex(
				transform.TransformVector(Owner.Vertices[VerticeIndices[0]].Pos),
				Theme.Metrics.PolygonMeshBackgroundVertexRadius,
				Theme.Metrics.PolygonMeshVertexRadius,
				Theme.Colors.PolygonMeshVertexBackgroundColor,
				Theme.Colors.PolygonMeshVertexColor
			);
		}

		public void RenderHovered(Matrix32 transform, bool isRemoving = false)
		{
			PolygonMeshUtils.RenderVertex(
				transform.TransformVector(Owner.Vertices[VerticeIndices[0]].Pos),
				1.3f * Theme.Metrics.PolygonMeshBackgroundVertexRadius,
				1.3f * Theme.Metrics.PolygonMeshVertexRadius,
				Theme.Colors.PolygonMeshHoverColor.Darken(0.7f),
				isRemoving ? Theme.Colors.PolygonMeshRemovalColor : Theme.Colors.PolygonMeshHoverColor
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
		public readonly bool IsConstrained;

		public TangerineEdge(IGeometry owner, int vertex1, int vertex2, bool isFraming, bool isConstrained)
		{
			Owner = owner;
			VerticeIndices = new[] { vertex1, vertex2 };
			IsFraming = isFraming;
			IsConstrained = isConstrained;
		}

		public bool HitTest(Vector2 position, Matrix32 transform, out float distance, float scale = 1.0f)
		{
			var p1 = transform.TransformVector(Owner.Vertices[VerticeIndices[0]].Pos);
			var p2 = transform.TransformVector(Owner.Vertices[VerticeIndices[1]].Pos);
			return PolygonMeshUtils.PointLineIntersection(
				position, p1, p2, Theme.Metrics.PolygonMeshEdgeHitTestRadius / scale, out distance
			);
		}

		public void Move(Vector2 positionDelta)
		{
			foreach (var i in VerticeIndices) {
				var v = Owner.Vertices[i];
				v.Pos += positionDelta;
				Owner.Vertices[i] = v;
			}
		}

		public void MoveUv(Vector2 uvDelta)
		{
			Owner.MoveVerticesUv(VerticeIndices, uvDelta);
		}

		public void Render(Matrix32 transform)
		{
			var foregroundColor = Theme.Colors.PolygonMeshInnerEdgeColor;
			var backgroundColor = Theme.Colors.PolygonMeshInnerEdgeBackgroundColor;
			var foregroundSize = new Vector2(
				Theme.Metrics.PolygonMeshBackgroundEdgeThickness * 2.0f,
				Theme.Metrics.PolygonMeshEdgeThickness
			);
			var backgroundSize = new Vector2(
				Theme.Metrics.PolygonMeshBackgroundEdgeThickness * 2.0f,
				Theme.Metrics.PolygonMeshBackgroundEdgeThickness
			);
			if (IsFraming) {
				foregroundColor = Theme.Colors.PolygonMeshFramingEdgeColor;
				backgroundColor = Theme.Colors.PolygonMeshFramingEdgeBackgroundColor;
				foregroundSize = new Vector2(Theme.Metrics.PolygonMeshEdgeThickness);
				backgroundSize = new Vector2(Theme.Metrics.PolygonMeshBackgroundEdgeThickness * 1.7f);
			}
			else if (IsConstrained) {
				foregroundColor = Theme.Colors.PolygonMeshFixedEdgeColor;
				backgroundColor = Theme.Colors.PolygonMeshFixedEdgeBackgroundColor;
				foregroundSize = new Vector2(Theme.Metrics.PolygonMeshEdgeThickness);
				backgroundSize = new Vector2(Theme.Metrics.PolygonMeshBackgroundEdgeThickness);
			}
			PolygonMeshUtils.RenderLine(
				transform.TransformVector(Owner.Vertices[VerticeIndices[0]].Pos),
				transform.TransformVector(Owner.Vertices[VerticeIndices[1]].Pos),
				backgroundSize,
				foregroundSize,
				backgroundColor,
				foregroundColor,
				!(IsFraming || IsConstrained)
			);
		}

		public void RenderHovered(Matrix32 transform, bool isRemoving = false)
		{
			var foregroundColor =
				isRemoving ?
				Theme.Colors.PolygonMeshRemovalColor :
				Theme.Colors.PolygonMeshHoverColor;
			var backgroundColor = Theme.Colors.PolygonMeshInnerEdgeBackgroundColor;
			var foregroundSize = new Vector2(
				Theme.Metrics.PolygonMeshBackgroundEdgeThickness * 2.0f,
				Theme.Metrics.PolygonMeshEdgeThickness
			);
			var backgroundSize = new Vector2(
				Theme.Metrics.PolygonMeshBackgroundEdgeThickness * 2.0f,
				Theme.Metrics.PolygonMeshBackgroundEdgeThickness
			);
			if (IsFraming) {
				backgroundColor = Theme.Colors.PolygonMeshFramingEdgeBackgroundColor;
				foregroundSize = new Vector2(Theme.Metrics.PolygonMeshEdgeThickness);
				backgroundSize = new Vector2(Theme.Metrics.PolygonMeshBackgroundEdgeThickness * 1.7f);
			}
			else if (IsConstrained) {
				backgroundColor = Theme.Colors.PolygonMeshFixedEdgeBackgroundColor;
				foregroundSize = new Vector2(Theme.Metrics.PolygonMeshEdgeThickness);
				backgroundSize = new Vector2(Theme.Metrics.PolygonMeshBackgroundEdgeThickness);
			}
			PolygonMeshUtils.RenderLine(
				transform.TransformVector(Owner.Vertices[VerticeIndices[0]].Pos),
				transform.TransformVector(Owner.Vertices[VerticeIndices[1]].Pos),
				backgroundSize,
				foregroundSize,
				backgroundColor,
				foregroundColor,
				!(IsFraming || IsConstrained)
			);
		}

		public Vector2 InterpolateUv(Vector2 position)
		{
			var v1 = Owner.Vertices[VerticeIndices[0]];
			var v2 = Owner.Vertices[VerticeIndices[1]];
			var weights = PolygonMeshUtils.CalcSegmentRelativeBarycentricCoordinates(position, v1.Pos, v2.Pos);
			return weights[0] * v1.UV1 + weights[1] * v2.UV1;
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

		public bool HitTest(Vector2 position, Matrix32 transform, out float distance, float scale = 1.0f)
		{
			var p1 = transform.TransformVector(Owner.Vertices[VerticeIndices[0]].Pos);
			var p2 = transform.TransformVector(Owner.Vertices[VerticeIndices[1]].Pos);
			var p3 = transform.TransformVector(Owner.Vertices[VerticeIndices[2]].Pos);

			p1 += Theme.Metrics.PolygonMeshEdgeHitTestRadius / (scale * 2.0f) * ((p2 - p1).Normalized + (p3 - p1).Normalized);
			p2 += Theme.Metrics.PolygonMeshEdgeHitTestRadius / (scale * 2.0f) * ((p1 - p2).Normalized + (p3 - p2).Normalized);
			p3 += Theme.Metrics.PolygonMeshEdgeHitTestRadius / (scale * 2.0f) * ((p2 - p3).Normalized + (p1 - p3).Normalized);

			distance = 0.0f;
			return PolygonMeshUtils.PointTriangleIntersection(position, p1, p2, p3);
		}

		public void Move(Vector2 positionDelta)
		{
			foreach (var i in VerticeIndices) {
				var v = Owner.Vertices[i];
				v.Pos += positionDelta;
				Owner.Vertices[i] = v;
			}
		}

		public void MoveUv(Vector2 uvDelta)
		{
			Owner.MoveVerticesUv(VerticeIndices, uvDelta);
		}

		public void Render(Matrix32 transform)
		{
		}

		public void RenderHovered(Matrix32 transform, bool isRemoving = false)
		{
			PolygonMeshUtils.RenderTriangle(
				transform.TransformVector(Owner.Vertices[VerticeIndices[0]].Pos),
				transform.TransformVector(Owner.Vertices[VerticeIndices[1]].Pos),
				transform.TransformVector(Owner.Vertices[VerticeIndices[2]].Pos),
				isRemoving ? Theme.Colors.PolygonMeshRemovalColor : Theme.Colors.PolygonMeshHoverColor
			);
		}

		/// <summary>
		/// https://en.wikipedia.org/wiki/Barycentric_coordinate_system#Conversion_between_barycentric_and_Cartesian_coordinates
		/// </summary>
		public Vector2 InterpolateUv(Vector2 position)
		{
			var v1 = Owner.Vertices[VerticeIndices[0]];
			var v2 = Owner.Vertices[VerticeIndices[1]];
			var v3 = Owner.Vertices[VerticeIndices[2]];
			var weights = PolygonMeshUtils.CalcTriangleRelativeBarycentricCoordinates(position, v1.Pos, v2.Pos, v3.Pos);
			return weights[0] * v1.UV1 + weights[1] * v2.UV1 + weights[2] * v3.UV1;
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

			[YuzuMember]
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

		public IAnimable Owner { get; set; }

		[YuzuMember]
		public List<Vertex> Vertices { get; set; }

		[YuzuMember]
		public List<int> IndexBuffer { get; set; }

		[YuzuMember]
		public List<HalfEdge> HalfEdges { get; set; }

		private Triangulator Triangulator { get; }
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
									new TangerineEdge(this, v1v2.Origin, v2v3.Origin, v1v2.Twin == -1, v1v2.Constrained));
								edges.Add(
									new TangerineEdge(this, v2v3.Origin, v3v1.Origin, v2v3.Twin == -1, v2v3.Constrained));
								edges.Add(
									new TangerineEdge(this, v3v1.Origin, v1v2.Origin, v3v1.Twin == -1, v3v1.Constrained));
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

		public static HalfEdge DummyHalfEdge => new HalfEdge(-1, -1);

		public static Vertex DummyVertex => new Vertex();

		public Geometry()
		{
			Vertices = new List<Vertex>();
			IndexBuffer = new List<int>();
			HalfEdges = new List<HalfEdge>();
			Triangulator = new Triangulator(this);
		}

		public Geometry(List<Vertex> vertices, List<int> indexBuffer, IAnimable owner)
		{
			if (vertices.Count != 4) {
				throw new InvalidOperationException();
			}
			Triangulator = new Triangulator(this);
			Owner = owner;
			Vertices = vertices;
			IndexBuffer = indexBuffer;
			HalfEdges = new List<HalfEdge>();

			Connect(0, 1, 2);
			Connect(2, 1, 3);
			MakeTwins(1, 3);
			Traverse();
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

		public void MoveVertex(int index, Vector2 positionDelta)
		{
			Triangulator.RemoveVertex(this, index);
			var v = Vertices[index];
			v.Pos += positionDelta;
			Vertices[index] = v;
			Triangulator.AddVertex(this, index);
			Invalidate();
			Traverse();
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

		public void AddVertex(Vertex vertex)
		{
			Vertices.Add(vertex);
			Triangulator.AddVertex(this, Vertices.Count - 1);
			Invalidate();
			Traverse();
		}

#if TANGERINE
		public void ResetCache()
		{
			framingVertices = null;
			tangerineVertices = null;
			tangerineEdges = null;
			tangerineFaces = null;
		}
#endif
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
#if TANGERINE
			ResetCache();
#endif
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
