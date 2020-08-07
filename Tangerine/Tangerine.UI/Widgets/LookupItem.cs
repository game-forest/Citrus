using System;
using Lime;

namespace Tangerine.UI
{
	public class LookupItem
	{
		public readonly Widget Widget;
		public readonly HighlightedText Name;
		public readonly ThemedSimpleText NameSimpleText;
		public readonly Action Action;

		public bool IsSelected { get; set; }

		public LookupItem(LookupWidget owner, string text, Action action)
		{
			Name = new HighlightedText(text);
			Action = action;

			Widget = new Frame {
				Nodes = {
					(NameSimpleText = new ThemedSimpleText {
						Text = text,
						ForceUncutText = false,
						Padding = new Thickness(left: 5.0f),
						MinHeight = 18.0f,
					})
				},
				Layout = new HBoxLayout(),
				Clicked = () => owner.Submit(this),
				HitTestTarget = true,
				Padding = new Thickness(horizontal: 0, vertical: 2),
			};
			NameSimpleText.CompoundPresenter.Add(new SimpleTextHighlightPresenter(Name));
			Widget.CompoundPresenter.Add(new ItemPresenter(this));
			Widget.Tasks.Add(Theme.MouseHoverInvalidationTask(Widget));
		}

		public class HighlightedText : ITextHighlightDataSource
		{
			public string Text { get; set; }
			public int[] HighlightSymbolsIndices { get; set; }
			public Color4 HighlightColor => Theme.Colors.TextSelection;

			public HighlightedText() { }

			public HighlightedText(string text)
			{
				Text = text;
			}
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
				if (widget.GloballyEnabled) {
					var isMouseOverThisOrDescendant = widget.IsMouseOverThisOrDescendant();
					if (item.IsSelected || isMouseOverThisOrDescendant) {
						var ro = RenderObjectPool<RenderObject>.Acquire();
						ro.CaptureRenderState(widget);
						ro.Size = widget.Size;
						ro.SelectedColor = Theme.Colors.SelectedBackground;
						ro.HoveredColor = Theme.Colors.SelectedBorder;
						ro.IsSelected = item.IsSelected;
						ro.IsHovered = isMouseOverThisOrDescendant;
						return ro;
					}
				}
				return null;
			}

			public bool PartialHitTest(Node node, ref HitTestArgs args) => false;

			private class RenderObject : WidgetRenderObject
			{
				public bool IsSelected;
				public bool IsHovered;
				public Vector2 Size;
				public Color4 SelectedColor;
				public Color4 HoveredColor;

				public override void Render()
				{
					PrepareRenderState();
					if (IsSelected) {
						Renderer.DrawRect(Vector2.Zero, Size, SelectedColor);
					}
					if (IsHovered) {
						Renderer.DrawRectOutline(Vector2.Zero, Size, HoveredColor);
					}
				}
			}
		}
	}
}
