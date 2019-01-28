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
						var start = textView.Content.Nodes.Count;
						textView.Append(message);
						Console.Instance.UpdateFontHeight(
							Instance.currentFontHeight, start, textView.Content.Nodes.Count
						);
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
		private int currentHighlightIndex;
		private int currentVersion;
		private int lastSearchVersion;
		private float lastSearchScrollPos;
		private bool isCaseSensitive;
		private bool isMatchingRegex;
		private float currentFontHeight;
		private readonly float defaultFontHeight;
		private List<(Rectangle rect, int line)> highlights;

		public Console(Panel panel)
		{
			toFind = "";
			currentHighlightIndex = -1;
			currentVersion = -1;
			lastSearchVersion = -1;
			lastSearchScrollPos = 0;
			defaultFontHeight = 15;
			currentFontHeight = 15;
			highlights = new List<(Rectangle rect, int line)>();
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
					CreateSearchPanel(),
					CreateTextView(),
				}
			};
			panel.ContentWidget.AddNode(RootWidget);
		}

		private Widget CreateSearchPanel()
		{
			var searchBox = new ThemedEditBox {
				LayoutCell = new LayoutCell(Alignment.Center),
			};
			searchBox.AddChangeWatcher(
				() => searchBox.Text,
				_ => {
					toFind = _;
					++currentVersion;
					currentHighlightIndex = -1;
				}
			);
			var caseSensitiveButton = new ToolbarButton {
				MinMaxSize = new Vector2(24),
				Size = new Vector2(24),
				LayoutCell = new LayoutCell(Alignment.LeftCenter),
				Anchors = Anchors.Left,
				Clicked = () => {
					ToggleCaseSensitiveMatching();
				},
				Texture = IconPool.GetTexture("Tools.ConsoleCaseSensitive"),
				Tooltip = "Case Sensitive Matching"
			};
			caseSensitiveButton.AddChangeWatcher(
				() => isCaseSensitive,
				_ => { caseSensitiveButton.Checked = _; }
			);
			var regexButton = new ToolbarButton {
				MinMaxSize = new Vector2(24),
				Size = new Vector2(24),
				LayoutCell = new LayoutCell(Alignment.LeftCenter),
				Anchors = Anchors.Left,
				Clicked = () => {
					ToggleRegexMatching();
				},
				Texture = IconPool.GetTexture("Tools.ConsoleRegex"),
				Tooltip = "Regex Matching"
			};
			regexButton.AddChangeWatcher(
				() => isMatchingRegex,
				_ => { regexButton.Checked = _; }
			);
			return new Widget {
				Layout = new HBoxLayout(),
				Padding = Theme.Metrics.ControlsPadding,
				LayoutCell = new LayoutCell { StretchX = 2 },
				Nodes = {
					new ToolbarButton {
						MinMaxSize = new Vector2(24),
						Size = new Vector2(24),
						LayoutCell = new LayoutCell(Alignment.LeftCenter),
						Anchors = Anchors.Left,
						Clicked = () => {
							Zoom(2);
						},
						Texture = IconPool.GetTexture("SceneView.ZoomIn"),
					},
					new ToolbarButton {
						MinMaxSize = new Vector2(24),
						Size = new Vector2(24),
						LayoutCell = new LayoutCell(Alignment.LeftCenter),
						Anchors = Anchors.Left,
						Clicked = () => {
							Zoom(-2);
						},
						Texture = IconPool.GetTexture("SceneView.ZoomOut"),
					},
					caseSensitiveButton,
					regexButton,
					new ThemedSimpleText("Find: ") {
						LayoutCell = new LayoutCell(Alignment.LeftCenter),
						Padding = new Thickness(left: 2, right: 2),
					},
					searchBox,
					new ThemedButton() {
						LayoutCell = new LayoutCell(Alignment.Center),
						Text = "Next",
						Clicked = () => {
							++currentHighlightIndex;
							Find();
						}
					},
					new ThemedButton() {
						LayoutCell = new LayoutCell(Alignment.Center),
						Text = "Previous",
						Clicked = () => {
							--currentHighlightIndex;
							Find();
						}
					}
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
				ConsoleCommands.ZoomIn,
				ConsoleCommands.ZoomOut,
				ConsoleCommands.ResetZoom,
				ConsoleCommands.CaseSensitiveMatching,
				ConsoleCommands.RegexMatching,
				Command.MenuSeparator,
				ConsoleCommands.Clear,
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
				if (ConsoleCommands.Clear.WasIssued()) {
					ConsoleCommands.Clear.Consume();
					if (textView.Content.Nodes.Count > 0) {
						textView.Clear();
						currentVersion = -1;
						currentHighlightIndex = -1;
					}
				}
				if (ConsoleCommands.ResetZoom.WasIssued()) {
					ConsoleCommands.ResetZoom.Consume();
					if (currentFontHeight != defaultFontHeight) {
						UpdateFontHeight(defaultFontHeight);
					}
				}
				if (ConsoleCommands.ZoomIn.WasIssued()) {
					ConsoleCommands.ZoomIn.Consume();
					Zoom(2);
				}
				if (ConsoleCommands.ZoomOut.WasIssued()) {
					ConsoleCommands.ZoomOut.Consume();
					Zoom(-2);
				}
				if (ConsoleCommands.CaseSensitiveMatching.WasIssued()) {
					ConsoleCommands.CaseSensitiveMatching.Consume();
					ToggleCaseSensitiveMatching();
				}
				if (ConsoleCommands.RegexMatching.WasIssued()) {
					ConsoleCommands.RegexMatching.Consume();
					ToggleRegexMatching();
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

		private void ToggleCaseSensitiveMatching()
		{
			isCaseSensitive = !isCaseSensitive;
			++currentVersion;
			currentHighlightIndex = -1;
		}

		private void ToggleRegexMatching()
		{
			isMatchingRegex = !isMatchingRegex;
			++currentVersion;
			currentHighlightIndex = -1;
		}

		private void Zoom(float value)
		{
			UpdateFontHeight(currentFontHeight + value);
		}

		private void Find()
		{
			if (highlights.Count > 0) {
				ClampHighlightIndex();
				var maxScrollPos = Mathf.Abs(textView.Content.Height - textView.ContentHeight);
				var currentTextPos = highlights[currentHighlightIndex].line;
				textView.ScrollPosition =
					Mathf.Clamp(currentTextPos * highlights[currentHighlightIndex].rect.Height, 0, maxScrollPos);
			}
		}

		private void ClampHighlightIndex()
		{
			if (currentHighlightIndex < 0) {
				currentHighlightIndex = highlights.Count - 1;
			} else {
				currentHighlightIndex %= highlights.Count;
			}
		}

		public void HighlightText()
		{
			if (currentVersion != lastSearchVersion) {
				lastSearchVersion = currentVersion;
				lastSearchScrollPos = textView.ScrollPosition;
				highlights.Clear();
				if (string.IsNullOrEmpty(toFind)) {
					return;
				}
				var pattern = isCaseSensitive ? toFind : toFind.ToLower();
				if (isMatchingRegex) {
					try {
						Regex.Match(string.Empty, pattern);
					}
					catch (ArgumentException) {
						return;
					}
				}
				var lines = textView.Content.Nodes;
				for (var i = 0; i < lines.Count; ++i) {
					if (lines[i] is ThemedSimpleText text) {
						var t = text.Text.Trim();
						var s = isCaseSensitive ? t : t.ToLower();
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
								var filterSize =
									text.Font.MeasureTextLine(
										isMatchingRegex ?
											t.Substring(index, newPattern.Length) :
											toFind,
										text.FontHeight,
										text.LetterSpacing
									);
								string skippedText = t.Substring(previousIndex, index - previousIndex);
								var skippedSize = text.Font.MeasureTextLine(skippedText, text.FontHeight, text.LetterSpacing);
								size.X += skippedSize.X;
								size.Y = Mathf.Max(size.Y, skippedSize.Y);
								var rect = new Rectangle(pos.X + size.X, pos.Y, pos.X + size.X + filterSize.X, pos.Y + size.Y);
								highlights.Add((rect, i));
								Renderer.DrawRect(rect.A, rect.B, ColorTheme.Current.Console.MatchColor);
								size.X += filterSize.X;
								size.Y = Mathf.Max(size.Y, filterSize.Y);
								previousIndex = index + (isMatchingRegex ? newPattern.Length : toFind.Length);
								if (isMatchingRegex) {
									match = match.NextMatch();
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
			} else {
				var diff = textView.ScrollPosition - lastSearchScrollPos;
				var shift =
					textView.Behaviour.ScrollDirection == ScrollDirection.Vertical ?
					new Vector2(0, diff) :
					new Vector2(diff, 0);
				for (var i = 0; i < highlights.Count; ++i) {
					var rect = highlights[i].rect;
					rect.A -= shift;
					rect.B -= shift;
					Renderer.DrawRect(rect.A, rect.B, ColorTheme.Current.Console.MatchColor);
				}
				if (currentHighlightIndex != -1) {
					var selectionRect = highlights[currentHighlightIndex].rect;
					Renderer.DrawRect(
						selectionRect.A - shift - Vector2.One,
						selectionRect.B - shift + Vector2.One,
						ColorTheme.Current.Console.SelectionColor
					);
				}
			}
		}

		public void UpdateFontHeight(float value)
		{
			UpdateFontHeight(value, 0, textView.Content.Nodes.Count);
		}

		public void UpdateFontHeight(float value, int start, int end)
		{
			++currentVersion;
			++currentFontHeight;
			currentFontHeight = Mathf.Clamp(value, 15, 48);
			for (var i = start; i < end; ++i) {
				if (textView.Content.Nodes[i] is ThemedSimpleText text) {
					text.FontHeight = value;
				}
			}
		}
	}
}
