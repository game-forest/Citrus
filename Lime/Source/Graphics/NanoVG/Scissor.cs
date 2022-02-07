using System.Runtime.InteropServices;
using Lime;

namespace NanoVG
{
	[StructLayout(LayoutKind.Sequential)]
	internal struct Scissor
	{
		public Transform Transform;
		public Vector2 Extent;
	}
}
