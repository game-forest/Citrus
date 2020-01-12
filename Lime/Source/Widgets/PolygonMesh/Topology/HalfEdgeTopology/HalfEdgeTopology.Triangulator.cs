using System;
using System.Collections.Generic;
using Lime.Source.Widgets.PolygonMesh;

namespace Lime.Widgets.PolygonMesh.Topology.HalfEdgeTopology
{
	public partial class HalfEdgeTopology
	{

		/// <summary>
		/// Adds a vertex to current triangulation using incremental algorithm Bowyer-Watson
		/// algorithm if vertex is completely inside triangulation otherwise
		/// creates triangles on visible boundary and restores delaunay property.
		/// </summary>
		/// <param name="vertexIndex">Vertex index.</param>
		private void AddVertex(int vertexIndex)
		{
			// First: locate the given vertex.
			// Determine whether it's in the specific triangle or
			// lies outside of triangulation.
			var halfEdge = LocateClosestTriangle(vertexIndex, out var isInsideTriangle);
			if (isInsideTriangle) {
				// Check special cases:
				// Same vertex already exists:

				// Vertex lies on the edge:

				// Boywer-Watson algorithm
				Root = BowyerWatson(vertexIndex, halfEdge);
			}
		}


		/// <summary>
		/// Locates a triangle that has specified vertex inside or
		/// closest one on the border to triangulation.
		/// Complexity is O(N) because we assume that triangulation may have concavities
		/// and holes. Further improvement is using spatial information data structures
		/// (e.g. R-tree).
		/// </summary>
		/// <param name="vertexIndex"></param>
		/// <returns>HalfEdge that uniquely identifies triangle</returns>
		private HalfEdge LocateClosestTriangle(int vertexIndex, out bool isInsideTriangle)
		{
			HalfEdge closest = null;
			var vertex = Vertices[vertexIndex].Pos;
			isInsideTriangle = false;
			var minDistance = float.MaxValue;
			foreach (var (e1, e2, e3) in Triangles()) {
				var (p1, p2, p3) = (Vertices[e1.Origin].Pos, Vertices[e2.Origin].Pos, Vertices[e3.Origin].Pos);
				if (
					e1.Origin == vertexIndex || e2.Origin == vertexIndex ||
					e3.Origin == vertexIndex || p1 == vertex  || p2 == vertex ||
					p3 == vertex || VertexInsideTriangle(vertex, e1)
				) {
					isInsideTriangle = true;
					return e1;
				}
				UpdateMinDistance(e1, e2);
				UpdateMinDistance(e2, e3);
				UpdateMinDistance(e3, e1);

				void UpdateMinDistance(HalfEdge s, HalfEdge e)
				{
					var v = Vertices[s.Origin].Pos;
					var w = Vertices[e.Origin].Pos;
					var distance = PointToSegmentSqrDistance(v, w, vertex);
					if (distance < minDistance) {
						minDistance = distance;
						closest = s;
					}
				}
			}
			return closest;
		}

		/// <summary>
		/// Insert vertex into delaunay triangulation.
		/// Complexity depends on triangle location function. In the worst case it's O(N^2) (see
		/// LocateClosestTriangle description). Some techniques like saving last 'touched' triangle
		/// leads to O(N^(3/2)) (assuming that LocateClosestTriangle is O(N)) in general.
		/// </summary>
		/// <param name="vertexIndex">Vertex index.</param>
		/// <param name="start">Optional: triangles that contains given vertex.</param>
		/// <returns>One of the newly constructed triangles.</returns>
		private HalfEdge BowyerWatson(int vertexIndex, HalfEdge start = null)
		{
			if (start == null) {
				// Find triangle that contains given vertex
				start = LocateClosestTriangle(vertexIndex, out var isInsideTriangle);
				if (!isInsideTriangle) {
					throw new InvalidOperationException("There is no triangle in triangulation that contains given vertex.");
				}
			}
			var vertex = Vertices[vertexIndex].Pos;
			if (!VertexInsideTriangle(vertex, start)) {
				throw new InvalidOperationException("There is no triangle in triangulation that contains given vertex.");
			}
			// Delete all triangles that contains given vertex in their circumcircle.
			// Formed polygonal hole P is star-shaped polygon. Inserted vertex is inside
			// kernel of P which mean that entire polygon boundary is visible from that vertex.
			// Connect vertex with edges of P. Constructed triangles will satisfy delaunay property.
			var polygon = new List<HalfEdge>();
			var queue = new Stack<HalfEdge>();
			queue.Push(start.Prev);
			queue.Push(start.Next);
			queue.Push(start);
			while (queue.Count > 0) {
				var halfEdge = queue.Pop();
				if (halfEdge.Twin == null || !InCircumcircle(halfEdge.Twin, vertex)) {
					polygon.Add(halfEdge);
					continue;
				}
				var twin = halfEdge.Twin;
				queue.Push(twin.Prev);
				queue.Push(twin.Next);
				halfEdge.Remove();
				twin.Remove();
			}
			MakeTriangle(polygon[0], vertexIndex);
			for (var i = 1; i < polygon.Count; i++) {
				var side = polygon[i];
				// Construct triangle on the side of polygon
				MakeTriangle(side, vertexIndex);
				// Twin shared edge with previously constructed triangle
				polygon[i - 1].Next.TwinWith(side.Prev);
			}
			polygon[0].Prev.TwinWith(polygon[polygon.Count - 1].Next);
			return polygon[0];
		}

		#region HelperMethods

		/// <summary>
		/// Constructs triangles on the side using given vertex.
		/// </summary>
		/// <param name="edge"></param>
		/// <param name="vertexIndex"></param>
		private static void MakeTriangle(HalfEdge edge, int vertexIndex)
		{
			var next = new HalfEdge(edge.Next.Origin);
			var prev = new HalfEdge(vertexIndex);
			edge.Next = next;
			next.Next = prev;
			prev.Next = edge;
		}

		#endregion

		#region Predicates

		private bool InCircumcircle(HalfEdge triangle, Vector2 vertex)
		{
			var v1 = Vertices[triangle.Origin].Pos;
			var v2 = Vertices[triangle.Next.Origin].Pos;
			var v3 = Vertices[triangle.Prev.Origin].Pos;
			return InCircle(vertex, v1, v2, v3);
		}

		private static bool InCircle(Vector2 vertex, Vector2 v1, Vector2 v2, Vector2 v3) =>
			GeometricPredicates.ExactInCircle(v1.X, v1.Y, v2.X, v2.Y, v3.X, v3.Y, vertex.X, vertex.Y) > 0;

		private static float PointToSegmentSqrDistance(Vector2 v, Vector2 w, Vector2 p)
		{
			var l2 = (w - v).SqrLength;
			if (l2 == 0) {
				return (p - v).SqrLength;
			}
			var t = Mathf.Max(0, Mathf.Min(1, Vector2.DotProduct(p - v, w - v) / l2));
			var proj = v + t * (w - v);
			return (p - proj).SqrLength;
		}

		private bool VertexInsideTriangle(Vector2 vertex, HalfEdge triangle)
		{
			var next = triangle.Next;
			var prev = triangle.Prev;
			var v1 = Vertices[triangle.Origin].Pos;
			var v2 = Vertices[next.Origin].Pos;
			var v3 = Vertices[prev.Origin].Pos;
			return VertexInsideTriangle(vertex, v1, v2, v3);
		}

		private static bool VertexInsideTriangle(Vector2 vertex, Vector2 v1, Vector2 v2, Vector2 v3) =>
			!ArePointsOnOppositeSidesOfSegment(v1, v2, vertex, v3) &&
			!ArePointsOnOppositeSidesOfSegment(v2, v3, vertex, v1) &&
			!ArePointsOnOppositeSidesOfSegment(v3, v1, vertex, v2);

		/// <summary>
		/// Tells weather <paramref name="p1"/> and <paramref name="p2"/> lies on
		/// opposite sides of segment [<paramref name="s1"/>, <paramref name="s2"/>].
		/// </summary>
		/// <param name="s1">Segment start.</param>
		/// <param name="s2">Segment end.</param>
		/// <param name="p1">First point.</param>
		/// <param name="p2">Second point.</param>
		private static bool ArePointsOnOppositeSidesOfSegment(Vector2 s1, Vector2 s2, Vector2 p1, Vector2 p2) =>
			GeometricPredicates.ExactOrient2D(s1.X, s1.Y, s2.X, s2.Y, p1.X, p1.Y) *
			GeometricPredicates.ExactOrient2D(s1.X, s1.Y, s2.X, s2.Y, p2.X, p2.Y) < 0;

		#endregion
	}
}
