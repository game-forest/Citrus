using System;
using System.Collections.Generic;
using Lime.PolygonMesh;
using HalfEdge = Lime.PolygonMesh.Geometry.HalfEdge;

namespace Lime.Source.Optimizations
{
	public class Triangulator
	{
		public void AddPoint(Geometry geometry, Vertex vertex)
		{
			geometry.Vertices.Add(vertex);
			Triangulate(GetContourPolygon(geometry, LocateTriangle(geometry, geometry.HalfEdges[0], vertex), vertex), geometry, vertex);
		}

		private HalfEdge LocateTriangle(Geometry geometry, HalfEdge start, Vertex vertex)
		{
			var current = geometry.HalfEdges[0];
			do {
				var next = geometry.Next(current);
				Vector2 p1 = geometry.Vertices[current.Origin].Pos;
				var side = geometry.Vertices[next.Origin].Pos - p1;
				var v1 = geometry.Vertices[geometry.Next(next).Origin].Pos - p1;
				var v2 = vertex.Pos - p1;
				if (
					Mathf.Sign(Vector2.CrossProduct(side, v1)) * Mathf.Sign(Vector2.CrossProduct(side, v2)) < 0 &&
					current.Twin != -1
				) {
					start = current = geometry.HalfEdges[current.Twin];
				}
				current = geometry.Next(current);
			} while (current.Index != start.Index);
			return current;
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

		private LinkedList<(int index, int target)> GetContourPolygon(Geometry geometry, HalfEdge start, Vertex vertex)
		{
			var polygon = new LinkedList<(int index, int target)>();
			polygon.AddLast((start.Index, geometry.HalfEdges[geometry.Next(start.Index)].Origin));
			polygon.AddLast((geometry.Next(start.Index), geometry.HalfEdges[geometry.Prev(start.Index)].Origin));
			polygon.AddLast((geometry.Prev(start.Index), start.Origin));
			var current = polygon.First;
			while (current != null) {
				var edge = geometry.HalfEdges[current.Value.index];
				if (edge.Twin != -1) {
					var twin = geometry.HalfEdges[edge.Twin];
					if (InCircumcircle(geometry, geometry.HalfEdges[edge.Twin], vertex)) {
						polygon.AddAfter(current, (geometry.Next(twin.Index), geometry.HalfEdges[geometry.Prev(twin.Index)].Origin));
						polygon.AddAfter(current.Next, (geometry.Prev(twin.Index), twin.Origin));
						geometry.HalfEdges[edge.Index] = new HalfEdge(-1, edge.Origin, edge.Twin);
						geometry.HalfEdges[twin.Index] = new HalfEdge(-1, twin.Origin, twin.Twin);
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
		private void Connect(Geometry geometry, (int index, int target) v1, (int index, int target) v2, int vi)
		{
			var he1 = geometry.HalfEdges[v1.index];
			var he2 = geometry.HalfEdges[v2.index];
			geometry.Connect(vi, v1.target, v2.target);
			geometry.MakeTwins(geometry.Prev(he1.Index), geometry.HalfEdges.Count - 3);
			if (he2.Twin != -1) {
				geometry.MakeTwins(he2.Twin, geometry.HalfEdges.Count - 2);
			}
		}

		private void Connect(Geometry geometry, (int index, int target) v1, int vi)
		{
			var he1 = geometry.HalfEdges[v1.index];
			geometry.Connect(vi, he1.Origin, v1.target);
			if (he1.Twin != -1) {
				geometry.MakeTwins(he1.Twin, geometry.HalfEdges.Count - 2);
			}
		}

		private void Triangulate(LinkedList<(int index, int target)> polygon, Geometry geometry, Vertex vertex)
		{
			var current = polygon.First;
			while (current != null) {
				var edge = geometry.HalfEdges[current.Value.index];
				if (current.Previous != null) {
					Connect(geometry, current.Previous.Value, current.Value, geometry.Vertices.Count - 1);

				} else {
					Connect(geometry, current.Value, geometry.Vertices.Count - 1);
				}
				edge.Index = -1;
				geometry.HalfEdges[current.Value.index] = edge;
				var currentValue = current.Value;
				currentValue.index = geometry.HalfEdges.Count - 3;
				current.Value = currentValue;
				current = current.Next;
			}
			geometry.MakeTwins(geometry.Prev(polygon.Last.Value.index), polygon.First.Value.index);
			geometry.Invalidate();
		}

	}
}
