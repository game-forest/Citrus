using System;
using Lime;

namespace Tangerine.UI
{
	public class LookupItem : ITextHighlightDataSource
	{
		public readonly Widget Widget;
		public readonly string Text;
		public readonly Action Action;

		public bool Selected { get; set; }
		public int[] HighlightSymbolsIndices { get; set; }
		public Color4 HighlightColor => Theme.Colors.TextSelection;

		public LookupItem(LookupWidget owner, string text, Action action)
		{
			Text = text;
			Action = action;

			ThemedSimpleText textWidget;
			Widget = new Frame {
				Nodes = {
					(textWidget = new ThemedSimpleText {
						Text = text,
						ForceUncutText = false,
						Padding = new Thickness(left: 5.0f),
						MinHeight = 20.0f,
					})
				},
				Layout = new HBoxLayout(),
				Clicked = () => owner.Submit(this),
				HitTestTarget = true,
				Padding = new Thickness(10),
			};
			textWidget.CompoundPresenter.Add(new SimpleTextHighlightPresenter(this));
			Widget.CompoundPresenter.Add(new ItemPresenter(this));
			Widget.Tasks.Add(Theme.MouseHoverInvalidationTask(Widget));
		}

		private class ItemPresenter : IPresenter
		{
			private readonly LookupItem item;

			public ItemPresenter(LookupItem item)
			{
				this.item = item;
			}

			public Lime.RenderObject GetRenderObject(Node node)
			{
				var widget = (Widget)node;
				if (widget.GloballyEnabled && (widget.IsMouseOverThisOrDescendant() || item.Selected)) {
					var ro = RenderObjectPool<RenderObject>.Acquire();
					ro.CaptureRenderState(widget);
					ro.Size = widget.Size;
					ro.Color = Theme.Colors.KeyboardFocusBorder;
					return ro;
				}
				return null;
			}

			public bool PartialHitTest(Node node, ref HitTestArgs args) => false;

			private class RenderObject : WidgetRenderObject
			{
				public Vector2 Size;
				public Color4 Color;

				public override void Render()
				{
					PrepareRenderState();
					Renderer.DrawRect(Vector2.Zero, Size, Color);
				}
			}
		}
	}
}
