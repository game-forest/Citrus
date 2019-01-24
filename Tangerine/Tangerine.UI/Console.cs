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
				Instance.log = "";
				Instance.logBackup = "";
				Instance.logSize = TryGetLogFileSize();
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
					var timestamped = $"[{DateTime.Now.ToLongTimeString()}] {value}";
#if DEBUG
					System.Diagnostics.Debug.Write(timestamped);
#endif // DEBUG
					SystemOut?.Write(timestamped);
					textView.Append(timestamped);
					Instance.log += timestamped;
					Instance.logBackup += timestamped;
					Instance.logSize += timestamped.Length;
				});
				Application.InvokeOnNextUpdate(textView.ScrollToEnd);
			}

			public override Encoding Encoding { get; }
		}

		public static Console Instance { get; private set; }
		public static string LogFileName
		{
			get {
				var date = DateTime.Today;
				return $"{Path.GetTempPath()}TangerineConsoleLog-{date.Day}-{date.Month}-{date.Year}.txt";
			}
		}

		private Panel panel;
		public readonly Widget RootWidget;
		private ThemedTextView textView;
		private TextWriter textWriter;
		private string log;
		private string logBackup;
		private string lastWriteDate;
		private int logSize;

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
					var proj =  Path.GetFileNameWithoutExtension(Project.Current.CitprojPath);
					try {
						var header = $"\n========== {proj} console log time: {DateTime.Now.ToLongTimeString()}\n";
						if (lastWriteDate != null) {
							if (DateTime.Today.ToLongDateString() != lastWriteDate) {
								header += "========== Changes detected: Date\n";
							} else if (logSize != TryGetLogFileSize() + log.Length) {
								header += "========== Changes detected: File has been modified during current session\n";
							} else {
								goto skipBackupLoad;
							}
							header += "========== Writing current session's log backup\n";
							log = logBackup;
						}
						skipBackupLoad:
						if (!string.IsNullOrEmpty(log)) {
							File.AppendAllText(LogFileName, $"{header}{log}");
							logSize = TryGetLogFileSize();
							lastWriteDate = DateTime.Today.ToLongDateString();
						}
						System.Diagnostics.Process.Start($@"{LogFileName}");
					} catch (System.Exception) {
						// ignored
					}
					log = "";
				}),
				Command.MenuSeparator,
				new Command("Clear", () => {
					textView.Clear();
					log = "";
					logSize = TryGetLogFileSize();
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
