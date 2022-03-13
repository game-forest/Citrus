using System;
using Lime;
using Tangerine.Core;

namespace Tangerine.UI
{
	public class ThemedAreaSlider : Slider
	{
		private readonly ThemedSimpleText label;
		private readonly ClickGesture rightClickGesture;

		/// <summary>
		/// EditBox to set specific value.
		/// </summary>
		public readonly ThemedNumericEditBox Editor;

		/// <summary>
		/// Specifies the numeric format for the value display.
		/// </summary>
		public string NumericFormat { get; set; } = "0.###";

		/// <summary>
		/// Used to override the text on the slider.
		/// Null if the default numeric display is required.
		/// </summary>
		public string LabelText { get; set; }

		public ThemedAreaSlider()
		{
			var rail = new Spline { Id = "Rail" };
			rail.AddNode(new SplinePoint { Position = new Vector2(0, 0.5f) });
			rail.AddNode(new SplinePoint { Position = new Vector2(1, 0.5f) });
			AddNode(rail);
			var thumb = new Widget {
				Id = "Thumb",
				LayoutCell = new LayoutCell { Ignore = true },
				Size = Vector2.Zero,
			};
			this.AddChangeWatcher(() => (Value, LabelText), _ => UpdateTextWidgets());
			AddNode(thumb);
			rail.ExpandToContainerWithAnchors();
			label = new ThemedSimpleText {
				HitTestTarget = false,
				HAlignment = HAlignment.Left,
				VAlignment = VAlignment.Bottom,
				Anchors = Anchors.LeftRightTopBottom,
				Padding = new Thickness { Left = 4, Top = 2 },
			};
			Editor = new ThemedNumericEditBox {
				Visible = false,
				HitTestTarget = true,
				MaxWidth = int.MaxValue,
			};
			rightClickGesture = new ClickGesture(buttonIndex: 1);
			Editor.FreezeInvisible = false;
			Editor.Submitted += (text) => {
				if (float.TryParse(Editor.Text, out float value)) {
					Value = value;
					RaiseChanged();
				}
			};
			Updating += OnUpdating;
			Anchors = Anchors.LeftRight;
			Layout = new StackLayout();
			TabTravesable = new TabTraversable();
			AddNode(Editor);
			AddNode(label);
			Editor.ExpandToContainerWithAnchors();
			label.ExpandToContainerWithAnchors();
			Gestures.Add(rightClickGesture);
			CompoundPresenter.Add(new SliderPresenter());
			LateTasks.Add(Theme.MouseHoverInvalidationTask(this));
			UpdateTextWidgets();
		}

		private void OnUpdating(float delta)
		{
			if (IsFocused()) {
				var focusScope = KeyboardFocusScope.GetEnclosingScope(this);
				if (focusScope != null) {
					if (focusScope.LastDirection == KeyboardFocusScope.Direction.Forward) {
						EditorSetFocus();
					} else {
						if (Editor.Visible) {
							focusScope.AdvanceFocus(KeyboardFocusScope.Direction.Backward);
						} else {
							EditorSetFocus();
						}
					}
				}
			}
			if (rightClickGesture.WasRecognized()) {
				EditorSetFocus();
			}
			label.Visible = !(Editor.Visible = Editor.IsFocused());

			void EditorSetFocus()
			{
				Editor.SetFocus();
				Editor.Text = Value.ToString();
			}
		}

		private void UpdateTextWidgets()
		{
			label.Text = LabelText ?? Value.ToString(NumericFormat);
			Editor.Text = Value.ToString();
		}

		public override bool IsNotDecorated() => false;

		private class SliderPresenter : IPresenter
		{
			public bool PartialHitTest(Node node, ref HitTestArgs args) => false;

			public Lime.RenderObject GetRenderObject(Node node)
			{
				var slider = (Slider)node;
				var ro = RenderObjectPool<RenderObject>.Acquire();
				ro.CaptureRenderState(slider);
				ro.RangeMin = slider.RangeMin;
				ro.RangeMax = slider.RangeMax;
				ro.Value = slider.Value;
				ro.Position = slider.ContentPosition;
				ro.Size = slider.ContentSize;
				ro.ShowSelectionRect = slider.IsFocused() || slider.IsMouseOverThisOrDescendant();
				return ro;
			}

			private class RenderObject : WidgetRenderObject
			{
				public float RangeMin;
				public float RangeMax;
				public float Value;
				public Vector2 Position;
				public Vector2 Size;
				public bool ShowSelectionRect;

				public override void Render()
				{
					PrepareRenderState();
					var sliderRange = RangeMax - RangeMin;
					var thumbPosition = (Value - RangeMin) / sliderRange * Size.X;
					var nvg = Lime.NanoVG.Context.Instance;
					RendererNvg.DrawRoundedRect(
						Position, Size, Theme.Colors.WhiteBackground, 4);
					nvg.Scissor(Vector2.Half, new Vector2(thumbPosition, Size.Y) - Vector2.One);
					RendererNvg.DrawRoundedRect(
						Position, Size, Theme.Colors.SelectedBackground, 4);
					nvg.ResetScissor();
					var borderColor = ShowSelectionRect ? Theme.Colors.KeyboardFocusBorder : Theme.Colors.ControlBorder;
					RendererNvg.DrawRoundedRectOutline(Position, Size, borderColor, 1, 4);
				}
			}
		}
	}
}
