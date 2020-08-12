using System;
using Lime;
using Tangerine.UI;

namespace Tangerine
{
	public class LookupDialogItem : LookupItem
	{
		private readonly Widget headerWidget;
		private readonly Image iconImage;
		private readonly ShortcutPresenter shortcutPresenter = new ShortcutPresenter {
			Color = Theme.Colors.WhiteBackground,
			Margin = new Thickness(horizontal: 3, vertical: 0),
		};

		public readonly RichTextHighlightComponent Header;
		public readonly RichText HeaderRichText;

		public ITexture IconTexture
		{
			get => iconImage.Texture;
			set
			{
				iconImage.Texture = value;
				iconImage.Visible = iconImage.Texture != null;
			}
		}

		public LookupDialogItem(LookupWidget owner, string headerText, string text, Action action) : base(owner, text, action)
		{
			Widget.Layout = new VBoxLayout();

			const float IconSize = 16;
			const float IconRightPadding = 5;
			Widget.Nodes.Push(headerWidget = new Widget {
				Layout = new HBoxLayout(),
				Padding = new Thickness(left: 5),
				Nodes = {
					(iconImage = new Image {
						LayoutCell = new LayoutCell {
							Stretch = Vector2.Zero,
							Alignment = new Alignment { X = HAlignment.Center, Y = VAlignment.Center }
						},
						Padding = new Thickness(right: IconRightPadding),
						MinMaxSize = new Vector2(IconSize + IconRightPadding, IconSize),
						Visible = false,
					}),
					(HeaderRichText = new RichText {
						Text = headerText,
						Padding = new Thickness(bottom: 5),
						MinHeight = 23f,
						Localizable = false,
						Color = Color4.White,
						HAlignment = HAlignment.Left,
						VAlignment = VAlignment.Top,
						OverflowMode = TextOverflowMode.Ellipsis,
						TrimWhitespaces = true,
						Nodes = {
							new TextStyle {
								Size = Theme.Metrics.TextHeight * 1.25f,
								TextColor = Theme.Colors.GrayText,
							},
							new TextStyle {
								Id = HighlightedTextStyleId,
								Size = Theme.Metrics.TextHeight * 1.25f,
								TextColor = Theme.Colors.BlackText,
								Bold = true,
							},
						},
						Components = {
							(Header = new RichTextHighlightComponent(headerText, HighlightedTextStyleId))
						}
					}),
				}
			});

			if (string.IsNullOrEmpty(NameRichText.Text)) {
				NameRichText.Visible = false;
			}
		}

		public LookupDialogItem(LookupWidget owner, string headerText, string text, Shortcut shortcut, Action action) : this(owner, headerText, text, action)
		{
			if (shortcut.Main != Key.Unknown) {
				const float Spacing = 9;
				var widget = new Widget {
					LayoutCell = new LayoutCell {
						Stretch = Vector2.Zero,
						Alignment = new Alignment { X = HAlignment.Right, Y = VAlignment.Center }
					},
					Layout = new HBoxLayout { Spacing = Spacing },
					Padding = new Thickness(left: 10, right: 15, top: 3),
					MinMaxHeight = 23.0f,
				};
				var width = 0f;
				headerWidget.AddNode(widget);

				void AddKey(string name)
				{
					var simpleText = new ThemedSimpleText {
						Text = name,
						ForceUncutText = false,
						MinMaxHeight = 18.0f,
					};
					var v = simpleText.Font.MeasureTextLine(simpleText.Text, simpleText.FontHeight, simpleText.LetterSpacing);
					simpleText.MinMaxWidth = v.X + simpleText.Padding.Left + simpleText.Padding.Right;
					width += simpleText.MaxWidth;
					simpleText.CompoundPresenter.Add(shortcutPresenter);
					widget.AddNode(simpleText);
				}

				if (shortcut.Modifiers.HasFlag(Modifiers.Alt)) AddKey("Alt");
				if (shortcut.Modifiers.HasFlag(Modifiers.Shift)) AddKey("Shift");
				if (shortcut.Modifiers.HasFlag(Modifiers.Control)) AddKey("Ctrl");
#if MAC
				if (shortcut.Modifiers.HasFlag(Modifiers.Command)) AddKey("Cmd");
#else
				if (shortcut.Modifiers.HasFlag(Modifiers.Win)) AddKey("Win");
#endif
				AddKey(shortcut.Main.ToString());

				widget.MinMaxWidth = width + Spacing * (widget.Nodes.Count - 1);
			}
		}

		private class ShortcutPresenter : IPresenter
		{
			public Color4 Color { get; set; }
			public Thickness Margin { get; set; }

			public Lime.RenderObject GetRenderObject(Node node)
			{
				var widget = (Widget)node;
				var ro = RenderObjectPool<RenderObject>.Acquire();
				ro.CaptureRenderState(widget);
				ro.Color = Color;
				ro.Position = -Margin.LeftTop;
				ro.Size = widget.Size + Margin.LeftTop + Margin.RightBottom;
				return ro;
			}

			public bool PartialHitTest(Node node, ref HitTestArgs args) => false;

			private class RenderObject : WidgetRenderObject
			{
				public Vector2 Position;
				public Vector2 Size;
				public Color4 Color;

				public override void Render()
				{
					PrepareRenderState();
					Renderer.DrawRect(Position, Position + Size, Color);
				}
			}
		}
	}
}
