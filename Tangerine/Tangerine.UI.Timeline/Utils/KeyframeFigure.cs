using Lime;

namespace Tangerine.UI
{
	public static class KeyframeFigure
	{
		public static void Render(Vector2 a, Vector2 b, Color4 color, KeyFunction func)
		{
			var nvg = Lime.NanoVG.Context.Instance;
			var segmentWidth = b.X - a.X;
			var segmentHeight = b.Y - a.Y;
			switch (func) {
				case KeyFunction.Linear:
					nvg.BeginPath();
					nvg.FillColor(color);
					nvg.MoveTo(a.X, b.Y - 0.5f);
					nvg.LineTo(b.X, a.Y);
					nvg.LineTo(b.X, b.Y - 0.5f);
					nvg.LineTo(a.X, b.Y - 0.5f);
					nvg.Fill();
					break;

				case KeyFunction.Step:
					var leftSmallRectVertexA = new Vector2(a.X + 0.5f, a.Y + segmentHeight / 2);
					var leftSmallRectVertexB = new Vector2(a.X + segmentWidth / 2, b.Y - 0.5f);
					RendererNvg.DrawRect(leftSmallRectVertexA, leftSmallRectVertexB, color);
					var rightBigRectVertexA = new Vector2(a.X + segmentWidth / 2, a.Y + 0.5f);
					var rightBigRectVertexB = new Vector2(b.X, b.Y - 0.5f);
					RendererNvg.DrawRect(rightBigRectVertexA, rightBigRectVertexB, color);
					break;

				case KeyFunction.Spline:
					var numSegments = 5;
					nvg.BeginPath();
					nvg.FillColor(color);
					nvg.MoveTo(a.X, b.Y - 0.5f);
					var center = new Vector2(a.X, b.Y - 0.5f);
					for (int i = 0; i < numSegments; i++) {
						var r = Vector2.CosSin(i * Mathf.HalfPi / (numSegments - 1));
						nvg.LineTo(center.X + r.X * segmentWidth, center.Y - r.Y * segmentHeight);
					}
					nvg.Fill();
					break;

				case KeyFunction.ClosedSpline:
					var circleCenter = new Vector2(a.X + segmentWidth / 2, a.Y + segmentHeight / 2);
					var circleRadius = 0f;
					if (segmentWidth < segmentHeight) {
						circleRadius = circleCenter.X - a.X - 0.5f;
					} else {
						circleRadius = circleCenter.Y - a.Y - 0.5f;
					}
					nvg.BeginPath();
					nvg.FillColor(color);
					nvg.Circle(circleCenter, circleRadius);
					nvg.Fill();
					break;
				default:
					throw new System.NotImplementedException("Unknown KeyFunction value");
			}
		}
	}
}
