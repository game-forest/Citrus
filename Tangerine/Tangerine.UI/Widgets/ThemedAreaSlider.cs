using System;
using Lime;
using Tangerine.Core;

namespace Tangerine.UI
{
	public class ThemedAreaSlider : Widget
	{
		private readonly ThemedSlider slider;

		/// <summary>
		/// Label in front of the slider.
		/// </summary>
		public readonly ThemedSimpleText Label;

		/// <summary>
		/// EditBox to set specific value.
		/// </summary>
		public readonly ThemedNumericEditBox Editor;

		private readonly string labelFormat;

		private float value;

		/// <summary>
		/// The current value of the slider.
		/// May be outside of the range specified when creating the slider.
		/// </summary>
		public float Value
		{
			get { return value; }
			set {
				if (this.value != value) {
					slider.Value = this.value = value;
					UpdateTextWidgets();
				}
			}
		}

		/// <summary>
		/// Returns the min value that can be set using the slider bar.
		/// </summary>
		public float RangeMin => slider.RangeMin;

		/// <summary>
		/// Returns the max value that can be set using the slider bar.
		/// </summary>
		public float RangeMax => slider.RangeMax;

		/// <summary>
		/// Used to set text to bypass the Value.
		/// Changing the Value property will overwrite the text.
		/// </summary>
		public string LabelText
		{
			set => Editor.Text = Label.Text = value;
		}

		/// <summary>
		/// Selected range color.
		/// </summary>
		public Color4 SelectedRangeColor { get; set; } = Theme.Colors.SelectedBackground;

		/// <summary>
		/// Called when changing Value.
		/// </summary>
		public Action Changed { get; set; }

		public event Action DragStarted
		{
			add => slider.DragStarted += value;
			remove => slider.DragStarted -= value;
		}

		public event Action DragEnded
		{
			add => slider.DragEnded += value;
			remove => slider.DragEnded -= value;
		}

		public ThemedAreaSlider(Vector2 range, string labelFormat = "0.###")
		{
			if (range.X >= range.Y) {
				throw new InvalidOperationException("Slider RangeMin >= RangeMax.");
			}
			this.labelFormat = labelFormat;
			Label = new ThemedSimpleText {
				HitTestTarget = false,
				HAlignment = HAlignment.Left,
				VAlignment = VAlignment.Bottom,
				Anchors = Anchors.LeftRightTopBottom,
				Padding = new Thickness { Left = 4, Top = 2 }
			};
			Editor = new ThemedNumericEditBox {
				Visible = false,
				HitTestTarget = true,
				MaxWidth = int.MaxValue
			};
			slider = new ThemedSlider {
				RangeMin = range.X,
				RangeMax = range.Y,
			};
			slider.Thumb.HitTestTarget = false;
			slider.Thumb.Size = new Vector2(1, slider.Thumb.Size.Y);
			var rightClickGesture = new ClickGesture(buttonIndex: 1);
			slider.Changed += () => {
				Value = slider.Value;
				Changed?.Invoke();
			};
			Editor.FreezeInvisible = false;
			Editor.Submitted += (text) => {
				if (float.TryParse(Editor.Text, out float value)) {
					Value = value;
					Changed?.Invoke();
				}
			};
			Updating += (delta) => {
				void EditorSetFocus()
				{
					Editor.SetFocus();
					Editor.Text = Value.ToString();
				}
				if (IsFocused()) {
					var focusScope = KeyboardFocusScope.GetEnclosingScope(this);
					if (focusScope == null) {
						Debug.Write("Error in ThemedAreaSlider: focusScope == null.");
					} else {
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
				Label.Visible = !(Editor.Visible = Editor.IsFocused());
			};
			MinSize = slider.MinSize;
			Size = new Vector2(100000, slider.Size.Y);
			Anchors = Anchors.LeftRight;
			Layout = new StackLayout();
			TabTravesable = new TabTraversable();
			AddNode(Editor);
			AddNode(Label);
			AddNode(slider);
			Editor.ExpandToContainerWithAnchors();
			Label.ExpandToContainerWithAnchors();
			Gestures.Add(rightClickGesture);
			slider.CompoundPresenter.Insert(0, new SliderPresenter());
			LateTasks.Add(Theme.MouseHoverInvalidationTask(this));
			UpdateTextWidgets();
		}

		private void UpdateTextWidgets()
		{
			Label.Text = value.ToString(labelFormat);
			Editor.Text = value.ToString();
		}

		public override bool IsNotDecorated() => false;

		private class SliderPresenter : IPresenter
		{
			public bool PartialHitTest(Node node, ref HitTestArgs args) => false;

			public Lime.RenderObject GetRenderObject(Node node)
			{
				var slider = (Slider)node;
				var container = (ThemedAreaSlider)node.Parent;
				var ro = RenderObjectPool<RenderObject>.Acquire();
				ro.CaptureRenderState(slider);
				ro.RangeMin = slider.RangeMin;
				ro.RangeMax = slider.RangeMax;
				ro.Value = slider.Value;
				ro.WidgetSize = (Size)container.Size;
				ro.RangeColor = container.SelectedRangeColor;
				ro.ShowSelectionRect = container.IsFocused() || slider.IsMouseOverThisOrDescendant();
				return ro;
			}

			private class RenderObject : WidgetRenderObject
			{
				public float RangeMin;
				public float RangeMax;
				public float Value;
				public Size WidgetSize;
				public Color4 RangeColor;
				public bool ShowSelectionRect;

				public override void Render()
				{
					float sliderRange = RangeMax - RangeMin;
					float thumbPosition = (Value - RangeMin) / sliderRange * WidgetSize.Width;
					if (thumbPosition > 1) {
						RendererWrapper.Current.DrawRect(
							Vector2.One, new Vector2(thumbPosition, WidgetSize.Height - 1), RangeColor);
					}
					if (ShowSelectionRect) {
						var rectColor = Theme.Colors.KeyboardFocusBorder;
						Renderer.DrawRectOutline(Vector2.Zero, (Vector2)WidgetSize, rectColor, thickness: 1);
					}
				}
			}
		}
	}
}
