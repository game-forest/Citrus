using System;
using System.IO;
using Lime;
using Tangerine.Core;
using Tangerine.UI;

namespace Tangerine
{
	public class FileOpenProject : CommandHandler
	{
		public override void Execute()
		{
			var dlg = new FileDialog { AllowedFileTypes = new string[] { "citproj" }, Mode = FileDialogMode.Open };
			if (dlg.RunModal()) {
				Execute(dlg.FileName);
			}
		}

		public static bool Execute(string fileName)
		{
			if (fileName != default && Project.Current.Close() && fileName.Length > 0) {
				new Project(fileName);
				AddRecentProject(fileName);
				return true;
			}
			return false;
		}

		public static void AddRecentProject(string path)
		{
			var prefs = AppUserPreferences.Instance;
			prefs.RecentProjects.Remove(path);
			prefs.RecentProjects.Insert(0, path);
			if (prefs.RecentProjects.Count > prefs.RecentProjectCount) {
				prefs.RecentProjects.RemoveAfter(index: prefs.RecentProjectCount - 1);
			}
			UserPreferences.Instance.Save();
		}
	}


	public class ProjectNew : CommandHandler
	{
		public override void Execute()
		{
			try {
				Orange.NewProject.NewProjectAction(FileOpenProject.Execute);
			} catch (System.Exception e) {
				AlertDialog.Show(e.Message);
			}
		}
	}

	public class FileCloseProject : CommandHandler
	{
		public override void Execute()
		{
			Project.Current.Close();
			UserPreferences.Instance.Save();
		}

		public override void RefreshCommand(ICommand command)
		{
			command.Enabled = Project.Current != Project.Null;
		}
	}

	public class FileNew : CommandHandler
	{
		private readonly DocumentFormat format;
		private readonly Type rootType;

		public FileNew(DocumentFormat format = DocumentFormat.Tan, Type rootType = null)
		{
			this.format = format;
			this.rootType = rootType;
		}

		public override void RefreshCommand(ICommand command)
		{
			command.Enabled = Project.Current != Project.Null;
		}

		public override void Execute()
		{
			if (Document.Current != null && Document.Current.History.IsTransactionActive) {
				return;
			}
			Project.Current.NewDocument(format, rootType);
		}
	}

	public class FileOpen : CommandHandler
	{
		public override void RefreshCommand(ICommand command)
		{
			command.Enabled = Project.Current != Project.Null;
		}

		public override void Execute()
		{
			var dlg = new FileDialog {
				AllowedFileTypes = Document.AllowedFileTypes,
				Mode = FileDialogMode.Open,
			};
			if (Document.Current != null) {
				dlg.InitialDirectory = Path.GetDirectoryName(Document.Current.FullPath);
			}
			if (dlg.RunModal()) {
				var document = Project.Current.OpenDocument(dlg.FileName, true);
			}
		}
	}

	public class FileSave : DocumentCommandHandler
	{
		static FileSave()
		{
			Document.PathSelector += SelectPath;
		}

		public override void ExecuteTransaction()
		{
			try {
				Document.Current.Save();
			}
			catch (System.Exception e) {
				ShowErrorMessageBox(e);
			}
		}

		public static void ShowErrorMessageBox(System.Exception e)
		{
			AlertDialog.Show($"Save document error: '{e.Message}'.\nYou may have to upgrade the document format.");
		}

		public static bool SelectPath(out string path)
		{
			var dlg = new FileDialog {
				AllowedFileTypes = new string[] { Document.Current.GetFileExtension() },
				Mode = FileDialogMode.Save,
				InitialDirectory = Project.IsDocumentUntitled(Document.Current.Path) ?
					Project.Current.AssetsDirectory : Path.GetDirectoryName(Document.Current.FullPath),
			};
			path = null;
			if (!dlg.RunModal()) {
				return false;
			}
			if (!Project.Current.TryGetAssetPath(dlg.FileName, out path)) {
				AlertDialog.Show("Can't save the document outside the project directory");
				return false;
			}
			return true;
		}
	}

	public class FileSaveAll : DocumentCommandHandler
	{
		public override void ExecuteTransaction()
		{
			foreach (var doc in Project.Current.Documents) {
				if (doc.Loaded || doc.IsModified) {
					doc.Save();
				}
			}
		}
	}

	public class FileRevert : DocumentCommandHandler
	{
		public override void ExecuteTransaction()
		{
			if (new AlertDialog($"Are you sure you want to revert \"{Document.Current.Path}\"?", "Yes", "Cancel").Show() == 0) {
				Project.Current.RevertDocument(Document.Current);
			}
		}
	}

	public class FileSaveAs : DocumentCommandHandler
	{
		public override void ExecuteTransaction()
		{
			SaveAs();
		}

		public static void SaveAs()
		{
			var dlg = new FileDialog {
				AllowedFileTypes = new string[] { Document.Current.GetFileExtension() },
				Mode = FileDialogMode.Save,
				InitialDirectory = Path.GetDirectoryName(Document.Current.FullPath),
				InitialFileName = Path.GetFileNameWithoutExtension(Document.Current.Path)
			};
			if (dlg.RunModal()) {
				string assetPath;
				if (!Project.Current.TryGetAssetPath(dlg.FileName, out assetPath)) {
					AlertDialog.Show("Can't save the document outside the project directory");
				} else {
					try {
						Document.Current.SaveAs(assetPath);
					}
					catch (System.Exception e) {
						FileSave.ShowErrorMessageBox(e);
					}
				}
			}
		}
	}

	public class FileClose : DocumentCommandHandler
	{
		public override void ExecuteTransaction()
		{
			if (Document.Current != null) {
				Project.Current.CloseDocument(Document.Current);
			}
		}
	}

	public class FileCloseAll : DocumentCommandHandler
	{
		public override void ExecuteTransaction()
		{
			if (Project.Current.Documents.Count != 0) {
				Project.Current.CloseAllDocuments();
			}
		}
	}

	public class FileCloseAllButCurrent : DocumentCommandHandler
	{
		public override void ExecuteTransaction()
		{
			if (Project.Current.Documents.Count != 0) {
				Project.Current.CloseAllDocumentsButThis(Document.Current);
			}
		}
	}
}
