using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Lime;
using Tangerine.Core;

namespace Tangerine.UI
{
	public class ColorPickerPanel
	{
		private const int EditorsLocationOffset = 1;

		private readonly (ToolbarButton, Widget)[] components;

		public readonly Widget EditorsWidget;
		public readonly Widget ButtonsWidget;

		private ColorHsva colorHsva;

		public event Action DragStarted;
		public event Action DragEnded;
		public event Action Changed;

		public Color4 Color
		{
			get => colorHsva.HsvaToRgb();
			set => colorHsva = ColorHsva.RgbToHsva(value);
		}

		private bool enabled = true;

		public bool Enabled
		{
			get => enabled;
			set
			{
				if (enabled != value) {
					enabled = value;
					foreach (var (button, component) in components) {
						button.Enabled = enabled;
						component.Enabled = enabled;
					}
				}
			}
		}

		public ColorPickerPanel(ColorPickerState editorsVisibleState)
		{
			var colorProperty = new Property<ColorHsva>(() => colorHsva, c => colorHsva = c);
			var hsvColorPicker = new HsvTriangleColorWheel(colorProperty);
			PickerEventsSubscribe(hsvColorPicker);
			hsvColorPicker.Widget.Visible = false;
			var alphaEditor = new AlphaEditor(colorProperty);
			var hsvEditor = new HsvEditor(colorProperty);
			var rgbEditor = new RgbEditor(colorProperty);
			var labEditor = new LabEditor(colorProperty);
			SliderEventsSubscribe(new List<ColorEditorSlider>() {
				alphaEditor.Slider,
				hsvEditor.HSlider,
				hsvEditor.SSlider,
				hsvEditor.VSlider,
				rgbEditor.RSlider,
				rgbEditor.GSlider,
				rgbEditor.BSlider,
				labEditor.LSlider,
				labEditor.ASlider,
				labEditor.BSlider
			});
			hsvColorPicker.Widget.LayoutCell = new LayoutCell(Alignment.Center);
			components = new (ToolbarButton, Widget)[] {
				( CreateIcon("Universal.HSV.Wheel"), hsvColorPicker.Widget ),
				( CreateIcon("Universal.Alpha.Slider"), alphaEditor ),
				( CreateIcon("Universal.HSV.Sliders"), hsvEditor ),
				( CreateIcon("Universal.LAB.Sliders"), labEditor ),
				( CreateIcon("Universal.RGB.Sliders"), rgbEditor ),
			};
			EditorsWidget = new Widget {
				Padding = new Thickness(horizontal: 0, vertical: 0),
				Layout = new VBoxLayout { Spacing = 8 },
				Nodes = { Spacer.HStretch() }
			};
			ButtonsWidget = new Widget {
				Layout = new HBoxLayout {
					DefaultCell = new DefaultLayoutCell(Alignment.Center),
					Spacing = 2f
				},
				Padding = new Thickness(0, 0)
			};
			foreach (var (button, component) in components) {
				ButtonsWidget.AddNode(button);
				button.Clicked = () => {
					button.Checked = !button.Checked;
					if (button.Checked) {
						int editorIndex = EditorsWidget.Nodes.IndexOf(component);
						int visibleEditorCount = components.Count(c => c.Item2.Visible);
						EditorsWidget.Nodes.Move(
							indexFrom: editorIndex,
							indexTo: EditorsLocationOffset + visibleEditorCount
						);
					}
					component.Visible = button.Checked;
				};
			}
			var orderedEditors = new Node[components.Length];
			var slotStates = new bool[components.Length];
			foreach (var state in editorsVisibleState.Enumerate()) {
				if (slotStates[state.Position]) {
					editorsVisibleState = ColorPickerState.Default;
					break;
				}
				slotStates[state.Position] = true;
			}
			int componentIndex = 0;
			foreach (var state in editorsVisibleState.Enumerate()) {
				var (button, component) = components[componentIndex];
				orderedEditors[state.Position] = component;
				component.Visible = state.Visible;
				button.Checked = state.Visible;
				componentIndex += 1;
			}
			EditorsWidget.Nodes.AddRange(orderedEditors);
			EditorsWidget.FocusScope = new KeyboardFocusScope(EditorsWidget);

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
			ToolbarButton CreateIcon(string icon)
			{
				var button = new ToolbarButton {
					Layout = new StackLayout(),
					LayoutCell = new LayoutCell(),
					Size = new Vector2(20),
					MinMaxSize = new Vector2(20),
					Padding = Thickness.Zero,
					Text = ""
				};
				button.AddNode(new Image(IconPool.GetTexture(icon)) {
					Padding = new Thickness(1)
				});
				return button;
			}
		}

		public ColorPickerState GetEditorsVisibleState()
		{
			var states = new ColorPickerState.EditorState[components.Length];
			for (int i = 0; i < states.Length; i++) {
				var (_, editor) = components[i];
				states[i] = new ColorPickerState.EditorState {
					Visible = editor.Visible,
					Position = EditorsWidget.Nodes.IndexOf(editor) - EditorsLocationOffset
				};
			}
			return new ColorPickerState(states);
		}

		private class HsvTriangleColorWheel
		{
			private readonly Property<ColorHsva> color;

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

			public HsvTriangleColorWheel(Property<ColorHsva> color)
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
								image[y * size + x] = new ColorHsva(pick.H.Value, 1, 1).HsvaToRgb();
							} else {
								image[y * size + x] = new ColorHsva(color.Value.H, pick.S.Value, pick.V.Value).HsvaToRgb();
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
				ShiftedCoordinates(x, y, out float nx, out float ny);
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
								ShiftedCoordinates(
									Widget.Input.MousePosition.X - Widget.GlobalPosition.X,
									Widget.Input.MousePosition.Y - Widget.GlobalPosition.Y,
									out float nx, out float ny);
								if (pick.Area == Area.Triangle) {
									var newPick = PositionToSV(nx, ny, ignoreBounds: true);
									color.Value = new ColorHsva {
										H = color.Value.H,
										S = Mathf.Min(Mathf.Max(newPick.S.Value, 0), 1),
										V = Mathf.Min(Mathf.Max(newPick.V.Value, 0), 1),
										A = color.Value.A
									};
								} else {
									var newPick = PositionToHue(nx, ny);
									color.Value = new ColorHsva {
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

			protected PixelsSlider(string name, int min, int max, int pixelCount, string format)
			{
				Padding = new Thickness(2, 0);
				Layout = new LinearLayout();
				Pixels = new Color4[pixelCount];
				Slider = new ThemedSlider {
					RangeMin = min,
					RangeMax = max,
					MinSize = new Vector2(30, 16),
					MaxHeight = 16
				};
				var value = new ThemedSimpleText {
					HitTestTarget = false,
					HAlignment = HAlignment.Left,
					VAlignment = VAlignment.Center,
					Padding = new Thickness { Left = 5, Top = 0 },
					MinSize = new Vector2(36, Slider.MinHeight),
					Text = Slider.Value.ToString(format)
				};
				Slider.Updating += _ => {
					value.Text = Slider.Value.ToString(format);
				};
				Slider.CompoundPresenter.Insert(0, new PixelsSliderPresenter(Pixels));
				var label = new ThemedSimpleText {
					HitTestTarget = false,
					HAlignment = HAlignment.Left,
					VAlignment = VAlignment.Center,
					Padding = new Thickness { Left = 2, Top = 0 },
					MinSize = new Vector2(20, Slider.MinHeight),
					Text = name,
				};
				AddNode(label);
				AddNode(Slider);
				AddNode(value);
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
					renderObject.BorderColor = Theme.Colors.ControlBorder;
					renderObject.Pixels = pixels;
					return renderObject;
				}
				public bool PartialHitTest(Node node, ref HitTestArgs args) => false;

				private class RenderObject : WidgetRenderObject
				{
					private readonly Texture2D texture = new Texture2D();

					public Vector2 Size;
					public Color4 BorderColor;
					public Color4[] Pixels;

					public override void Render()
					{
						PrepareRenderState();
						texture.LoadImage(Pixels, width: Pixels.Length, height: 1);
						Renderer.DrawSprite(
							texture1: texture,
							color: Color4.White,
							position: Vector2.Zero,
							size: Size,
							uv0: new Vector2(1, 0),
							uv1: new Vector2(0, 1)
						);
						Renderer.DrawRectOutline(a: Vector2.Zero, b: Size, color: BorderColor);
					}
				}
			}
		}

		private abstract class ColorEditorSlider : PixelsSlider
		{
			private ColorHsva cachedColor;

			protected Property<ColorHsva> SliderColor { get; }

			protected ColorEditorSlider(
				Property<ColorHsva> color,
				string name,
				int min,
				int max,
				int pixelCount,
				string format
			) : base(name, min, max, pixelCount, format) {
				cachedColor = color.Value;
				SliderColor = color;
				Slider.Changed += OnSliderValueChange;
				Slider.Updating += _ => {
					if (!cachedColor.IsEqual(color.Value)) {
						OnColorValueChange();
					}
				};
			}

			protected abstract void OnSliderValueChange();
			protected abstract void OnColorValueChange();
		}

		private class LabColorHolderCompoenent : NodeComponent
		{
			public ColorLab Color { get; set; }
		}

		private abstract class LabColorEditorSlider : ColorEditorSlider
		{
			private ColorLab initialLAB;

			protected ColorLab CurrentLab
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

			protected LabColorEditorSlider(
				Property<ColorHsva> color,
				string name,
				int min,
				int max,
				int pixelCount,
				string format
			) : base(color, name, min, max, pixelCount, format) {
				initialLAB = ColorLab.RgbToLab(color.Value.HsvaToRgb());
			}

			protected override void OnColorValueChange()
			{
				if (SliderColor.Value.HsvaToRgb() != CurrentLab.LabToRgb()) {
					var c = ColorLab.RgbToLab(SliderColor.Value.HsvaToRgb());
					CurrentLab = c;
				}
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

		private class AlphaEditor : ColorEditor
		{
			public readonly AlphaSlider Slider;

			public AlphaEditor(Property<ColorHsva> color)
			{
				Slider = new AlphaSlider(color);
				AddNode(Slider);
				Visible = false;
			}

			public class AlphaSlider : ColorEditorSlider
			{
				private const int PixelCount = 100;

				public AlphaSlider(Property<ColorHsva> color) :
					base(color, name: "A", min: 0, max: 255, PixelCount, format: "0.")
				{
					Slider.CompoundPresenter.Insert(1, new BackgroundPresenter());
				}

				protected override void OnSliderValueChange()
				{
					SliderColor.Value = new ColorHsva(
						hue: SliderColor.Value.H,
						saturation: SliderColor.Value.S,
						value: SliderColor.Value.V,
						alpha: Slider.Value / 255f
					);
				}

				protected override void OnColorValueChange()
				{
					Slider.Value = (byte)(SliderColor.Value.A * 255);
					var color = SliderColor.Value.HsvaToRgb();
					for (int i = 0; i < PixelCount; i++) {
						color.A = (byte)((float)i / PixelCount * 255);
						Pixels[PixelCount - i - 1] = color;
					}
				}

				private class BackgroundPresenter : SyncCustomPresenter
				{
					public override void Render(Node node)
					{
						var widget = node.AsWidget;
						widget.PrepareRendererState();
						RendererWrapper.Current.DrawRect(Vector2.Zero, widget.Size, Color4.White);
						const int numChecks = 20;
						var checkSize = new Vector2(widget.Width / numChecks, widget.Height / 2);
						for (int i = 0; i < numChecks; i++) {
							var checkPos = new Vector2(i * checkSize.X, (i % 2 == 0) ? 0 : checkSize.Y);
							Renderer.DrawRect(checkPos, checkPos + checkSize, Color4.Black);
						}
					}
				}
			}
		}

		private class HsvEditor : ColorEditor
		{
			public readonly ColorEditorSlider HSlider;
			public readonly ColorEditorSlider SSlider;
			public readonly ColorEditorSlider VSlider;

			public HsvEditor(Property<ColorHsva> color)
			{
				AddNode(HSlider = new HueSlider(color));
				AddNode(SSlider = new SaturationSlider(color));
				AddNode(VSlider = new ValueSlider(color));
				Visible = false;
			}

			private class HueSlider : ColorEditorSlider
			{
				private const int PixelCount = 360;

				public HueSlider(Property<ColorHsva> color) :
					base(color, name: "H", min: 0, max: 360, PixelCount, format: "0.#")
				{
					for (var i = 0; i < PixelCount; i++) {
						Pixels[PixelCount - i - 1] = new ColorHsva(hue: i, saturation: 1, value: 1).HsvaToRgb();
					}
				}

				protected override void OnSliderValueChange()
				{
					SliderColor.Value = new ColorHsva(
						hue: Slider.Value,
						saturation: SliderColor.Value.S,
						value: SliderColor.Value.V,
						alpha: SliderColor.Value.A
					);
				}

				protected override void OnColorValueChange() => Slider.Value = (int)SliderColor.Value.H;
			}

			private class SaturationSlider : ColorEditorSlider
			{
				private const int PixelCount = 100;

				public SaturationSlider(Property<ColorHsva> color) :
					base(color, name: "S", min: 0, max: 1, PixelCount, format: "0.###") { }

				protected override void OnSliderValueChange()
				{
					SliderColor.Value = new ColorHsva(
						hue: SliderColor.Value.H,
						saturation: Slider.Value,
						value: SliderColor.Value.V,
						alpha: SliderColor.Value.A
					);
				}

				protected override void OnColorValueChange()
				{
					Slider.Value = SliderColor.Value.S;
					for (var i = 0; i < PixelCount; i++) {
						Pixels[PixelCount - i - 1] = new ColorHsva(
							hue: SliderColor.Value.H,
							saturation: i / (float)PixelCount,
							value: SliderColor.Value.V,
							alpha: 1
						).HsvaToRgb();
					}
				}
			}

			private class ValueSlider : ColorEditorSlider
			{
				private const int PixelCount = 100;

				public ValueSlider(Property<ColorHsva> color) :
					base(color, name: "V", min: 0, max: 1, PixelCount, format: "0.###") { }

				protected override void OnSliderValueChange()
				{
					SliderColor.Value = new ColorHsva(
						hue: SliderColor.Value.H,
						saturation: SliderColor.Value.S,
						value: Slider.Value,
						alpha: SliderColor.Value.A
					);
				}

				protected override void OnColorValueChange()
				{
					Slider.Value = SliderColor.Value.V;
					for (var i = 0; i < PixelCount; i++) {
						Pixels[PixelCount - i - 1] = new ColorHsva(
							hue: SliderColor.Value.H,
							saturation: SliderColor.Value.S,
							value: i / (float)PixelCount,
							alpha: 1
						).HsvaToRgb();
					}
				}
			}
		}

		private class RgbEditor : ColorEditor
		{
			public readonly ColorEditorSlider RSlider;
			public readonly ColorEditorSlider GSlider;
			public readonly ColorEditorSlider BSlider;

			public RgbEditor(Property<ColorHsva> color)
			{
				AddNode(RSlider = new ColorChannelSlider(color, "R", new ColorChannelSlider.RChannel()));
				AddNode(GSlider = new ColorChannelSlider(color, "G", new ColorChannelSlider.GChannel()));
				AddNode(BSlider = new ColorChannelSlider(color, "B", new ColorChannelSlider.BChannel()));
				Visible = false;
			}

			private class ColorChannelSlider : ColorEditorSlider
			{
				private const int PixelCount = 256;

				private readonly Channel channel;

				public ColorChannelSlider(Property<ColorHsva> color, string name, Channel channel)
					: base(color, name, min: 0, max: 255, PixelCount, format: "0.") => this.channel = channel;

				protected override void OnSliderValueChange()
				{
					var sliderValue = (byte)Slider.Value;
					var color = SliderColor.Value.HsvaToRgb();
					channel.SetValue(ref color, sliderValue);
					SliderColor.Value = ColorHsva.RgbToHsva(color);
				}

				protected override void OnColorValueChange()
				{
					var color = SliderColor.Value.HsvaToRgb();
					Slider.Value = channel.GetValue(color);
					color.A = 255;
					for (var i = 0; i < PixelCount; i++) {
						channel.SetValue(ref color, (byte)i);
						Pixels[PixelCount - i - 1] = color;
					}
				}

				public abstract class Channel
				{
					public abstract byte GetValue(Color4 color);
					public abstract void SetValue(ref Color4 color, byte value);
				}

				public class RChannel : Channel
				{
					public override byte GetValue(Color4 color) => color.R;
					public override void SetValue(ref Color4 color, byte value) => color.R = value;
				}

				public class BChannel : Channel
				{
					public override byte GetValue(Color4 color) => color.B;
					public override void SetValue(ref Color4 color, byte value) => color.B = value;
				}

				public class GChannel : Channel
				{
					public override byte GetValue(Color4 color) => color.G;
					public override void SetValue(ref Color4 color, byte value) => color.G = value;
				}
			}
		}

		private class LabEditor : ColorEditor
		{
			public readonly ColorEditorSlider LSlider;
			public readonly ColorEditorSlider ASlider;
			public readonly ColorEditorSlider BSlider;

			public LabEditor(Property<ColorHsva> color)
			{
				AddNode(LSlider = new LChanneSlider(color));
				AddNode(ASlider = new AChanneSlider(color));
				AddNode(BSlider = new BChanneSlider(color));
				Visible = false;
			}

			private class LChanneSlider : LabColorEditorSlider
			{
				private const int PixelCount = 100;

				public LChanneSlider(Property<ColorHsva> color) :
					base(color, name: "L", min: 0, max: 100, PixelCount, format: "0.#") { }

				protected override void OnColorValueChange()
				{
					base.OnColorValueChange();
					var c = new ColorLab(CurrentLab.L, CurrentLab.A, CurrentLab.B, CurrentLab.Alpha);
					Slider.Value = (int)c.L;
					for (int i = 0; i < PixelCount; i++) {
						c.L = i;
						var color = c.LabToRgb();
						color.A = 255;
						Pixels[PixelCount - i - 1] = color;
					}
				}

				protected override void OnSliderValueChange()
				{
					var sliderValue = Math.Round(Slider.Value);
					CurrentLab.L = sliderValue;
					SliderColor.Value = ColorHsva.RgbToHsva(CurrentLab.LabToRgb());
				}
			}

			private class AChanneSlider : LabColorEditorSlider
			{
				private const int PixelCount = 256;

				public AChanneSlider(Property<ColorHsva> color) :
					base(color, name: "A", min: -128, max: 128, PixelCount, format: "0.#") { }

				protected override void OnColorValueChange()
				{
					base.OnColorValueChange();
					var c = new ColorLab(CurrentLab.L, CurrentLab.A, CurrentLab.B, CurrentLab.Alpha);
					Slider.Value = (int)c.A;
					for (int i = 0; i < PixelCount; i++) {
						c.A = i - 127;
						Pixels[PixelCount - i - 1] = c.LabToRgb();
						Pixels[PixelCount - i - 1].A = 255;
					}
				}

				protected override void OnSliderValueChange()
				{
					var sliderValue = Math.Round(Slider.Value);
					CurrentLab.A = sliderValue;
					SliderColor.Value = ColorHsva.RgbToHsva(CurrentLab.LabToRgb());
				}
			}

			private class BChanneSlider : LabColorEditorSlider
			{
				private const int PixelCount = 256;

				public BChanneSlider(Property<ColorHsva> color) :
					base(color, name: "B", min: -128, max: 128, PixelCount, format: "0.#") { }

				protected override void OnColorValueChange()
				{
					base.OnColorValueChange();
					var c = new ColorLab(CurrentLab.L, CurrentLab.A, CurrentLab.B, CurrentLab.Alpha);
					Slider.Value = (int)c.B;
					for (int i = 0; i < PixelCount; i++) {
						c.B = i - 127;
						Pixels[PixelCount - i - 1] = c.LabToRgb();
						Pixels[PixelCount - i - 1].A = 255;
					}
				}

				protected override void OnSliderValueChange()
				{
					var sliderValue = Math.Round(Slider.Value);
					CurrentLab.B = sliderValue;
					SliderColor.Value = ColorHsva.RgbToHsva(CurrentLab.LabToRgb());
				}
			}
		}

		private class ColorEditor : Widget
		{
			protected ColorEditor()
			{
				Layout = new VBoxLayout { Spacing = 4 };
				Padding = new Thickness(left: 0, right: 0, top: 4, bottom: 4);
				Presenter = new WidgetFlatFillPresenter(ColorTheme.Current.Inspector.StripeBackground2) {
					IgnorePadding = true
				};
			}
		}

		private struct ColorHsva
		{
			public float H;
			public float S;
			public float V;
			public float A;

			public ColorHsva(float hue, float saturation, float value, float alpha = 1)
			{
				H = hue;
				S = saturation;
				V = value;
				A = alpha;
			}

			public static ColorHsva RgbToHsva(Color4 rgb)
			{
				var c = new ColorHsva();
				int max = Math.Max(rgb.R, Math.Max(rgb.G, rgb.B));
				int min = Math.Min(rgb.R, Math.Min(rgb.G, rgb.B));
				c.H = GetHue(rgb);
				c.S = max == 0 ? 0 : 1f - 1f * min / max;
				c.V = max / 255f;
				c.A = rgb.A / 255f;
				return c;
			}

			public Color4 HsvaToRgb()
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
				float diff = maxval - minval;
				float rnorm = (maxval - r) / diff;
				float gnorm = (maxval - g) / diff;
				float bnorm = (maxval - b) / diff;
				float hue = 0.0f;
				if (r == maxval) {
					hue = 60.0f * (6.0f + bnorm - gnorm);
				}
				if (g == maxval) {
					hue = 60.0f * (2.0f + rnorm - bnorm);
				}
				if (b == maxval) {
					hue = 60.0f * (4.0f + gnorm - rnorm);
				}
				if (hue >= 360.0f) {
					hue -= 360.0f;
				}
				return hue;
			}

			public bool IsEqual(ColorHsva rv) =>
				Math.Abs(H - rv.H) < Mathf.ZeroTolerance &&
				Math.Abs(S - rv.S) < Mathf.ZeroTolerance &&
				Math.Abs(V - rv.V) < Mathf.ZeroTolerance &&
				Math.Abs(A - rv.A) < Mathf.ZeroTolerance;
		}

		private readonly struct ColorXyz
		{
			public readonly double X;
			public readonly double Y;
			public readonly double Z;
			public readonly float alpha;

			public ColorXyz(double X, double Y, double Z, float alpha)
			{
				this.X = X;
				this.Y = Y;
				this.Z = Z;
				this.alpha = alpha;
			}
		};

		private class ColorLab
		{
			// Bradford-adapted D50 white point
			private static readonly (double X, double Y, double Z) whitePoint = (0.964212, 1, 0.825188);
			private const double kE = 216.0 / 24389.0;
			private const double kK = 24389.0 / 27.0;
			private const double kKE = 8.0;

			public double L;
			public double A;
			public double B;
			public float Alpha;

			public ColorLab(double L, double A, double B, float alpha)
			{
				this.L = L;
				this.A = A;
				this.B = B;
				Alpha = alpha;
			}

			public static ColorLab RgbToLab(Color4 rgb) => XyzToLab(RgbToXyz(rgb));

			private static ColorXyz RgbToXyz(Color4 rgb)
			{
				var r = InverseCompand(rgb.R / 255.0);
				var g = InverseCompand(rgb.G / 255.0);
				var b = InverseCompand(rgb.B / 255.0);
				// Constants for D50 white point
				return new ColorXyz(
					X: r * 0.4360747 + g * 0.3850649 + b * 0.1430804,
					Y: r * 0.2225045 + g * 0.7168786 + b * 0.0606169,
					Z: r * 0.0139322 + g * 0.0971045 + b * 0.7141733,
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

			private static ColorLab XyzToLab(ColorXyz xyz)
			{
				double y = ConvertXyzToLab(xyz.Y / whitePoint.Y);
				double x = ConvertXyzToLab(xyz.X / whitePoint.X);
				double z = ConvertXyzToLab(xyz.Z / whitePoint.Z);
				var l = 116.0 * y - 16.0;
				var a = 500.0 * (x - y);
				var b = 200.0 * (y - z);
				return new ColorLab(Math.Round(l), Math.Round(a), Math.Round(b), xyz.alpha);

				double ConvertXyzToLab(double colorPart) =>
					colorPart > kE ? Math.Pow(colorPart, 1.0 / 3.0) : (kK * colorPart + 16.0) / 116.0;
			}

			public Color4 LabToRgb() => XyzToRgb(LabToXyz());

			private ColorXyz LabToXyz()
			{
				double y = (L + 16.0) / 116.0;
				double x = A * 0.002 + y;
				double z = y - B * 0.005;
				x = (Math.Pow(x, 3) > kE) ? Math.Pow(x, 3) : (116.0 * x - 16.0) / kK;
				y = (L > kKE) ? Math.Pow((L + 16.0) / 116.0, 3.0) : (L / kK);
				z = (Math.Pow(z, 3) > kE) ? Math.Pow(z, 3) : (116.0 * z - 16.0) / kK;
				return new ColorXyz(x * whitePoint.X, y * whitePoint.Y, z * whitePoint.Z, Alpha);
			}

			private static Color4 XyzToRgb(ColorXyz xyz)
			{
				// Constants for D50 white point
				double r = Compand(xyz.X * 3.1338561 + xyz.Y * -1.6168667 + xyz.Z * -0.4906146);
				double g = Compand(xyz.X * -0.9787684 + xyz.Y * 1.9161415 + xyz.Z * 0.0334540);
				double b = Compand(xyz.X * 0.0719453 + xyz.Y * -0.2289914 + xyz.Z * 1.4052427);
				return new Color4(
					(byte)Math.Round(r * 255.0),
					(byte)Math.Round(g * 255.0),
					(byte)Math.Round(b * 255.0),
					(byte)xyz.alpha
				);

				double Compand(double colorPart)
				{
					var sign = 1.0;
					if (colorPart < 0.0) {
						sign = -1.0;
						colorPart = -colorPart;
					}
					var companded = sign * (colorPart <= 0.0031308 ?
						colorPart * 12.92 : 1.055 * Math.Pow(colorPart, 1.0 / 2.4) - 0.055);
					return companded < 0 ? 0 : companded * 255 > 255 ? 1 : companded;
				}
			}
		}
	}
}
