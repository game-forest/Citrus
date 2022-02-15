using OpenTK.Graphics.OpenGL;

namespace Lime
{
	public static class RendererNvg
	{
		public static void DrawLine(Vector2 a, Vector2 b, NanoVG.Paint paint, float thickness = 1, LineCap cap = LineCap.Butt)
		{
			var nvg = NanoVG.Context.Instance;
			nvg.BeginPath();
			nvg.StrokePaint(paint);
			nvg.StrokeWidth(thickness);
			if (cap != LineCap.Butt) {
				nvg.LineCap(cap);
			}
			nvg.MoveTo(a);
			nvg.LineTo(b);
			nvg.Stroke();
			if (cap != LineCap.Butt) {
				nvg.LineCap(LineCap.Butt);
			}
		}

		public static void DrawRect(Vector2 a, Vector2 b, NanoVG.Paint paint)
		{
			var nvg = NanoVG.Context.Instance;
			nvg.BeginPath();
			nvg.Rect(a, b - a);
			nvg.FillPaint(paint);
			nvg.Fill();
		}

		public static void DrawRoundedRectWithBorder(
			Vector2 a,
			Vector2 b,
			NanoVG.Paint paint,
			Color4 border,
			float thickness,
			float cornerRadius
		) {
			var nvg = NanoVG.Context.Instance;
			nvg.BeginPath();
			nvg.RoundedRect(a.X + 1, a.Y + 1, b.X - a.X - 2, b.Y - a.Y - 2, cornerRadius - 1);
			nvg.FillPaint(paint);
			nvg.Fill();
			nvg.BeginPath();
			nvg.RoundedRect(a.X + 0.5f, a.Y + 0.5f, b.X - a.X - 1, b.Y - a.Y - 1, cornerRadius - 0.5f);
			nvg.StrokeColor(border);
			nvg.StrokeWidth(thickness);
			nvg.Stroke();
		}

		public static void DrawRoundedRectOutline(
			Vector2 a,
			Vector2 b,
			NanoVG.Paint paint,
			float thickness,
			float cornerRadius
		) {
			var nvg = NanoVG.Context.Instance;
			nvg.BeginPath();
			nvg.RoundedRect(a.X + 0.5f, a.Y + 0.5f, b.X - a.X - 1, b.Y - a.Y - 1, cornerRadius - 0.5f);
			nvg.StrokeWidth(thickness);
			nvg.StrokePaint(paint);
			nvg.Stroke();
		}

		public static void DrawQuadrangleOutline(Quadrangle q, NanoVG.Paint paint, float thickness = 1)
		{
			var nvg = NanoVG.Context.Instance;
			nvg.BeginPath();
			nvg.StrokeWidth(thickness);
			nvg.StrokePaint(paint);
			nvg.MoveTo(q[0]);
			for (int i = 0; i < 4; i++) {
				nvg.LineTo(q[(i + 1) % 4]);
			}
			nvg.Stroke();
		}

		public static void DrawQuadrangle(Quadrangle q, NanoVG.Paint paint)
		{
			var nvg = NanoVG.Context.Instance;
			nvg.BeginPath();
			nvg.FillPaint(paint);
			nvg.MoveTo(q[0]);
			for (int i = 0; i < 4; i++) {
				nvg.LineTo(q[(i + 1) % 4]);
			}
			nvg.Fill();
		}

		public static void DrawRound(Vector2 center, float radius, NanoVG.Paint paint)
		{
			var nvg = NanoVG.Context.Instance;
			nvg.BeginPath();
			nvg.Circle(center.X, center.Y, radius);
			nvg.FillPaint(paint);
			nvg.Fill();
		}

		public static void DrawCircle(Vector2 center, float radius, NanoVG.Paint paint, float thickness = 1)
		{
			var nvg = NanoVG.Context.Instance;
			nvg.BeginPath();
			nvg.StrokeWidth(thickness);
			nvg.Circle(center.X, center.Y, radius);
			nvg.StrokePaint(paint);
			nvg.Stroke();
		}
	}
}
