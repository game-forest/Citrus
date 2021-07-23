using Lime;
using System.Collections.Generic;
using System.Linq;
using Tangerine.Core;

namespace Tangerine.UI.Inspector
{
	public class KeyFunctionDropdown
	{
		private KeyframeButton keyframeButton;
		private Window window;
		private Widget content;
		private Image lastHoverImage;
		private List<Image> images = new List<Image>();
		private ThemedInvalidableWindowWidget rootWidget;

		private static KeyFunction[] allowedKeyFunctionSequence;
		private static readonly KeyFunction[] keyFunctionSequence = {
			KeyFunction.Linear, KeyFunction.Spline,
			KeyFunction.Steep, KeyFunction.ClosedSpline
		};

		private static KeyFunctionDropdown instance;

		public static KeyFunctionDropdown Instance
		{
			get
			{
				if (instance == null) {
					instance = new KeyFunctionDropdown();
				}
				return instance;
			}
		}

		private KeyFunctionDropdown()
		{
			window = new Window(new WindowOptions {
				Style = WindowStyle.Borderless,
				UseTimer = false,
				VSync = false,
				Visible = false,
				Centered = false,
				Type = WindowType.ToolTip,
			});
			content = new Widget {
				Layout = new VBoxLayout() { Spacing = 7 },
				Nodes = {
				},
				Presenter = new ThemedFramePresenter(Color4.Transparent, Color4.Transparent),
			};
			rootWidget = new ThemedInvalidableWindowWidget(window) {
				Nodes = {
					content
				},
			};
			content.Tasks.AddLoop(() => {
				if (window.Visible) {
					var hoverImage = images.FirstOrDefault(image => {
						return (lastHoverImage != image && IsMouseOverImage(image));
					});
					if (hoverImage != null) {
						lastHoverImage = hoverImage;
						window.Invalidate();
					} else if (!IsMouseOverImage(lastHoverImage)) {
						lastHoverImage = null;
						window.Invalidate();
					}
				}
			});
		}

		public void ShowWindow(KeyframeButton keyframeButton, List<ITexture> textures)
		{
			this.keyframeButton = keyframeButton;
			AddImages(textures);
			window.Visible = true;
			window.ClientSize = window.DecoratedSize = content.Size = content.EffectiveMinSize;
			window.ClientPosition = window.DecoratedPosition = Window.Current.LocalToDesktop(keyframeButton.GlobalPosition);
		}

		public void HideWindow()
		{
			window.Visible = false;
			lastHoverImage = null;
			keyframeButton = null;
		}

		public bool TryGetKeyFunction(out KeyFunction keyFunction)
		{
			keyFunction = KeyFunction.Linear;
			if (IsMouseOverImage(lastHoverImage)) {
				int index = content.Nodes.IndexOf(lastHoverImage);
				if (keyframeButton.AllowedKeyFunctions == null) {
					keyFunction = keyFunctionSequence[index];
				} else {
					keyFunction = allowedKeyFunctionSequence[index];
				}
				return true;
			}
			return false;
		}

		private bool IsMouseOverImage(Image image)
		{
			if (image == null) {
				return false;
			}
			var mousePos = window.Input.MouseDesktopToLocal(Application.Input.DesktopMousePosition);
			var rect = new Rectangle(image.Position, image.Position + image.Size);
			return rect.Contains(mousePos);
		}

		private void AddImages(List<ITexture> textures)
		{
			images.Clear();
			content.Nodes.Clear();
			if (keyframeButton.AllowedKeyFunctions != null) {
				var sequence = new List<KeyFunction>();
				foreach (var k in keyFunctionSequence) {
					if (keyframeButton.AllowedKeyFunctions.Contains<KeyFunction>(k)) {
						sequence.Add(k);
						AddImage(textures[(int)k]);
					}
				}
				allowedKeyFunctionSequence = sequence.ToArray();
			} else {
				foreach (var k in keyFunctionSequence) {
					AddImage(textures[(int)k]);
				}
			}
			content.Nodes.AddRange(images);
		}

		private void AddImage(ITexture texture)
		{
			var image = new Image {
				Texture = texture,
				MinMaxSize = keyframeButton.Size,
				Color = keyframeButton.KeyColor,
			};
			image.CompoundPresenter.Push(new SyncDelegatePresenter<Widget>(w => {
				if (IsMouseOverImage((Image)w)) {
					var backgroundColor = ColorTheme.Current.Toolbar.ButtonHighlightBackground;
					backgroundColor.A /= 2;
					Renderer.DrawRect(Vector2.Zero, w.Size, backgroundColor);
					Renderer.DrawRectOutline(Vector2.Zero, w.Size, ColorTheme.Current.Toolbar.ButtonHighlightBorder);
				}
			}));
			images.Add(image);
		}
	}
}
