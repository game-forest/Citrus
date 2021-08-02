using Lime;

namespace Tangerine.UI
{
	/// <summary>
	/// This class provides information about engine and current project
	/// </summary>
	public static class About
	{
		public static void DisplayInformation()
		{
			string info = "Hello";
			var alertDialog = new AlertDialog(info, "Copy", "Close");
			switch (alertDialog.Show()) {
				case 0: {
						// Copy to clipboard
						Clipboard.Text = info;
						break;
					}
			}
		}
	}
}
