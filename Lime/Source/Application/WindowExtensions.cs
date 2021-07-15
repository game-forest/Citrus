using System;
using System.Runtime.InteropServices;

namespace Lime
{
	public static class WindowExtensions
	{
#if WIN
		[DllImport("user32.dll")]
		private static extern int ShowWindow(IntPtr hWnd, uint Msg);

		private const uint SW_RESTORE = 0x09;

		public static void Restore(this System.Windows.Forms.Form form)
		{
			if (form.WindowState == System.Windows.Forms.FormWindowState.Minimized) {
				ShowWindow(form.Handle, SW_RESTORE);
			}
		}
#endif
	}
}
