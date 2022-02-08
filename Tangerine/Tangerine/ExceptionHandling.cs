using System;
using System.IO;
using System.Text;
using Lime;
using Tangerine.Core;
using Tangerine.UI;

namespace Tangerine
{
	public class ExceptionHandling
	{
		public static void Handle(System.Exception e)
		{
			var errorText = $"{About.GetInformation()} \n {e.Message} \n {e.StackTrace}";
			var alertDialog = new AlertDialog(errorText, "Copy", "Save As", "Ok");
			switch (alertDialog.Show()) {
				case 0: {
					// Copy
					Clipboard.Text = errorText;
					break;
				}
				case 1: {
					// Save As
					if (TrySelectFileSavePath(out var filePath)) {
						using (var fs = new FileStream(filePath, FileMode.Create)) {
							var a = Encoding.UTF8.GetBytes(errorText);
							fs.Write(a, 0, a.Length);
						}
					}
					break;
				}
			}
			var doc = Document.Current;
			if (doc != null) {
				while (doc.History.IsTransactionActive) {
					doc.History.EndTransaction();
				}
				var closeConfirmation = Document.CloseConfirmation;
				try {
					Document.CloseConfirmation = d => {
						var alert = new AlertDialog(
							$"Save the changes to document '{d.Path}' before closing?",
							"Yes",
							"No"
						);
						switch (alert.Show()) {
							case 0: return Document.CloseAction.SaveChanges;
							default: return Document.CloseAction.DiscardChanges;
						}
					};
					var fullPath = doc.FullPath;

					if (!File.Exists(fullPath)) {
						doc.Save();
					}
					var path = doc.Path;
					Project.Current.CloseDocument(doc);
					Project.Current.OpenDocument(path);
				} finally {
					Document.CloseConfirmation = closeConfirmation;
				}
			}
		}

		private static bool TrySelectFileSavePath(out string path, string[] allowedFileTypes = null)
		{
			var test = Project.IsDocumentUntitled(Document.Current?.Path ?? string.Empty) ?
					Project.Current.AssetsDirectory : Path.GetDirectoryName(Document.Current?.FullPath);
			var dlg = new FileDialog {
				AllowedFileTypes = allowedFileTypes ?? new string[] { "txt" },
				Mode = FileDialogMode.Save,
				InitialDirectory = test,
			};
			if (dlg.RunModal()) {
				path = dlg.FileName;
				return true;
			}
			path = null;
			return false;
		}
	}
}
