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
			} else {
				// Find boundary of triangulation that is visible from given vertex.
				// Build triangles on the sides of the visible boundary.
				// Use flip algorithm to restore delaunay property.
				var visibleBoundary = GetVisibleBoundary(vertexIndex, halfEdge);
				// Should probably triangulate only leftmost\rightmost\closest chain
				// of visible edges (for better UE).
				var edgesToCheck = new List<HalfEdge>(visibleBoundary[0].Count * 2);
				HalfEdge lastConstructedTriangle = null;
				foreach (var edge in visibleBoundary[0]) {
					var twin = new HalfEdge(edge.Next.Origin) {
						Next = new HalfEdge(edge.Origin) {
							Next = new HalfEdge(vertexIndex),
						}
					};
					twin.Prev.Next = twin;
					edge.TwinWith(twin);
					lastConstructedTriangle?.Prev.TwinWith(twin.Next);
					lastConstructedTriangle = twin;
					edgesToCheck.Add(edge);
					edgesToCheck.Add(twin.Next);
				}
				RestoreDelaunayProperty(edgesToCheck);
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
				halfEdge.Detach();
				twin.Detach();
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

		/// <summary>
		/// Gets boundary of triangulation that is visible from <paramref name="sightPoint"/>.
		/// Edges of visible boundary are in clockwise order.
		/// </summary>
		/// <param name="sightPoint"></param>
		/// <param name="start"></param>
		/// <returns></returns>
		private List<List<HalfEdge>> GetVisibleBoundary(int sightPoint, HalfEdge start = null)
		{
			if (start == null) {
				// Find triangle that contains given vertex
				start = LocateClosestTriangle(sightPoint, out var isInsideTriangle);
				if (isInsideTriangle) {
					throw new InvalidOperationException("Sight point is inside triangulation.");
				}
			}
			var vertex = Vertices[sightPoint].Pos;
			// Ensure that 'start' is edge of the boundary (Twin is null).
			// Find boundary edge otherwise.
			if (start.Twin != null) {
				// Check which HalfEdge is closer to sightPoint `start` or it's twin.
				var d1 = (vertex - Vertices[start.Origin].Pos).SqrLength;
				var d2 = (vertex - Vertices[start.Twin.Origin].Pos).SqrLength;
				start = d1 > d2 ? start : start.Twin;
				// Check adjacent edges (for start.Origin) until boundary edge is found.
				while (start.Twin != null) {
					start = start.Twin.Prev;
				}
			}
			// We assume that our triangulation may be non-convex.
			// It means that it is not enough to check triplets
			// of vertices (edge + sightPoint) if they clockwise ordered (imagine pac-man).
			// By the way visible boundary may have gaps.
			// Everything mentioned above leads to O(N^2) complexity
			// because for each boundary edge we have to check 2 things:
			// -- Is triplet of vertices is clockwise ordered;
			// -- Does segment [sightPoint, endOfCurrentEdge] intersect triangulation.
			// Perhaps there is spatial data structures that can improve complexity
			// at least to O(NlogN) in general so label this as TODO.

			// Skip first possibly visible edges in order to
			// get rid of need to merge set of edges that we accidentally splitted.
			var a = Vertices[start.Origin].Pos;
			var b = Vertices[start.Next.Origin].Pos;
			while (AreClockwiseOrdered(a, vertex, b)) {
				do {
					start = start.Next;
					start = start.Twin ?? start;
				} while (start.Twin != null);
				a = Vertices[start.Origin].Pos;
				b = Vertices[start.Next.Origin].Pos;
			}
			var boundary = new List<HalfEdge>();
			var current = start;
			do {
				boundary.Add(current);
				do {
					current = current.Next;
					current = current.Twin ?? current;
				} while (current.Twin != null);
			} while (current != start);
			List<HalfEdge> currentVisibleEdges = null;
			var visibleBoundary = new List<List<HalfEdge>> {};
			bool isContinuous = false;
			foreach (var self in boundary) {
				a = Vertices[self.Origin].Pos;
				b = Vertices[self.Next.Origin].Pos;
				if (AreClockwiseOrdered(a, vertex, b)) {
					var doesIntersectTriangulation = false;
					foreach (var other in boundary) {
						if (self == other) {
							continue;
						}
						var c = Vertices[other.Origin].Pos;
						var d = Vertices[other.Next.Origin].Pos;
						if (
							self.Origin != other.Next.Origin &&
							SegmentSegmentIntersection(a, vertex, c, d, out _) ||
							self.Next.Origin != other.Origin &&
						    SegmentSegmentIntersection(b, vertex, c, d, out _)
						) {
							doesIntersectTriangulation = true;
							break;
						}
					}
					if (doesIntersectTriangulation) {
						isContinuous = false;
						continue;
					}
					if (!isContinuous) {
						visibleBoundary.Add(currentVisibleEdges = new List<HalfEdge>());
						isContinuous = true;
					}
					currentVisibleEdges.Add(self);
				} else {
					isContinuous = false;
				}
			}
			return visibleBoundary;
		}

		private void RestoreDelaunayProperty(List<HalfEdge> possiblyNonDelaunay)
		{
			while (possiblyNonDelaunay.Count > 0) {
				var he = possiblyNonDelaunay[possiblyNonDelaunay.Count - 1];
				possiblyNonDelaunay.RemoveAt(possiblyNonDelaunay.Count - 1);
				if (he.Twin != null && !he.Detached && InCircumcircle(he.Twin, Vertices[he.Prev.Origin].Pos)) {
					he = Flip(he);
					possiblyNonDelaunay.Add(he.Next);
					possiblyNonDelaunay.Add(he.Prev);
					possiblyNonDelaunay.Add(he.Twin.Next);
					possiblyNonDelaunay.Add(he.Twin.Prev);
				}
			}
		}

		private HalfEdge Flip(HalfEdge edge)
		{
			var twin = edge.Twin;
			var edgeNext = edge.Next;
			var twinNext = twin.Next;
			edge.Prev.Next = twinNext;
			twin.Prev.Next = edgeNext;
			edge.Detach();
			twin.Detach();
			edge = new HalfEdge(edgeNext.Next.Origin) { Next = twinNext.Next };
			twin = new HalfEdge(twinNext.Next.Origin) { Next = edgeNext.Next };
			edge.TwinWith(twin);
			edgeNext.Next = edge;
			twinNext.Next = twin;
			return edge;
		}

		#region HelperMethods

		/// <summary>
		/// Constructs triangles on the side using given vertex.
		/// </summary>
		/// <param name="edge">Side.</param>
		/// <param name="vertexIndex">Vertex index.</param>
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


		/// <summary>
		/// Checks whether (a, b,c) are clockwise ordered.
		/// </summary>
		/// <param name="a">Vertex a.</param>
		/// <param name="b">Vertex b.</param>
		/// <param name="c">Vertex c.</param>
		/// <returns><c>true</c> if clockwise ordered and <c>false</c> otherwise.</returns>
		private static bool AreClockwiseOrdered(Vector2 a, Vector2 b, Vector2 c) =>
			GeometricPredicates.ExactOrient2D(a.X, a.Y, b.X, b.Y, c.X, c.Y) > 0;

		public static bool SegmentSegmentIntersection(Vector2 a1, Vector2 b1, Vector2 a2, Vector2 b2, out Vector2 intersectionPoint)
		{
			intersectionPoint = new Vector2(float.NaN, float.NaN);
			var d = (a1.X - b1.X) * (a2.Y - b2.Y) - (a1.Y - b1.Y) * (a2.X - b2.X);
			var uNumerator = ((a1.Y - b1.Y) * (a1.X - a2.X) - (a1.X - b1.X) * (a1.Y - a2.Y));
			if (Mathf.Abs(d) <= Mathf.ZeroTolerance) {
				return uNumerator == 0;
			}
			var t = ((a1.X - a2.X) * (a2.Y - b2.Y) - (a1.Y - a2.Y) * (a2.X - b2.X)) / d;
			var u = uNumerator / d;
			if (t >= 0.0f && t <= 1.0f && u >= 0.0f && u <= 1.0f) {
				intersectionPoint.X = a1.X + t * (b1.X - a1.X);
				intersectionPoint.Y = a1.Y + t * (b1.Y - a1.Y);
				return true;
			}
			return false;
		}

		#endregion
	}
}
