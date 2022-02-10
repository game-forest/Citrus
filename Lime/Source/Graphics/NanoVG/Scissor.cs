﻿using System.Runtime.InteropServices;

namespace Lime.NanoVG
{
	[StructLayout(LayoutKind.Sequential)]
	internal struct Scissor
	{
		public Transform Transform;
		public Vector2 Extent;
	}
}
