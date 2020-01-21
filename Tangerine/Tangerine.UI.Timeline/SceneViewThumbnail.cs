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
		private readonly Vector2 thumbnailOffset = new Vector2(30);

		public SceneViewThumbnail(OverviewPane overviewPane)
		{
			this.overviewPane = overviewPane;
			window = new Window(new WindowOptions {
				Style = WindowStyle.Borderless,
				FixedSize = true,
				Visible = false,
				Centered = false,
				Type = WindowType.ToolTip
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
#if MAC
			pos.Y -= window.DecoratedSize.Y + thumbnailOffset.Y;
#else
			pos.Y += thumbnailOffset.Y;
#endif
			pos.X += thumbnailOffset.X;
			if (pos.X + root.Width >= Lime.Environment.GetDesktopSize().X) {
				pos.X = Lime.Environment.GetDesktopSize().X - root.Width - Theme.Metrics.ControlsPadding.Right;
			}
			window.DecoratedPosition = new Vector2(pos.X.Truncate(), pos.Y.Truncate());
		}

		private void UpdateThumbnailTexture()
		{
			var doc = Document.Current;
			var frame = CalcPreviewFrameIndex();
			var marker = FindMarker(frame);
			if (marker != null && !string.IsNullOrEmpty(marker.Id)) {
				label.Text = $"{frame} - {marker.Id}";
			} else {
				label.Text = frame.ToString();
			}
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

		private Marker FindMarker(int frame)
		{
			Marker result = null;
			foreach (var m in Document.Current.Animation.Markers) {
				if (m.Frame > frame) {
					break;
				}
				if (!string.IsNullOrEmpty(m.Id)) {
					result = m;
				}
			}
			return result;
		}

		private int CalcPreviewFrameIndex()
		{
			return (overviewPane.RootWidget.LocalMousePosition().X / (TimelineMetrics.ColWidth * overviewPane.ContentWidget.Scale.X)).Round();
		}
	}
}
