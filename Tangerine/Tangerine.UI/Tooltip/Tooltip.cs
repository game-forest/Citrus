using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Lime;

namespace Tangerine.UI
{
	public class Tooltip
	{
		private readonly Window window;
		private readonly Widget content;
		private readonly Widget textWidgetWrapper;
		private readonly ThemedSimpleText textWidget;
		private readonly Regex regex;

		private static Tooltip instance;
		public static Tooltip Instance => instance ?? (instance = new Tooltip());

		public string Text
		{
			get => textWidget.Text;
			set
			{
				if (value != textWidget.Text) {
					textWidget.Text = value;
					// Doing toggle twice to resize window
					Toggle();
					Toggle();
					UpdatePosition(window.DecoratedPosition);
				}
			}
		}

		public bool IsVisible => window.Visible;

		private Tooltip()
		{
			window = new Window(new WindowOptions {
				Style = WindowStyle.Borderless,
				FixedSize = false,
				Visible = false,
				Centered = false,
				Type = WindowType.ToolTip,
				Title = "Tooltip",
			});
			textWidget = new ThemedSimpleText {
				Padding = new Thickness(4),
				OverflowMode = TextOverflowMode.Ellipsis
			};
			content = new ThemedFrame {
				LayoutCell = new LayoutCell { Ignore = true },
				Layout = new StackLayout(),
				Nodes = { textWidget },
				Presenter = new ThemedFramePresenter(Color4.Yellow.Transparentify(0.8f), Color4.Black),
			};
			new ThemedInvalidableWindowWidget(window) {
				Padding = new Thickness(8),
				Layout = new VBoxLayout(),
				Nodes = { content }
			};
			regex = new Regex("\x20+", RegexOptions.Compiled);
		}

		public void Hide()
		{
			window.Visible = false;
		}

		public void Show()
		{
			window.Visible = !string.IsNullOrEmpty(Text);
			if (!window.Visible) {
				return;
			}
			textWidget.Text = SplitText(Text);
			window.ClientSize = window.DecoratedSize = content.Size = textWidget.Size = textWidget.EffectiveMinSize;

			string SplitText(string text)
			{
				const int MaxRowLength = 80;
				var strings = new List<string>(regex.Replace(text.Trim(), " ").Split('\n'));
				for (int i = 0; i < strings.Count - 1; ++i) {
					strings[i] += '\n';
				}
				for (int lineIndex = 0; lineIndex < strings.Count; ++lineIndex) {
					var textLine = strings[lineIndex];
					if (textLine.Length > MaxRowLength + 1) {
						int spaceIndex = MaxRowLength;
						while (--spaceIndex > 0 && textLine[spaceIndex] != ' ');
						if (spaceIndex == 0) {
							strings[lineIndex] = textLine.Substring(startIndex: 0, length: MaxRowLength - 3) + "...\n";
							strings.Insert(index: lineIndex + 1, item: "..." + textLine.Substring(MaxRowLength - 3));
						} else {
							strings[lineIndex] = textLine.Substring(startIndex: 0, length: spaceIndex) + '\n';
							if (spaceIndex < textLine.Length - 1) {
								strings.Insert(index: lineIndex + 1, item: textLine.Substring(spaceIndex + 1));
							}
						}
					}
				}
				return string.Concat(strings);
			}
		}

		public void Show(string text, Vector2 position)
		{
			if (Text != text) {
				textWidget.Text = text;
				window.Invalidate();
			}
			Show();
			UpdatePosition(position);
		}
		
		public void Toggle()
		{
			if (window.Visible) {
				Hide();
			} else {
				Show();
			}
		}

		public IEnumerator<object> Delay(float delay, Func<bool> cancelPredicate)
		{
			for (float t = 0; t < delay; t += Task.Current.Delta) {
				if (cancelPredicate()) {
					break;
				}
				yield return null;
			}
		}

		private void UpdatePosition(Vector2 position)
		{
			var bounds = Lime.Environment.GetDesktopBounds();
			if (position.X + content.Width >= bounds.Right) {
				position.X = bounds.Right - content.Width - Theme.Metrics.ControlsPadding.Right;
			}
			if (position.Y + content.Height >= bounds.Bottom) {
				position.Y -= 2 * content.Height + Theme.Metrics.ControlsPadding.Bottom;
			}
			window.ClientPosition = window.DecoratedPosition = Vector2.Truncate(position);
		}
	}
}
