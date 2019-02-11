using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Lime;
using Tangerine.Core;
using Tangerine.UI.Docking;

namespace Tangerine.UI
{
	public class Console
	{
		private class TextViewWriter : TextWriter
		{
			private readonly ThemedTextView textView;
			public TextWriter SystemOut;

			public TextViewWriter(ThemedTextView textView)
			{
				this.textView = textView;
			}

			public override void WriteLine(string value)
			{
				Write(value + '\n');
			}

			public override void Write(string value)
			{
				value = Encoding.UTF8.GetString(
					Encoding.Convert(Encoding.Default, Encoding.UTF8, Encoding.Default.GetBytes(value))
				);
				if (string.IsNullOrEmpty(value)) {
					return;
				}
				Application.InvokeOnMainThread(() => {
					var time = DateTime.Now.ToLongTimeString();
					var proj = $"{Path.GetFileNameWithoutExtension(Project.Current?.CitprojPath ?? string.Empty)} ".TrimStart(' ');
					var timestamped = $"[{proj}{time}] {value}".Replace("\r\n", "\n").Replace("\r", "\n");
#if WIN
					timestamped = timestamped.Replace("\n", "\r\n");
#endif // WIN
#if MAC
					timestamped = timestamped.Replace("\n", "\r");
#endif // MAC
#if DEBUG
					System.Diagnostics.Debug.Write(timestamped);
#endif // DEBUG
					SystemOut?.Write(timestamped);
					textView.Append(timestamped);
					File.AppendAllText(LogFileName, timestamped);
					Instance.logBackup += timestamped;
				});
				Application.InvokeOnNextUpdate(textView.ScrollToEnd);
			}

			public override Encoding Encoding { get; }
		}

		public static Console Instance { get; private set; }

		public static string LogFileName
		{
			get
			{
				var tempPath = Path.GetTempPath();
				var logDir = Project.Current?.CitprojPath?.Replace(Path.GetFileName(Project.Current.CitprojPath), "Logs") ?? tempPath;
				if (!Directory.Exists(logDir)) {
					Directory.CreateDirectory(logDir);
				}
				var path = $"{logDir}\\TangerineConsoleLog [{Instance.sessionStartTime}].txt";
				if (logDir == tempPath) {
					File.AppendAllText(path, Instance.logBackup);
				}
				return path;
			}
		}

		private Panel panel;
		public readonly Widget RootWidget;
		private ThemedTextView textView;
		private TextWriter textWriter;
		private string logBackup;
		private string sessionStartTime;

		public Console(Panel panel)
		{
			logBackup = "";
			sessionStartTime = DateTime.Now.ToString().Replace('/', '-').Replace(':', '-');
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

		private static int TryGetLogFileSize()
		{
			try {
				return (int)new FileInfo($@"{LogFileName}").Length;
			} catch (FileNotFoundException) {
				File.WriteAllText(LogFileName, string.Empty);
				return 0;
			}
		}

		private IEnumerator<object> ManageTextViewTask()
		{
			var menu = new Menu() {
				new Command("View in editor", () => {
					System.Diagnostics.Process.Start($@"{LogFileName}");
				}),
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
				if (textView.Content.Nodes.Count >= 500) {
					textView.Content.Nodes.RemoveRange(0, 250);
				}
				yield return null;
			}
		}
	}
}
