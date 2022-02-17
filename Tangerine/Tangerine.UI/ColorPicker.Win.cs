#if WIN
using System;
using System.Runtime.InteropServices;
using Lime;

namespace Tangerine.UI
{
	public static class ColorPicker
	{
		[DllImport("gdi32")]
		private static extern uint GetPixel(IntPtr hDC, int xPos, int yPos);

		[DllImport("user32.dll", CharSet = CharSet.Auto)]
		private static extern bool GetCursorPos(out Point pt);

		[DllImport("User32.dll", CharSet = CharSet.Auto)]
		private static extern IntPtr GetWindowDC(IntPtr hWnd);

		public static Color4 PickAtCursor()
		{
			var handle = GetWindowDC(IntPtr.Zero);
			Point p;
			GetCursorPos(out p);
			var color = new Color4(GetPixel(handle, p.X, p.Y));
			color.A = 255;
			return color;
		}

		[StructLayout(LayoutKind.Sequential)]
		private struct Point
		{
			public int X;
			public int Y;
			public Point(int x, int y)
			{
				X = x;
				Y = y;
			}
		}
	}
}
#endif
