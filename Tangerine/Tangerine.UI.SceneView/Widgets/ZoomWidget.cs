using System.Collections.Generic;
using Lime;
using Tangerine.Core;

namespace Tangerine.UI.SceneView
{
	public class ZoomWidget : ThemedFrame
	{
		public const float FrameHeight = 24;

		public static readonly List<float> zoomTable = new List<float> {
			0.001f, 0.0025f, 0.005f, 0.01f, 0.025f, 0.05f, 0.10f,
			0.15f, 0.25f, 0.5f, 0.75f, 1f, 1.5f, 2f, 3f,
			4f, 5f, 6f, 7f, 8f, 9f, 10f, 11f,
			12f, 13f, 14f, 15f, 16f,
		};
		private readonly Slider slider;

		private static SceneView SceneView => SceneView.Instance;
		private float CurrentSliderValue => zoomTable[(int)slider.Value.Clamp(0, zoomTable.Count - 1)];

		public ZoomWidget()
		{
			MinMaxHeight = FrameHeight;
			LayoutCell = new LayoutCell(new Alignment { X = HAlignment.Center, Y = VAlignment.Bottom }, 1, 0);
			Layout = new HBoxLayout { Spacing = 8 };
			Padding = new Thickness(10, 0);
			Anchors = Anchors.LeftRight | Anchors.Bottom;
			HitTestTarget = true;

			slider = new ThemedSlider {
				MinMaxSize = new Vector2(100, 2),
				Size = new Vector2(100, 2),
				Y = FrameHeight / 2,
				LayoutCell = new LayoutCell(Alignment.RightCenter, 1),
				Anchors = Anchors.Right,
				RangeMin = 0,
				RangeMax = zoomTable.Count - 1,
				Step = 1,
			};
			slider.CompoundPresenter.Add(
				new SliderCenterPresenter(FindNearest(1f, 0, zoomTable.Count), zoomTable.Count)
			);

			var zoomInButton = new ToolbarButton {
				MinMaxSize = new Vector2(FrameHeight),
				Size = new Vector2(FrameHeight),
				LayoutCell = new LayoutCell(Alignment.RightCenter),
				Anchors = Anchors.Right,
				Clicked = () => {
					if (CurrentSliderValue <= SceneView.Scene.Scale.X) {
						slider.Value = (slider.Value + slider.Step).Clamp(slider.RangeMin, slider.RangeMax);
					}
					Zoom(CurrentSliderValue);
				},
				Texture = IconPool.GetTexture("SceneView.ZoomIn"),
			};
			var zoomOutButton = new ToolbarButton {
				MinMaxSize = new Vector2(FrameHeight),
				Size = new Vector2(FrameHeight),
				LayoutCell = new LayoutCell(Alignment.RightCenter),
				Anchors = Anchors.Right,
				Clicked = () => {
					if (CurrentSliderValue >= SceneView.Scene.Scale.X) {
						slider.Value = (slider.Value - slider.Step).Clamp(slider.RangeMin, slider.RangeMax);
					}
					Zoom(CurrentSliderValue);
				},
				Texture = IconPool.GetTexture("SceneView.ZoomOut"),
			};

			var zoomEditor = new ThemedEditBox {
				LayoutCell = new LayoutCell(Alignment.RightCenter),
				Anchors = Anchors.Right,
				MinMaxWidth = 50,
			};
			zoomEditor.Submitted += value => {
				var success = float.TryParse(value.TrimEnd('%'), out var zoom) && zoomTable.Count > 0;
				if (success) {
					Zoom(Mathf.Clamp(zoom / 100, zoomTable[0], zoomTable[zoomTable.Count - 1]));
				}
			};

			this.AddChangeWatcher(() => SceneView.Scene.Scale.X, value => {
				var index = FindNearest(value, 0, zoomTable.Count);
				slider.Value = index;
				zoomEditor.Text = $"{value * 100f}%";
			});
			slider.Changed += () => Zoom(CurrentSliderValue);
			AddNode(new Widget { LayoutCell = new LayoutCell(Alignment.LeftCenter, 1) });
			AddNode(zoomEditor);
			AddNode(zoomOutButton);
			AddNode(slider);
			AddNode(zoomInButton);
		}

		private static void Zoom(float newZoom)
		{
			var prevZoom = SceneView.Scene.Scale.X;
			var p = (SceneView.Frame.Size / 2 - SceneView.Scene.Position) / SceneView.Scene.Scale.X;
			SceneView.Scene.Scale = newZoom * Vector2.One;
			SceneView.Scene.Position -= p * (newZoom - prevZoom);
		}

		public static int FindNearest(float x, int left, int right)
		{
			while (true) {
				if (right - left == 1) {
					return left;
				}
				var idx = left + (right - left) / 2;
				if (x < zoomTable[idx]) {
					right = idx;
					continue;
				}
				left = idx;
			}
		}

		private class SliderCenterPresenter : SyncCustomPresenter
		{
			private readonly int middleIndex;
			private readonly int partsCount;

			public SliderCenterPresenter(int middleIndex, int parts)
			{
				this.middleIndex = middleIndex;
				partsCount = parts;
			}

			public override void Render(Node node)
			{
				var widget = node.AsWidget;
				widget.PrepareRendererState();
				var xPos = widget.Width / partsCount * middleIndex;
				Renderer.DrawRect(new Vector2(xPos - 1f, -5), new Vector2(xPos + 1f, 7), Theme.Colors.ControlBorder);
			}
		}
	}
}
