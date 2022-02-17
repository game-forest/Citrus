#if WIN
using System.Collections.Generic;
using System.Windows.Forms;

namespace Lime
{
	internal class Display : IDisplay
	{
		private Screen screen;
		public static List<Display> Displays = new List<Display>();

		public static Display GetDisplay(Screen screen)
		{
			foreach (var d in Displays) {
				if (d.screen.Equals(screen)) {
					return d;
				}
			}

			var nd = new Display { screen = screen };
			Displays.Add(nd);
			return nd;
		}

		public Vector2 Position => new Vector2(
			screen.Bounds.Left / Window.Current.PixelScale,
			screen.Bounds.Top / Window.Current.PixelScale);

		public Vector2 Size => new Vector2(
			screen.Bounds.Width / Window.Current.PixelScale,
			screen.Bounds.Height / Window.Current.PixelScale);
	}
}
#endif
