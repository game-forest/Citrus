using System.Runtime.InteropServices;

namespace Lime.NanoVG
{
	[StructLayout(LayoutKind.Sequential)]
	internal struct Point
	{
		public float X;
		public float Y;
		public float DeltaX;
		public float DeltaY;
		public float Length;
		public float Dmx;
		public float Dmy;
		public byte Flags;

		public void Reset()
		{
			X = Y = DeltaX = DeltaY = Length = Dmx = Dmy = 0;
			Flags = 0;
		}
	}
}