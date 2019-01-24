using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
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
				file = new StreamWriter(File.Open(LogFilePath, FileMode.Append, FileAccess.Write, FileShare.Read));
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
		private string toFind;
		private int currentTextPos;
		private bool isCaseSensitive;
		private bool isMatchingRegex;

		public Console(Panel panel)
		{
			toFind = "";
			currentTextPos = -1;
			isCaseSensitive = false;
			isMatchingRegex = false;
			if (Instance != null) {
				throw new InvalidOperationException();
			}
			Instance = this;
			this.panel = panel;
			RootWidget = new Widget {
				Layout = new VBoxLayout {
					Spacing = 6,
				},
				Nodes = {
					CreateSearchBox(),
					CreateSearchCheckboxes(),
					CreateTextView(),
				}
			};
			panel.ContentWidget.AddNode(RootWidget);
		}

		private ICommand commandClear = new Command("Clear");

		private Widget CreateSearchBox()
		{
			var searchBox = new ThemedEditBox {
				LayoutCell = new LayoutCell(Alignment.Center),
			};
			searchBox.AddChangeWatcher(
				() => searchBox.Text,
				_ => { toFind = _; }
			);
			return new Widget {
				Layout = new HBoxLayout(),
				Padding = Theme.Metrics.ControlsPadding,
				LayoutCell = new LayoutCell { StretchX = 2 },
				Nodes = {
					new ThemedSimpleText("Find: ") {
						LayoutCell = new LayoutCell(Alignment.LeftCenter),
					},
					searchBox,
					new ThemedButton() {
						LayoutCell = new LayoutCell(Alignment.Center),
						Text = "Next",
						Clicked = () =>
							Find(i => {
								return (i + 1) % textView.Content.Nodes.Count;
							})
					},
					new ThemedButton() {
						LayoutCell = new LayoutCell(Alignment.Center),
						Text = "Previous",
						Clicked = () =>
							Find(i => {
								--i;
								while (i < 0) {
									i += textView.Content.Nodes.Count;
								}
								return i % textView.Content.Nodes.Count;
							})
					}
				}
			};
		}

		private Widget CreateSearchCheckboxes()
		{
			var caseSensitiveCheckBox = new ThemedCheckBox {
				TabTravesable = null
			};
			caseSensitiveCheckBox.Changed += args => { isCaseSensitive = !isCaseSensitive; };
			var regexMatchingCheckBox = new ThemedCheckBox {
				TabTravesable = null
			};
			regexMatchingCheckBox.Changed += args => { isMatchingRegex = !isMatchingRegex; };
			return new Widget {
				Layout = new HBoxLayout(),
				Padding = Theme.Metrics.ControlsPadding,
				LayoutCell = new LayoutCell { StretchX = 2, StretchY = 0 },
				Nodes = {
					caseSensitiveCheckBox,
					new ThemedSimpleText {
						LayoutCell = new LayoutCell(Alignment.LeftCenter),
						Padding = new Thickness(left: 2, right: 8),
						Text = "Case sensitive",
						ForceUncutText = true
					},
					regexMatchingCheckBox,
					new ThemedSimpleText {
						LayoutCell = new LayoutCell(Alignment.LeftCenter),
						Padding = new Thickness(left: 2, right: 8),
						Text = "Regex",
						ForceUncutText = true
					},
				}
			};
		}

		private Widget CreateTextView()
		{
			textView = new ThemedTextView {
				SquashDuplicateLines = true,
			};
			textWriter = new TextViewWriter(textView) {
				SystemOut = System.Console.Out,
			};
			System.Console.SetOut(textWriter);
			System.Console.SetError(textWriter);
			textView.Tasks.Add(ManageTextViewTask);
			textView.Content.CompoundPresenter.Add(
				new SyncDelegatePresenter<Widget>(w => {
					HighlightText();
				})
			);
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
						Lime.Environment.ShellExecute(textWriter.LogFilePath);
					}
				})),
				Command.MenuSeparator,
				new Command("Clear", () => {
					textView.Clear();
				}),
				Command.Copy,
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
					textView.Content.Nodes.RemoveRange(
						0, textView.Content.Nodes.Count - TextViewWriter.MaxMessages / 2
					);
				}
				textWriter.ProcessPendingMessages();
				yield return null;
			}
		}

		private void Find(Func<int, int> inc)
		{
			var pattern = isCaseSensitive ? toFind : toFind.ToLower();
			var lines = textView.Content.Nodes;
			var maxScrollPos = Mathf.Abs(textView.Content.Height - textView.ContentHeight);
			for (var counter = 0; counter < lines.Count; ++counter) {
				currentTextPos = inc.Invoke(currentTextPos);
				if (lines[currentTextPos] is ThemedSimpleText text) {
					var s = isCaseSensitive ? text.Text : text.Text.ToLower();
					if (isMatchingRegex ? Regex.Matches(s, pattern).Count > 0 : s.Contains(pattern)) {
						textView.ScrollPosition = Mathf.Clamp((currentTextPos) * text.Height, 0, maxScrollPos);
						break;
					}
				}
			}
		}

		public void HighlightText()
		{
			if (string.IsNullOrEmpty(toFind)) {
				return;
			}
			var pattern = isCaseSensitive ? toFind : toFind.ToLower();
			var lines = textView.Content.Nodes;
			for (var i = 0; i < lines.Count; ++i) {
				if (lines[i] is ThemedSimpleText text) {
					var s = isCaseSensitive ? text.Text : text.Text.ToLower();
					if (isMatchingRegex ? Regex.Matches(s, pattern).Count > 0 : s.Contains(pattern)) {
						textView.PrepareRendererState();
						int index;
						int previousIndex = 0;
						var size = Vector2.Zero;
						var pos = text.CalcPositionInSpaceOf(textView);
						pos.X += text.Padding.Left;
						pos.Y += text.Padding.Top;
						var match = isMatchingRegex ? Regex.Match(s, pattern) : null;
						var newPattern = isMatchingRegex ? match.ToString() : pattern;
						while ((index = s.IndexOf(isMatchingRegex ? newPattern : pattern, previousIndex, StringComparison.Ordinal)) >= 0) {
							var filterSize = text.Font.MeasureTextLine(isMatchingRegex ? newPattern : toFind, text.FontHeight, text.LetterSpacing);
							string skippedText = text.Text.Substring(previousIndex, index - previousIndex);
							var skippedSize = text.Font.MeasureTextLine(skippedText, text.FontHeight, text.LetterSpacing);
							size.X += skippedSize.X;
							size.Y = Mathf.Max(size.Y, skippedSize.Y);
							Renderer.DrawRect(pos.X + size.X, pos.Y, pos.X + size.X + filterSize.X, pos.Y + size.Y, ColorTheme.Current.Hierarchy.MatchColor);
							size.X += filterSize.X;
							size.Y = Mathf.Max(size.Y, filterSize.Y);
							previousIndex = index + toFind.Length;
							match?.NextMatch();
							if (isMatchingRegex) {
								if (!match.Success) {
									break;
								} else {
									newPattern = match.ToString();
								}
							}
						}
					}
				}
			}
		}
	}
}
