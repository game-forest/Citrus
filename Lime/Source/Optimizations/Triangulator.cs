using System;
using System.Collections.Generic;
using System.Linq;
using Lime.PolygonMesh;
using Yuzu;
using HalfEdge = Lime.PolygonMesh.Geometry.HalfEdge;

namespace Lime.Source.Optimizations
{
	public class Triangulator
	{
		public static Triangulator Instance { get; } = new Triangulator();

		private List<(int, int)> constrainedEdges = new List<(int, int)>();
		private readonly Random random = new Random();

		public void AddVertex(Geometry geometry, int vi)
		{
			var vertex = geometry.Vertices[vi];
			var t = LocateTriangle(geometry, RandomValidEdge(geometry), vertex, out var inside);
			if (inside) {
				if (OnEdge(geometry, t, vi, out var edge)) {
					SplitEdge(geometry, edge, vi);
					geometry.Invalidate();
					return;
				}
				TriangulateStarShapedPolygon(geometry, GetContourPolygon(geometry, t, vertex), vi);
			} else {
				var q = TriangulateVisibleBoundary(geometry, GetVisibleBoundary(geometry, vertex, GetTriangulationBoundary(geometry, vertex, t)), vi);
				System.Diagnostics.Debug.Assert(q.Count > 0);
				RestoreDelaunayProperty(geometry, q);
				System.Diagnostics.Debug.Assert(FullCheck(geometry));
			}
			InsertConstrainedEdges(geometry, constrainedEdges);
			constrainedEdges.Clear();
		}

		private HalfEdge RandomEdge(Geometry geometry) =>
			geometry.HalfEdges[random.Next(0, geometry.HalfEdges.Count - 1)];

		public void RemoveVertex(Geometry geometry, int vi, bool keepConstrainedEdges = false)
		{
			var vertex = geometry.Vertices[vi];
			var polygon = GetBoundaryPolygon(geometry, FindIncidentEdge(geometry, LocateTriangle(geometry, RandomValidEdge(geometry), vertex, out _), vi));
			RestoreDelaunayProperty(geometry,
				geometry.HalfEdges[geometry.Next(polygon.Last.Value)].Origin == vi ?
					RemovePolygon(geometry, polygon, keepConstrainedEdges) :
					TriangulatePolygonByEarClipping(geometry, polygon, keepConstrainedEdges));
		}

		public bool FullCheck(Geometry geometry)
		{
			return true;
			for (int i = 0; i < geometry.HalfEdges.Count; i += 3) {
				if (
					geometry.HalfEdges[i].Index == -1 || geometry.HalfEdges[i + 1].Index == -1 ||
					geometry.HalfEdges[i + 2].Index == -1
				) {
					continue;
				}
				int p = geometry.HalfEdges[i].Origin,
					q = geometry.HalfEdges[i + 1].Origin,
					r = geometry.HalfEdges[i + 2].Origin;
				var he = geometry.HalfEdges[i];
				for (int j = 0; j < geometry.Vertices.Count; j++) {
					if (j != p && j != q && j != r) {
						if (InCircumcircle(geometry, he, geometry.Vertices[j])) {
							return false;
						}
					}
				}
			}
			return true;
		}

		//TODO make OnTriangleEdge and OnEdge
		private bool OnEdge(Geometry geometry, HalfEdge triangle, int vi, out HalfEdge edge)
		{
			var v1 = geometry.Vertices[triangle.Origin].Pos;
			var v2 = geometry.Vertices[geometry.Next(triangle).Origin].Pos;
			var v3 = geometry.Vertices[geometry.Prev(triangle).Origin].Pos;
			var v = geometry.Vertices[vi].Pos;
			edge = triangle;
			var p = PolygonMeshUtils.PointProjectionToLine(v, v1, v2, out bool isInside);
			const float zt2 = Mathf.ZeroTolerance * Mathf.ZeroTolerance;
			if (isInside && Math.Abs((p - v).SqrLength) < zt2) {
				return true;
			}
			p = PolygonMeshUtils.PointProjectionToLine(v, v2, v3, out isInside);
			if (isInside && Math.Abs((p - v).SqrLength) < zt2) {
				edge = geometry.Next(triangle);
				return true;
			}
			p = PolygonMeshUtils.PointProjectionToLine(v, v3, v1, out isInside);
			if (isInside && Math.Abs((p - v).SqrLength) < zt2) {
				edge = geometry.Prev(triangle);
				return true;
			}
			return false;
		}

		private bool AreOnOppositeSides(Vector2 s1, Vector2 s2, Vector2 p1, Vector2 p2)
		{
			var side = s2 - s1;
			var v1 = p1 - s1;
			var v2 = p2 - s1;
			return Mathf.Sign(Vector2.CrossProduct(side, v1)) * Mathf.Sign(Vector2.CrossProduct(side, v2)) < 0;
		}

		private HalfEdge LocateTriangle(Geometry geometry, HalfEdge start, Vertex vertex, out bool inside)
		{
			var current = start;
			inside = true;
			do {
				var next = geometry.Next(current);
				Vector2 p1 = geometry.Vertices[current.Origin].Pos;
				var areOpposite = AreOnOppositeSides(p1, geometry.Vertices[next.Origin].Pos, geometry.Vertices[geometry.Next(next).Origin].Pos, vertex.Pos);
				inside &= !areOpposite;
				if (areOpposite && current.Twin != -1) {
					start = current = geometry.HalfEdges[current.Twin];
					inside = true;
				}
				current = geometry.Next(current);
			} while (current.Index != start.Index);
			return current;
		}

		private HalfEdge FindIncidentEdge(Geometry geometry, HalfEdge edge, int vi)
		{
			return edge.Origin == vi ? edge :
				(edge = geometry.Next(edge)).Origin == vi ? edge :
					 (edge = geometry.Next(edge)).Origin == vi ? edge : throw new InvalidOperationException();
		}

		private bool InCircumcircle(Geometry geometry, HalfEdge edge, Vertex vertex)
		{
			var v1 = geometry.Vertices[edge.Origin].Pos;
			var next = geometry.Next(edge);
			var v2 = geometry.Vertices[next.Origin].Pos;
			var v3 = geometry.Vertices[geometry.Next(next).Origin].Pos;
			var p = vertex.Pos;
			var n1 = (double)v1.SqrLength;
			var n2 = (double)v2.SqrLength;
			var n3 = (double)v3.SqrLength;
			var a = (double)v1.X * v2.Y + (double)v2.X * v3.Y + (double)v3.X * v1.Y -
					((double)v2.Y * v3.X + (double)v3.Y * v1.X + (double)v1.Y * v2.X);
			var b = n1 * v2.Y + n2 * v3.Y + n3 * v1.Y - (v2.Y * n3 + v3.Y * n1 + v1.Y * n2);
			var c = n1 * v2.X + n2 * v3.X + n3 * v1.X - (v2.X * n3 + v3.X * n1 + v1.X * n2);
			var d = n1 * v2.X * v3.Y + n2 * v3.X * v1.Y + n3 * v1.X * v2.Y - (v2.X * n3 * v1.Y + v3.X * n1 * v2.Y + v1.X * n2 * v3.Y);
			return (a * p.SqrLength - b * p.X + c * p.Y - d) * Math.Sign(a) < 0;
		}

		private LinkedList<int> GetContourPolygon(Geometry geometry, HalfEdge start, Vertex vertex)
		{
			var polygon = new LinkedList<int>();
			polygon.AddLast(start.Index);
			polygon.AddLast(geometry.Next(start.Index));
			polygon.AddLast(geometry.Prev(start.Index));
			var current = polygon.First;
			while (current != null) {
				var edge = geometry.HalfEdges[current.Value];
				if (edge.Twin != -1) {
					var twin = geometry.HalfEdges[edge.Twin];
					if (InCircumcircle(geometry, geometry.HalfEdges[edge.Twin], vertex)) {
						polygon.AddAfter(current, geometry.Next(twin.Index));
						polygon.AddAfter(current.Next, geometry.Prev(twin.Index));
						geometry.RemoveHalfEdge(edge.Index);
						geometry.RemoveHalfEdge(twin.Index);
						KeepConstrainedEdges(geometry, edge.Index);
						KeepConstrainedEdges(geometry, twin.Index);
						var next = current.Next;
						polygon.Remove(current);
						current = next;
						continue;
					}
				}
				current = current.Next;
			}
			return polygon;
		}

		private LinkedList<int> GetBoundaryPolygon(Geometry geometry, HalfEdge incident)
		{
			var polygon = new LinkedList<int>();
			bool reverse = false;
			var current = incident;
			do {
				var i = current.Index;
				if (reverse) {
					polygon.AddFirst(geometry.Next(i));
				} else {
					polygon.AddLast(geometry.Next(i));
				}
				var next = geometry.HalfEdges[reverse ? i : geometry.Next(polygon.Last.Value)];
				if (next.Twin == -1) {
					if (reverse) {
						System.Diagnostics.Debug.Assert(next.Index != -1);
						polygon.AddFirst(next.Index);
						return polygon;
					}
					polygon.RemoveFirst();
					polygon.AddLast(next.Index);
					System.Diagnostics.Debug.Assert(next.Index != -1);
					reverse = true;
					current = incident;
					continue;
				}
				current = reverse ? geometry.HalfEdges[geometry.Next(next.Twin)] : geometry.HalfEdges[next.Twin];
			} while (polygon.First.Value != polygon.Last.Value || polygon.Count < 2);
			polygon.Remove(polygon.Last);
			return polygon;
		}

		private void Connect(Geometry geometry, HalfEdge he, int vi)
		{
			geometry.Connect(he.Origin, geometry.Next(he).Origin, vi);
			if (he.Twin != -1) {
				geometry.MakeTwins(he.Twin, geometry.HalfEdges.Count - 3);
			}
			geometry.SetConstrain(geometry.HalfEdges.Count - 3, he.Constrained);
		}

		private void Connect(Geometry geometry, HalfEdge he1, HalfEdge he2)
		{
			geometry.Connect(he1.Origin, he2.Origin, geometry.Next(he2).Origin);
			if (he1.Twin != -1) {
				geometry.MakeTwins(he1.Twin, geometry.HalfEdges.Count - 3);
			}
			if (he2.Twin != -1) {
				geometry.MakeTwins(he2.Twin, geometry.HalfEdges.Count - 2);
			}
			geometry.SetConstrain(geometry.HalfEdges.Count - 3, he1.Constrained);
			geometry.SetConstrain(geometry.HalfEdges.Count - 2, he2.Constrained);
		}

		private float Area(Vector2 v1, Vector2 v2, Vector2 v3) =>
			(v2.X - v1.X) * (v3.Y - v1.Y) - (v2.Y - v1.Y) * (v3.X - v1.X);

		private void KeepConstrainedEdges(Geometry geometry, int triangle)
		{
			KeepConstrainedEdge(geometry, triangle);
			KeepConstrainedEdge(geometry, geometry.Prev(triangle));
			KeepConstrainedEdge(geometry, geometry.Next(triangle));
		}

		private void KeepConstrainedEdge(Geometry geometry, int edge)
		{
			var he = geometry.HalfEdges[edge];
			if (he.Constrained) {
				constrainedEdges.Add((he.Origin, geometry.HalfEdges[geometry.Next(edge)].Origin));
			}
		}

		private void SplitEdge(Geometry geometry, HalfEdge edge, int splitPoint)
		{
			void IternalSplitEdge(HalfEdge e, int sp)
			{
				var next = geometry.Next(e);
				var prev = geometry.Prev(e);
				geometry.Connect(e.Origin, sp, prev.Origin);
				geometry.Connect(sp, next.Origin, prev.Origin);
				if (next.Twin != -1) {
					geometry.MakeTwins(geometry.HalfEdges.Count - 2, next.Twin);
				}
				if (prev.Twin != -1) {
					geometry.MakeTwins(geometry.HalfEdges.Count - 4, prev.Twin);
				}
				geometry.MakeTwins(geometry.HalfEdges.Count - 5, geometry.HalfEdges.Count - 1);
			}
			IternalSplitEdge(edge, splitPoint);
			var res = (geometry.HalfEdges.Count - 6, geometry.HalfEdges.Count - 3);
			if (edge.Twin != -1) {
				IternalSplitEdge(geometry.HalfEdges[edge.Twin], splitPoint);
				geometry.MakeTwins(res.Item2, geometry.HalfEdges.Count - 6);
				geometry.MakeTwins(res.Item1, geometry.HalfEdges.Count - 3);
				geometry.RemoveTriangle(edge.Twin);
			}
			geometry.SetConstrain(res.Item1, edge.Constrained);
			geometry.SetConstrain(res.Item2, edge.Constrained);
			geometry.RemoveTriangle(edge.Index);
		}

		private void TriangulateStarShapedPolygon(Geometry geometry, LinkedList<int> polygon, int vi)
		{
			var current = polygon.First;
			while (current != null) {
				var edge = geometry.HalfEdges[current.Value];
				Connect(geometry, geometry.HalfEdges[current.Value], vi);
				if (current.Previous != null) {
					geometry.MakeTwins(geometry.HalfEdges.Count - 1, geometry.Next(current.Previous.Value));
				}
				geometry.RemoveHalfEdge(edge.Index);
				KeepConstrainedEdges(geometry, edge.Index);
				current.Value = geometry.HalfEdges.Count - 3;
				current = current.Next;
			}
			geometry.MakeTwins(geometry.Next(polygon.Last.Value), geometry.Prev(polygon.First.Value));
		}

		private Queue<int> TriangulatePolygonByEarClipping(Geometry geometry, LinkedList<int> polygon, bool keepConstrainedEdges = false)
		{
			var queue = new Queue<int>();
			bool CanCreateTriangle(int cur, int next)
			{
				HalfEdge he1 = geometry.HalfEdges[cur];
				int o1 = he1.Origin,
					o2 = geometry.HalfEdges[next].Origin,
					o3 = geometry.HalfEdges[geometry.Next(next)].Origin;
				Vector2 v1 = geometry.Vertices[o1].Pos,
						v2 = geometry.Vertices[o2].Pos,
						v3 = geometry.Vertices[o3].Pos;
				if (Vector2.DotProduct(new Vector2(-(v2 - v1).Y, (v2 - v1).X), v3 - v2) < 0) {
					return false;
				}
				foreach (var i in polygon) {
					var j = geometry.HalfEdges[i].Origin;
					if (j != o1 && j != o2 && j != o3) {
						var v = geometry.Vertices[j].Pos;
						if (Area(v, v1, v2) >= 0 && Area(v, v2, v3) >= 0 && Area(v, v3, v1) >= 0) {
							return false;
						}
					}
				}
				return true;
			}
			var current = polygon.First;
			while (polygon.Count > 2) {
				var next = current.Next ?? polygon.First;
				if (CanCreateTriangle(current.Value, next.Value)) {
					var he1 = geometry.HalfEdges[current.Value];
					var he2 = geometry.HalfEdges[next.Value];
					he2.Index = next.Value;
					Connect(geometry, he1, he2);
					queue.Enqueue(geometry.HalfEdges.Count - 1);
					var twin = new HalfEdge(geometry.HalfEdges.Count, he1.Origin, geometry.HalfEdges.Count - 1);
					polygon.AddAfter(next, geometry.HalfEdges.Count);
					geometry.HalfEdges.Add(twin);
					geometry.HalfEdges.Add(new HalfEdge(-1, geometry.Next(he2).Origin, -1));
					geometry.HalfEdges.Add(Geometry.DummyHalfEdge);
					geometry.RemoveTriangle(he1.Index);
					geometry.RemoveTriangle(he2.Index);
					if (keepConstrainedEdges) {
						if (he1.Index != -1) {
							KeepConstrainedEdges(geometry, he1.Index);
						}
						KeepConstrainedEdges(geometry, he2.Index);
					}
					polygon.Remove(next);
					polygon.Remove(current);
					current = polygon.First;
				} else {
					current = next;
				}
			}
			System.Diagnostics.Debug.Assert(polygon.Count == 2);
			var last = geometry.HalfEdges[polygon.Last.Value];
			var lastOriginal = geometry.HalfEdges[geometry.HalfEdges[polygon.First.Value].Twin];
			lastOriginal.Twin = last.Twin;
			if (last.Twin != -1) {
				geometry.MakeTwins(last.Twin, lastOriginal.Index);
			}
			geometry.RemoveTriangle(last.Index);
			if (keepConstrainedEdges) {
				KeepConstrainedEdges(geometry, polygon.Last.Value);
			}
			geometry.HalfEdges[lastOriginal.Index] = lastOriginal;
			queue.Enqueue(lastOriginal.Index);
			geometry.HalfEdges[polygon.First.Value] = Geometry.DummyHalfEdge;
			return queue;
		}

		private void RestoreDelaunayProperty(Geometry geometry, Queue<int> queue)
		{
			while (queue.Count > 0) {
				var i = queue.Dequeue();
				var he = geometry.HalfEdges[i];
				for (int j = 0; j < 3; ++j) {
					if (
						!he.Constrained && he.Twin != -1 &&
						InCircumcircle(geometry, he, geometry.Vertices[geometry.Prev(geometry.HalfEdges[he.Twin]).Origin])
					) {
						Flip(geometry, he);
						queue.Enqueue(i);
						queue.Enqueue(geometry.HalfEdges[geometry.Next(i)].Twin);
						break;
					}
					i = geometry.Next(i);
					he = geometry.HalfEdges[i];
				}
			}
		}

		private void Flip(Geometry geometry, HalfEdge he)
		{
			System.Diagnostics.Debug.Assert(he.Twin != -1);
			var twin = geometry.HalfEdges[he.Twin];
			var nextTwin = geometry.Next(twin);
			var prevTwin = geometry.Next(nextTwin);
			var next = geometry.Next(he);
			var prev = geometry.Next(next);
			var i = geometry.Next(he.Index);
			var j = geometry.Next(twin.Index);
			geometry.HalfEdges[i] = new HalfEdge(i, prevTwin.Origin, j);
			geometry.HalfEdges[j] = new HalfEdge(j, prev.Origin, i);
			nextTwin.Index = he.Index;
			geometry.HalfEdges[he.Index] = nextTwin;
			if (nextTwin.Twin != -1) {
				geometry.MakeTwins(nextTwin.Index, nextTwin.Twin);
			}
			next.Index = twin.Index;
			geometry.HalfEdges[twin.Index] = next;
			if (next.Twin != -1) {
				geometry.MakeTwins(next.Index, next.Twin);
			}
		}

		private HalfEdge FakeTwin(Geometry geometry, HalfEdge he1, HalfEdge he2)
		{
			var twin = new HalfEdge(geometry.HalfEdges.Count, he1.Origin, geometry.HalfEdges.Count - 1);
			geometry.HalfEdges.Add(twin);
			geometry.HalfEdges.Add(new HalfEdge(-1, geometry.Next(he2).Origin, -1));
			geometry.HalfEdges.Add(Geometry.DummyHalfEdge);
			return twin;
		}

		private Queue<int> RemovePolygon(Geometry geometry, LinkedList<int> polygon, bool keepConstrainedEdges = false)
		{
			geometry.RemoveTriangle(polygon.First.Value);
			geometry.RemoveTriangle(polygon.Last.Value);
			if (keepConstrainedEdges) {
				KeepConstrainedEdges(geometry, polygon.First.Value);
				KeepConstrainedEdges(geometry, polygon.Last.Value);
			}
			polygon.Remove(polygon.First);
			polygon.Remove(polygon.Last);
			var current = polygon.First;
			var queue = new Queue<int>();
			while (current != null) {
				var next = current.Next;
				geometry.RemoveHalfEdge(geometry.Next(current.Value));
				geometry.RemoveHalfEdge(geometry.Prev(current.Value));
				if (keepConstrainedEdges) {
					KeepConstrainedEdges(geometry, current.Value);
				}
				current = next;
			}
			current = polygon.First;
			while (current?.Next != null) {
				var he1 = geometry.HalfEdges[current.Value];
				var he2 = geometry.HalfEdges[current.Next.Value];
				he2.Index = current.Next.Value;
				var v1 = geometry.Vertices[he1.Origin].Pos;
				var v2 = geometry.Vertices[he2.Origin].Pos;
				var v3 = geometry.Vertices[geometry.HalfEdges[geometry.Next(current.Next.Value)].Origin].Pos;
				if (Area(v1, v2, v3) > 0) {
					Connect(geometry, he1, he2);
					queue.Enqueue(geometry.HalfEdges.Count - 1);
					var twin = FakeTwin(geometry, he1, he2);
					polygon.AddAfter(current.Next, twin.Index);
					geometry.RemoveTriangle(he1.Index);
					geometry.RemoveHalfEdge(he2.Index);
					polygon.Remove(current.Next);
					polygon.Remove(current);
					current = polygon.First;
					continue;
				}
				current = current.Next;
			}
			foreach (var side in polygon) {
				geometry.Untwin(side);
				geometry.RemoveTriangle(side);
			}
			return queue;
		}

		private float PointToSegmentSqrDistance(Vector2 v, Vector2 w, Vector2 p)
		{
			var t = Mathf.Max(0f, Mathf.Min(Vector2.DotProduct(p - v, w - v) / (w - v).SqrLength, 1));
			return (v + t * (w - v)).Length;
		}

		private LinkedList<int> GetTriangulationBoundary(Geometry geometry, Vertex vertex, HalfEdge start)
		{
			var minD = float.MaxValue;
			var current = start;
			for (int i = 0; i < 3; i++) {
				var s1 = geometry.Vertices[current.Origin].Pos;
				var s2 = geometry.Vertices[geometry.Next(current).Origin].Pos;
				var p1 = geometry.Vertices[geometry.Prev(current).Origin].Pos;
				var d = PointToSegmentSqrDistance(s1, s2, vertex.Pos);
				if (AreOnOppositeSides(s1, s2, p1, vertex.Pos) && d < minD) {
					minD = d;
					start = current;
				}
				current = geometry.Next(current);
			}
			var boundary = new LinkedList<int>();
			boundary.AddFirst(start.Index);
			do {
				var prev = geometry.HalfEdges[boundary.Last.Value];
				var next = geometry.Next(prev);
				while (next.Twin != -1) {
					next = geometry.HalfEdges[geometry.Next(next.Twin)];
				}
				boundary.AddLast(next.Index);
			} while (boundary.First.Value != boundary.Last.Value);
			boundary.RemoveLast();
			return boundary;
		}

		private LinkedList<int> GetVisibleBoundary(Geometry geometry, Vertex vertex, LinkedList<int> boundary)
		{
			var broke = false;
			LinkedListNode<int> first = null;
			LinkedList<int> visibleBoundary = new LinkedList<int>();
			foreach (var side in boundary) {
				var currentHe = geometry.HalfEdges[side];
				var c = geometry.Vertices[currentHe.Origin].Pos;
				var d = geometry.Vertices[geometry.Next(currentHe).Origin].Pos;
				var listNode = boundary.First;
				if (Area(c, vertex.Pos, d) > 0) {
					while (listNode != null) {
						var current = geometry.HalfEdges[listNode.Value];
						var a = geometry.Vertices[current.Origin].Pos;
						var b = geometry.Vertices[geometry.Next(current).Origin].Pos;
						if (
							listNode.Value != side &&
							(b != c && PolygonMeshUtils.LineLineIntersection(a, b, vertex.Pos, c, out _) ||
							 a != d && PolygonMeshUtils.LineLineIntersection(a, b, vertex.Pos, d, out _))
						) {
							break;
						}
						listNode = listNode.Next;
					}
				}
				if (listNode != null) {
					if (!broke) {
						first = visibleBoundary.First;
					}
					broke = true;
					continue;
				}
				if (broke) {
					if (first == null) {
						visibleBoundary.AddFirst(side);
					} else {
						visibleBoundary.AddBefore(first, side);
					}
				} else {
					visibleBoundary.AddLast(side);
				}
			}
			return visibleBoundary;
		}

		public Queue<int> TriangulateVisibleBoundary(Geometry geometry, LinkedList<int> boundary, int vi)
		{
			var current = boundary.First;
			var q = new Queue<int>();
			while (current != null) {
				var v = geometry.HalfEdges[geometry.Next(current.Value)].Origin;
				geometry.Connect(v, geometry.HalfEdges[current.Value].Origin, vi);
				geometry.MakeTwins(current.Value, geometry.HalfEdges.Count - 3);
				q.Enqueue(geometry.HalfEdges.Count - 1);
				if (current.Previous != null) {
					geometry.MakeTwins(geometry.HalfEdges.Count - 2, geometry.HalfEdges.Count - 4);
				}
				current = current.Next;
			}
			return q;
		}

		private bool AreEdgesCollinear(Vector2 v11, Vector2 v12, Vector2 v21, Vector2 v22)
		{
			// p + tr = q + us
			Vector2 p = v11, q = v21, r = v12 - v11, s = v22 - v21;
			var denominator = Vector2.CrossProduct(r, s);
			var numerator = Vector2.CrossProduct((q - p), r);
			return Math.Abs(denominator) < Mathf.ZeroTolerance && Math.Abs(numerator) < Mathf.ZeroTolerance;
		}

		private HalfEdge RandomValidEdge(Geometry geometry)
		{
			while (true) {
				var he = geometry.HalfEdges[random.Next(0, geometry.HalfEdges.Count - 1)];
				if (he.Index != -1) {
					return he;
				}
			}
		}

		public void InsertConstrainedEdge(Geometry geometry, (int a, int b) ce)
		{
			var start = FindIncidentEdge(geometry,
				LocateTriangle(geometry, RandomValidEdge(geometry), geometry.Vertices[ce.Item1], out _), ce.a);
			var current = start;
			Vector2 a = geometry.Vertices[ce.a].Pos, b = geometry.Vertices[ce.b].Pos;
			var upPolygon = new LinkedList<int>();
			var downPolygon = new LinkedList<int>();
			var forward = true;
			// Rotate over point to find proper triangle to traverse further
			while (true) {
				var next = geometry.Next(current);
				var prev = geometry.Prev(current);
				// Check if constrained edge equals to existing edge of triangle (and make it constrained if it's true)
				if (next.Origin == ce.b) {
					geometry.SetConstrain(current.Index, true);
					return;
				}
				if (prev.Origin == ce.b) {
					geometry.SetConstrain(prev.Index, true);
					return;
				}
				Vector2 nextV = geometry.Vertices[next.Origin].Pos;
				Vector2 prevV = geometry.Vertices[prev.Origin].Pos;
				// Check whether constrained edge is collinear to incident edges (then split constrained edge if it's true)
				if (AreEdgesCollinear(a, nextV, a, b)) {
					geometry.SetConstrain(current.Index, true);
					InsertConstrainedEdge(geometry, (next.Origin, ce.b));
					return;
				}
				if (AreEdgesCollinear(a, prevV, a, b)) {
					geometry.SetConstrain(prev.Index, true);
					InsertConstrainedEdge(geometry, (prev.Origin, ce.b));
					return;
				}
				if (PolygonMeshUtils.LineLineIntersection(nextV, prevV, a, b, out _)) {
					break;
				}
				forward = forward ? prev.Twin != -1 : forward;
				current = forward ? geometry.HalfEdges[prev.Twin]  : geometry.HalfEdges[geometry.Next(current.Twin)];
			}
			upPolygon.AddLast(current.Index);
			downPolygon.AddFirst(geometry.Prev(current.Index));
			// Traverse towards constrained edge direction
			geometry.RemoveTriangle(current.Index);
			current = geometry.HalfEdges[geometry.Next(current).Twin];
			while (true) {
				var next = geometry.Next(current);
				var prev = geometry.Prev(current);
				var currentV = geometry.Vertices[current.Origin].Pos;
				var nextV = geometry.Vertices[next.Origin].Pos;
				var prevV = geometry.Vertices[prev.Origin].Pos;
				if (prev.Origin == ce.b) {
					upPolygon.AddLast(next.Index);
					downPolygon.AddFirst(prev.Index);
					var first = geometry.HalfEdges[upPolygon.First.Value];
					geometry.Connect(prev.Origin, first.Origin, 0);
					geometry.Connect(first.Origin, prev.Origin, 0);
					geometry.MakeTwins(geometry.HalfEdges.Count - 3, geometry.HalfEdges.Count - 6);
					geometry.SetConstrain(geometry.HalfEdges.Count - 3, true);
					upPolygon.AddLast(geometry.HalfEdges.Count - 6);
					downPolygon.AddFirst(geometry.HalfEdges.Count - 3);
					var q1 = TriangulatePolygonByEarClipping(geometry, upPolygon);
					var q2 = TriangulatePolygonByEarClipping(geometry, downPolygon);
					RestoreDelaunayProperty(geometry, q1);
					RestoreDelaunayProperty(geometry, q2);
					return;
				}
				geometry.RemoveTriangle(current.Index);
				if (PolygonMeshUtils.LineLineIntersection(a, b, nextV, prevV, out _)) {
					downPolygon.AddFirst(prev.Index);
					current = geometry.HalfEdges[next.Twin];
				} else if (PolygonMeshUtils.LineLineIntersection(a, b, currentV, prevV, out _)) {
					upPolygon.AddLast(next.Index);
					current = geometry.HalfEdges[prev.Twin];
				}
			}
		}

		public void InsertConstrainedEdges(Geometry geometry, List<(int, int)> constrainedEdges)
		{
			if (constrainedEdges.Count == 0) {
				return;
			}
			foreach (var constrainedEdge in constrainedEdges) {
				InsertConstrainedEdge(geometry, constrainedEdge);
			}
			geometry.Invalidate();
		}
	}
}
