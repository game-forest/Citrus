using System;
using System.Linq;
using System.Collections.Generic;
using Lime;
using Tangerine.Core;

namespace Tangerine.UI
{
	public class ColorPickerPanel
	{
		private Dictionary<ToolbarButton, Widget> colorComponents;
		private readonly HsvTriangleColorWheel hsvColorPicker;
		private readonly AlphaEditorSlider alphaSlider;
		private readonly HsvEditorSlider hsvSlider;
		private readonly RgbEditorSlider rgbSlider;
		private readonly LabEditorSlider labSlider;
		public readonly Widget Widget;
		private ColorHSVA colorHSVA;

		public event Action DragStarted;
		public event Action DragEnded;
		public event Action Changed;

		public Color4 Color
		{
			get { return colorHSVA.HSVAtoRGB(); }
			set { colorHSVA = ColorHSVA.RGBtoHSVA(value); }
		}

		private bool enabled = true;
		public bool Enabled
		{
			get => enabled;
			set
			{
				if (enabled != value) {
					enabled = value;
					foreach (var (button, component) in colorComponents) {
						button.Enabled = enabled;
						component.Enabled = enabled;
					};
				}
			}
		}

		public ColorPickerPanel()
		{
			var colorProperty = new Property<ColorHSVA>(() => colorHSVA, c => colorHSVA = c);
			hsvColorPicker = new HsvTriangleColorWheel(colorProperty);
			PickerEventsSubscribe(hsvColorPicker);
			hsvColorPicker.Widget.Visible = false;
			alphaSlider = new AlphaEditorSlider(colorProperty);
			hsvSlider = new HsvEditorSlider(colorProperty);
			rgbSlider = new RgbEditorSlider(colorProperty);
			labSlider = new LabEditorSlider(colorProperty);
			SliderEventsSubscribe(new List<ColorEditorSlider>() {
				alphaSlider.alphaSlider,
				hsvSlider.hSlider,
				hsvSlider.sSlider,
				hsvSlider.vSlider,
				rgbSlider.rSlider,
				rgbSlider.gSlider,
				rgbSlider.bSlider,
				labSlider.lSlider,
				labSlider.aSlider,
				labSlider.bSlider
			});
			colorComponents = new Dictionary<ToolbarButton, Widget>{
				{new ToolbarButton("Alpha"), alphaSlider},
				{new ToolbarButton("HSV Wheel"), hsvColorPicker.Widget},
				{new ToolbarButton("HSV"), hsvSlider},
				{new ToolbarButton("LAB"), labSlider},
				{new ToolbarButton("RGB"), rgbSlider},
			};

			Widget = new Widget {
				Padding = new Thickness(28, 8),
				Layout = new VBoxLayout { Spacing = 8 },
				Nodes = {
					new Widget {
						Layout = new HBoxLayout { DefaultCell = new DefaultLayoutCell(Alignment.Center), Spacing = 4f },
						Padding = new Thickness(8),
					}
				}
			};
			foreach (var (button, component) in colorComponents) {
				Widget.Nodes.Add(component);
				button.Clicked = () => {
					button.Checked = !button.Checked;
					component.Visible = button.Checked;
				};
				Widget.Nodes[0].Nodes.Add(button);
			}
			Widget.FocusScope = new KeyboardFocusScope(Widget);

			void SliderEventsSubscribe(List<ColorEditorSlider> sliders)
			{
				foreach (var slider in sliders) {
					slider.Slider.DragStarted += () => DragStarted?.Invoke();
					slider.Slider.Changed += () => Changed?.Invoke();
					slider.Slider.DragEnded += () => DragEnded?.Invoke();
				}
			}
			void PickerEventsSubscribe(HsvTriangleColorWheel picker)
			{
				picker.DragStarted += () => DragStarted?.Invoke();
				picker.Changed += () => Changed?.Invoke();
				picker.DragEnded += () => DragEnded?.Invoke();
			}
		}
		class HsvTriangleColorWheel
		{
			private readonly Property<ColorHSVA> color;

			public const float InnerRadius = 100;
			public const float OuterRadius = 120;
			public const float Margin = 1.05f;
			public readonly Widget Widget;

			public event Action DragStarted;
			public event Action DragEnded;
			public event Action Changed;

			private const float CenterX = OuterRadius;
			private const float CenterY = OuterRadius;
			private const float CursorRadius = (OuterRadius - InnerRadius * Margin) / 2;

			private bool wasHueChanged = true;

			public HsvTriangleColorWheel(Property<ColorHSVA> color)
			{
				this.color = color;
				Widget = new Widget {
					HitTestTarget = true,
					MinMaxSize = OuterRadius * 2 * Vector2.One,
					PostPresenter = new SyncDelegatePresenter<Widget>(Render)
				};
				Widget.Tasks.Add(SelectTask());
				Widget.AddChangeWatcher(() => color.Value.H, _ => wasHueChanged = true);
			}

			void Render(Widget widget)
			{
				widget.PrepareRendererState();
				DrawControl();
				DrawTriangleCursor();
				DrawWheelCursor();
			}

			void DrawTriangleCursor()
			{
				var cursor = new Vector2(
					CenterX - InnerRadius * (1 - 3 * color.Value.S * color.Value.V) / 2,
					CenterY + InnerRadius * Mathf.Sqrt(3) *
					(color.Value.S * color.Value.V - 2 * color.Value.V + 1) / 2
				);
				Renderer.DrawCircle(cursor, CursorRadius, 20, Color4.Black);
			}

			void DrawWheelCursor()
			{
				var cursor =
					Vector2.CosSin(color.Value.H * Mathf.DegToRad) *
					(InnerRadius * Margin + OuterRadius) / 2 +
					Vector2.One * new Vector2(CenterX, CenterY);
				Renderer.DrawCircle(cursor, CursorRadius, 20, Color4.Black);
			}

			private Texture2D texture = new Texture2D();
			private Color4[] image;

			void DrawControl()
			{
				int size = (int)Math.Floor(OuterRadius * 2);
				if (wasHueChanged) {
					if (image == null) {
						image = new Color4[size * size];
					}
					for (int y = 0; y < size; ++y) {
						for (int x = 0; x < size; ++x) {
							var pick = Pick(size - x - 1, y);
							if (pick.Area == Area.Outside) {
								image[y * size + x] = Color4.Transparent;
							} else if (pick.Area == Area.Wheel) {
								image[y * size + x] = new ColorHSVA(pick.H.Value, 1, 1).HSVAtoRGB();
							} else {
								image[y * size + x] = new ColorHSVA(color.Value.H, pick.S.Value, pick.V.Value).HSVAtoRGB();
							}
						}
					}
					texture.LoadImage(image, size, size);
					wasHueChanged = false;
				}
				Renderer.DrawSprite(texture, Color4.White, Vector2.Zero, Vector2.One * size, new Vector2(1, 0), new Vector2(0, 1));
			}

			enum Area
			{
				Outside,
				Wheel,
				Triangle
			}

			struct Result
			{
				public Area Area { get; set; }
				public float? H { get; set; }
				public float? S { get; set; }
				public float? V { get; set; }
			}

			private void ShiftedCoordinates(float x, float y, out float nx, out float ny)
			{
				nx = x - CenterX;
				ny = y - CenterY;
			}

			private static Result PositionToHue(float nx, float ny)
			{
				float angle = Mathf.Atan2(ny, nx);
				if (angle < 0) {
					angle += Mathf.TwoPi;
				}
				return new Result { Area = Area.Wheel, H = angle / Mathf.DegToRad };
			}

			private static Result PositionToSV(float nx, float ny, bool ignoreBounds = false)
			{
				float sqrt3 = Mathf.Sqrt(3);
				float x1 = -ny / InnerRadius;
				float y1 = -nx / InnerRadius;
				if (
					!ignoreBounds && (
					0 * x1 + 2 * y1 > 1 ||
					sqrt3 * x1 + (-1) * y1 > 1 ||
					-sqrt3 * x1 + (-1) * y1 > 1)
				) {
					return new Result { Area = Area.Outside };
				} else {
					var sat = (1 - 2 * y1) / (sqrt3 * x1 - y1 + 2);
					var val = (sqrt3 * x1 - y1 + 2) / 3;
					return new Result { Area = Area.Triangle, S = sat, V = val };
				}
			}

			private Result Pick(float x, float y)
			{
				float nx, ny;
				ShiftedCoordinates(x, y, out nx, out ny);
				float centerDistance = Mathf.Sqrt(nx * nx + ny * ny);
				if (centerDistance > OuterRadius) {
					return new Result { Area = Area.Outside };
				} else if (centerDistance > InnerRadius * Margin) {
					return PositionToHue(nx, ny);
				} else {
					return PositionToSV(nx, ny);
				}
			}

			IEnumerator<object> SelectTask()
			{
				while (true) {
					if (Widget.GloballyEnabled && Widget.Input.WasMousePressed()) {
						var pick = Pick(
							Widget.Input.MousePosition.X - Widget.GlobalPosition.X,
							Widget.Input.MousePosition.Y - Widget.GlobalPosition.Y);
						if (pick.Area != Area.Outside) {
							DragStarted?.Invoke();
							while (Widget.Input.IsMousePressed()) {
								float nx, ny;
								ShiftedCoordinates(
									Widget.Input.MousePosition.X - Widget.GlobalPosition.X,
									Widget.Input.MousePosition.Y - Widget.GlobalPosition.Y,
									out nx, out ny);
								if (pick.Area == Area.Triangle) {
									var newPick = PositionToSV(nx, ny, ignoreBounds: true);
									color.Value = new ColorHSVA {
										H = color.Value.H,
										S = Mathf.Min(Mathf.Max(newPick.S.Value, 0), 1),
										V = Mathf.Min(Mathf.Max(newPick.V.Value, 0), 1),
										A = color.Value.A
									};
								} else {
									var newPick = PositionToHue(nx, ny);
									color.Value = new ColorHSVA {
										H = Mathf.Min(Mathf.Max(newPick.H.Value, 0), 360),
										S = color.Value.S,
										V = color.Value.V,
										A = color.Value.A
									};
								}
								Window.Current.Invalidate();
								Changed?.Invoke();
								yield return null;
							}
							DragEnded?.Invoke();
						}
					}
					yield return null;
				}
			}
		}
		class PixelsSlider : Widget
		{
			public ThemedSlider Slider { get; }
			protected Color4[] Pixels { get; }
			public PixelsSlider(string name, int min, int max, int pixelCount)
			{
				Padding = new Thickness(8, 0);
				Layout = new LinearLayout();
				Pixels = new Color4[pixelCount];
				Slider = new ThemedSlider() {
					RangeMin = min,
					RangeMax = max,
					MinSize = new Vector2(30, 16),
					MaxHeight = 16
				};
				var labelWidget = new ThemedSimpleText {
					HitTestTarget = false,
					HAlignment = HAlignment.Left,
					VAlignment = VAlignment.Bottom,
					Anchors = Anchors.LeftRightTopBottom,
					Padding = new Thickness { Left = 6, Top = 2 },
					MinSize = new Vector2(60, Slider.MinSize.Y),
					Text = Slider.Value.ToString("0.###")
				};
				Slider.Updating += _ => {
					labelWidget.Text = Slider.Value.ToString("0.###");
				};
				Slider.CompoundPresenter.Insert(0, new PixelsSliderPresenter(Pixels));
				var textWidget = new ThemedSimpleText {
					HitTestTarget = false,
					HAlignment = HAlignment.Left,
					VAlignment = VAlignment.Bottom,
					Anchors = Anchors.LeftRightTopBottom,
					Padding = new Thickness { Left = 4, Top = 2 },
					MinSize = new Vector2(20, Slider.MinSize.Y),
					Text = name,
				};
				AddNode(textWidget);
				AddNode(Slider);
				AddNode(labelWidget);
			}
			class PixelsSliderPresenter : IPresenter
			{
				private readonly Color4[] pixels;
				public PixelsSliderPresenter(Color4[] pixels)
				{
					this.pixels = pixels;
				}
				public Lime.RenderObject GetRenderObject(Node node)
				{
					var widget = (Widget)node;
					var renderObject = RenderObjectPool<RenderObject>.Acquire();
					renderObject.CaptureRenderState(widget);
					renderObject.Size = widget.Size;
					renderObject.Color = Theme.Colors.WhiteBackground;
					renderObject.BorderColor = Theme.Colors.ControlBorder;
					renderObject.Pixels = pixels;
					return renderObject;
				}
				public bool PartialHitTest(Node node, ref HitTestArgs args) => false;
				private class RenderObject : WidgetRenderObject
				{
					public Vector2 Size;
					public Color4 Color;
					public Color4 BorderColor;
					public Color4[] Pixels;
					private readonly Texture2D texture = new Texture2D();

					public override void Render()
					{
						PrepareRenderState();
						texture.LoadImage(Pixels, Pixels.Length, 1);
						Renderer.DrawRect(Vector2.Zero, Size, Color);
						Renderer.DrawRectOutline(Vector2.Zero, Size, BorderColor);
						Renderer.DrawSprite(texture, Color4.White, Vector2.Zero, Vector2.One * Size,
							new Vector2(1, 0), new Vector2(0, 1));
					}
				}
			}
		}
		abstract class ColorEditorSlider : PixelsSlider
		{
			protected Property<ColorHSVA> SliderColor { get; }
			public ColorEditorSlider(Property<ColorHSVA> color, string name, int min, int max, int pixelCount) : base(name, min, max, pixelCount)
			{
				SliderColor = color;
				Slider.Changed += OnSliderValueChange;
				Slider.Updating += _ => OnColorValueChange();
			}
			protected abstract void OnSliderValueChange();
			protected abstract void OnColorValueChange();
		}
		abstract class LABColorEditorSlider : ColorEditorSlider
		{
			protected static ColorLAB currentLAB;
			public LABColorEditorSlider(Property<ColorHSVA> color, string name, int min, int max, int pixelCount) : base(color, name, min, max, pixelCount)
			{
				currentLAB = ColorLAB.RGBtoLAB(color.Value.HSVAtoRGB());
			}
		}
		class AlphaEditorSlider : Widget
		{
			public AlphaSlider alphaSlider;
			public AlphaEditorSlider(Property<ColorHSVA> color)
			{
				alphaSlider = new AlphaSlider(color);
				Padding = new Thickness(0, 8);
				Layout = new VBoxLayout { Spacing = 8 };
				Nodes.AddRange(new List<Node>() {
					alphaSlider
				});
				Visible = false;
			}
			public class AlphaSlider : ColorEditorSlider
			{
				private static int pixelCount = 100;
				public AlphaSlider(Property<ColorHSVA> color) : base(color, "A", 0, 1, pixelCount)
				{
				}
				protected override void OnSliderValueChange()
				{
					SliderColor.Value = new ColorHSVA(SliderColor.Value.H, SliderColor.Value.S, SliderColor.Value.V, Slider.Value);
				}
				protected override void OnColorValueChange()
				{
					Slider.Value = (SliderColor.Value.A);
					for (var i = 0; i < pixelCount; i++) {
						Pixels[pixelCount - i - 1] = SliderColor.Value.HSVAtoRGB();
					}
				}
			}
		}
		class HsvEditorSlider : Widget
		{
			public HSlider hSlider;
			public SSlider sSlider;
			public VSlider vSlider;
			public HsvEditorSlider(Property<ColorHSVA> color)
			{
				hSlider = new HSlider(color);
				sSlider = new SSlider(color);
				vSlider = new VSlider(color);

				Padding = new Thickness(0, 8);
				Layout = new VBoxLayout { Spacing = 8 };
				Nodes.AddRange(new List<Node>() {
					hSlider,
					sSlider,
					vSlider,
				});
				Visible = false;
			}
			public class HSlider : ColorEditorSlider
			{
				private static int pixelCount = 360;
				public HSlider(Property<ColorHSVA> color) : base(color, "H", 0, 360, pixelCount)
				{
					for (var i = 0; i < pixelCount; i++) {
						Pixels[pixelCount - i - 1] = new ColorHSVA(i, 1, 1).HSVAtoRGB();
					}
				}

				protected override void OnSliderValueChange()
				{
					SliderColor.Value = new ColorHSVA(Slider.Value, SliderColor.Value.S, SliderColor.Value.V, SliderColor.Value.A);
				}

				protected override void OnColorValueChange()
				{
					Slider.Value = ((int)SliderColor.Value.H);
				}
			}
			public class SSlider : ColorEditorSlider
			{
				private static int pixelCount = 100;
				public SSlider(Property<ColorHSVA> color) : base(color, "S", 0, 1, pixelCount)
				{
				}
				protected override void OnSliderValueChange()
				{
					SliderColor.Value = new ColorHSVA(SliderColor.Value.H, Slider.Value, SliderColor.Value.V, SliderColor.Value.A);
				}
				protected override void OnColorValueChange()
				{
					Slider.Value = (SliderColor.Value.S);
					for (var i = 0; i < pixelCount; i++) {
						Pixels[pixelCount - i - 1] = new ColorHSVA(SliderColor.Value.H, i / (float)pixelCount,
							SliderColor.Value.V, 1).HSVAtoRGB();
					}
				}
			}
			public class VSlider : ColorEditorSlider
			{
				private static int pixelCount = 100;
				public VSlider(Property<ColorHSVA> color) : base(color, "V", 0, 1, pixelCount)
				{
				}
				protected override void OnSliderValueChange()
				{
					SliderColor.Value = new ColorHSVA(SliderColor.Value.H, SliderColor.Value.S, Slider.Value, SliderColor.Value.A);
				}
				protected override void OnColorValueChange()
				{
					Slider.Value = (SliderColor.Value.V);
					for (var i = 0; i < pixelCount; i++) {
						Pixels[pixelCount - i - 1] = new ColorHSVA(SliderColor.Value.H, SliderColor.Value.S,
							i / (float)pixelCount, 1).HSVAtoRGB();
					}
				}
			}
		}
		class RgbEditorSlider : Widget
		{
			public RSlider rSlider;
			public GSlider gSlider;
			public BSlider bSlider;
			public RgbEditorSlider(Property<ColorHSVA> color)
			{
				rSlider = new RSlider(color);
				gSlider = new GSlider(color);
				bSlider = new BSlider(color);
				Padding = new Thickness(0, 8);
				Layout = new VBoxLayout { Spacing = 8 };
				Nodes.AddRange(new List<Node>() {
					rSlider,
					gSlider,
					bSlider,
				});
				Visible = false;
			}
			public class RSlider : ColorEditorSlider
			{
				private static int pixelCount = 256;
				public RSlider(Property<ColorHSVA> color) : base(color, "R", 0, 255, pixelCount)
				{
				}
				protected override void OnSliderValueChange()
				{
					var sliderValue = (byte)Slider.Value;
					var rgba = SliderColor.Value.HSVAtoRGB();
					rgba.R = sliderValue;
					SliderColor.Value = ColorHSVA.RGBtoHSVA(rgba);
				}
				protected override void OnColorValueChange()
				{
					Slider.Value = (SliderColor.Value.HSVAtoRGB().R);
					for (var i = 0; i < pixelCount; i++) {
						Pixels[pixelCount - i - 1] = SliderColor.Value.HSVAtoRGB();
						Pixels[pixelCount - i - 1].R = (byte)i;
						Pixels[pixelCount - i - 1].A = 255;
					}
				}
			}
			public class GSlider : ColorEditorSlider
			{
				private static int pixelCount = 256;
				public GSlider(Property<ColorHSVA> color) : base(color, "G", 0, 255, pixelCount)
				{
				}
				protected override void OnSliderValueChange()
				{
					var sliderValue = (byte)Slider.Value;
					var rgba = SliderColor.Value.HSVAtoRGB();
					rgba.G = sliderValue;
					SliderColor.Value = ColorHSVA.RGBtoHSVA(rgba);
				}
				protected override void OnColorValueChange()
				{
					Slider.Value = (SliderColor.Value.HSVAtoRGB().G);
					for (var i = 0; i < pixelCount; i++) {
						Pixels[pixelCount - i - 1] = SliderColor.Value.HSVAtoRGB();
						Pixels[pixelCount - i - 1].G = (byte)i;
						Pixels[pixelCount - i - 1].A = 255;
					}
				}
			}
			public class BSlider : ColorEditorSlider
			{
				private static int pixelCount = 256;
				public BSlider(Property<ColorHSVA> color) : base(color, "B", 0, 255, pixelCount)
				{
				}
				protected override void OnSliderValueChange()
				{
					var sliderValue = (byte)Slider.Value;
					var rgba = SliderColor.Value.HSVAtoRGB();
					rgba.B = sliderValue;
					SliderColor.Value = ColorHSVA.RGBtoHSVA(rgba);
				}
				protected override void OnColorValueChange()
				{
					Slider.Value = (SliderColor.Value.HSVAtoRGB().B);
					for (var i = 0; i < pixelCount; i++) {
						Pixels[pixelCount - i - 1] = SliderColor.Value.HSVAtoRGB();
						Pixels[pixelCount - i - 1].B = (byte)i;
						Pixels[pixelCount - i - 1].A = 255;
					}
				}
			}
		}
		class LabEditorSlider : Widget
		{
			public LSlider lSlider;
			public ASlider aSlider;
			public BSlider bSlider;
			public LabEditorSlider(Property<ColorHSVA> color)
			{
				lSlider = new LSlider(color);
				aSlider = new ASlider(color);
				bSlider = new BSlider(color);
				Padding = new Thickness(0, 8);
				Layout = new VBoxLayout { Spacing = 8 };
				Nodes.AddRange(new List<Node>() {
					lSlider,
					aSlider,
					bSlider,
				});
				Visible = false;

			}
			public class LSlider : LABColorEditorSlider
			{
				private static int pixelCount = 100;
				public LSlider(Property<ColorHSVA> color) : base(color, "L", 0, 100, pixelCount)
				{
				}
				protected override void OnColorValueChange()
				{
					var c = new ColorLAB(currentLAB.L, currentLAB.A, currentLAB.B, currentLAB.alpha);
					if (!SliderColor.Value.isEqual(ColorHSVA.RGBtoHSVA(currentLAB.LABtoRGB()))) {
						c = ColorLAB.RGBtoLAB(SliderColor.Value.HSVAtoRGB());
						currentLAB = c;
					} 
					Slider.Value = (int)c.L;
					for (int i = 0; i < pixelCount; i++) {
						c.L = i;
						Pixels[pixelCount - i - 1] = c.LABtoRGB();
						Pixels[pixelCount - i - 1].A = 255;
					}
				}
				protected override void OnSliderValueChange()
				{
					var sliderValue = Math.Round(Slider.Value);
					currentLAB.L = sliderValue;
					SliderColor.Value = ColorHSVA.RGBtoHSVA(currentLAB.LABtoRGB());
				}
			}
			public class ASlider : LABColorEditorSlider
			{
				private static int pixelCount = 256;
				public ASlider(Property<ColorHSVA> color) : base(color, "A", -128, 128, pixelCount)
				{
				}
				protected override void OnColorValueChange()
				{
					var c = new ColorLAB(currentLAB.L, currentLAB.A, currentLAB.B, currentLAB.alpha);
					if (!SliderColor.Value.isEqual(ColorHSVA.RGBtoHSVA(currentLAB.LABtoRGB()))) {
						c = ColorLAB.RGBtoLAB(SliderColor.Value.HSVAtoRGB());
						currentLAB = c;
					} 
					Slider.Value = (int)c.A;
					for (int i = 0; i < pixelCount; i++) {
						c.A = i - 127;
						Pixels[pixelCount - i - 1] = c.LABtoRGB();
						Pixels[pixelCount - i - 1].A = 255;
					}
				}
				protected override void OnSliderValueChange()
				{
					var sliderValue = Math.Round(Slider.Value);
					currentLAB.A = sliderValue;
					SliderColor.Value = ColorHSVA.RGBtoHSVA(currentLAB.LABtoRGB());
				}
			}
			public class BSlider : LABColorEditorSlider
			{
				private static int pixelCount = 256;
				public BSlider(Property<ColorHSVA> color) : base(color, "B", -128, 128, pixelCount)
				{
				}
				protected override void OnColorValueChange()
				{
					var c = new ColorLAB(currentLAB.L, currentLAB.A, currentLAB.B, currentLAB.alpha);
					if (!SliderColor.Value.isEqual(ColorHSVA.RGBtoHSVA(currentLAB.LABtoRGB()))) {
						c = ColorLAB.RGBtoLAB(SliderColor.Value.HSVAtoRGB());
						currentLAB = c;
					}
					Slider.Value = (int)c.B;
					for (int i = 0; i < pixelCount; i++) {
						c.B = i - 127;
						Pixels[pixelCount - i - 1] = c.LABtoRGB();
						Pixels[pixelCount - i - 1].A = 255;
					}
				}
				protected override void OnSliderValueChange()
				{
					var sliderValue = Math.Round(Slider.Value);
					currentLAB.B = sliderValue;
					SliderColor.Value = ColorHSVA.RGBtoHSVA(currentLAB.LABtoRGB());
				}
			}
		}
		public struct ColorHSVA
		{
			public float H;
			public float S;
			public float V;
			public float A;
			public ColorHSVA(float hue, float saturation, float value, float alpha = 1)
			{
				H = hue;
				S = saturation;
				V = value;
				A = alpha;
			}
			public static ColorHSVA RGBtoHSVA(Color4 rgb)
			{
				var c = new ColorHSVA();
				int max = Math.Max(rgb.R, Math.Max(rgb.G, rgb.B));
				int min = Math.Min(rgb.R, Math.Min(rgb.G, rgb.B));
				c.H = GetHue(rgb);
				c.S = (max == 0) ? 0 : 1f - (1f * min / max);
				c.V = max / 255f;
				c.A = rgb.A / 255f;
				return c;
			}
			public Color4 HSVAtoRGB()
			{
				var a = (byte)(A * 255);
				int hi = Convert.ToInt32(Math.Floor(H / 60)) % 6;
				float f = H / 60f - (float)Math.Floor(H / 60);
				byte v = Convert.ToByte(V * 255);
				byte p = Convert.ToByte(v * (1 - S));
				byte q = Convert.ToByte(v * (1 - f * S));
				byte t = Convert.ToByte(v * (1 - (1 - f) * S));
				switch (hi) {
					case 0:
						return new Color4(v, t, p, a);
					case 1:
						return new Color4(q, v, p, a);
					case 2:
						return new Color4(p, v, t, a);
					case 3:
						return new Color4(p, q, v, a);
					case 4:
						return new Color4(t, p, v, a);
					default:
						return new Color4(v, p, q, a);
				}
			}
			private static float GetHue(Color4 rgb)
			{
				int r = rgb.R;
				int g = rgb.G;
				int b = rgb.B;
				byte minval = (byte)Math.Min(r, Math.Min(g, b));
				byte maxval = (byte)Math.Max(r, Math.Max(g, b));
				if (maxval == minval) {
					return 0.0f;
				}
				float diff = (float)(maxval - minval);
				float rnorm = (maxval - r) / diff;
				float gnorm = (maxval - g) / diff;
				float bnorm = (maxval - b) / diff;
				float hue = 0.0f;
				if (r == maxval)
					hue = 60.0f * (6.0f + bnorm - gnorm);
				if (g == maxval)
					hue = 60.0f * (2.0f + rnorm - bnorm);
				if (b == maxval)
					hue = 60.0f * (4.0f + gnorm - rnorm);
				if (hue >= 360.0f)
					hue = hue - 360.0f;
				return hue;
			}
			public bool isEqual(ColorHSVA rv)
			{
				return H == rv.H && S == rv.S && V == rv.V && A == rv.A;
			}
		}
		struct ColorXYZ
		{
			public double X;
			public double Y;
			public double Z;
			public float alpha;
			public ColorXYZ(int X, int Y, int Z, int alpha)
			{
				this.X = X;
				this.Y = Y;
				this.Z = Z;
				this.alpha = alpha;
			}
			public ColorXYZ(double X, double Y, double Z, float alpha)
			{
				this.X = X;
				this.Y = Y;
				this.Z = Z;
				this.alpha = alpha;
			}
		};
		class ColorLAB
		{
			public double L;
			public double A;
			public double B;
			public float alpha;
			// Constants for D65 white point
			//private static (double X, double Y, double Z) D50 = (0.95047, 1, 1.0883);
			// Bradford-adapted D50 white point. 
			private static (double X, double Y, double Z) D50 = (0.964212, 1, 0.825188);
			private static readonly double kE = 216.0 / 24389.0;
			private static readonly double kK = 24389.0 / 27.0;
			private static readonly double kKE = 8.0;
			public ColorLAB(double L, double A, double B, float alpha)
			{
				this.L = L;
				this.A = A;
				this.B = B;
				this.alpha = alpha;
			}			
			public static ColorLAB RGBtoLAB(Color4 rgb)
			{
				return XYZtoLAB(RGBtoXYZ(rgb));
			}
			private static ColorXYZ RGBtoXYZ(Color4 rgb)
			{
				var var_R = InverseCompand(rgb.R / 255.0);
				var var_G = InverseCompand(rgb.G / 255.0);
				var var_B = InverseCompand(rgb.B / 255.0);
				return new ColorXYZ(
					// constants for D65 white point
					//X: var_R * 0.4124 + var_G * 0.3576 + var_B * 0.1805,
					//Y: var_R * 0.2126 + var_G * 0.7152 + var_B * 0.0722,
					//Z: var_R * 0.0193 + var_G * 0.1192 + var_B * 0.9505,	
								
					X: var_R * 0.4360747 + var_G * 0.3850649 + var_B * 0.1430804,
					Y: var_R * 0.2225045 + var_G * 0.7168786 + var_B * 0.0606169,
					Z: var_R * 0.0139322 + var_G * 0.0971045 + var_B * 0.7141733,
					alpha: rgb.A);
				double InverseCompand(double colorPart)
				{
					var sign = 1.0;
					if (colorPart < 0.0) {
						sign = -1.0;
						colorPart = -colorPart;
					}
					var linear = (colorPart <= 0.04045) ? (colorPart / 12.92) : Math.Pow((colorPart + 0.055) / 1.055, 2.4);
					linear *= sign;
					return linear;
				}
			}
			private static ColorLAB XYZtoLAB(ColorXYZ xyz)
			{
				double var_Y = ConvertXYZtoLAB(xyz.Y / D50.Y);
				double var_X = ConvertXYZtoLAB(xyz.X / D50.X);
				double var_Z = ConvertXYZtoLAB(xyz.Z / D50.Z);
				var L = 116.0 * var_Y - 16.0;
				var A = 500.0 * (var_X - var_Y);
				var B = 200.0 * (var_Y - var_Z);
				return new ColorLAB(Math.Round(L), Math.Round(A), Math.Round(B), xyz.alpha);
				double ConvertXYZtoLAB(double colorPart)
				{
					return (colorPart > kE)
						? Math.Pow(colorPart, 1.0 / 3.0)
						: ((kK * colorPart + 16.0)/ 116.0);
				}
			}
			public Color4 LABtoRGB()
			{
				return XYZtoRGB(LABtoXYZ());
			}
			private ColorXYZ LABtoXYZ()
			{
				double var_y = (L + 16.0) / 116.0;
				double var_x = A * 0.002 + var_y;
				double var_z = var_y - B * 0.005;
				var_x = (Math.Pow(var_x, 3) > kE) ? Math.Pow(var_x, 3) : ((116.0 * var_x - 16.0) / kK);
				var_y = (L > kKE) ? Math.Pow((L + 16.0) / 116.0, 3.0) : (L / kK);
				var_z = (Math.Pow(var_z, 3) > kE) ? Math.Pow(var_z, 3) : ((116.0 * var_z - 16.0) / kK);
				return new ColorXYZ(var_x * D50.X, var_y * D50.Y, var_z * D50.Z, alpha);
			}
			private static Color4 XYZtoRGB(ColorXYZ xyz)
			{
				// constants for D65 white point
				//double r = Compand(xyz.X * 3.2406 + xyz.Y * -1.5372 + xyz.Z * -0.4986);
				//double g = Compand(xyz.X * -0.9689 + xyz.Y * 1.8758 + xyz.Z * 0.0415);
				//double b = Compand(xyz.X * 0.0557 + xyz.Y * -0.204 + xyz.Z * 1.057);

				double r = Compand(xyz.X * 3.1338561 + xyz.Y * -1.6168667 + xyz.Z * -0.4906146);
				double g = Compand(xyz.X * -0.9787684 + xyz.Y * 1.9161415 + xyz.Z * 0.0334540);
				double b = Compand(xyz.X * 0.0719453 + xyz.Y * -0.2289914 + xyz.Z * 1.4052427);
				return new Color4((byte)Math.Round(r * 255.0), (byte)Math.Round(g * 255.0), (byte)Math.Round(b * 255.0), (byte)xyz.alpha);
				double Compand(double colorPart)
				{
					var sign = 1.0;
					if (colorPart < 0.0) {
						sign = -1.0;
						colorPart = -colorPart;
					}
					var companded = (colorPart <= 0.0031308) ? (colorPart * 12.92) : (1.055 * Math.Pow(colorPart, 1.0 / 2.4) - 0.055);
					companded *= sign;
					return companded < 0
						? 0
						: companded * 255 > 255
							? 1
							: companded;
				}
			}
		}
	}
}
