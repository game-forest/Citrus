using System;
using System.Collections.Generic;
using Tangerine.Core;
using Lime;

namespace Tangerine.UI.Timeline
{
	public class SceneViewThumbnail
	{
		private readonly Widget root;
		private readonly Window window;
		private readonly Image thumbnailImage;
		private readonly OverviewPane overviewPane;
		private readonly ThemedSimpleText label;

		public SceneViewThumbnail(OverviewPane overviewPane)
		{
			this.overviewPane = overviewPane;
			window = new Window(new WindowOptions {
				Style = WindowStyle.Borderless,
				Floating = true,
				FixedSize = true,
				Visible = false,
				Centered = false,
				ToolWindow = true,
			});
			root = new ThemedFrame {
				Layout = new StackLayout(),
				Nodes = {
					new Frame {
						Layout = new VBoxLayout(),
						Nodes = {
							new Widget { LayoutCell = new LayoutCell { StretchY = 1 } },
							(label = new ThemedSimpleText { Padding = new Thickness(2) })
						}
					},
					(thumbnailImage = new Image { Padding = new Thickness(1) }),
				},
				Presenter = new ThemedFramePresenter(Color4.Black, Color4.Black)
			};
			new ThemedInvalidableWindowWidget(window) {
				LayoutBasedWindowSize = true,
				Layout = new VBoxLayout(),
				Nodes = {
					root
				}
			};
			overviewPane.RootWidget.Tasks.Add(ShowOnMouseOverTask());
		}

		private IEnumerator<object> ShowOnMouseOverTask()
		{
			while (true) {
				yield return null;
				if (overviewPane.RootWidget.IsMouseOver()) {
					var showPopup = true;
					for (float t = 0; t < 0.5f; t += Task.Current.Delta) {
						if (!overviewPane.RootWidget.IsMouseOver()) {
							showPopup = false;
							break;
						}
						yield return null;
					}
					if (showPopup) {
						UpdateThumbnailTexture();
						window.Visible = true;
						while (overviewPane.RootWidget.IsMouseOver()) {
							UpdateThumbnailTexture();
							RefreshThumbnailPosition(window);
							yield return null;
						}
						window.Visible = false;
					}
				}
			}
		}

		private void RefreshThumbnailPosition(IWindow window)
		{
			var pos = Application.Input.DesktopMousePosition;
			pos.Y -= window.DecoratedSize.Y + 30;
			pos.X += 30;
			if (pos.X + root.Width >= Lime.Environment.GetDesktopSize().X) {
				pos.X = Lime.Environment.GetDesktopSize().X - root.Width - Theme.Metrics.ControlsPadding.Right;
			}
			window.ClientPosition = new Vector2(pos.X.Truncate(), pos.Y.Truncate());
		}

		private void UpdateThumbnailTexture()
		{
			var doc = Document.Current;
			var frame = CalcPreviewFrameIndex();
			label.Text = frame.ToString();
			doc.SceneViewThumbnailProvider.Generate(frame, texture => {
				var sceneSize = (Vector2)texture.ImageSize;
				var thumbSize = new Vector2(200);
				if (sceneSize.X > sceneSize.Y) {
					thumbSize.Y *= sceneSize.Y / sceneSize.X;
				} else {
					thumbSize.X *= sceneSize.X / sceneSize.Y;
				}
				thumbnailImage.Texture = texture;
				thumbnailImage.MinMaxSize = thumbSize;
				window.Invalidate();
			});
		}

		private int CalcPreviewFrameIndex()
		{
			var input = overviewPane.RootWidget.Input;
			return (input.MousePosition.X / (TimelineMetrics.ColWidth * overviewPane.ContentWidget.Scale.X)).Round();
		}
	}
}
