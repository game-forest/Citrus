using System;
using Lime;
using Tangerine.UI;

namespace Tangerine
{
	public class LookupDialogItem : LookupItem
	{
		private static readonly ShortcutPresenter shortcutPresenter = new ShortcutPresenter {
			Color = Theme.Colors.WhiteBackground,
			Margin = new Thickness(horizontal: 3, vertical: 0),
		};

		private readonly ITexture iconTexture;
		private readonly Shortcut shortcut;
		private readonly string label;
		private Widget headerWidget;

		public readonly RichTextHighlightComponent Header;
		public RichText HeaderRichText { get; private set; }

		public LookupDialogItem(string headerText, string text, Action action) : base(text, action)
		{
			Header = new RichTextHighlightComponent(headerText, HighlightedTextStyleId);
		}

		public LookupDialogItem(string headerText, string text, string label, Action action) : this(headerText, text, action)
		{
			this.label = label;
		}
		
		public LookupDialogItem(string headerText, string text, Shortcut shortcut, Action action) : this(headerText, text, action)
		{
			this.shortcut = shortcut;
		}

		public LookupDialogItem(string headerText, string text, ITexture iconTexture, Action action) : this(headerText, text, action)
		{
			this.iconTexture = iconTexture;
		}

		public LookupDialogItem(string headerText, string text, Shortcut shortcut, ITexture iconTexture, Action action) : this(headerText, text, action)
		{
			this.shortcut = shortcut;
			this.iconTexture = iconTexture;
		}

		public override void CreateVisuals()
		{
			if (Widget != null) {
				return;
			}

			base.CreateVisuals();

			const float IconSize = 16;
			const float IconRightPadding = 5;
			Widget.Layout = new VBoxLayout();
			Widget.Nodes.Push(headerWidget = new Widget {
				Layout = new HBoxLayout(),
				Padding = new Thickness(left: 5),
				Nodes = {
					new Image {
						LayoutCell = new LayoutCell {
							Stretch = Vector2.Zero,
							Alignment = new Alignment { X = HAlignment.Center, Y = VAlignment.Center }
						},
						Padding = new Thickness(right: IconRightPadding),
						MinMaxSize = new Vector2(IconSize + IconRightPadding, IconSize),
						Texture = iconTexture,
						Visible = iconTexture != null,
					},
					(HeaderRichText = new RichText {
						Text = Header.Text,
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
						Components = { Header },
					}),
				}
			});

			if (string.IsNullOrEmpty(NameRichText.Text)) {
				NameRichText.Visible = false;
			}

			if (!string.IsNullOrEmpty(label) || shortcut.Main != Key.Unknown) {
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
				
				void AddLabel(string name)
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
				if (!string.IsNullOrEmpty(label)) {
					AddLabel(label);
				}
				if (shortcut.Main != Key.Unknown) {
					if (shortcut.Modifiers.HasFlag(Modifiers.Alt)) AddLabel("Alt");
					if (shortcut.Modifiers.HasFlag(Modifiers.Shift)) AddLabel("Shift");
					if (shortcut.Modifiers.HasFlag(Modifiers.Control)) AddLabel("Ctrl");
#if MAC
					if (shortcut.Modifiers.HasFlag(Modifiers.Command)) AddKey("Cmd");
#else
					if (shortcut.Modifiers.HasFlag(Modifiers.Win)) AddLabel("Win");
#endif
					AddLabel(shortcut.Main.ToString());
				}
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
