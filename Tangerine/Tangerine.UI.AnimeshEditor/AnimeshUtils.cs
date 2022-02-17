using System.Collections.Generic;
using System.Linq;
using Lime;

namespace Tangerine.UI.AnimeshEditor
{
	public class AnimeshUtils
	{
		private class Vector2Comparer : IEqualityComparer<Vector2>
		{
			public bool Equals(Vector2 v1, Vector2 v2)
			{
				return Vector2.Distance(v1, v2) <= Mathf.ZeroTolerance;
			}

			public int GetHashCode(Vector2 v)
			{
				// If Equals() returns true for a pair of objects
				// then GetHashCode() must return the same value for these objects.
				// https://docs.microsoft.com/en-us/dotnet/api/system.linq.enumerable.distinct?view=netframework-4.8
				return 0;
			}
		}

		public static float CrossProduct(Vector2 p1, Vector2 p2, Vector2 p3)
		{
			return
				(p1.X - p3.X) * (p2.Y - p3.Y) -
				(p2.X - p3.X) * (p1.Y - p3.Y);
		}

		public static float DistanceFromPointToLine(
			Vector2 point, Vector2 p1, Vector2 p2, out Vector2 intersectionPoint
		) {
			var d = Vector2.Distance(p1, p2);
			if (d <= 1.0f) {
				intersectionPoint = p1;
				return Vector2.Distance(p1, point);
			}

			var u = ((point.X - p1.X) * (p2.X - p1.X) + (point.Y - p1.Y) * (p2.Y - p1.Y)) / (d * d);
			intersectionPoint = new Vector2(p1.X + u * (p2.X - p1.X), p1.Y + u * (p2.Y - p1.Y));
			return Vector2.Distance(intersectionPoint, point);
		}

		public static Vector2 PointProjectionToLine(Vector2 point, Vector2 p1, Vector2 p2, out bool isInside)
		{
			var a = point - p1;
			var b = (p2 - p1) / Vector2.Distance(p1, p2);
			point = Vector2.DotProduct(a, b) * b + p1;
			isInside =
				point.X >= Mathf.Min(p1.X, p2.X) && point.X <= Mathf.Max(p1.X, p2.X) &&
				point.Y >= Mathf.Min(p1.Y, p2.Y) && point.Y <= Mathf.Max(p1.Y, p2.Y);
			return point;
		}

		public static bool PointPointIntersection(Vector2 p1, Vector2 p2, float threshold, out float distance)
		{
			distance = Vector2.Distance(p1, p2);
			return distance <= threshold;
		}

		public static bool PointLineIntersection(
			Vector2 point, Vector2 p1, Vector2 p2, float threshold, out float distance
		) {
			var len = Vector2.Distance(p1, p2);
			distance = DistanceFromPointToLine(point, p1, p2, out var intersectionPoint);
			return
				distance <= threshold &&
				(p1 - intersectionPoint).Length <= len - threshold &&
				(p2 - intersectionPoint).Length <= len - threshold;
		}

		public static bool PointTriangleIntersection(Vector2 point, Vector2 p1, Vector2 p2, Vector2 p3)
		{
			var d1 = CrossProduct(point, p1, p2);
			var d2 = CrossProduct(point, p2, p3);
			var d3 = CrossProduct(point, p3, p1);

			return !(
				((d1 < 0) || (d2 < 0) || (d3 < 0)) &&
				((d1 > 0) || (d2 > 0) || (d3 > 0))
			);
		}

		public static bool LineLineIntersection(
			Vector2 a1, Vector2 b1, Vector2 a2, Vector2 b2, out Vector2 intersectionPoint
		) {
			intersectionPoint = new Vector2(float.NaN, float.NaN);
			var d = (a1.X - b1.X) * (a2.Y - b2.Y) - (a1.Y - b1.Y) * (a2.X - b2.X);
			var uNumerator = (a1.Y - b1.Y) * (a1.X - a2.X) - (a1.X - b1.X) * (a1.Y - a2.Y);
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

		public static bool PointPolygonIntersection(Vector2 point, Vector2[] polygon)
		{
			var rightmostPointX = float.MinValue;
			for (var i = 0; i < polygon.Length; ++i) {
				rightmostPointX = Mathf.Max(polygon[i].X, rightmostPointX);
			}
			if (point.X > rightmostPointX) {
				return false;
			}
			LinePolygonIntersection(
				point,
				new Vector2(1000 * Mathf.Abs(rightmostPointX), point.Y),
				polygon,
				out var intersectionPoints
			);
			return intersectionPoints.Count % 2 == 1;
		}

		public static bool LinePolygonIntersection(
			Vector2 p1, Vector2 p2, Vector2[] polygon, out List<Vector2> intersectionPoints
		) {
			intersectionPoints = new List<Vector2>();
			for (var i = 0; i < polygon.Length; ++i) {
				if (
					LineLineIntersection(
						p1, p2, polygon[i], polygon[i + 1 == polygon.Length ? 0 : i + 1], out var point
					)
				) {
					intersectionPoints.Add(point);
				}
			}
			intersectionPoints = intersectionPoints.Distinct(new Vector2Comparer()).ToList();
			return intersectionPoints.Count > 0;
		}

		public static bool PolylinePolygonIntersection(
			Vector2[] polyline, Vector2[] polygon, out List<Vector2> intersectionPoints
		) {
			intersectionPoints = new List<Vector2>();
			for (var i = 0; i < polyline.Length - 1; ++i) {
				if (LinePolygonIntersection(polyline[i], polyline[i + 1], polygon, out var points)) {
					foreach (var point in points) {
						intersectionPoints.Add(point);
					}
				}
			}
			return intersectionPoints.Count > 0;
		}

		public static float[] CalcSegmentRelativeBarycentricCoordinates(Vector2 point, Vector2 p1, Vector2 p2)
		{
			var len = Vector2.Distance(p1, p2);
			var w1 = 1 - Vector2.Distance(point, p1) / len;
			var w2 = 1 - w1;
			return new[] { w1, w2 };
		}

		public static float[] CalcTriangleRelativeBarycentricCoordinates(
			Vector2 point, Vector2 p1, Vector2 p2, Vector2 p3
		) {
			var w1 =
				((p2.Y - p3.Y) * (point.X - p3.X) + (p3.X - p2.X) * (point.Y - p3.Y)) /
				((p2.Y - p3.Y) * (p1.X - p3.X) + (p3.X - p2.X) * (p1.Y - p3.Y));

			var w2 =
				((p3.Y - p1.Y) * (point.X - p3.X) + (p1.X - p3.X) * (point.Y - p3.Y)) /
				((p2.Y - p3.Y) * (p1.X - p3.X) + (p3.X - p2.X) * (p1.Y - p3.Y));

			var w3 = 1 - w1 - w2;

			return new[] { w1, w2, w3 };
		}

		public static float SqrDistanceFromPointToSegment(Vector2 v, Vector2 w, Vector2 p)
		{
			var l2 = (w - v).SqrLength;
			if (l2 == 0) {
				return (p - v).SqrLength;
			}
			var t = Mathf.Max(0, Mathf.Min(1, Vector2.DotProduct(p - v, w - v) / l2));
			var proj = v + t * (w - v);
			return (p - proj).SqrLength;
		}

		public static void RenderVertex(
			Vector2 pos, float backgroundRadius, float radius, Color4 backgroundColor, Color4 foregroundColor)
		{
			Renderer.DrawRound(
				pos,
				backgroundRadius,
				numSegments: 32,
				backgroundColor,
				backgroundColor
			);
			Renderer.DrawRound(
				pos,
				radius,
				numSegments: 32,
				foregroundColor,
				foregroundColor
			);
		}

		public static void RenderLine(
			Vector2 p1,
			Vector2 p2,
			Vector2 backgroundSize,
			Vector2 foregroundSize,
			Color4 backgroundColor,
			Color4 foregroundColor,
			bool isDashed = false
		) {
			if (isDashed) {
				Renderer.DrawDashedLine(
					p1,
					p2,
					backgroundColor,
					backgroundSize
				);
				Renderer.DrawDashedLine(
					p1,
					p2,
					foregroundColor,
					foregroundSize
				);
			} else {
				Renderer.DrawLine(
					p1,
					p2,
					backgroundColor,
					backgroundSize.X
				);
				Renderer.DrawLine(
					p1,
					p2,
					foregroundColor,
					foregroundSize.X
				);
			}
		}

		public static void RenderTriangle(Vector2 p1, Vector2 p2, Vector2 p3, Color4 color)
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
				new Vertex { Pos = p1, Color = color },
				new Vertex { Pos = p2, Color = color },
				new Vertex { Pos = p3, Color = color },
			};
			Renderer.DrawTriangleStrip(texture, vertices, vertices.Length);
		}
	}
}
