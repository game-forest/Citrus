using System;
using System.Linq;
using System.Collections.Generic;
using Lime;
using Tangerine.Core;

namespace Tangerine.UI
{
	public class ColorPickerPanel
	{
		private readonly Dictionary<ToolbarButton, Widget> colorComponents;
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
				{ new ToolbarButton("HSV Wheel"), hsvColorPicker.Widget },
				{ new ToolbarButton("Alpha"), alphaSlider },
				{ new ToolbarButton("HSV"), hsvSlider },
				{ new ToolbarButton("LAB"), labSlider },
				{ new ToolbarButton("RGB"), rgbSlider },
			};

			Widget = new Widget {
				Padding = new Thickness(horizontal: 28, vertical: 8),
				Layout = new VBoxLayout { Spacing = 8 },
				Nodes = {
					new Widget {
						Layout = new HBoxLayout { DefaultCell = new DefaultLayoutCell(Alignment.Center), Spacing = 4f },
						Padding = new Thickness(overall: 8),
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
		private class HsvTriangleColorWheel
		{
			private readonly Property<ColorHSVA> color;

			private const float InnerRadius = 100;
			private const float OuterRadius = 120;
			private const float Margin = 1.05f;
			
			public readonly Widget Widget;

			public event Action DragStarted;
			public event Action DragEnded;
			public event Action Changed;

			private const float CenterX = OuterRadius;
			private const float CenterY = OuterRadius;
			private const float CursorRadius = (OuterRadius - InnerRadius * Margin) / 2;

			private bool wasHueChanged = true;

			private Texture2D texture = new Texture2D();
			private Color4[] image;
			
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

			private void Render(Widget widget)
			{
				widget.PrepareRendererState();
				DrawControl();
				DrawTriangleCursor();
				DrawWheelCursor();
			}

			private void DrawTriangleCursor()
			{
				var cursor = new Vector2(
					CenterX - InnerRadius * (1 - 3 * color.Value.S * color.Value.V) / 2,
					CenterY + InnerRadius * Mathf.Sqrt(3) *
					(color.Value.S * color.Value.V - 2 * color.Value.V + 1) / 2
				);
				Renderer.DrawCircle(cursor, CursorRadius, 20, Color4.Black);
			}

			private void DrawWheelCursor()
			{
				var cursor =
					Vector2.CosSin(color.Value.H * Mathf.DegToRad) *
					(InnerRadius * Margin + OuterRadius) / 2 +
					Vector2.One * new Vector2(CenterX, CenterY);
				Renderer.DrawCircle(cursor, CursorRadius, 20, Color4.Black);
			}

			private void DrawControl()
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
			
			private static void ShiftedCoordinates(float x, float y, out float nx, out float ny)
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

			private static Result Pick(float x, float y)
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

			private IEnumerator<object> SelectTask()
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
			
			private enum Area
			{
				Outside,
				Wheel,
				Triangle
			}

			private struct Result
			{
				public Area Area { get; set; }
				public float? H { get; set; }
				public float? S { get; set; }
				public float? V { get; set; }
			}
		}
		
		private class PixelsSlider : Widget
		{
			public ThemedSlider Slider { get; }
			protected Color4[] Pixels { get; }

			protected PixelsSlider(string name, int min, int max, int pixelCount)
			{
				Padding = new Thickness(8, 0);
				Layout = new LinearLayout();
				Pixels = new Color4[pixelCount];
				Slider = new ThemedSlider {
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
			
			private class PixelsSliderPresenter : IPresenter
			{
				private readonly Color4[] pixels;
				
				public PixelsSliderPresenter(Color4[] pixels) => this.pixels = pixels;

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
					private readonly Texture2D texture = new Texture2D();
					
					public Vector2 Size;
					public Color4 Color;
					public Color4 BorderColor;
					public Color4[] Pixels;

					public override void Render()
					{
						PrepareRenderState();
						texture.LoadImage(Pixels, width: Pixels.Length, height: 1);
						Renderer.DrawRect(a: Vector2.Zero, b: Size, color: Color);
						Renderer.DrawRectOutline(a: Vector2.Zero, b: Size, color: BorderColor);
						Renderer.DrawSprite(
							texture1: texture, 
							color: Color4.White, 
							position: Vector2.Zero, 
							size: Vector2.One * Size,
							uv0: new Vector2(1, 0), 
							uv1: new Vector2(0, 1)
						);
					}
				}
			}
		}
		
		private abstract class ColorEditorSlider : PixelsSlider
		{
			protected Property<ColorHSVA> SliderColor { get; }

			protected ColorEditorSlider(
				Property<ColorHSVA> color,
				string name,
				int min,
				int max,
				int pixelCount
			) : base(name, min, max, pixelCount) {
				SliderColor = color;
				Slider.Changed += OnSliderValueChange;
				Slider.Updating += _ => OnColorValueChange();
			}
			protected abstract void OnSliderValueChange();
			protected abstract void OnColorValueChange();
		}

		private class LabColorHolderCompoenent : NodeComponent
		{
			public ColorLAB Color { get; set; }
		}

		private abstract class LABColorEditorSlider : ColorEditorSlider
		{
			private ColorLAB initialLAB;
			
			protected ColorLAB CurrentLAB
			{
				get => labHolder?.Color ?? initialLAB;
				set
				{
					if (labHolder != null) {
						labHolder.Color = value;
					} else {
						initialLAB = value;
					}					
				}
			}
			
			private LabColorHolderCompoenent labHolder;

			protected LABColorEditorSlider(
				Property<ColorHSVA> color,
				string name,
				int min,
				int max,
				int pixelCount
			) : base(color, name, min, max, pixelCount) {
				initialLAB = ColorLAB.RGBtoLAB(color.Value.HSVAtoRGB());
			}

			protected override void OnParentChanged(Node oldParent)
			{
				base.OnParentChanged(oldParent);
				if (Parent != null) {
					labHolder = Parent.Components.GetOrAdd<LabColorHolderCompoenent>();
					labHolder.Color = initialLAB;
				}
			}
		}
		
		private class AlphaEditorSlider : Widget
		{
			public AlphaSlider alphaSlider;
			
			public AlphaEditorSlider(Property<ColorHSVA> color)
			{
				alphaSlider = new AlphaSlider(color);
				Padding = new Thickness(horizontal: 0, vertical: 8);
				Layout = new VBoxLayout { Spacing = 8 };
				Nodes.AddRange(new List<Node> {
					alphaSlider
				});
				Visible = false;
			}
			
			public class AlphaSlider : ColorEditorSlider
			{
				private const int PixelCount = 100;

				public AlphaSlider(Property<ColorHSVA> color) : base(color, name: "A", min: 0, max: 1, PixelCount) { }
				
				protected override void OnSliderValueChange() => SliderColor.Value = new ColorHSVA(
					hue: SliderColor.Value.H, 
					saturation: SliderColor.Value.S, 
					value: SliderColor.Value.V, 
					alpha: Slider.Value
				);

				protected override void OnColorValueChange()
				{
					Slider.Value = SliderColor.Value.A;
					for (var i = 0; i < PixelCount; i++) {
						Pixels[PixelCount - i - 1] = SliderColor.Value.HSVAtoRGB();
					}
				}
			}
		}
		
		private class HsvEditorSlider : Widget
		{
			public readonly HSlider hSlider;
			public readonly SSlider sSlider;
			public readonly VSlider vSlider;
			
			public HsvEditorSlider(Property<ColorHSVA> color)
			{
				hSlider = new HSlider(color);
				sSlider = new SSlider(color);
				vSlider = new VSlider(color);

				Padding = new Thickness(horizontal: 0, vertical: 8);
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
				private const int PixelCount = 360;

				public HSlider(Property<ColorHSVA> color) : base(color, name: "H", min: 0, max: 360, PixelCount)
				{
					for (var i = 0; i < PixelCount; i++) {
						Pixels[PixelCount - i - 1] = new ColorHSVA(hue: i, saturation: 1, value: 1).HSVAtoRGB();
					}
				}

				protected override void OnSliderValueChange() => SliderColor.Value = new ColorHSVA(
					hue: Slider.Value, 
					saturation: SliderColor.Value.S, 
					value: SliderColor.Value.V, 
					alpha: SliderColor.Value.A
				);

				protected override void OnColorValueChange() => Slider.Value = ((int)SliderColor.Value.H);
			}
			public class SSlider : ColorEditorSlider
			{
				private const int PixelCount = 100;

				public SSlider(Property<ColorHSVA> color) : base(color, name: "S", min: 0, max: 1, PixelCount) { }
				
				protected override void OnSliderValueChange() => SliderColor.Value = new ColorHSVA(
					hue: SliderColor.Value.H, 
					saturation: Slider.Value, 
					value: SliderColor.Value.V, 
					alpha: SliderColor.Value.A
				);

				protected override void OnColorValueChange()
				{
					Slider.Value = SliderColor.Value.S;
					for (var i = 0; i < PixelCount; i++) {
						Pixels[PixelCount - i - 1] = new ColorHSVA(
							hue: SliderColor.Value.H, 
							saturation: i / (float)PixelCount,
							value: SliderColor.Value.V, 
							alpha: 1
						).HSVAtoRGB();
					}
				}
			}
			public class VSlider : ColorEditorSlider
			{
				private const int PixelCount = 100;

				public VSlider(Property<ColorHSVA> color) : base(color, name: "V", min: 0, max: 1, PixelCount) { }
				
				protected override void OnSliderValueChange() => SliderColor.Value = new ColorHSVA(
					hue: SliderColor.Value.H, 
					saturation: SliderColor.Value.S, 
					value: Slider.Value, 
					alpha: SliderColor.Value.A
				);

				protected override void OnColorValueChange()
				{
					Slider.Value = SliderColor.Value.V;
					for (var i = 0; i < PixelCount; i++) {
						Pixels[PixelCount - i - 1] = new ColorHSVA(
							hue: SliderColor.Value.H,
							saturation: SliderColor.Value.S,
							value: i / (float)PixelCount,
							alpha: 1
						).HSVAtoRGB();
					}
				}
			}
		}
		
		private class RgbEditorSlider : Widget
		{
			public readonly RSlider rSlider;
			public readonly GSlider gSlider;
			public readonly BSlider bSlider;
			
			public RgbEditorSlider(Property<ColorHSVA> color)
			{
				rSlider = new RSlider(color);
				gSlider = new GSlider(color);
				bSlider = new BSlider(color);
				Padding = new Thickness(horizontal: 0, vertical: 8);
				Layout = new VBoxLayout { Spacing = 8 };
				Nodes.AddRange(new List<Node> {
					rSlider,
					gSlider,
					bSlider,
				});
				Visible = false;
			}
			
			public class RSlider : ColorEditorSlider
			{
				private const int PixelCount = 256;

				public RSlider(Property<ColorHSVA> color) : base(color, name: "R", min: 0, max: 255, PixelCount) { }
				
				protected override void OnSliderValueChange()
				{
					var sliderValue = (byte)Slider.Value;
					var rgba = SliderColor.Value.HSVAtoRGB();
					rgba.R = sliderValue;
					SliderColor.Value = ColorHSVA.RGBtoHSVA(rgba);
				}
				
				protected override void OnColorValueChange()
				{
					Slider.Value = SliderColor.Value.HSVAtoRGB().R;
					for (var i = 0; i < PixelCount; i++) {
						Pixels[PixelCount - i - 1] = SliderColor.Value.HSVAtoRGB();
						Pixels[PixelCount - i - 1].R = (byte)i;
						Pixels[PixelCount - i - 1].A = 255;
					}
				}
			}
			
			public class GSlider : ColorEditorSlider
			{
				private const int pixelCount = 256;

				public GSlider(Property<ColorHSVA> color) : base(color, name: "G", min: 0, max: 255, pixelCount) { }
				
				protected override void OnSliderValueChange()
				{
					var sliderValue = (byte)Slider.Value;
					var rgba = SliderColor.Value.HSVAtoRGB();
					rgba.G = sliderValue;
					SliderColor.Value = ColorHSVA.RGBtoHSVA(rgba);
				}
				
				protected override void OnColorValueChange()
				{
					Slider.Value = SliderColor.Value.HSVAtoRGB().G;
					for (var i = 0; i < pixelCount; i++) {
						Pixels[pixelCount - i - 1] = SliderColor.Value.HSVAtoRGB();
						Pixels[pixelCount - i - 1].G = (byte)i;
						Pixels[pixelCount - i - 1].A = 255;
					}
				}
			}
			
			public class BSlider : ColorEditorSlider
			{
				private const int PixelCount = 256;

				public BSlider(Property<ColorHSVA> color) : base(color, name: "B", min: 0, max: 255, PixelCount) { }
				
				protected override void OnSliderValueChange()
				{
					var sliderValue = (byte)Slider.Value;
					var rgba = SliderColor.Value.HSVAtoRGB();
					rgba.B = sliderValue;
					SliderColor.Value = ColorHSVA.RGBtoHSVA(rgba);
				}
				
				protected override void OnColorValueChange()
				{
					Slider.Value = SliderColor.Value.HSVAtoRGB().B;
					for (var i = 0; i < PixelCount; i++) {
						Pixels[PixelCount - i - 1] = SliderColor.Value.HSVAtoRGB();
						Pixels[PixelCount - i - 1].B = (byte)i;
						Pixels[PixelCount - i - 1].A = 255;
					}
				}
			}
		}
		
		private class LabEditorSlider : Widget
		{
			public readonly LSlider lSlider;
			public readonly ASlider aSlider;
			public readonly BSlider bSlider;
			
			public LabEditorSlider(Property<ColorHSVA> color)
			{
				lSlider = new LSlider(color);
				aSlider = new ASlider(color);
				bSlider = new BSlider(color);
				Padding = new Thickness(horizontal: 0, vertical: 8);
				Layout = new VBoxLayout { Spacing = 8 };
				Nodes.AddRange(new List<Node> {
					lSlider,
					aSlider,
					bSlider,
				});
				Visible = false;
			}
			
			public class LSlider : LABColorEditorSlider
			{
				private const int PixelCount = 100;

				public LSlider(Property<ColorHSVA> color) : base(color, name: "L", min: 0, max: 100, PixelCount) { }
				
				protected override void OnColorValueChange()
				{
					var c = new ColorLAB(CurrentLAB.L, CurrentLAB.A, CurrentLAB.B, CurrentLAB.alpha);
					if (!SliderColor.Value.IsEqual(ColorHSVA.RGBtoHSVA(CurrentLAB.LABtoRGB()))) {
						c = ColorLAB.RGBtoLAB(SliderColor.Value.HSVAtoRGB());
						CurrentLAB = c;
					} 
					Slider.Value = (int)c.L;
					for (int i = 0; i < PixelCount; i++) {
						c.L = i;
						Pixels[PixelCount - i - 1] = c.LABtoRGB();
						Pixels[PixelCount - i - 1].A = 255;
					}
				}
				
				protected override void OnSliderValueChange()
				{
					var sliderValue = Math.Round(Slider.Value);
					CurrentLAB.L = sliderValue;
					SliderColor.Value = ColorHSVA.RGBtoHSVA(CurrentLAB.LABtoRGB());
				}
			}
			
			public class ASlider : LABColorEditorSlider
			{
				private const int PixelCount = 256;

				public ASlider(Property<ColorHSVA> color) : base(color, name: "A", min: -128, max: 128, PixelCount) { }
				
				protected override void OnColorValueChange()
				{
					var c = new ColorLAB(CurrentLAB.L, CurrentLAB.A, CurrentLAB.B, CurrentLAB.alpha);
					if (!SliderColor.Value.IsEqual(ColorHSVA.RGBtoHSVA(CurrentLAB.LABtoRGB()))) {
						c = ColorLAB.RGBtoLAB(SliderColor.Value.HSVAtoRGB());
						CurrentLAB = c;
					} 
					Slider.Value = (int)c.A;
					for (int i = 0; i < PixelCount; i++) {
						c.A = i - 127;
						Pixels[PixelCount - i - 1] = c.LABtoRGB();
						Pixels[PixelCount - i - 1].A = 255;
					}
				}
				
				protected override void OnSliderValueChange()
				{
					var sliderValue = Math.Round(Slider.Value);
					CurrentLAB.A = sliderValue;
					SliderColor.Value = ColorHSVA.RGBtoHSVA(CurrentLAB.LABtoRGB());
				}
			}
			
			public class BSlider : LABColorEditorSlider
			{
				private const int PixelCount = 256;

				public BSlider(Property<ColorHSVA> color) : base(color, name: "B", min: -128, max: 128, PixelCount) { }
				
				protected override void OnColorValueChange()
				{
					var c = new ColorLAB(CurrentLAB.L, CurrentLAB.A, CurrentLAB.B, CurrentLAB.alpha);
					if (!SliderColor.Value.IsEqual(ColorHSVA.RGBtoHSVA(CurrentLAB.LABtoRGB()))) {
						c = ColorLAB.RGBtoLAB(SliderColor.Value.HSVAtoRGB());
						CurrentLAB = c;
					}
					Slider.Value = (int)c.B;
					for (int i = 0; i < PixelCount; i++) {
						c.B = i - 127;
						Pixels[PixelCount - i - 1] = c.LABtoRGB();
						Pixels[PixelCount - i - 1].A = 255;
					}
				}
				
				protected override void OnSliderValueChange()
				{
					var sliderValue = Math.Round(Slider.Value);
					CurrentLAB.B = sliderValue;
					SliderColor.Value = ColorHSVA.RGBtoHSVA(CurrentLAB.LABtoRGB());
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
				return hi switch {
					0 => new Color4(v, t, p, a),
					1 => new Color4(q, v, p, a),
					2 => new Color4(p, v, t, a),
					3 => new Color4(p, q, v, a),
					4 => new Color4(t, p, v, a),
					_ => new Color4(v, p, q, a)
				};
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
			
			public bool IsEqual(ColorHSVA rv)
			{
				// TODO Equality comparison of floating point numbers. Possible loss of precision while rounding values ?
				return H == rv.H && S == rv.S && V == rv.V && A == rv.A;
			}
		}
		
		private readonly struct ColorXYZ
		{
			public readonly double X;
			public readonly double Y;
			public readonly double Z;
			public readonly float alpha;

			public ColorXYZ(double X, double Y, double Z, float alpha)
			{
				this.X = X;
				this.Y = Y;
				this.Z = Z;
				this.alpha = alpha;
			}
		};
		
		private class ColorLAB
		{
			// Constants for D65 white point
			// private (double X, double Y, double Z) whitePoint = (0.95047, 1, 1.0883);
			// Bradford-adapted D50 white point. 
			private static (double X, double Y, double Z) whitePoint = (0.964212, 1, 0.825188);
			private const double kE = 216.0 / 24389.0;
			private const double kK = 24389.0 / 27.0;
			private const double kKE = 8.0;
			
			public double L;
			public double A;
			public double B;
			public float alpha;

			public ColorLAB(double L, double A, double B, float alpha)
			{
				this.L = L;
				this.A = A;
				this.B = B;
				this.alpha = alpha;
			}
			
			public static ColorLAB RGBtoLAB(Color4 rgb) => XYZtoLAB(RGBtoXYZ(rgb));

			private static ColorXYZ RGBtoXYZ(Color4 rgb)
			{
				var var_R = InverseCompand(rgb.R / 255.0);
				var var_G = InverseCompand(rgb.G / 255.0);
				var var_B = InverseCompand(rgb.B / 255.0);
				// constants for D65 white point
				//X: var_R * 0.4124 + var_G * 0.3576 + var_B * 0.1805,
				//Y: var_R * 0.2126 + var_G * 0.7152 + var_B * 0.0722,
				//Z: var_R * 0.0193 + var_G * 0.1192 + var_B * 0.9505,	
				return new ColorXYZ(
					X: var_R * 0.4360747 + var_G * 0.3850649 + var_B * 0.1430804,
					Y: var_R * 0.2225045 + var_G * 0.7168786 + var_B * 0.0606169,
					Z: var_R * 0.0139322 + var_G * 0.0971045 + var_B * 0.7141733,
					alpha: rgb.A
				);
				
				double InverseCompand(double colorPart)
				{
					var sign = 1.0;
					if (colorPart < 0.0) {
						sign = -1.0;
						colorPart = -colorPart;
					}
					var linear = colorPart <= 0.04045 ? colorPart / 12.92 : Math.Pow((colorPart + 0.055) / 1.055, 2.4);
					linear *= sign;
					return linear;
				}
			}
			
			private static ColorLAB XYZtoLAB(ColorXYZ xyz)
			{
				double var_Y = ConvertXYZtoLAB(xyz.Y / whitePoint.Y);
				double var_X = ConvertXYZtoLAB(xyz.X / whitePoint.X);
				double var_Z = ConvertXYZtoLAB(xyz.Z / whitePoint.Z);
				var L = 116.0 * var_Y - 16.0;
				var A = 500.0 * (var_X - var_Y);
				var B = 200.0 * (var_Y - var_Z);
				return new ColorLAB(Math.Round(L), Math.Round(A), Math.Round(B), xyz.alpha);
				
				double ConvertXYZtoLAB(double colorPart) =>
					colorPart > kE ? Math.Pow(colorPart, 1.0 / 3.0) : (kK * colorPart + 16.0)/ 116.0;
			}
			
			public Color4 LABtoRGB() => XYZtoRGB(LABtoXYZ());

			private ColorXYZ LABtoXYZ()
			{
				double var_y = (L + 16.0) / 116.0;
				double var_x = A * 0.002 + var_y;
				double var_z = var_y - B * 0.005;
				var_x = (Math.Pow(var_x, 3) > kE) ? Math.Pow(var_x, 3) : ((116.0 * var_x - 16.0) / kK);
				var_y = (L > kKE) ? Math.Pow((L + 16.0) / 116.0, 3.0) : (L / kK);
				var_z = (Math.Pow(var_z, 3) > kE) ? Math.Pow(var_z, 3) : ((116.0 * var_z - 16.0) / kK);
				return new ColorXYZ(var_x * whitePoint.X, var_y * whitePoint.Y, var_z * whitePoint.Z, alpha);
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
					return companded < 0 ? 0 : companded * 255 > 255 ? 1 : companded;
				}
			}
		}
	}
}
