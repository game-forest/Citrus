using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Lime;
using Tangerine.Core;
using Tangerine.UI.Docking;
using Debug = System.Diagnostics.Debug;

namespace Tangerine.UI
{
	public class Console
	{
		private class TextViewWriter : TextWriter
		{
			public const int MaxMessages = 500;
			private readonly ThemedTextView textView;
			private readonly StringBuilder logBeforeProjectOpened = new StringBuilder();
			private StreamWriter file;
			private readonly ConcurrentQueue<string> messageQueue = new ConcurrentQueue<string>();

			public string LogFilePath { get; private set; }
			public TextWriter SystemOut;

			public override Encoding Encoding { get; }

			public TextViewWriter(ThemedTextView textView)
			{
				this.textView = textView;
				Project.Opening += ProjectOpening;
				Project.Closing += ProjectClosing;
			}

			public override void WriteLine(string value)
			{
				Write(value + '\n');
			}

			public override void Write(string value)
			{
				value = value.Replace("\r\n", "\n").Replace('\r', '\n');
				value = value.Replace("\n", System.Environment.NewLine);
				messageQueue.Enqueue(value);
			}

			public void ProcessPendingMessages()
			{
				if (messageQueue.Count > 0) {
					while (messageQueue.TryDequeue(out string message)) {
#if DEBUG
						Debug.Write(message);
#endif // DEBUG
						SystemOut?.Write(message);
						var fileMessage = $"[{DateTime.Now.ToLongTimeString()}] {message}";
						if (file != null) {
							file.Write(fileMessage);
						} else {
							logBeforeProjectOpened.Append(fileMessage);
						}
						textView.Append(message);
					}
					file?.Flush();
					Application.InvokeOnNextUpdate(textView.ScrollToEnd);
				}
			}

			private void ProjectOpening(string projectFilePath)
			{
				var logDir = Path.GetDirectoryName(projectFilePath);
				if (!Directory.Exists(logDir)) {
					Directory.CreateDirectory(logDir);
				}
				LogFilePath = Path.Combine(logDir, "TangerineLog.txt");
				if (logBeforeProjectOpened.Length > 0) {
					var logBeforeProjectOpenedString = logBeforeProjectOpened.ToString();
					logBeforeProjectOpened.Clear();
					textView.Append(logBeforeProjectOpenedString);
					File.WriteAllText(LogFilePath, logBeforeProjectOpenedString);
				}
				file = new StreamWriter(File.Open(LogFilePath, FileMode.Append, FileAccess.Write));
			}

			private void ProjectClosing()
			{
				LogFilePath = null;
				logBeforeProjectOpened.Clear();
				file?.Close();
				file = null;
			}

			protected override void Dispose(bool disposing)
			{
				Project.Opening -= ProjectOpening;
				Project.Closing -= ProjectClosing;
				file?.Close();
				file = null;

				base.Dispose(disposing);
			}
		}

		public static Console Instance { get; private set; }

		private readonly Panel panel;
		public readonly Widget RootWidget;
		private ThemedTextView textView;
		private TextViewWriter textWriter;

		public Console(Panel panel)
		{
			if (Instance != null) {
				throw new InvalidOperationException();
			}
			Instance = this;
			this.panel = panel;
			RootWidget = new Widget {
				Layout = new VBoxLayout {
					Spacing = 6
				}
			};
			panel.ContentWidget.AddNode(RootWidget);
			RootWidget.AddNode(CreateTextView());
		}

		private Widget CreateTextView()
		{
			textView = new ThemedTextView {
				SquashDuplicateLines = true
			};
			textWriter = new TextViewWriter(textView) {
				SystemOut = System.Console.Out,
			};
			System.Console.SetOut(textWriter);
			System.Console.SetError(textWriter);
			textView.Tasks.Add(ManageTextViewTask);
			return textView;
		}

		public void Show()
		{
			DockManager.Instance.ShowPanel(panel.Id);
		}

		private IEnumerator<object> ManageTextViewTask()
		{
			Command viewInExternalEditorCommand;
			var menu = new Menu {
				(viewInExternalEditorCommand = new Command("View in External Editor", () => {
					if (textWriter.LogFilePath != null) {
						System.Diagnostics.Process.Start(textWriter.LogFilePath);
					}
				})),
				Command.MenuSeparator,
				new Command("Clear", () => {
					textView.Clear();
				}),
				Command.Copy
			};
			textView.Gestures.Add(
				new ClickGesture(0, () => {
					textView.SetFocus();
					Window.Current.Activate();
				})
			);
			textView.Gestures.Add(
				new ClickGesture(1, () => {
					textView.SetFocus();
					Window.Current.Activate();
					viewInExternalEditorCommand.Enabled = textWriter.LogFilePath != null;
					menu.Popup();
				})
			);
			while (true) {
				if (textView.IsFocused()) {
					Command.Copy.Enabled = true;
					if (Command.Copy.WasIssued()) {
						Command.Copy.Consume();
						Clipboard.Text = textView.DisplayText;
					}
				}
				if (textView.Content.Nodes.Count > TextViewWriter.MaxMessages) {
					textView.Content.Nodes.RemoveRange(0, textView.Content.Nodes.Count - TextViewWriter.MaxMessages / 2);
				}
				textWriter.ProcessPendingMessages();
				yield return null;
			}
		}
	}
}
