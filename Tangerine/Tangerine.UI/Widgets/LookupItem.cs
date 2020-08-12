using System;
using Lime;

namespace Tangerine.UI
{
	public class LookupItem
	{
		protected const string HighlightedTextStyleId = "b";

		public readonly Widget Widget;
		public readonly RichTextHighlightComponent Name;
		public readonly RichText NameRichText;
		public readonly Action Action;

		public bool IsSelected { get; set; }

		public LookupItem(LookupWidget owner, string text, Action action)
		{
			Action = action;

			Widget = new Frame {
				Nodes = {
					(NameRichText = new RichText {
						Text = text,
						Padding = new Thickness(left: 5.0f),
						MinHeight = 18.0f,
						Localizable = false,
						Color = Color4.White,
						HAlignment = HAlignment.Left,
						VAlignment = VAlignment.Top,
						OverflowMode = TextOverflowMode.Ellipsis,
						TrimWhitespaces = true,
						Nodes = {
							new TextStyle {
								Size = Theme.Metrics.TextHeight,
								TextColor = Theme.Colors.GrayText,
							},
							new TextStyle {
								Id = HighlightedTextStyleId,
								Size = Theme.Metrics.TextHeight,
								TextColor = Theme.Colors.BlackText,
								Bold = true,
							},
						},
						Components = {
							(Name = new RichTextHighlightComponent(text, HighlightedTextStyleId))
						}
					})
				},
				Layout = new HBoxLayout(),
				Clicked = () => owner.Submit(this),
				HitTestTarget = true,
				Padding = new Thickness(horizontal: 0, vertical: 2),
			};
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
				if (widget.GloballyEnabled) {
					var isMouseOverThisOrDescendant = widget.IsMouseOverThisOrDescendant();
					if (item.IsSelected || isMouseOverThisOrDescendant) {
						var ro = RenderObjectPool<RenderObject>.Acquire();
						ro.CaptureRenderState(widget);
						ro.Size = widget.Size;
						ro.SelectedColor = Theme.Colors.WhiteBackground;
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
