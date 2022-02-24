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
			var rail = new Spline { Id = "Rail" };
			rail.AddNode(new SplinePoint { Position = new Vector2(0, 0.5f) });
			rail.AddNode(new SplinePoint { Position = new Vector2(1, 0.5f) });
			AddNode(rail);
			rail.ExpandToContainerWithAnchors();
			var thumb = new Widget {
				Id = "Thumb",
				Size = new Vector2(8, 16),
				Pivot = Vector2.Half,
			};
			AddNode(thumb);
			MinSize = new Vector2(30, 16);
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
				ro.Size = widget.Size;
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
					RendererNvg.DrawCircle(Size / 2, Size.Y / 2 - 1, BorderColor, 2);
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
				ro.Size = widget.Size;
				ro.Color = Theme.Colors.WhiteBackground;
				ro.BorderColor = Theme.Colors.ControlBorder;
				return ro;
			}

			public bool PartialHitTest(Node node, ref HitTestArgs args) => false;

			private class RenderObject : WidgetRenderObject
			{
				public Vector2 Size;
				public Color4 Color;
				public Color4 BorderColor;

				public override void Render()
				{
					PrepareRenderState();
					RendererNvg.DrawRoundedRectWithBorder(Vector2.Zero, Size, Color, BorderColor, 1, 4);
				}
			}
		}
	}
}
#endif
