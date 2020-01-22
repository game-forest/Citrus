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
			private string logBeforeProjectOpened = string.Empty;
			public string LogFilePath { get; private set; }
			public TextWriter SystemOut;

			public TextViewWriter(ThemedTextView textView)
			{
				this.textView = textView;
				Project.Opening += path => {
					var logDir = Path.GetDirectoryName(path);
					if (!Directory.Exists(logDir)) {
						Directory.CreateDirectory(logDir);
					}
					LogFilePath = Path.Combine(logDir, "TangerineLog.txt");
					File.WriteAllText(LogFilePath, logBeforeProjectOpened);
				};
			}
			
			public override void WriteLine(string value)
			{
				Write(value + '\n');
			}

			public override void Write(string value)
			{
				if (string.IsNullOrEmpty(value)) {
					return;
				}
				value = Encoding.UTF8.GetString(
					Encoding.Convert(Encoding.Default, Encoding.UTF8, Encoding.Default.GetBytes(value))
				);
				Application.InvokeOnMainThread(() => {
					value = $"[{DateTime.Now.ToLongTimeString()}] {value}";
					value = value.Replace("\r\n", "\n").Replace('\r', '\n');
					value = value.Replace("\n", System.Environment.NewLine);
#if DEBUG
					System.Diagnostics.Debug.Write(value);
#endif // DEBUG
					
					SystemOut?.Write(value);
					textView.Append(value);
					if (LogFilePath != null) {
						File.AppendAllText(LogFilePath, value);
					} else {
						logBeforeProjectOpened += value;
					}
				});
				Application.InvokeOnNextUpdate(textView.ScrollToEnd);
			}

			public override Encoding Encoding { get; }
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
			var menu = new Menu() {
				new Command("View in External Editor", () => {
					if (textWriter.LogFilePath != null) {
						System.Diagnostics.Process.Start(textWriter.LogFilePath);
					}
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
