using System;
using System.Runtime.InteropServices;

namespace Lime.NanoVG
{
	[StructLayout(LayoutKind.Sequential)]
	public struct Paint
	{
		public Matrix32 Transform;
		public Vector2 Extent;
		public float Radius;
		public float Feather;
		public Color4 InnerColor;
		public Color4 OuterColor;
		public int Image;
		
		public Paint(Color4 color)
		{
			Transform = new Matrix32();
			Extent = new Vector2();
			Transform = Matrix32.Identity;
			Radius = 0.0f;
			Feather = 1.0f;
			InnerColor = color;
			OuterColor = color;
			Image = 0;
		}

		public static implicit operator Paint(Color4 color)
		{
			return new Paint(color);
		}

		public static Paint LinearGradient(
			float startX,
			float startY,
			float endX,
			float endY,
			Color4 innerColor,
			Color4 outerColor
		) {
			var p = new Paint();
			var large = 1e5f;
			var dx = endX - startX;
			var dy = endY - startY;
			var d = MathF.Sqrt(dx * dx + dy * dy);
			if (d > 0.0001f) {
				dx /= d;
				dy /= d;
			} else {
				dx = 0;
				dy = 1;
			}
			p.Transform.UX = dy;
			p.Transform.UY = -dx;
			p.Transform.VX = dx;
			p.Transform.VY = dy;
			p.Transform.TX = startX - dx * large;
			p.Transform.TY = startY - dy * large;
			p.Extent.X = large;
			p.Extent.Y = large + d * 0.5f;
			p.Radius = 0.0f;
			p.Feather = Math.Max(1.0f, d);
			p.InnerColor = innerColor;
			p.OuterColor = outerColor;
			return p;
		}

		public static Paint RadialGradient(
			float centerX,
			float centerY,
			float innerRadius,
			float outerRadius,
			Color4 innerColor,
			Color4 outerColor
		) {
			var p = new Paint();
			var r = (innerRadius + outerRadius) * 0.5f;
			var f = outerRadius - innerRadius;
			p.Transform = Matrix32.Identity;
			p.Transform.TX = centerX;
			p.Transform.TY = centerY;
			p.Extent.X = r;
			p.Extent.Y = r;
			p.Radius = r;
			p.Feather = Math.Max(1.0f, f);
			p.InnerColor = innerColor;
			p.OuterColor = outerColor;
			return p;
		}

		public static Paint BoxGradient(
			float x,
			float y,
			float width,
			float height,
			float radius,
			float feather,
			Color4 innerColor,
			Color4 outerColor
		) {
			var p = new Paint();
			p.Transform = Matrix32.Identity;
			p.Transform.TX = x + width * 0.5f;
			p.Transform.TY = y + height * 0.5f;
			p.Extent.X = width * 0.5f;
			p.Extent.Y = height * 0.5f;
			p.Radius = radius;
			p.Feather = Math.Max(1.0f, feather);
			p.InnerColor = innerColor;
			p.OuterColor = outerColor;
			return p;
		}

		public static Paint ImagePattern(float cx, float cy, float w, float h, float angle, int image, float alpha)
		{
			var p = new Paint();
			p.Transform = Matrix32.Rotation(angle);
			p.Transform.TX = cx;
			p.Transform.TY = cy;
			p.Extent.X = w;
			p.Extent.Y = h;
			p.Image = image;
			p.InnerColor = p.OuterColor = Color4.FromFloats(1.0f, 1.0f, 1.0f, alpha);
			return p;
		}
	}
}