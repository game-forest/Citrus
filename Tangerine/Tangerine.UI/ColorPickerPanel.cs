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
		private readonly HsvTriangleColorWheel labColorPicker;
		private readonly AlphaEditorSlider alphaSlider;
		private readonly HsvEditorSlider hsvSlider;
		private readonly RgbEditorSlider rgbSlider;
		private readonly RgbEditorSlider labSlider;
		public readonly Widget Widget;
		ColorHSVA colorHSVA;

		public event Action DragStarted;
		public event Action DragEnded;
		public event Action Changed;

		public Color4 Color
		{
			get { return colorHSVA.ToRGBA(); }
			set { colorHSVA = ColorHSVA.FromRGBA(value); }
		}

		private bool enabled = true;
		public bool Enabled
		{
			get => enabled;
			set
			{
				if (enabled != value) {
					enabled = value;
					foreach(var (button, component) in colorComponents) {
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
			labColorPicker = new HsvTriangleColorWheel(colorProperty);
			PickerEventsSubscribe(labColorPicker);
			labColorPicker.Widget.Visible = false;

			alphaSlider = new AlphaEditorSlider(colorProperty);
			hsvSlider = new HsvEditorSlider(colorProperty);
			rgbSlider = new RgbEditorSlider(colorProperty);
			labSlider = new RgbEditorSlider(colorProperty);
			SliderEventsSubscribe(new List<ColorEditorSlider>() {
				alphaSlider,
				hsvSlider.hSlider,
				hsvSlider.sSlider,
				hsvSlider.vSlider,
				rgbSlider.rSlider,
				rgbSlider.gSlider,
				rgbSlider.bSlider,
			});
			colorComponents = new Dictionary<ToolbarButton, Widget>{
				{new ToolbarButton("Alpha"), alphaSlider},
				{new ToolbarButton("HSV Wheel"), hsvColorPicker.Widget},
				{new ToolbarButton("LAB Wheel"), labColorPicker.Widget},
				{new ToolbarButton("HSV"), hsvSlider},
				{new ToolbarButton("LAB"), labSlider},
				{new ToolbarButton("RGB"), rgbSlider},
			};

			Widget = new Widget {
				Padding = new Thickness(8),
				Layout = new VBoxLayout { Spacing = 8 },
				Nodes = {
					new Widget {
						Layout = new HBoxLayout { DefaultCell = new DefaultLayoutCell(Alignment.Center), Spacing = 4f },
						Padding = new Thickness(8),
					}
				}
			};
			foreach(var (button, component) in colorComponents) {
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
				foreach(var slider in sliders) {
					slider.Slider.DragStarted += () => DragStarted?.Invoke();
					slider.Slider.DragEnded += () => DragEnded?.Invoke();
					slider.Slider.Changed += () => Changed?.Invoke();
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
								image[y * size + x] = new ColorHSVA(pick.H.Value, 1, 1).ToRGBA();
							} else {
								image[y * size + x] = new ColorHSVA(color.Value.H, pick.S.Value, pick.V.Value).ToRGBA();
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
				}
				else {
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
				}
				else if (centerDistance > InnerRadius * Margin) {
					return PositionToHue(nx, ny);
				}
				else {
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
								}
								else {
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
					Padding = new Thickness { Left = 4, Top = 2 },
					MinSize = new Vector2(60, Slider.MinSize.Y),
					Text = Slider.Value.ToString("0.###")
				};
				Slider.Changed += () => {
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
			protected Property<ColorHSVA> Color { get; }

			public ColorEditorSlider(Property<ColorHSVA> color, string name, int min, int max, int pixelCounts) : base(name, min, max, pixelCounts)
			{
				Color = color;
				Slider.Changed += OnSliderValueChange;
				Slider.Updating += _ => OnColorValueChange();
			}

			protected abstract void OnSliderValueChange();
			protected abstract void OnColorValueChange();
		}

		class AlphaEditorSlider : ColorEditorSlider
		{

			private static int pixelCounts = 100;
			public AlphaEditorSlider(Property<ColorHSVA> color) : base(color, "A", 0, 1, pixelCounts)
			{
				Visible = false;
			}

			protected override void OnSliderValueChange()
			{
				Color.Value = new ColorHSVA(Color.Value.H, Color.Value.S, Color.Value.V, Slider.Value);
				
			}

			protected override void OnColorValueChange()
			{
				Slider.SetValue(Color.Value.A);
				for (var i = 0; i < pixelCounts; i++) {
					Pixels[pixelCounts - i - 1] = Color.Value.ToRGBA();
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
				Layout = new VBoxLayout {Spacing = 8};
				Nodes.AddRange(new List<Node>() {
					hSlider,
					sSlider,
					vSlider,
				});
				Visible = false;
			}

			public class HSlider : ColorEditorSlider
			{
				private static int pixelCounts = 360;
				public HSlider(Property<ColorHSVA> color) : base(color, "H", 0, 360, pixelCounts)
				{
					for (var i = 0; i < pixelCounts; i++) {
						Pixels[pixelCounts - i - 1] = new ColorHSVA(i, 1, 1).ToRGBA();
					}
				}

				protected override void OnSliderValueChange()
				{
					Color.Value = new ColorHSVA(Slider.Value, Color.Value.S, Color.Value.V, Color.Value.A);
				}

				protected override void OnColorValueChange()
				{
					Slider.SetValue(Color.Value.H);
				}
			}

			public class SSlider : ColorEditorSlider
			{
				private static int pixelCounts = 100;

				public SSlider(Property<ColorHSVA> color) : base(color, "S", 0, 1, pixelCounts)
				{
				}

				protected override void OnSliderValueChange()
				{
					Color.Value = new ColorHSVA(Color.Value.H, Slider.Value, Color.Value.V, Color.Value.A);
				}

				protected override void OnColorValueChange()
				{
					Slider.SetValue(Color.Value.S);
					for (var i = 0; i < pixelCounts; i++) {
						Pixels[pixelCounts - i - 1] = new ColorHSVA(Color.Value.H, i / (float)pixelCounts,
							Color.Value.V, 1).ToRGBA();
					}
				}
			}

			public class VSlider : ColorEditorSlider
			{
				private static int pixelCounts = 100;

				public VSlider(Property<ColorHSVA> color) : base(color, "V", 0, 1, pixelCounts)
				{
				}

				protected override void OnSliderValueChange()
				{
					Color.Value = new ColorHSVA(Color.Value.H, Color.Value.S, Slider.Value, Color.Value.A);
				}

				protected override void OnColorValueChange()
				{
					Slider.SetValue(Color.Value.V);
					for (var i = 0; i < pixelCounts; i++) {
						Pixels[pixelCounts - i - 1] = new ColorHSVA(Color.Value.H, Color.Value.S,
							i / (float)pixelCounts, 1).ToRGBA();
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
				Layout = new VBoxLayout {Spacing = 8};
				Nodes.AddRange(new List<Node>() {
					rSlider,
					gSlider,
					bSlider,
				});
				Visible = false;
			}

			public class RSlider : ColorEditorSlider
			{
				private static int pixelCounts = 256;

				public RSlider(Property<ColorHSVA> color) : base(color, "R", 0, 255, pixelCounts)
				{
				}

				protected override void OnSliderValueChange()
				{
					var sliderValue = (byte)Slider.Value;
					var rgba = Color.Value.ToRGBA();
					rgba.R = sliderValue;
					Color.Value = ColorHSVA.FromRGBA(rgba);
				}

				protected override void OnColorValueChange()
				{
					Slider.SetValue(Color.Value.ToRGBA().R);
					for (var i = 0; i < pixelCounts; i++) {
						Pixels[pixelCounts - i - 1] = Color.Value.ToRGBA();
						Pixels[pixelCounts - i - 1].R = (byte)i;
						Pixels[pixelCounts - i - 1].A = 255;
					}
				}
			}

			public class GSlider : ColorEditorSlider
			{
				private static int pixelCounts = 256;

				public GSlider(Property<ColorHSVA> color) : base(color, "G", 0, 255, pixelCounts)
				{
				}

				protected override void OnSliderValueChange()
				{
					var sliderValue = (byte)Slider.Value;
					var rgba = Color.Value.ToRGBA();
					rgba.G = sliderValue;
					Color.Value = ColorHSVA.FromRGBA(rgba);
				}

				protected override void OnColorValueChange()
				{
					Slider.SetValue(Color.Value.ToRGBA().G);
					for (var i = 0; i < pixelCounts; i++) {
						Pixels[pixelCounts - i - 1] = Color.Value.ToRGBA();
						Pixels[pixelCounts - i - 1].G = (byte)i;
						Pixels[pixelCounts - i - 1].A = 255;
					}
				}
			}

			public class BSlider : ColorEditorSlider
			{
				private static int pixelCounts = 256;

				public BSlider(Property<ColorHSVA> color) : base(color, "B", 0, 255, pixelCounts)
				{
				}

				protected override void OnSliderValueChange()
				{
					var sliderValue = (byte)Slider.Value;
					var rgba = Color.Value.ToRGBA();
					rgba.B = sliderValue;
					Color.Value = ColorHSVA.FromRGBA(rgba);
				}

				protected override void OnColorValueChange()
				{
					Slider.SetValue(Color.Value.ToRGBA().B);
					for (var i = 0; i < pixelCounts; i++) {
						Pixels[pixelCounts - i - 1] = Color.Value.ToRGBA();
						Pixels[pixelCounts - i - 1].B = (byte)i;
						Pixels[pixelCounts - i - 1].A = 255;
					}
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

			public static ColorHSVA FromRGBA(Color4 rgb)
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

			public Color4 ToRGBA()
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
		}
	}
}
