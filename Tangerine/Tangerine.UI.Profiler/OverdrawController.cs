#if PROFILER
using System.Linq;
using Lime;
using Lime.Profiler.Graphics;

namespace Tangerine.UI
{
	internal class OverdrawController : Widget
	{
		private readonly GradientControlWidget gradientControlWidget;
		private readonly ColorPickerPanel colorPickerPanel;
		private readonly ThemedNumericEditBox positionInput;
		private readonly ThemedEditBox colorInput;

		private GradientControlPoint currentPoint;


		public OverdrawController()
		{
			int overdrawColorsCount = OverdrawShaderProgram.StatesCount;
			Layout = new VBoxLayout();
			gradientControlWidget = new GradientControlWidget() {
				Anchors = Anchors.LeftRight,
				Padding = new Thickness(8),
				MinMaxHeight = 64
			};
			Widget UnclampWidgetSize(Widget widget)
			{
				widget.MinWidth = 0;
				widget.MaxWidth = 100000;
				return widget;
			}
			AddNode(new Widget {
				Layout = new HBoxLayout { Spacing = 4 },
				Padding = new Thickness(8, 8, 8, 0),
				Nodes = {
					UnclampWidgetSize(new ThemedButton("Default Preset") {
						Clicked = () => SetPalette(GetDefaultPreset()) }),
					UnclampWidgetSize(new ThemedButton("Green-Red-White Preset") {
						Clicked = () => SetPalette(GetGreenRedWhitePreset()) }),
					UnclampWidgetSize(new ThemedButton("Green-Yellow-Red Preset") {
						Clicked = () => SetPalette(GetGreenYellowRedPreset()) })
				}
			});
			AddNode(gradientControlWidget);
			colorPickerPanel = new ColorPickerPanel();
			AddNode(new Widget {
				Layout = new HBoxLayout { Spacing = 4 },
				Padding = new Thickness(8),
				Nodes = {
					UnclampWidgetSize(new ThemedButton("Overdraw [0-255]") { Enabled = false }),
					UnclampWidgetSize(positionInput = new ThemedNumericEditBox()),
					UnclampWidgetSize(new ThemedButton("Color") { Enabled = false }),
					UnclampWidgetSize(colorInput = new ThemedEditBox()),
					UnclampWidgetSize(new ThemedButton("ColorPicker") { Clicked = () =>
						colorPickerPanel.Widget.Visible = !colorPickerPanel.Widget.Visible })
				}
			});
			void NoPointSelected()
			{
				positionInput.Text = "No point selected!";
				colorInput.Text = "No point selected!";
			}
			gradientControlWidget.SelectionChanged += (point) => {
				currentPoint = point;
				colorInput.Text = point.Color.ToString(Color4.StringPresentation.Dec);
				positionInput.Text = ((int)(point.Position * (overdrawColorsCount - 1))).ToString();
			};
			gradientControlWidget.ControlPointPositionChanged += (f,i) =>
				OverdrawInterpreter.Gradient = gradientControlWidget.Gradient;
			positionInput.Step = 1;
			positionInput.Submitted += (text) => {
				if (currentPoint != null) {
					if (int.TryParse(text, out int value)) {
						int clampedValue = Mathf.Clamp(value, 0, overdrawColorsCount - 1);
						if (value != clampedValue) {
							positionInput.Text = clampedValue.ToString();
						}
						currentPoint.Position = clampedValue / (float)(overdrawColorsCount - 1);
						OverdrawInterpreter.Gradient = gradientControlWidget.Gradient;
					} else {
						positionInput.Text = "Must be integer!";
					}
				} else {
					NoPointSelected();
				}
			};
			colorPickerPanel.Changed += () => {
				if (currentPoint != null) {
					currentPoint.Color = colorPickerPanel.Color;
					colorInput.Text = colorPickerPanel.Color.ToString(Color4.StringPresentation.Dec);
					OverdrawInterpreter.Gradient = gradientControlWidget.Gradient;
				} else {
					NoPointSelected();
				}
			};
			colorInput.Submitted += (text) => {
				if (currentPoint != null) {
					if (Color4.TryParse(text, out var color)) {
						colorPickerPanel.Color = color;
						currentPoint.Color = color;
						OverdrawInterpreter.Gradient = gradientControlWidget.Gradient;
					} else {
						colorInput.Text = "Wrong Format!";
					}
				} else {
					NoPointSelected();
				}
			};
			AddNode(colorPickerPanel.Widget);
			AddNode(new Widget());
			SetPalette(GetDefaultPreset());
		}

		private void SetPalette(ColorGradient gradient)
		{
			gradientControlWidget.Gradient = gradient;
			colorPickerPanel.Color = gradient.Last().Color;
			OverdrawInterpreter.Gradient = gradient;
		}

		private static ColorGradient GetDefaultPreset() => OverdrawInterpreter.DefaultGradient;

		private static ColorGradient GetGreenRedWhitePreset()
		{
			var gradient = new ColorGradient();
			gradient.Add(new GradientControlPoint(new Color4(0, 178, 102, 255), 0f / 256f));
			gradient.Add(new GradientControlPoint(new Color4(178, 0, 0, 255), 16f / 256f));
			gradient.Add(new GradientControlPoint(new Color4(255, 255, 255, 255), 64f / 256f));
			return gradient;
		}

		private static ColorGradient GetGreenYellowRedPreset()
		{
			var gradient = new ColorGradient();
			gradient.Add(new GradientControlPoint(new Color4(0, 178, 102, 255), 0f / 256f));
			gradient.Add(new GradientControlPoint(new Color4(204, 204, 0, 255), 16f / 256f));
			gradient.Add(new GradientControlPoint(new Color4(178, 0, 0, 255), 64f / 256f));
			return gradient;
		}
	}
}
#endif // PROFILER
