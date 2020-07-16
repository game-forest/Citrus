using System;
using Lime;

namespace Tangerine.UI
{
	public interface ITextHighlightDataSource
	{
		int[] HighlightSymbolsIndices { get; }
		Color4 HighlightColor { get; }
	}

	public class SimpleTextHighlightPresenter : IPresenter
	{
		private readonly ITextHighlightDataSource dataSource;

		public SimpleTextHighlightPresenter(ITextHighlightDataSource dataSource)
		{
			this.dataSource = dataSource;
		}

		public Lime.RenderObject GetRenderObject(Node node)
		{
			var indices = dataSource.HighlightSymbolsIndices;
			if (indices == null || indices.Length == 0) {
				return null;
			}
			var simpleText = (SimpleText)node;
			if (!simpleText.GloballyEnabled) {
				return null;
			}
			var ro = RenderObjectPool<RenderObject>.Acquire();
			ro.CaptureRenderState(simpleText);
			if (ro.Highlights == null || ro.Highlights.Length < indices.Length) {
				var ceilingToPow2 = Math.Pow(2, Math.Ceiling(Math.Log(indices.Length) / Math.Log(2))).Round();
				var size = Math.Max(ceilingToPow2, 2);
				ro.Highlights = new Rectangle[size];
			}
			ro.HighlightsCount = indices.Length;
			ro.HighlightColor = dataSource.HighlightColor;
			var cursor = 0;
			var dx = simpleText.Padding.Left;
			for (var i = 0; i < indices.Length; i++) {
				var index = indices[i];
				var v0 = simpleText.Font.MeasureTextLine(simpleText.Text, simpleText.FontHeight, cursor, index - cursor, simpleText.LetterSpacing);
				v0.Y = simpleText.Padding.Top;
				var v1 = simpleText.Font.MeasureTextLine(simpleText.Text, simpleText.FontHeight, index, 1, simpleText.LetterSpacing);
				v0.X += dx;
				v1.X += v0.X;
				v1.Y += v0.Y;
				dx = v1.X;
				ro.Highlights[i] = new Rectangle(v0, v1);
				cursor = index + 1;
			}
			return ro;
		}

		public bool PartialHitTest(Node node, ref HitTestArgs args) => false;

		private class RenderObject : WidgetRenderObject
		{
			public Rectangle[] Highlights;
			public int HighlightsCount;
			public Color4 HighlightColor;

			public override void Render()
			{
				PrepareRenderState();
				for (var i = 0; i < HighlightsCount; i++) {
					var highlight = Highlights[i];
					Renderer.DrawRect(highlight.A, highlight.B, HighlightColor);
				}
			}
		}
	}
}
