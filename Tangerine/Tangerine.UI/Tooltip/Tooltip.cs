using System;
using System.Collections.Generic;
using Lime;

namespace Tangerine.UI
{
	public class Tooltip
	{
		private readonly Window window;
		private readonly Widget content;
		private readonly Widget textWidgetWrapper;
		private readonly ThemedSimpleText textWidget;
		private readonly float maxWidth;

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
			textWidgetWrapper = new Widget {
				MinSize = Vector2.Zero,
				MaxSize = Vector2.PositiveInfinity,
				Nodes = { textWidget },
			};
			content = new ThemedFrame {
				LayoutCell = new LayoutCell { Ignore = true },
				Layout = new StackLayout(),
				Nodes = { textWidgetWrapper },
				Presenter = new ThemedFramePresenter(Color4.Yellow.Transparentify(0.8f), Color4.Black),
			};
			new ThemedInvalidableWindowWidget(window) {
				Padding = new Thickness(8),
				Layout = new VBoxLayout(),
				Nodes = { content }
			};
			maxWidth = textWidget.MeasureTextLine(new string('W', 80)).X;
		}

		public void Hide()
		{
			window.Visible = false;
		}

		public void Show()
		{
			window.Visible = true;
			textWidget.Size = new Vector2(maxWidth, 1000);
			int lineCount = string.IsNullOrEmpty(Text) ?
				0 : textWidget.SplitText(Text).Count;
			float textWidth = Math.Min(maxWidth, textWidget.EffectiveMinSize.X);
			float textHeight = textWidget.CalcTotalHeight(lineCount);
			var size = new Vector2(textWidth,  Math.Min(10 + textHeight, 1000));
			window.ClientSize = window.DecoratedSize = content.Size = textWidgetWrapper.Size = textWidget.Size = size;
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
