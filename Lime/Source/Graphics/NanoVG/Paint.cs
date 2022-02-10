using System;
using System.Runtime.InteropServices;

namespace Lime.NanoVG
{
	[StructLayout(LayoutKind.Sequential)]
	public struct Paint
	{
		public Transform Transform;
		public Vector2 Extent;
		public float Radius;
		public float Feather;
		public Color4 InnerColor;
		public Color4 OuterColor;
		public int Image;
		
		public Paint(Color4 color)
		{
			Transform = new Transform();
			Extent = new Vector2();
			Transform.SetIdentity();
			Radius = 0.0f;
			Feather = 1.0f;
			InnerColor = color;
			OuterColor = color;
			Image = 0;
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
			p.Transform.T1 = dy;
			p.Transform.T2 = -dx;
			p.Transform.T3 = dx;
			p.Transform.T4 = dy;
			p.Transform.T5 = startX - dx * large;
			p.Transform.T6 = startY - dy * large;
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
			p.Transform.SetIdentity();
			p.Transform.T5 = centerX;
			p.Transform.T6 = centerY;
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
			p.Transform.SetIdentity();
			p.Transform.T5 = x + width * 0.5f;
			p.Transform.T6 = y + height * 0.5f;
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
			p.Transform.SetRotate(angle);
			p.Transform.T5 = cx;
			p.Transform.T6 = cy;
			p.Extent.X = w;
			p.Extent.Y = h;
			p.Image = image;
			p.InnerColor = p.OuterColor = Color4.FromFloats(1.0f, 1.0f, 1.0f, alpha);
			return p;
		}
	}
}