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
		private ColorGradient defaultGradientClone;
		private ColorGradient greenRedWhiteGradientClone;
		private ColorGradient greenYellowRedGradientClone;

		public OverdrawController()
		{
			defaultGradientClone = GetDefaultPreset();
			greenRedWhiteGradientClone = GetGreenRedWhitePreset();
			greenYellowRedGradientClone = GetGreenYellowRedPreset();
			int overdrawColorCount = OverdrawShaderProgram.StateCount;
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
			var gradientGroup = new Widget {
				Layout = new VBoxLayout(),
				Presenter = new WidgetFlatFillPresenter(Theme.Colors.ControlBorder),
				Nodes = {
					new Widget {
						Layout = new VBoxLayout { Spacing = 4 },
						Padding = new Thickness(16),
						Nodes = {
							new ThemedSimpleText("Current gradient control point"),
							new Widget {
								Layout = new HBoxLayout(),
								Nodes = {
									UnclampWidgetSize(new ThemedButton("Overdraw [0-255]") { Enabled = false }),
									UnclampWidgetSize(positionInput = new ThemedNumericEditBox()),
								}
							},
							new Widget {
								Layout = new HBoxLayout(),
								Nodes = {
									UnclampWidgetSize(new ThemedButton("Color") { Enabled = false }),
									UnclampWidgetSize(colorInput = new ThemedEditBox())
								}
							}
						}
					}
				}
			};
			var presetsGroup = new Widget {
				Layout = new VBoxLayout(),
				Presenter = new WidgetFlatFillPresenter(Theme.Colors.ControlBorder),
				Nodes = {
					new Widget {
						Layout = new VBoxLayout { Spacing = 4 },
						Padding = new Thickness(16),
						Nodes = {
							new ThemedSimpleText("Gradient presets"),
							new Widget {
								Layout = new HBoxLayout { Spacing = 4 },
								Nodes = {
									UnclampWidgetSize(new ThemedButton("Default") {
										Clicked = () => SetPalette(defaultGradientClone) }),
									new ThemedButton("Reset") {
										Clicked = () => SetPalette(defaultGradientClone = GetDefaultPreset())
									}
								}
							},
							new Widget {
								Layout = new HBoxLayout { Spacing = 4 },
								Nodes = {
									UnclampWidgetSize(new ThemedButton("Green-Red-White") {
										Clicked = () => SetPalette(greenRedWhiteGradientClone) }),
									new ThemedButton("Reset") {
										Clicked = () => SetPalette(greenRedWhiteGradientClone = GetGreenRedWhitePreset())
									}
								}
							},
							new Widget {
								Layout = new HBoxLayout { Spacing = 4 },
								Nodes = {
									UnclampWidgetSize(new ThemedButton("Green-Yellow-Red") {
										Clicked = () => SetPalette(greenYellowRedGradientClone) }),
									new ThemedButton("Reset") {
										Clicked = () => SetPalette(greenYellowRedGradientClone = GetGreenYellowRedPreset())
									}
								}
							}
						}
					}
				}
			};
			AddNode(gradientControlWidget);
			colorPickerPanel = new ColorPickerPanel();
			var toggleOverdrawButton = UnclampWidgetSize(new ThemedButton());
			toggleOverdrawButton.Clicked = () => Overdraw.Enabled = !Overdraw.Enabled;
			toggleOverdrawButton.Updating = (d) => toggleOverdrawButton.Text =
				(Overdraw.EnabledAtUpdateThread ? "Disable Overdraw Mode" : "Enable Overdraw Mode");
			AddNode(new Widget {
				Layout = new HBoxLayout { Spacing = 4 },
				Padding = new Thickness(8),
				Nodes = {
					new Widget {
						Layout = new VBoxLayout { Spacing = 8 },
						Nodes = {
							gradientGroup,
							presetsGroup,
							toggleOverdrawButton,
						}
					},
					colorPickerPanel.Widget
				}
			});
			void NoPointSelected()
			{
				positionInput.Text = "No point selected!";
				colorInput.Text = "No point selected!";
				colorPickerPanel.Color = Color4.Gray;
			}
			gradientControlWidget.SelectionChanged += (point) => {
				currentPoint = point;
				colorPickerPanel.Color = point.Color;
				colorInput.Text = point.Color.ToString(Color4.StringPresentation.Dec);
				positionInput.Text = ((int)(point.Position * (overdrawColorCount - 1))).ToString();
			};
			void UpdateGradient() => OverdrawInterpreter.Gradient = gradientControlWidget.Gradient;
			gradientControlWidget.ControlPointCreated += (f, i) => UpdateGradient();
			gradientControlWidget.ControlPointRemoved += (i) => UpdateGradient();
			gradientControlWidget.ControlPointPositionChanged += (f,i) => UpdateGradient();
			positionInput.Step = 1;
			positionInput.Submitted += (text) => {
				if (currentPoint != null) {
					if (int.TryParse(text, out int value)) {
						int clampedValue = Mathf.Clamp(value, 0, overdrawColorCount - 1);
						if (value != clampedValue) {
							positionInput.Text = clampedValue.ToString();
						}
						currentPoint.Position = clampedValue / (float)(overdrawColorCount - 1);
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
			AddNode(new Widget());
			SetPalette(GetDefaultPreset());
		}

		private void SetPalette(ColorGradient gradient)
		{
			gradientControlWidget.Gradient = gradient;
			colorPickerPanel.Color = gradient.Last().Color;
			OverdrawInterpreter.Gradient = gradient;
		}

		private static ColorGradient GetDefaultPreset() => OverdrawInterpreter.DefaultGradient.Clone();

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
