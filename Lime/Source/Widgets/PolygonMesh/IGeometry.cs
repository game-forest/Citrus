using System;
using System.Collections.Generic;

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
		ITangerineGeometryPrimitive[] this[GeometryPrimitive primitive] { get; }

		void AddVertex(Vertex vertex);
		void MoveVertex(int index, Vector2 positionDelta);
		int[] Traverse();
	}

	public interface ITangerineGeometryPrimitive
	{
		IGeometry Owner { get; set; }
		int[] VerticeIndices { get; set; }

		bool HitTest(Vector2 position, Matrix32 transform, float radius = 1.0f, float scale = 1.0f);
		void Move(Vector2 positionDelta);
		void Render(Matrix32 transform, Color4 color, float radius = 1.0f);
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

		public bool HitTest(Vector2 position, Matrix32 transform, float radius = 1.0f, float scale = 1.0f)
		{
			var p = transform.TransformVector(Owner.Vertices[VerticeIndices[0]].Pos);
			return Vector2.Distance(p, position) <= radius / scale;
		}

		public void Move(Vector2 positionDelta)
		{
			Owner.MoveVertex(VerticeIndices[0], positionDelta);
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

		public bool HitTest(Vector2 position, Matrix32 transform, float radius = 1.0f, float scale = 1.0f)
		{
			var p1 = transform.TransformVector(Owner.Vertices[VerticeIndices[0]].Pos);
			var p2 = transform.TransformVector(Owner.Vertices[VerticeIndices[1]].Pos);
			var len = Vector2.Distance(p1, p2);
			return
				DistanceFromPoint(p1, p2, position, out var intersectionPoint) <= radius / (scale * 2.0f) &&
				(p1 - intersectionPoint).Length <= len - radius / scale &&
				(p2 - intersectionPoint).Length <= len - radius / scale;
		}

		private float DistanceFromPoint(Vector2 p1, Vector2 p2, Vector2 p3, out Vector2 intersectionPoint)
		{
			var d = Vector2.Distance(p1, p2);
			if (d <= 1.0f) {
				intersectionPoint = p1;
				return Vector2.Distance(p1, p3);
			}

			var u = ((p3.X - p1.X) * (p2.X - p1.X) + (p3.Y - p1.Y) * (p2.Y - p1.Y)) / (d * d);
			intersectionPoint = new Vector2(p1.X + u * (p2.X - p1.X), p1.Y + u * (p2.Y - p1.Y));
			return Vector2.Distance(intersectionPoint, p3);
		}

		public void Move(Vector2 positionDelta)
		{
			Owner.MoveVertex(VerticeIndices[0], positionDelta);
			Owner.MoveVertex(VerticeIndices[1], positionDelta);
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

		public bool HitTest(Vector2 position, Matrix32 transform, float radius = 1.0f, float scale = 1.0f)
		{
			var p1 = transform.TransformVector(Owner.Vertices[VerticeIndices[0]].Pos);
			var p2 = transform.TransformVector(Owner.Vertices[VerticeIndices[1]].Pos);
			var p3 = transform.TransformVector(Owner.Vertices[VerticeIndices[2]].Pos);

			p1 += radius / (scale * 2.0f) * ((p2 - p1).Normalized + (p3 - p1).Normalized);
			p2 += radius / (scale * 2.0f) * ((p1 - p2).Normalized + (p3 - p2).Normalized);
			p3 += radius / (scale * 2.0f) * ((p2 - p3).Normalized + (p1 - p3).Normalized);

			var d1 = CrossProduct(position, p1, p2);
			var d2 = CrossProduct(position, p2, p3);
			var d3 = CrossProduct(position, p3, p1);

			return !(
				((d1 < 0) || (d2 < 0) || (d3 < 0)) &&
				((d1 > 0) || (d2 > 0) || (d3 > 0))
			);
		}

		private float CrossProduct(Vector2 p1, Vector2 p2, Vector2 p3)
		{
			return
				(p1.X - p3.X) * (p2.Y - p3.Y) -
				(p2.X - p3.X) * (p1.Y - p3.Y);
		}

		public void Move(Vector2 positionDelta)
		{
			Owner.MoveVertex(VerticeIndices[0], positionDelta);
			Owner.MoveVertex(VerticeIndices[1], positionDelta);
			Owner.MoveVertex(VerticeIndices[2], positionDelta);
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
	}

	public class Geometry : IGeometry
	{
		public struct HalfEdge
		{
			public int Origin;
			public int Index;
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

		public List<Vertex> Vertices { get; set; }

		public List<HalfEdge> HalfEdges { get; set; }

		public List<int> Faces { get; set; }

		public HalfEdge DummyHalfEdge => new HalfEdge(-1, -1);

		public Vertex DummyVertex => new Vertex();

		public ITangerineGeometryPrimitive[] this[GeometryPrimitive primitive]
		{
			get
			{
				ITangerineGeometryPrimitive[] objects = null;
				switch (primitive) {
					case GeometryPrimitive.Vertex:
						objects = new ITangerineGeometryPrimitive[Vertices.Count];
						for (var i = 0; i < Vertices.Count; ++i) {
							objects[i] = new TangerineVertex(this, i);
						}
						break;
					case GeometryPrimitive.Edge:
						objects = new ITangerineGeometryPrimitive[Faces.Count * 3];
						for (var i = 0; i < Faces.Count; ++i) {
							var v1v2 = HalfEdges[Faces[i]];
							var v2v3 = HalfEdges[Next(Faces[i])];
							var v3v1 = HalfEdges[Prev(Faces[i])];
							objects[3 * i] =
								new TangerineEdge(this, v1v2.Origin, v2v3.Origin, v1v2.Twin == -1);
							objects[3 * i + 1] =
								new TangerineEdge(this, v2v3.Origin, v3v1.Origin, v2v3.Twin == -1);
							objects[3 * i + 2] =
								new TangerineEdge(this, v3v1.Origin, v1v2.Origin, v3v1.Twin == -1);
						}
						break;
					case GeometryPrimitive.Face:
						objects = new ITangerineGeometryPrimitive[Faces.Count];
						for (var i = 0; i < Faces.Count; ++i) {
							var v1v2 = HalfEdges[Faces[i]];
							var v2v3 = HalfEdges[Next(Faces[i])];
							var v3v1 = HalfEdges[Prev(Faces[i])];
							objects[i] =
								new TangerineFace(this, v1v2.Origin, v2v3.Origin, v3v1.Origin);
						}
						break;
				}
				return objects;
			}
		}

		public Geometry()
		{
			Vertices = new List<Vertex>();
			HalfEdges = new List<HalfEdge>();
			Faces = new List<int>();
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
			Faces.Add(i);
		}

		public void Connect(HalfEdge he1, HalfEdge he2, int vertex)
		{
			var i = HalfEdges.Count;
			var new1 = new HalfEdge(i, he1.Origin, he1.Twin);
			var new2 = new HalfEdge(i + 1, he2.Origin, he2.Twin);
			var new3 = new HalfEdge(i + 1, vertex);
			if (he1.Twin != -1) {
				MakeTwins(i, he1.Twin);
			}
			if (he2.Twin != -1) {
				MakeTwins(i + 1, he2.Twin);
			}
			HalfEdges.Add(new1);
			HalfEdges.Add(new2);
			HalfEdges.Add(new3);
			Faces.Add(i);
		}

		public void MoveVertex(int index, Vector2 positionDelta)
		{
			var v = Vertices[index];
			v.Pos += positionDelta;
			Vertices[index] = v;
		}

		public int[] Traverse()
		{
			var order = new int[Faces.Count * 3];
			for (var i = 0; i < Faces.Count; ++i) {
				order[3 * i] = HalfEdges[Faces[i]].Origin; 
				order[3 * i + 1] = HalfEdges[Next(Faces[i])].Origin; 
				order[3 * i + 2] = HalfEdges[Prev(Faces[i])].Origin;
			}
			return order;
		}

		public void AddVertex(Vertex vertex)
		{
			Vertices.Add(vertex);
			// TO DO: Call triangulator.
		}
	}
}
