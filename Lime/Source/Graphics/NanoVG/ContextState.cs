using System.Runtime.InteropServices;

namespace Lime.NanoVG
{
	[StructLayout(LayoutKind.Sequential)]
	internal class ContextState
	{
		public int ShapeAntiAlias;
		public Paint Fill;
		public Paint Stroke;
		public float StrokeWidth;
		public float MiterLimit;
		public LineCap LineJoin;
		public LineCap LineCap;
		public float Alpha;
		public Matrix32 Transform;
		public Scissor Scissor;

		public void Reset()
		{
			Fill = new Paint(Color4.White);
			Stroke = new Paint(Color4.Black);
			ShapeAntiAlias = 1;
			StrokeWidth = 1.0f;
			MiterLimit = 10.0f;
			LineCap = LineCap.Butt;
			LineJoin = LineCap.Miter;
			Alpha = 1.0f;
			Transform = Matrix32.Identity;
			Scissor.Extent.X = -1.0f;
			Scissor.Extent.Y = -1.0f;
		}

		public void CloneTo(ContextState destination)
		{
			destination.ShapeAntiAlias = ShapeAntiAlias;
			destination.Fill = Fill;
			destination.Stroke = Stroke;
			destination.StrokeWidth = StrokeWidth;
			destination.MiterLimit = MiterLimit;
			destination.LineJoin = LineJoin;
			destination.LineCap = LineCap;
			destination.Alpha = Alpha;
			destination.Transform = Transform;
			destination.Scissor = Scissor;
		}
	}
}
