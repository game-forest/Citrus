using System.Runtime.InteropServices;

namespace Lime.NanoVG
{
	[StructLayout(LayoutKind.Sequential)]
	internal struct Scissor
	{
		public Matrix32 Transform;
		public Vector2 Extent;
	}
}
