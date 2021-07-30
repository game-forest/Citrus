using Lime;
using System;
using System.Collections.Generic;
using System.Linq;
using Tangerine.Core;

namespace Tangerine.UI.Inspector
{
	public class KeyFunctionDropdown
	{
		private Window window;
		private Widget content;
		private ThemedInvalidableWindowWidget rootWidget;
		private List<Image> images = new List<Image>();
		private KeyframeButton keyframeButton;
		private Image lastHoverImage;
		private Image cancelImage;
		private KeyFunction currentKeyfunction;
		private readonly float borderThickness = 0.6f;

		private static KeyFunction[] allowedKeyFunctionSequence;
		private static readonly KeyFunction[] keyFunctionSequence = {
			KeyFunction.Linear,
			KeyFunction.Spline,
			KeyFunction.Steep,
			KeyFunction.ClosedSpline
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
				Layout = new VBoxLayout() { Spacing = 3 },
				Padding = new Thickness(3),
				Presenter = new ThemedFramePresenter(Theme.Colors.WhiteBackground, Color4.Transparent),
			};
			content.CompoundPresenter.Push(new SyncDelegatePresenter<Widget>(w => {
				Renderer.DrawRectOutline(Vector2.Zero, w.Size, ColorTheme.Current.Inspector.BorderAroundKeyframeColorbox, borderThickness);
			}));
			content.Tasks.AddLoop(() => {
				if (window.Visible) {
					var hoverImage = (Image) content.Nodes.FirstOrDefault(node => {
						var image = node as Image;
						return lastHoverImage != image && IsMouseOverImage(image);
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
			rootWidget = new ThemedInvalidableWindowWidget(window) {
				Nodes = {
					content
				},
			};
			cancelImage = new Image {
				Padding = new Thickness(1.5f),
				Shader = ShaderId.Silhuette,
				Texture = new SerializableTexture(),
				Color = Color4.White
			};
			cancelImage.CompoundPresenter.Push(new SyncDelegatePresenter<Widget>(w => {
				DrawBorderForImage((Image)w, ColorTheme.Current.Inspector.BorderAroundKeyframeColorbox);
				if (IsMouseOverImage((Image)w)) {
					DrawBorderForImage((Image)w, ColorTheme.Current.Toolbar.ButtonHighlightBorder, 0, 2);
				}
			}));
		}

		public void ShowWindow(KeyframeButton keyframeButton, List<ITexture> textures, KeyFunction selected)
		{
			this.keyframeButton = keyframeButton;
			this.currentKeyfunction = selected;
			var offset = new Vector2(0, keyframeButton.Size.Y);
			var dropdownPosition = Window.Current.LocalToDesktop(keyframeButton.GlobalPosition) + offset;
			AddImages(textures);
			window.Visible = true;
			window.ClientSize = window.DecoratedSize = content.Size = content.EffectiveMinSize;
			window.ClientPosition = window.DecoratedPosition = dropdownPosition;
		}

		public void HideWindow()
		{
			window.Visible = false;
			lastHoverImage = null;
			keyframeButton = null;
		}

		public bool TryGetKeyFunction(out KeyFunction? keyFunction)
		{
			keyFunction = null;
			if (lastHoverImage == cancelImage) {
				return true;
			}
			if (IsMouseOverImage(lastHoverImage)) {
				int index = images.IndexOf(lastHoverImage);
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
			cancelImage.MinMaxSize = keyframeButton.Size;
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
			content.Nodes.Add(cancelImage);
		}

		private void AddImage(ITexture texture)
		{
			var image = new Image {
				Texture = texture,
				MinMaxSize = keyframeButton.Size,
				Color = keyframeButton.KeyColor,
				Padding = new Thickness(1)
			};
			image.CompoundPresenter.Push(new SyncDelegatePresenter<Widget>(w => {
				DrawBorderForImage((Image)w, ColorTheme.Current.Inspector.BorderAroundKeyframeColorbox);
				if (IsMouseOverImage((Image)w)) {
					DrawBorderForImage((Image)w, ColorTheme.Current.Toolbar.ButtonHighlightBorder, 0, 2);
				}
				if (keyframeButton.Checked && w == images[Array.IndexOf(keyFunctionSequence, currentKeyfunction)]) {
					var backgroundColor = ColorTheme.Current.Toolbar.ButtonHighlightBackground.Transparentify(0.5f);
					Renderer.DrawRect(Vector2.Zero, w.Size, backgroundColor);
					DrawBorderForImage((Image)w, ColorTheme.Current.Toolbar.ButtonHighlightBorder);
				}
			}));
			images.Add(image);
		}

		private void DrawBorderForImage(Image target, Color4 color, float transparency = 0, float thickness = 1)
		{
			Renderer.DrawRectOutline(Vector2.Zero, target.Size, color.Transparentify(transparency), thickness);
		}
	}
}
