using System.ComponentModel.Composition;
using Lime;

namespace Orange
{
	public static partial class Actions
	{
		[Export(nameof(OrangePlugin.MenuItems))]
		[ExportMetadata("Label", "Unpack Bundle")]
		[ExportMetadata("Priority", 100)]
		public static void UnpackBundle()
		{
			var dialog = new FileDialog {
				AllowedFileTypes = new[] { "*" },
				Mode = FileDialogMode.Open,
				Title = "Select bundle",
				AllowsMultipleSelection = true,
			};
			bool? dialogCanceled = null;
			Application.InvokeOnMainThread(() => dialogCanceled = !dialog.RunModal());
			while (!dialogCanceled.HasValue) {
				System.Threading.Thread.Sleep(50);
			}
			if (dialogCanceled.Value) {
				return;
			}
			foreach (var filename in dialog.FileNames) {
				System.Console.WriteLine($"Unpacking asset bundle `{filename}`.");
				BundleUtils.UnpackBundle(filename, filename + ".Unpacked");
			}
		}
	}
}
