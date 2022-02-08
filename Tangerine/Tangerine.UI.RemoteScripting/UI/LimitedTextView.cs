using System.Collections.Generic;
using System.IO;
using Lime;

namespace Tangerine.UI.RemoteScripting
{
	public class LimitedTextView : ThemedTextView
	{
		private static readonly object scrollToEndTaskTag = new object();
		private readonly int maxRowCount;
		private readonly int removeRowCount;

		private string filePath;
		private StreamWriter file;
		private int fileBufferSize;

		public string FilePath
		{
			get => filePath;
			set
			{
				CloseFile();
				filePath = value;
				try {
					file =
						!string.IsNullOrEmpty(filePath) ?
						new StreamWriter(File.Open(filePath, FileMode.Append, FileAccess.Write, FileShare.Read)) :
						null;
				} catch (System.Exception exception) {
					System.Console.WriteLine(exception);
					CloseFile();
					filePath = null;
				}
			}
		}

		public LimitedTextView(int maxRowCount = 500, int removeRowCount = 250)
		{
			this.maxRowCount = maxRowCount;
			this.removeRowCount = removeRowCount;
			TrimWhitespaces = false;
			ICommand viewInExternalEditorCommand;
			ICommand commandCopy;
			ICommand commandClear;
			var menu = new Menu {
				(viewInExternalEditorCommand = new Command("View in External Editor")),
				Command.MenuSeparator,
				(commandCopy = new Command("Copy")),
				(commandClear = new Command("Clear")),
			};
			Updated += _ => {
				if (Input.WasKeyPressed(Key.Mouse0) || Input.WasKeyPressed(Key.Mouse1)) {
					SetFocus();
					Window.Current.Activate();
				}
				if (Input.WasKeyPressed(Key.Mouse1)) {
					viewInExternalEditorCommand.Enabled = !string.IsNullOrEmpty(FilePath);
					menu.Popup();
				}
				if (viewInExternalEditorCommand.WasIssued()) {
					viewInExternalEditorCommand.Consume();
					if (file != null && fileBufferSize > 0) {
						try {
							file.Flush();
							fileBufferSize = 0;
						} catch (System.Exception exception) {
							System.Console.WriteLine(exception);
							CloseFile();
						}
					}
					if (!string.IsNullOrEmpty(FilePath)) {
						Environment.ShellExecute(FilePath);
					}
				}
				if (commandCopy.WasIssued()) {
					commandCopy.Consume();
					Clipboard.Text = Text;
				}
				if (commandClear.WasIssued()) {
					commandClear.Consume();
					Clear();
				}
			};
		}

		public void AppendLine(string text)
		{
			if (text == null) {
				return;
			}
			var isScrolledToEnd =
				Mathf.Abs(Behaviour.ScrollPosition - Behaviour.MaxScrollPosition) < Mathf.ZeroTolerance;
			if (text.Length == 0 || text[^1] != '\n') {
				text += '\n';
			}
			Append(text);
			if (file != null) {
				try {
					file.Write(text);
					fileBufferSize += text.Length;
					const int FileBufferSizeLimit = 2048;
					if (fileBufferSize >= FileBufferSizeLimit) {
						file.Flush();
						fileBufferSize = 0;
					}
				} catch (System.Exception exception) {
					System.Console.WriteLine(exception);
					CloseFile();
				}
			}
			if (Content.Nodes.Count >= maxRowCount) {
				Content.Nodes.RemoveRange(0, removeRowCount);
			}
			if (isScrolledToEnd || Behaviour.Content.LateTasks.AnyTagged(scrollToEndTaskTag)) {
				Behaviour.Content.LateTasks.StopByTag(scrollToEndTaskTag);
				Behaviour.Content.LateTasks.Add(DefferedScrollToEnd, scrollToEndTaskTag);
			}

			IEnumerator<object> DefferedScrollToEnd()
			{
				yield return null;
				ScrollToEnd();
			}
		}

		public void CloseFile()
		{
			try {
				file?.Close();
			} catch (Exception exception) {
				System.Console.WriteLine(exception);
			}
			fileBufferSize = 0;
			file = null;
		}
	}
}
