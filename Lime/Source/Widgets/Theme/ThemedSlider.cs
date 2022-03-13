#if !ANDROID && !iOS
using System;
using Lime.NanoVG;

namespace Lime
{
	[YuzuDontGenerateDeserializer]
	public class ThemedSlider : Slider
	{
		public override bool IsNotDecorated() => false;

		public ThemedSlider()
		{
			var rail = new Spline { Id = "Rail", Padding = new Thickness(Theme.Metrics.SliderThumbWidth / 2, 0) };
			rail.AddNode(new SplinePoint { Position = new Vector2(0, 0.5f) });
			rail.AddNode(new SplinePoint { Position = new Vector2(1, 0.5f) });
			AddNode(rail);
			rail.ExpandToContainerWithAnchors();
			var thumb = new Widget {
				Id = "Thumb",
				Size = new Vector2(Theme.Metrics.SliderThumbWidth),
				Pivot = Vector2.Half,
			};
			AddNode(thumb);
			MinSize = new Vector2(30 + Theme.Metrics.SliderThumbWidth, Theme.Metrics.SliderThumbWidth);
			thumb.CompoundPresenter.Add(new SliderThumbPresenter());
			CompoundPresenter.Add(new SliderPresenter());
		}

		private class SliderThumbPresenter : IPresenter
		{
			public Lime.RenderObject GetRenderObject(Node node)
			{
				var widget = (Widget)node;
				var ro = RenderObjectPool<RenderObject>.Acquire();
				ro.CaptureRenderState(widget);
				ro.Size = widget.ContentSize;
				ro.Gradient = Theme.Colors.ButtonDefault;
				ro.BorderColor = Theme.Colors.ControlBorder;
				return ro;
			}

			public bool PartialHitTest(Node node, ref HitTestArgs args) => false;

			private class RenderObject : WidgetRenderObject
			{
				public Vector2 Size;
				public ColorGradient Gradient;
				public Color4 BorderColor;

				public override void Render()
				{
					PrepareRenderState();
					var fillPaint = Paint.LinearGradient(0, 0,  0, Size.Y, Gradient[0].Color, Gradient[1].Color);
					RendererNvg.DrawRound(Size / 2, Size.Y / 2 - 1, fillPaint);
					RendererNvg.DrawCircle(Size / 2, Size.Y / 2 - 1, BorderColor, 1);
				}
			}
		}

		private class SliderPresenter : IPresenter
		{
			public Lime.RenderObject GetRenderObject(Node node)
			{
				var widget = (Widget)node;
				var ro = RenderObjectPool<RenderObject>.Acquire();
				ro.CaptureRenderState(widget);
				ro.Position = widget.ContentPosition + new Vector2(Theme.Metrics.SliderThumbWidth / 2, 0);
				ro.Size = widget.ContentSize - new Vector2(Theme.Metrics.SliderThumbWidth, 0);
				ro.Color = Theme.Colors.WhiteBackground;
				ro.BorderColor = Theme.Colors.ControlBorder;
				return ro;
			}

			public bool PartialHitTest(Node node, ref HitTestArgs args) => false;

			private class RenderObject : WidgetRenderObject
			{
				public Vector2 Position;
				public Vector2 Size;
				public Color4 Color;
				public Color4 BorderColor;

				public override void Render()
				{
					PrepareRenderState();
					RendererNvg.DrawRoundedRectWithBorder(Position, Position + Size, Color, BorderColor, 1, 4);
				}
			}
		}
	}
}
#endif
