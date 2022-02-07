using System.Runtime.InteropServices;
using Lime;

namespace NanoVG
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

		public void Zero()
		{
			Transform.Zero();
			Extent = Vector2.Zero;
			Radius = 0;
			Feather = 0;
			InnerColor = Color4.Transparent;
			OuterColor = Color4.Transparent;
			Image = 0;
		}
	}
}