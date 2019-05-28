using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lime.PolygonMesh.Structure
{
	public interface IPolygonMeshStructureObject
	{
		PolygonMesh Owner { get; }

		void Move(Vector2 start, Vector2 destination, bool isCtrlPressed);
		void Remove();

		bool HitTest(Vector2 position, float scale = 1.0f);
		void Render(Color4 color);

		IPolygonMeshStructureObject Transform(Matrix32 matrix, bool restoreBeforeTransformation = true);
		void InversionTransform();
	}

	public class HalfEdge
	{
		/// <summary>
		/// Origin-target direction.
		/// </summary>
		internal HalfEdge Next { get; set; }

		/// <summary>
		/// Target-origin direction.
		/// </summary>
		internal HalfEdge Prev { get; set; }

		/// <summary>
		/// Opposite traversal direction half-edge.
		/// </summary>
		internal HalfEdge Twin { get; set; }

		/// <summary>
		/// Relating face.
		/// Right if clockwise vertex traversal direction.
		/// Left if counter clockwise verex traversal direction.
		/// </summary>
		internal PolygonMeshFace Face { get; set; }

		/// <summary>
		/// Parent edge.
		/// </summary>
		internal PolygonMeshEdge Parent { get; set; }

		/// <summary>
		/// Vertex of origin.
		/// </summary>
		internal PolygonMeshVertex Origin { get; set; }

		/// <summary>
		/// Targeting vertex.
		/// </summary>
		internal PolygonMeshVertex Target { get; set; }

		/// <summary>
		/// Euclidian length of an edge.
		/// </summary>
		internal float Length => Vector2.Distance(Origin.Position, Target.Position);

		/// <summary>
		/// Creates a half-edge given two vertices.
		/// </summary>
		/// <param name="origin">Starting vertex.</param>
		/// <param name="target">Destination vertex.</param>
		public HalfEdge(PolygonMeshVertex origin, PolygonMeshVertex target)
		{
			Origin = origin;
			Target = target;
		}
	}

	public class PolygonMeshVertex : IPolygonMeshStructureObject
	{
		public static float Radius { get; } = 4.0f;

		public Matrix32 TransformMatrix { get; private set; } = Matrix32.Identity;

		/// <summary>
		/// PolygonMesh this vertex is a part of.
		/// </summary>
		public PolygonMesh Owner => HalfEdge.Face?.Owner ?? HalfEdge.Twin.Face?.Owner ?? null;

		/// <summary>
		/// Position in space of Owner.
		/// </summary>
		public Vector2 Position { get; internal set; }

		/// <summary>
		/// Texture coordinates. 
		/// </summary>
		public Vector2 UV { get; internal set; }

		/// <summary>
		/// A half-edge that was aliased with this vertex.
		/// </summary>
		internal HalfEdge HalfEdge { get; set; }

		/// <summary>
		/// List of adjacent edges.
		/// </summary>
		internal List<PolygonMeshEdge> AdjacentEdges
		{
			get
			{
				var edges = new List<PolygonMeshEdge>() { HalfEdge.Parent };
				var next = HalfEdge.Twin.Next;
				while (next != null) {
					if (next.Parent == edges[0]) {
						return edges;
					}
					edges.Add(next.Parent);
					next = next.Twin.Next;
				}
				// In case a gap has been found, change traverse
				// direction and add the rest of the adjacent edges.
				// The edges list is reversed to make them consequent.
				if (HalfEdge.Prev != null) {
					edges.Reverse();
					var prev = HalfEdge.Prev;
					while (prev != null) {
						edges.Add(prev.Parent);
						prev = prev.Twin.Prev;
					}
				}
				return edges;
			}
		}

		/// <summary>
		/// Creates a vertex given a position in space of PolygonMesh.
		/// </summary>
		/// <param name="posistion">Desired vertex' position.</param>
		public PolygonMeshVertex(Vector2 position)
		{
			Position = position;
		}

		/// <summary>
		/// Moves a vertex on a difference between given starting position and destination.
		/// </summary>
		/// <param name="start">Movement starting point.</param>
		/// <param name="destination">Movement destination point.</param>
		public void Move(Vector2 start, Vector2 destination, bool isCtrlPressed)
		{
			var d = destination - start;
			Position += d;
			if (isCtrlPressed) {
				UV += d / Owner.Size;
			}
		}

		public void Remove()
		{
			// TO DO
		}

		/// <summary>
		/// Checks whether the given position is in proximity of this vertex.
		/// </summary>
		/// <param name="position">Position in space of this vertex' Owner.</param>
		/// <returns>True if test succeeded, false otherwise.</returns>
		public bool HitTest(Vector2 position, float scale = 1.0f)
		{
			return Vector2.Distance(Position, position) <= Radius / scale;
		}

		/// <summary>
		/// Draws the vertex as a round of given color with a slightly darker outline.
		/// </summary>
		/// <param name="color">Vertex' color.</param>
		public void Render(Color4 color)
		{
			Renderer.DrawRound(Position, 1.3f * Radius, 64, Color4.Black.Lighten(0.1f), Color4.Black.Transparentify(0.2f));
			Renderer.DrawRound(Position, Radius, 32, color, color);
		}

		/// <summary>
		/// Applies transform matrix to its position.
		/// </summary>
		/// <param name="matrix">Transform matrix.</param>
		/// <param name="restoreBeforeTransformation">Should restore transformation before applying this one.</param>
		/// <returns>Vertex with transformed position.</returns>
		public IPolygonMeshStructureObject Transform(Matrix32 matrix, bool restoreBeforeTransformation = true)
		{
			if (restoreBeforeTransformation) {
				InversionTransform();
			}
			Position = matrix.TransformVector(Position);
			TransformMatrix = matrix;
			return this;
		}

		/// <summary>
		/// Restores position to its original value via calculating inversed transform matrix.
		/// </summary>
		public void InversionTransform()
		{
			Position = TransformMatrix.CalcInversed().TransformVector(Position);
			TransformMatrix = Matrix32.Identity;
		}
	}

	public class PolygonMeshEdge : IPolygonMeshStructureObject
	{
		/// <summary>
		/// PolygonMesh this edge is a part of.
		/// </summary>
		public PolygonMesh Owner => AB.Face?.Owner ?? AB.Twin.Face?.Owner ?? null;

		/// <summary>
		/// A half-edge that was aliased with this edge.
		/// </summary>
		internal HalfEdge AB { get; set; }

		/// <summary>
		/// Determines whether this edge is a bounding one
		/// via checking if it's adjacent to the only face.
		/// </summary>
		public bool IsFraming => AB.Face != null && AB.Twin.Face == null || AB.Face == null && AB.Twin.Face != null;

		/// <summary>
		/// Determines whether this edge can be safely removed.
		/// </summary>
		public bool IsRedundant => AB.Face == null && AB.Twin.Face == null;

		/// <summary>
		/// Creates an edge between two given vertices.
		/// </summary>
		/// <param name="A">Starting vertex.</param>
		/// <param name="B">Destination vertex.</param>
		public PolygonMeshEdge(PolygonMeshVertex A, PolygonMeshVertex B)
		{
			var AB = new HalfEdge(A, B) {
				Parent = this
			};
			var BA = new HalfEdge(B, A) {
				Parent = this
			};
			AB.Twin = BA;
			BA.Twin = AB;
			if (A.HalfEdge == null) {
				A.HalfEdge = AB;
			}
			if (B.HalfEdge == null) {
				B.HalfEdge = BA;
			}
			this.AB = AB;
		}

		/// <summary>
		/// Moves the edge on a difference between given starting position and destination.
		/// </summary>
		/// <param name="start">Movement starting point.</param>
		/// <param name="destination">Movement destination point.</param>
		public void Move(Vector2 start, Vector2 destination, bool isCtrlPressed)
		{
			AB.Origin.Move(start, destination, isCtrlPressed);
			AB.Target.Move(start, destination, isCtrlPressed);
		}

		public void Remove()
		{
			// TO DO
		}

		/// <summary>
		/// Checks whether the given position is in proximity of this edge.
		/// </summary>
		/// <param name="position">Position in space of this edge's Owner.</param>
		/// <returns>True if test succeeded, false otherwise.</returns>
		public bool HitTest(Vector2 position, float scale = 1.0f)
		{
			return
				DistanceFromPoint(position, out var intersectionPoint) <= PolygonMeshVertex.Radius / (scale * 2.0f) &&
				(AB.Origin.Position - intersectionPoint).Length <= AB.Length - PolygonMeshVertex.Radius / scale &&
				(AB.Target.Position - intersectionPoint).Length <= AB.Length - PolygonMeshVertex.Radius / scale;
		}

		/// <summary>
		/// Draws the edge as a line.
		/// </summary>
		/// <param name="color">Edge's color.</param>
		public void Render(Color4 color)
		{
			if (IsFraming) {
				Renderer.DrawLine(AB.Origin.Position, AB.Target.Position, Color4.Black.Transparentify(0.2f), PolygonMeshVertex.Radius);
				Renderer.DrawLine(AB.Origin.Position, AB.Target.Position, color, PolygonMeshVertex.Radius / 2.0f);
			} else {
				Renderer.DrawDashedLine(AB.Origin.Position, AB.Target.Position, color, new Vector2(PolygonMeshVertex.Radius * 2.0f, PolygonMeshVertex.Radius / 2.0f));
			}
		}

		/// <summary>
		/// http://paulbourke.net/geometry/pointlineplane/
		/// </summary>
		/// <param name="point">Position in space of this edge's Owner.</param>
		/// <returns>Distance from given point to this edge.</returns>
		private float DistanceFromPoint(Vector2 point, out Vector2 intersectionPoint)
		{
			var A = AB.Origin.Position;
			var B = AB.Target.Position;
			var d = Vector2.Distance(A, B);
			if (d <= 1.0f) {
				intersectionPoint = A;
				return Vector2.Distance(A, point);
			}

			var u = ((point.X - A.X) * (B.X - A.X) + (point.Y - A.Y) * (B.Y - A.Y)) / (d * d);
			intersectionPoint = new Vector2(A.X + u * (B.X - A.X), A.Y + u * (B.Y - A.Y));
			return Vector2.Distance(intersectionPoint, point);
		}

		/// <summary>
		/// Creates a clone applying transform matrix to A and B.
		/// </summary>
		/// <param name="matrix">Transform matrix.</param>
		/// <returns>New instance of an edge with transformed vertices.</returns>
		public IPolygonMeshStructureObject Transform(Matrix32 matrix, bool restoreBeforeTransformation = true)
		{
			AB.Origin.Transform(matrix, restoreBeforeTransformation);
			AB.Target.Transform(matrix, restoreBeforeTransformation);
			return this;
		}

		/// <summary>
		/// Restores AB vertices' positions to their original values via calculating inversed transform matrix.
		/// </summary>
		public void InversionTransform()
		{
			AB.Origin.InversionTransform();
			AB.Target.InversionTransform();
		}
	}

	public class PolygonMeshFace : IPolygonMeshStructureObject
	{
		/// <summary>
		/// PolygonMesh this face is a part of.
		/// </summary>
		public PolygonMesh Owner { get; internal set; }

		/// <summary>
		/// A half-edge that was aliased with this face.
		/// Face can be traversed given only one half-edgMe.
		/// </summary>
		public HalfEdge Root { get; internal set; }

		/// <summary>
		/// Three vertices that determine this face.
		/// </summary>
		public PolygonMeshVertex[] Vertices => new[] { Root.Origin, Root.Next.Origin, Root.Prev.Origin };

		/// <summary>
		/// Three edges that determine this face.
		/// </summary>
		public PolygonMeshEdge[] Edges => new[] { Root.Parent, Root.Next.Parent, Root.Prev.Parent };

		/// <summary>
		/// Constructs a new face given desired triangle vertice' coordinates.
		/// Takes AB segment as its root.
		/// </summary>
		/// <param name="A">First vertex.</param>
		/// <param name="B">Second vertex.</param>
		/// <param name="C">Third vertex.</param>
		public PolygonMeshFace(PolygonMeshVertex A, PolygonMeshVertex B, PolygonMeshVertex C)
		{
			if (A == null || B == null || C == null) {
				throw new ArgumentNullException();
			}
			Connect(
				new PolygonMeshEdge(A, B).AB,
				new PolygonMeshEdge(B, C).AB,
				new PolygonMeshEdge(C, A).AB
			);
		}

		/// <summary>
		/// Constructs a new face given a half-edge and a coordinate.
		/// Takes half-edge twin as its root.
		/// </summary>
		/// <param name="root">Potential Root of this face.</param>
		/// <param name="point">Point that will be included in a triangle given root's AB.</param>
		public PolygonMeshFace(HalfEdge root, PolygonMeshVertex point)
		{
			var vertices = new[] {
				root.Twin.Origin,
				root.Twin.Target,
				point,
			};
			Connect(
				root.Twin,
				new PolygonMeshEdge(vertices[1], vertices[2]).AB,
				new PolygonMeshEdge(vertices[2], vertices[0]).AB
			);
		}

		/// <summary>
		/// Connects three given edges consecutively into a triangle.
		/// </summary>
		/// <param name="edges">Three half-edges to be connected.</param>
		private void Connect(params HalfEdge[] edges)
		{
			for (var i = 0; i < 3; ++i) {
				edges[i].Next = edges[(i + 1) % 3];
			}
			for (var i = 2; i >= 0; --i) {
				edges[i].Prev = edges[i - 1 >= 0 ? i - 1 : 2];
			}
			foreach (var edge in edges) {
				edge.Face = this;
			}
			Root = edges[0];
		}

		/// <summary>
		/// Finds the half-edge opposite to the given vertex inside current face.
		/// </summary>
		/// <param name="vertex">Vertex opposite of which the edge will be looked up.</param>
		/// <returns>Half-edge of the current face that is opposite to the given vertex. Null in case the vertex doesn't belong to current face.</returns>
		internal HalfEdge GetOppositeEdge(PolygonMeshVertex vertex)
		{
			foreach (var v in Vertices) {
				if (v == vertex) {
					goto vertex_found;
				}
			}
			return null;

		vertex_found:
			var e = Root;
			while (e.Origin == vertex || e.Target == vertex) {
				e = e.Next;
			}
			return e;
		}

		public void Move(Vector2 start, Vector2 destination, bool isCtrlPressed)
		{
			Root.Origin.Move(start, destination, isCtrlPressed);
			Root.Next.Origin.Move(start, destination, isCtrlPressed);
			Root.Prev.Origin.Move(start, destination, isCtrlPressed);
		}

		public void Remove()
		{
		}

		/// <summary>
		/// Checks whether the given position is inside of this face.
		/// </summary>
		/// <param name="position">Position in space of this face's Owner.</param>
		/// <returns></returns>
		public bool HitTest(Vector2 position, float scale = 1.0f)
		{
			var A = Root.Origin.Position;
			var B = Root.Next.Origin.Position;
			var C = Root.Prev.Origin.Position;

			A += PolygonMeshVertex.Radius / (scale * 2.0f) * ((B - A).Normalized + (C - A).Normalized);
			B += PolygonMeshVertex.Radius / (scale * 2.0f) * ((A - B).Normalized + (C - B).Normalized);
			C += PolygonMeshVertex.Radius / (scale * 2.0f) * ((B - C).Normalized + (A - C).Normalized);

			var d1 = CrossProduct(position, A, B);
			var d2 = CrossProduct(position, B, C);
			var d3 = CrossProduct(position, C, A);

			return !(
				((d1 < 0) || (d2 < 0) || (d3 < 0)) &&
				((d1 > 0) || (d2 > 0) || (d3 > 0))
			);
		}

		/// <summary>
		/// Draws the triangle as a solid transparent texture inside its area.
		/// </summary>
		/// <param name="color">Face's color.</param>
		public void Render(Color4 color)
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
			var vertices = new[] {
				new Vertex() { Pos = Root.Origin.Position, Color = color },
				new Vertex() { Pos = Root.Target.Position, Color = color },
				new Vertex() { Pos = Root.Prev.Origin.Position, Color = color },
			};
			Renderer.DrawTriangleStrip(texture, vertices, vertices.Length);
		}

		private float CrossProduct(Vector2 A, Vector2 B, Vector2 C)
		{
			return
				(A.X - C.X) * (B.Y - C.Y) -
				(B.X - C.X) * (A.Y - C.Y);
		}

		/// <summary>
		/// Creates a clone applying transform matrix to vertices.
		/// </summary>
		/// <param name="matrix">Transform matrix.</param>
		/// <returns>New instance of a face with transformed vertices.</returns>
		public IPolygonMeshStructureObject Transform(Matrix32 matrix, bool restoreBeforeTransformation = true)
		{
			Root.Origin.Transform(matrix, restoreBeforeTransformation);
			Root.Next.Origin.Transform(matrix, restoreBeforeTransformation);
			Root.Prev.Origin.Transform(matrix, restoreBeforeTransformation);
			return this;
		}

		/// <summary>
		/// Restores vertices' positions to their original values via calculating inversed transform matrix.
		/// </summary>
		public void InversionTransform()
		{
			Root.Origin.InversionTransform();
			Root.Next.Origin.InversionTransform();
			Root.Prev.Origin.InversionTransform();
		}
	}
}
