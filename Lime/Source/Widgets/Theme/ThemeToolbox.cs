namespace Lime
{
	public static class ThemeToolbox
	{
		public static void DrawRoundedRect(
			Vector2 position,
			Vector2 size,
			ColorGradient verticalGradient,
			Color4 border,
			float cornerRadius
		) {
			var x = position.X;
			var y = position.Y;
			var w = size.X;
			var h = size.Y;
			var nvg = NanoVG.Context.Instance;
			nvg.BeginPath();
			nvg.RoundedRect(x + 1, y + 1, w - 2, h - 2, cornerRadius - 1);
			nvg.FillPaint(
				NanoVG.Paint.LinearGradient(
					x,
					y,
					x,
					y + h,
					verticalGradient[0].Color,
					verticalGradient[1].Color
				)
			);
			nvg.Fill();
			nvg.BeginPath();
			nvg.RoundedRect(x + 0.5f, y + 0.5f, w - 1, h - 1, cornerRadius - 0.5f);
			nvg.StrokeColor(border);
			nvg.Stroke();
		}
		
		public static void DrawRoundedRect(Vector2 position, Vector2 size, Color4 inner, Color4 border, float cornerRadius)
		{
			var x = position.X;
			var y = position.Y;
			var w = size.X;
			var h = size.Y;
			var nvg = NanoVG.Context.Instance;
			nvg.BeginPath();
			nvg.RoundedRect(x + 1, y + 1, w - 2, h - 2, cornerRadius - 1);
			nvg.FillColor(inner);
			nvg.Fill();
			nvg.BeginPath();
			nvg.RoundedRect(x + 0.5f, y + 0.5f, w - 1, h - 1, cornerRadius - 0.5f);
			nvg.StrokeColor(border);
			nvg.Stroke();
		}
		
		public static void DrawRoundedRectOutline(
			Vector2 position,
			Vector2 size,
			Color4 color,
			float thickness,
			float cornerRadius
		) {
			var x = position.X;
			var y = position.Y;
			var w = size.X;
			var h = size.Y;
			var nvg = NanoVG.Context.Instance;
			nvg.BeginPath();
			nvg.RoundedRect(x + 0.5f, y + 0.5f, w - 1, h - 1, cornerRadius - 0.5f);
			nvg.StrokeWidth(thickness);
			nvg.StrokeColor(color);
			nvg.Stroke();
			nvg.StrokeWidth(1);
		}
	}
}
