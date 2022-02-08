#if WIN
using System.Linq;
using WinForms = System.Windows.Forms;

namespace Lime
{
	/// <summary>
	/// Brings access to files and folders through the dialog screen.
	/// </summary>
	public class FileDialog : IFileDialog
	{
		/// <summary>
		/// Title text of the file dialog window.
		/// </summary>
		public string Title { get; set; }
		/// <summary>
		/// Gets or sets an array of allowed file types. Example { "txt", "png" }.
		/// </summary>
		public string[] AllowedFileTypes { get; set; }

		/// <summary>
		/// Gets or sets a value indicating whether it is allowed to select more then one file in the open file dialog.
		/// </summary>
		public bool AllowsMultipleSelection { get; set; }

		/// <summary>
		/// Gets or sets a value indicating whether the folder browser dialog has the "New Folder" button.
		/// </summary>
		public bool CanCreateDirectories { get; set; }

		/// <summary>
		/// Gets a string containing file or folder path selected in the dialog.
		/// </summary>
		public string FileName { get; private set; }

		/// <summary>
		/// Gets an array of strings containing paths of all selected files.
		/// </summary>
		public string[] FileNames { get; private set; }

		/// <summary>
		/// Gets or sets a mode indicating whether this dialog opens file, saves file or selects folder.
		/// </summary>
		public FileDialogMode Mode { get; set; }

		/// <summary>
		/// Gets or sets the dialog's initial directory.
		/// </summary>
		public string InitialDirectory { get; set; }

		/// <summary>
		/// Gets or sets initial file name showed in file dialog.
		/// </summary>
		public string InitialFileName { get; set; }

		/// <summary>
		/// Shows dialog.
		/// </summary>
		/// <returns>return true if user clicks OK in the dialog.</returns>
		public bool RunModal()
		{
			FileName = null;
			FileNames = null;
			return Mode switch {
				FileDialogMode.Open => ShowOpenFileDialog(),
				FileDialogMode.Save => ShowSaveFileDialog(),
				FileDialogMode.SelectFolder => ShowFolderBrowserDialog(),
				_ => false,
			};
		}

		private void SetFilter(WinForms.FileDialog dialog)
		{
			if (AllowedFileTypes != null) {
				var f = string.Join("; ", AllowedFileTypes.Select(s => "*." + s));
				dialog.Filter = f + '|' + f;
			}
		}

		private bool ShowFileDialog(WinForms.FileDialog dialog)
		{
			if (InitialDirectory != null) {
				dialog.InitialDirectory = InitialDirectory;
			}
			if (dialog.ShowDialog() == WinForms.DialogResult.OK) {
				FileName = dialog.FileName;
				FileNames = dialog.FileNames;
				return true;
			}
			return false;
		}

		private bool ShowFolderBrowserDialog()
		{
			using (var folderBrowserDialog = new WinForms.FolderBrowserDialog()) {
				folderBrowserDialog.UseDescriptionForTitle = true;
				if (!string.IsNullOrEmpty(Title)) {
					folderBrowserDialog.Description = Title;
				}
				if (InitialDirectory != null) {
					folderBrowserDialog.SelectedPath = InitialDirectory;
				}
				if (folderBrowserDialog.ShowDialog() == WinForms.DialogResult.OK) {
					FileName = folderBrowserDialog.SelectedPath;
					FileNames = new[] { FileName };
					return true;
				}
			}
			return false;
		}

		private bool ShowOpenFileDialog()
		{
			using var openFileDialog = new WinForms.OpenFileDialog();
			SetFilter(openFileDialog);
			if (!string.IsNullOrEmpty(Title)) {
				openFileDialog.Title = Title;
			}
			openFileDialog.RestoreDirectory = true;
			openFileDialog.Multiselect = AllowsMultipleSelection;
			openFileDialog.FileName = InitialFileName ?? string.Empty;
			return ShowFileDialog(openFileDialog);
		}

		private bool ShowSaveFileDialog()
		{
			using var saveFileDialog = new WinForms.SaveFileDialog();
			SetFilter(saveFileDialog);
			if (!string.IsNullOrEmpty(Title)) {
				saveFileDialog.Title = Title;
			}
			saveFileDialog.RestoreDirectory = true;
			saveFileDialog.FileName = InitialFileName ?? string.Empty;
			return ShowFileDialog(saveFileDialog);
		}
	}
}
#endif
