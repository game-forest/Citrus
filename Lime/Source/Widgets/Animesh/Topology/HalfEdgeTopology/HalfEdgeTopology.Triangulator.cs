using System;
using System.Collections.Generic;
using Lime.Source.Widgets.Animesh;

namespace Lime.Widgets.Animesh.Topology.HalfEdgeTopology
{
	public partial class HalfEdgeTopology
	{
		private enum LocationResult
		{
			SameVertex, OnEdge, InsideTriangle, OutsideTriangulation,
		}

		/// <summary>
		/// Adds a vertex to current triangulation using incremental algorithm Bowyer-Watson
		/// algorithm if vertex is completely inside triangulation otherwise
		/// creates triangles on visible boundary and restores delaunay property.
		/// </summary>
		/// <param name="vertexIndex">Vertex index.</param>
		private bool AddVertex(int vertexIndex)
		{
			// First: locate the given vertex.
			// Determine whether it's in the specific triangle or
			// lies outside of triangulation.
			var vertex = InnerVertices[vertexIndex].Pos;
			var result = LocateClosestTriangle(vertex, out var halfEdge);
			// Check special cases:
			// Same vertex already exists:
			if (result == LocationResult.SameVertex) {
				// Shouldn't insert vertex
				int sameVertexIndex = -1;
				for (int i = 0; i < Vertices.Count; i++) {
					if (i != vertexIndex && Vertices[i].Pos == vertex) {
						sameVertexIndex = i;
						break;
					}
				}
				if (sameVertexIndex >= 0) {
					return false;
				}
				// otherwise it can be bounding figure vertex.
				if (!BelongsToBoundingFigure(vertex, out var bfvi) || halfEdge.Origin != -bfvi) {
					return false;
				}
				foreach (var incidentEdge in IncidentEdges(halfEdge)) {
					incidentEdge.Origin = vertexIndex;
				}
			} else if (result == LocationResult.OnEdge) {
				// Should split edge
				var prev = halfEdge.Origin;
				var next = halfEdge.Next.Origin;
				SplitEdge(vertexIndex, halfEdge);
				if (InnerBoundary.Contains(prev) && InnerBoundary.Next(prev) == next) {
					// Then we splitted boundary edge.
					InnerBoundary.Insert(prev, vertexIndex);
				} else if (InnerBoundary.Contains(next) && InnerBoundary.Next(next) == prev) {
					InnerBoundary.Insert(next, vertexIndex);
				}
			} else if (result == LocationResult.InsideTriangle) {
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
			return true;
		}


		private LocationResult LocateClosestTriangle(int index, out HalfEdge edge)
		{
			return LocateClosestTriangle(InnerVertices[index].Pos, out edge);
		}

		/// <summary>
		/// Locates a triangle that has specified vertex inside or
		/// closest one on the border to triangulation.
		/// Complexity is O(sqrt(N)) in general case (see Skvortsov A.V. work about Delaunay triangulation).
		/// </summary>
		/// <param name="vertex">Vertex.</param>
		/// <param name="edge">Closest triangle identified by single edge.</param>
		/// <param name="start">Search start.</param>
		/// <returns>Location result.
		/// If LocationResult.SameVertex then <paramref name="edge"/>'s Origin is that vertex.
		/// If LocationResult.OnEdge then <paramref name="edge"/> is edge that vertex lies on.
		/// If LocationResult.InsideTriangle then <paramref name="edge"/> identifies closest triangle.
		/// If LocationResult.OutsideTriangulation then <paramref name="edge"/> is the closest edge.
		/// </returns>
		private LocationResult LocateClosestTriangle(Vector2 vertex, out HalfEdge edge, HalfEdge start = null)
		{
			start = start ?? Root;
			var current = start;
			do {
				var next = current.Next;
				var b1 = InnerVertices[current.Origin].Pos;
				var b2 = InnerVertices[next.Origin].Pos;
				var v = InnerVertices[next.Next.Origin].Pos;
				if (current.Twin != null && ArePointsOnOppositeSidesOfSegment(b1, b2, v, vertex)) {
					current = start = current.Twin;
				}
				current = current.Next;
			} while (current != start);
			var v1 = InnerVertices[current.Origin].Pos;
			var v2 = InnerVertices[current.Next.Origin].Pos;
			var v3 = InnerVertices[current.Prev.Origin].Pos;
			var r2 = VertexHitTestRadius * VertexHitTestRadius;
			if (v1 == vertex || (vertex - v1).SqrLength <= r2) {
				edge = current;
				return LocationResult.SameVertex;
			}
			if (v2 == vertex || (vertex - v2).SqrLength <= r2) {
				edge = current.Next;
				return LocationResult.SameVertex;
			}
			if (v3 == vertex || (vertex - v3).SqrLength <= r2) {
				edge = current.Prev;
				return LocationResult.SameVertex;
			}
			var isExactEdgeHitTesting = EdgeHitTestDistance == 0f;
			var d2 = EdgeHitTestDistance * EdgeHitTestDistance;
			if (VertexInsideTriangle(vertex, v1, v2, v3)) {
				edge = current;
				if (isExactEdgeHitTesting) {
					if (IsVertexOnEdge(vertex, current)) {
						return LocationResult.OnEdge;
					}
					if (IsVertexOnEdge(vertex, current.Next)) {
						edge = current.Next;
						return LocationResult.OnEdge;
					}
					if (IsVertexOnEdge(vertex, current.Prev)) {
						edge = current.Prev;
						return LocationResult.OnEdge;
					}
				} else {
					var de1 = PointToSegmentSqrDistance(v1, v2, vertex);
					var de2 = PointToSegmentSqrDistance(v2, v3, vertex);
					var de3 = PointToSegmentSqrDistance(v3, v1, vertex);
					if (de1 <= d2 && de1 <= de2 && de1 <= de3) {
						return LocationResult.OnEdge;
					}
					if (de2 <= d2 && de2 <= de1 && de2 <= de3) {
						edge = current.Next;
						return LocationResult.OnEdge;
					}
					if (de3 <= d2 && de3 <= de1 && de3 <= de2) {
						edge = current.Prev;
						return LocationResult.OnEdge;
					}
				}
				return LocationResult.InsideTriangle;
			}
			edge = current;
			return LocationResult.OutsideTriangulation;
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
				var result = LocateClosestTriangle(vertexIndex, out start);
				if (result == LocationResult.OutsideTriangulation) {
					throw new InvalidOperationException("There is no triangle in triangulation that contains given vertex.");
				}
			}
			var vertex = InnerVertices[vertexIndex].Pos;
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
				if (halfEdge.Constrained || halfEdge.Twin == null || !InCircumcircle(halfEdge.Twin, vertex)) {
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
				var result = LocateClosestTriangle(sightPoint, out start);
				if (result != LocationResult.OutsideTriangulation) {
					throw new InvalidOperationException("Sight point is inside triangulation.");
				}
			}
			var vertex = InnerVertices[sightPoint].Pos;
			// Ensure that 'start' is edge of the boundary (Twin is null).
			// Find boundary edge otherwise.
			var current = start;
			do {
				if (current.Twin == null) {
					start = current;
					break;
				}
				current = current.Next;
			} while (current != start);
			if (start.Twin != null) {
				// Check which HalfEdge is closer to sightPoint `start` or it's twin.
				var d1 = (vertex - InnerVertices[start.Origin].Pos).SqrLength;
				var d2 = (vertex - InnerVertices[start.Twin.Origin].Pos).SqrLength;
				start = d1 > d2 ? start : start.Twin;
				// Check incident edges (for start.Origin) until boundary edge is found.
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
			var a = InnerVertices[start.Origin].Pos;
			var b = InnerVertices[start.Next.Origin].Pos;
			while (AreClockwiseOrdered(a, vertex, b)) {
				do {
					start = start.Next;
					start = start.Twin ?? start;
				} while (start.Twin != null);
				a = InnerVertices[start.Origin].Pos;
				b = InnerVertices[start.Next.Origin].Pos;
			}
			var boundary = new List<HalfEdge>();
			current = start;
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
				a = InnerVertices[self.Origin].Pos;
				b = InnerVertices[self.Next.Origin].Pos;
				if (AreClockwiseOrdered(a, vertex, b)) {
					var doesIntersectTriangulation = false;
					foreach (var other in boundary) {
						if (self == other) {
							continue;
						}
						var c = InnerVertices[other.Origin].Pos;
						var d = InnerVertices[other.Next.Origin].Pos;
						if (
							self.Origin != other.Next.Origin &&
							RobustSegmentSegmentIntersection(a, vertex, c, d) ||
							self.Next.Origin != other.Origin &&
							RobustSegmentSegmentIntersection(b, vertex, c, d)
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
				if (!IsDelaunay(he)) {
					Root = he = Flip(he);
					possiblyNonDelaunay.Add(he.Next);
					possiblyNonDelaunay.Add(he.Prev);
					possiblyNonDelaunay.Add(he.Twin.Next);
					possiblyNonDelaunay.Add(he.Twin.Prev);
				}
			}
		}

		private bool IsDelaunay(HalfEdge edge) =>
			edge.Constrained || edge.Twin == null || edge.Detached || !InCircumcircle(edge.Twin, InnerVertices[edge.Prev.Origin].Pos);

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

		/// <summary>
		/// Removes vertex from triangulation.
		/// </summary>
		/// <param name="vertexIndex">Vertex index.</param>
		/// <returns>List of isolated vertices.</returns>
		private void InnerRemoveVertex(int vertexIndex)
		{
			var polygon = GetBoundingPolygon(vertexIndex);
			var isBorderVertex = polygon[0].Origin == vertexIndex;
			if (isBorderVertex) {
				// Then it is definitely a vertex that lies on bounding figure.
				// (Btw, bounding figure vertices are handled on a higher abstraction level).
				// So here is steps to remove it:
				// 1. Merge the first and the second edge of the bounding polygon;
				// 2. Triangulate polygonal hole.
				polygon.RemoveAt(0);
				polygon[polygon.Count - 1].Next = polygon[0].Next;
			}
			TriangulatePolygonByEarClipping(polygon);
		}

		/// <summary>
		/// Gets bounding polygon of <paramref name="vertexIndex"/>.
		/// Bounding polygon is a polygon constructed on the edges of triangles that
		/// lies opposite to <paramref name="vertexIndex"/>.
		/// </summary>
		/// <param name="vertexIndex">Vertex index.</param>
		/// <param name="start">Triangle that contains <paramref name="vertexIndex"/></param>
		/// <returns>Bounding polygon</returns>
		private List<HalfEdge> GetBoundingPolygon(int vertexIndex, HalfEdge start = null)
		{
			// Find triangle that contains given vertex;
			if (start == null) {
				LocateClosestTriangle(vertexIndex, out start);
			}
			// Find edge that is incident to given vertex
			start = start.Origin == vertexIndex ? start : start.Next.Origin == vertexIndex ? start.Next : start.Prev;
			System.Diagnostics.Debug.Assert(start.Origin == vertexIndex);
			// Find polygon constructed from edges that lies
			// opposite to vertex to be removed.
			// First: check if vertex is not on the border of triangulation
			// otherwise get border edge to start from. That is needed in order to
			// avoid iterating back and forward and then merging edge chains to form polygon.
			var isBorderVertex = TryFindBorderEdge(start, out var current);
			// Second: iterate over incident edges to form polygon.
			var polygon = new List<HalfEdge>();
			if (isBorderVertex) {
				start = current;
				polygon.Add(start);
			} else {
				current = start;
			}
			do {
				polygon.Add(current.Next);
				current = current.Prev;
				if (current.Twin == null) {
					polygon.Add(current);
					break;
				}
				current = current.Twin;
			} while (current != start);
			return polygon;
		}

		private bool TryFindBorderEdge(HalfEdge start, out HalfEdge borderEdge)
		{
			borderEdge = null;
			var current = start;
			do {
				if (current.Twin == null) {
					borderEdge = current;
					return true;
				}
				current = current.Twin.Next;
			} while (current != start);
			return false;
		}

		/// <summary>
		/// Triangulates polygon using Ear Clipping method.
		/// Complexity is O(N^2). Can be improved to O(NlogN) (Seidel algorithm),
		/// O(Nlog*N) or O(N) (Chazelle algorithm, there is no existing implementation).
		/// </summary>
		/// <param name="polygon">Input polygon. </param>
		/// <param name="restoreDelaunay">If true methods calls RestoreDelaunayProperty on newly created triangles, otherwise
		/// leaves newly created triangles in <paramref name="polygon"/>.</param>
		private void TriangulatePolygonByEarClipping(List<HalfEdge> polygon, bool restoreDelaunay = true)
		{
			// An ear of a polygon is defined as a vertex v such that the line segment between
			// the two neighbors of v lies entirely in the interior of the polygon.
			// The two ears theorem states that every simple polygon has at least two ears.
			var p = new LinkedList<HalfEdge>(polygon);
			var prev = p.First;
			var current = prev.Next;
			while (p.Count > 3) {
				var next = current.Next ?? p.First;
				HalfEdge e1 = prev.Value, e2 = current.Value, e3 = next.Value;
				int o1 = e1.Origin, o2 = e2.Origin, o3 = e3.Origin;
				Vector2 v1 = InnerVertices[o1].Pos, v2 = InnerVertices[o2].Pos, v3 = InnerVertices[o3].Pos;
				if (AreClockwiseOrdered(v1, v2, v3)) {
					var other = next.Next ?? p.First;
					var isEar = true;
					while (other != prev) {
						var e = other.Value;
						if (VertexInsideTriangle(InnerVertices[e.Origin].Pos, v1, v2, v3)) {
							// Definitely not an ear
							isEar = false;
							break;
						}
						other = other.Next ?? p.First;
					}
					if (isEar) {
						e1.Next = e2;
						e2.Next = new HalfEdge(o3) { Next = e1 };
						var twin = new HalfEdge(o1);
						polygon.Add(e2.Next);
						e2.Next.TwinWith(twin);
						p.AddAfter(current, twin);
						current = current.Next;
						p.Remove(prev.Next ?? p.First);
						p.Remove(prev);
					}
				}
				prev = current;
				current = next;
			}
			HalfEdge he1 = p.First.Value, he2 = p.First.Next.Value, he3 = p.Last.Value;
			he1.Next = he2;
			he2.Next = he3;
			he3.Next = he1;
			Root = he1;
			if (restoreDelaunay) {
				RestoreDelaunayProperty(polygon);
			}
		}

		private bool InnerInsertConstrainedEdge(int index0, int index1, bool destroyConstrained = false)
		{
			var location = LocateClosestTriangle(index0, out var start);
			System.Diagnostics.Debug.Assert(location == LocationResult.SameVertex);
			System.Diagnostics.Debug.Assert(start.Origin == index0);
			// Check test cases for explanation
			var upperUsed = new HashSet<int>();
			var lowerUsed = new HashSet<int>();
			var shouldBeReinserted = new HashSet<int>();
			var shouldBeReconstrained = new List<(int, int)>();
			var a = InnerVertices[index0].Pos;
			var b = InnerVertices[index1].Pos;
			var ab = b - a;
			var signab = new IntVector2(Mathf.Sign(ab.X), Mathf.Sign(ab.Y));
			// In order to insert a constrain edge we have to delete all
			// triangles that (index0, index1) crosses. Thereafter we
			// have 2 polygonal holes that should be triangulated.
			List<HalfEdge> upperPolygon = null, lowerPolygon = null;
			var current = start;
			foreach (var incidentEdge in IncidentEdges(start)) {
				var next = incidentEdge.Next;
				var prev = next.Next;
				// Special case: requested edge already exists.
				if (next.Origin == index1) {
					return incidentEdge.Constrained = true;
				}
				if (prev.Origin == index1) {
					return prev.Constrained = true;
				}
				var c = InnerVertices[next.Origin].Pos;
				var d = InnerVertices[prev.Origin].Pos;
				var e = InnerVertices[incidentEdge.Origin].Pos;
				// Special case: requested edge lies on the same line as [e, c] or [e, d].
				// If true then mark edge as constrained and insert edge [next.Origin, index1] or [prev.Origin, index1].
				if (IsVertexOnLine(a, e, c) && IsVertexOnLine(b, e, c)) {
					var ec = c - e;
					// Check for co-directionality in order to prevent looping
					if (Mathf.Sign(ec.X) == signab.X && Mathf.Sign(ec.Y) == signab.Y) {
						return incidentEdge.Constrained = InnerInsertConstrainedEdge(next.Origin, index1, destroyConstrained);
					}

				} else if (IsVertexOnLine(a, e, d) && IsVertexOnLine(b, e, d)) {
					var ed = d - e;
					// Check for co-directionality in order to prevent looping
					if (Mathf.Sign(ed.X) == signab.X && Mathf.Sign(ed.Y) == signab.Y) {
						return prev.Constrained = InnerInsertConstrainedEdge(prev.Origin, index1, destroyConstrained);
					}
				} else if (RobustSegmentSegmentIntersection(a, b, c, d)) {
					// Should not delete existing constrain edges.
					if (next.Constrained && !destroyConstrained) {
						return false;
					}
					// Then we should start traversing
					upperPolygon = new List<HalfEdge> { incidentEdge };
					lowerPolygon = new List<HalfEdge> { prev };
					lowerUsed.Add(prev.Origin);
					upperUsed.Add(incidentEdge.Origin);
					current = next;
					break;
				}
			}
			while (true) {
				if (current.Twin == null) {
					// Something horrible has happened..
					// Something like trying to connect two vertices trough concavity.
					// Should not happen at all in current implementation.
					return false;
				}
				current = current.Twin;
				var next = current.Next;
				var prev = next.Next;
				if (prev.Origin == index1) {
					// We found the end of the requested edge.
					// Stop traversing and triangulate both polygons.
					AddLower(prev);
					AddUpper(next);
					Finish(index0, index1);
					return true;
				}
				var c = InnerVertices[next.Origin].Pos;
				var d = InnerVertices[prev.Origin].Pos;
				var e = InnerVertices[current.Origin].Pos;
				if (IsVertexOnLine(d, a, b)) {
					// Special case: [a, b] intersects triangle exactly in the
					// vertex that is opposite to basis (`current` edge).
					// Should finish traversing and insert constrain edge
					// between prev.Origin and index1.
					AddLower(prev);
					AddUpper(next);
					var edge = Finish(index0, prev.Origin);
					edge.Constrained = InnerInsertConstrainedEdge(prev.Origin, index1, destroyConstrained);
					RestoreDelaunayProperty(upperPolygon);
					RestoreDelaunayProperty(lowerPolygon);
					return edge.Constrained;

				}
				if (RobustSegmentSegmentIntersection(a, b, c, d)) {
					if (next.Constrained && !destroyConstrained) {
						return false;
					}
					AddLower(prev);
					current = next;
				} else if (RobustSegmentSegmentIntersection(a, b, d, e)) {
					if (prev.Constrained && !destroyConstrained) {
						return false;
					}
					AddUpper(next);
					current = prev;
				} else {
					System.Diagnostics.Debug.Fail("This is impossible case.");
				}
			}
			void AddUpper(HalfEdge e)
			{
				if (upperUsed.Add(e.Origin)) {
					upperPolygon.Add(e);
				} else {
					// It means that triangles from i to polygon.Count should be removed.
					// Removing those triangles makes some vertices isolated and
					// also may remove previously inserted constrained edges.
					// So we keep that in mind in order to reinsert missing elements later.
					var i = upperPolygon.FindIndex(edge => edge.Origin == e.Origin);
					for (int j = upperPolygon.Count - 1; j >= i; j--) {
						var t = upperPolygon[j];
						shouldBeReinserted.Add(t.Origin);
						shouldBeReinserted.Add(t.Next.Origin);
						if (t.Constrained) {
							shouldBeReconstrained.Add((t.Origin, t.Next.Origin));
						}
						upperPolygon.RemoveAt(j);
					}
					upperPolygon.Add(e);
				}
			}
			void AddLower(HalfEdge e)
			{
				if (lowerUsed.Add(e.Origin)) {
					lowerPolygon.Add(e);
				} else {
					// Same as for upper polygon except that it is filled in reverse order.
					var i = lowerPolygon.FindIndex(edge => edge.Origin == e.Origin);
					for (int j = lowerPolygon.Count - 1; j > i; j--) {
						var t = lowerPolygon[j];
						shouldBeReinserted.Add(t.Origin);
						shouldBeReinserted.Add(t.Next.Origin);
						if (t.Constrained) {
							shouldBeReconstrained.Add((t.Origin, t.Next.Origin));
						}
						lowerPolygon.RemoveAt(j);
					}
					shouldBeReinserted.Add(e.Next.Origin);
				}
			}
			HalfEdge Finish(int i0, int i1)
			{
				// Create (i0, i1) half edges.
				var e1 = new HalfEdge(i1);
				var e2 = new HalfEdge(i0);
				e1.TwinWith(e2);
				e1.Constrained = true;
				AddUpper(e1);
				AddLower(e2);
				lowerPolygon.Reverse();
				// Triangulate both polygonal holes.
				TriangulatePolygonByEarClipping(upperPolygon, false);
				TriangulatePolygonByEarClipping(lowerPolygon, false);
				RestoreDelaunayProperty(upperPolygon);
				// Reinsert missing elements.
				foreach (var vertexIndex in shouldBeReinserted) {
					AddVertex(vertexIndex);
				}
				foreach (var edge in shouldBeReconstrained) {
					InnerInsertConstrainedEdge(edge.Item1, edge.Item2);
				}
				return e2;
			}
		}

		/// <summary>
		/// Gets all edges incident to <c>edge.Origin</c>.
		/// </summary>
		/// <param name="start">Edge that determines vertex.</param>
		/// <returns>Edges incident to <c>edge.Origin</c></returns>
		private IEnumerable<HalfEdge> IncidentEdges(HalfEdge start)
		{
			var current = start;
			var backward = false;
			do {
				yield return current;
				var next = current.Next;
				var prev = next.Next;
				if (prev.Twin == null && !backward) {
					backward = true;
					current = start;
					if (current.Twin != null) {
						current = current.Twin.Next;
					}
					continue;
				}
				if (current.Twin == null && backward) {
					yield break;
				}
				current = backward ? current.Twin.Next : prev.Twin;
			} while (current != start);
		}

		/// <summary>
		/// Split an edge with <paramref name="vertexIndex"/>.
		/// For each half edge two new triangles replace old one.
		/// </summary>
		/// <param name="vertexIndex">Vertex index.</param>
		/// <param name="edge">Half edge.</param>
		private void SplitEdge(int vertexIndex, HalfEdge edge)
		{
			var twin = edge.Twin;
			var edgesToCheck = new List<HalfEdge>(12);
			SplitHalfEdge(edge);
			if (twin != null) {
				SplitHalfEdge(twin);
				var twinSplitNext = twin.Next.Twin.Next;
				twin.TwinWith(edge.Next.Twin.Next);
				twinSplitNext.TwinWith(edge);
			}
			edge.Constrained = edge.Constrained;
			edge.Next.Twin.Next.Constrained = edge.Constrained;
			RestoreDelaunayProperty(edgesToCheck);
			void SplitHalfEdge(HalfEdge e)
			{
				var next = e.Next;
				var prev = next.Next;
				// Form first triangle
				var split = new HalfEdge(vertexIndex) { Next = prev, };
				e.Next = split;
				// Form second triangle
				var splitTwin = new HalfEdge(prev.Origin);
				var splitTwinNext = new HalfEdge(vertexIndex) { Next = next, };
				splitTwin.Next = splitTwinNext;
				next.Next = splitTwin;
				splitTwin.TwinWith(split);
				edgesToCheck.Add(e);
				edgesToCheck.Add(split);
				edgesToCheck.Add(prev);
				edgesToCheck.Add(splitTwin);
				edgesToCheck.Add(splitTwinNext);
				edgesToCheck.Add(next);
			}
		}

		private void ToConvexHull(HalfEdge borderEdge = null)
		{
			var current = borderEdge;
			if (current == null) {
				foreach (var halfEdge in HalfEdges) {
					if (halfEdge.Twin == null) {
						current = halfEdge;
						break;
					}
				}
			}
			var edgesToCheck = new List<HalfEdge>();
			var prev = PrevBorderEdge(current);
			var start = current;
			// Same as Ear clipping method but for 'outside' ears.
			do {
				var o1 = prev.Origin;
				var o2 = current.Origin;
				var o3 = current.Next.Origin;
				var p1 = InnerVertices[o1].Pos;
				var p2 = InnerVertices[o2].Pos;
				var p3 = InnerVertices[o3].Pos;
				if (!AreClockwiseOrderedOrCollinear(p1, p2, p3)) {
					var c = start;
					var intersectAny = false;
					do {
						if (c.Origin == o1 || c.Origin == o2 || c.Origin == o3 || c.Next.Origin == o1) {
							c = NextBorderEdge(c);
							continue;
						}
						var s1 = InnerVertices[c.Origin].Pos;
						var s2 = InnerVertices[c.Next.Origin].Pos;
						if (RobustSegmentSegmentIntersection(p1, p3, s1, s2)) {
							intersectAny = true;
							break;
						}
						c = NextBorderEdge(c);
					} while (start != c);
					if (!intersectAny) {
						var t = new HalfEdge(current.Next.Origin) {
							Next = new HalfEdge(current.Origin) {
								Next = new HalfEdge(prev.Origin),
							},
						};
						edgesToCheck.Add(t.Prev);
						t.Prev.Next = t;
						t.TwinWith(current);
						t.Next.TwinWith(prev);
						start = current = t.Prev;
						prev = PrevBorderEdge(start);
						continue;
					}
				}
				prev = current;
				current = NextBorderEdge(current);
				if (current == start) {
					break;
				}
			} while (true);
			RestoreDelaunayProperty(edgesToCheck);
		}

		private HalfEdge NextBorderEdge(HalfEdge borderEdge)
		{
			do {
				borderEdge = borderEdge.Next;
				borderEdge = borderEdge.Twin ?? borderEdge;
			} while (borderEdge.Twin != null);
			return borderEdge;
		}

		private HalfEdge PrevBorderEdge(HalfEdge borderEdge)
		{
			do {
				borderEdge = borderEdge.Prev;
				borderEdge = borderEdge.Twin ?? borderEdge;
			} while (borderEdge.Twin != null);
			return borderEdge;
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
			var v1 = InnerVertices[triangle.Origin].Pos;
			var v2 = InnerVertices[triangle.Next.Origin].Pos;
			var v3 = InnerVertices[triangle.Prev.Origin].Pos;
			return InCircle(vertex, v1, v2, v3);
		}

		private static bool InCircle(Vector2 vertex, Vector2 v1, Vector2 v2, Vector2 v3) =>
			GeometricPredicates.AdaptiveInCircle(v1.X, v1.Y, v2.X, v2.Y, v3.X, v3.Y, vertex.X, vertex.Y) > 0;

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
			var v1 = InnerVertices[triangle.Origin].Pos;
			var v2 = InnerVertices[next.Origin].Pos;
			var v3 = InnerVertices[prev.Origin].Pos;
			return VertexInsideTriangle(vertex, v1, v2, v3);
		}

		private static bool VertexInsideTriangle(Vector2 vertex, Vector2 v1, Vector2 v2, Vector2 v3) =>
			Orient2D(v1, v2, vertex) >= 0 && Orient2D(v2, v3, vertex) >= 0 && Orient2D(v3, v1, vertex) >= 0;

		/// <summary>
		/// Tells weather <paramref name="p1"/> and <paramref name="p2"/> lies on
		/// opposite sides of segment [<paramref name="s1"/>, <paramref name="s2"/>].
		/// </summary>
		/// <param name="s1">Segment start.</param>
		/// <param name="s2">Segment end.</param>
		/// <param name="p1">First point.</param>
		/// <param name="p2">Second point.</param>
		private static bool ArePointsOnOppositeSidesOfSegment(Vector2 s1, Vector2 s2, Vector2 p1, Vector2 p2) =>
			GeometricPredicates.AdaptiveOrient2D(s1.X, s1.Y, s2.X, s2.Y, p1.X, p1.Y) *
			GeometricPredicates.AdaptiveOrient2D(s1.X, s1.Y, s2.X, s2.Y, p2.X, p2.Y) < 0;


		/// <summary>
		/// Checks whether (a, b,c) are clockwise ordered.
		/// </summary>
		/// <param name="a">Vertex a.</param>
		/// <param name="b">Vertex b.</param>
		/// <param name="c">Vertex c.</param>
		/// <returns><c>true</c> if clockwise ordered and <c>false</c> otherwise.</returns>
		private static bool AreClockwiseOrdered(Vector2 a, Vector2 b, Vector2 c) => Orient2D(a, b, c) > 0;

		private static bool AreClockwiseOrderedOrCollinear(Vector2 a, Vector2 b, Vector2 c) => Orient2D(a, b, c) >= 0;

		private static bool RobustSegmentSegmentIntersection(Vector2 a, Vector2 b, Vector2 c, Vector2 d) =>
			Mathf.Max(Mathf.Min(a.X, b.X), Mathf.Min(c.X, d.X)) <=
			Mathf.Min(Mathf.Max(a.X, b.X), Mathf.Max(c.X, d.X)) &&
			Mathf.Max(Mathf.Min(a.Y, b.Y), Mathf.Min(c.Y, d.Y)) <=
			Mathf.Min(Mathf.Max(a.Y, b.Y), Mathf.Max(c.Y, d.Y)) &&
			Orient2D(a, b, c) * Orient2D(a, b, d) <= 0 &&
			Orient2D(c, d, a) * Orient2D(c, d, b) <= 0;

		private static bool SegmentSegmentIntersection(Vector2 a1, Vector2 b1, Vector2 a2, Vector2 b2, out Vector2 intersectionPoint)
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

		private bool IsVertexOnEdge(Vector2 vertex, HalfEdge edge) =>
			IsVertexOnEdge(vertex, InnerVertices[edge.Origin].Pos, InnerVertices[edge.Next.Origin].Pos);

		private static bool IsVertexOnEdge(Vector2 vertex, Vector2 s, Vector2 e) =>
			vertex.X <= Mathf.Max(s.X, e.X) && vertex.X >= Mathf.Min(s.X, e.X) &&
			vertex.Y <= Mathf.Max(s.Y, e.Y) && vertex.Y >= Mathf.Min(s.Y, e.Y) &&
			IsVertexOnLine(vertex, s, e);

		private static bool IsVertexOnLine(Vector2 vertex, Vector2 s, Vector2 e) =>
			Orient2D(vertex, s, e) == 0;

		private static int Orient2D(Vector2 a, Vector2 b, Vector2 c) =>
			Math.Sign(GeometricPredicates.AdaptiveOrient2D(a.X, a.Y, b.X, b.Y, c.X, c.Y));

		#endregion
	}
}
