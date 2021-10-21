using Lime;

namespace Tangerine.UI.Widgets.ConflictingAnimators
{
	public sealed class ThemedCaption : RichText
	{
		public readonly TextStyle BoldStyle;
		public readonly TextStyle RegularStyle;
		public readonly TextStyle PropertyStyle;

		public float ExtraWidth { get; set; }

		public ThemedCaption(string text = null, float extraWidth = 0.0f)
		{
			LayoutCell = new LayoutCell(Alignment.LeftCenter);
			Padding = new Thickness(left: 5.0f);
			MinMaxHeight = Theme.Metrics.TextHeight;
			Localizable = false;
			Color = Color4.White;
			HAlignment = HAlignment.Left;
			VAlignment = VAlignment.Center;
			OverflowMode = TextOverflowMode.Ellipsis;
			TrimWhitespaces = true;
			Text = text;
			ExtraWidth = extraWidth;
			AddNode(BoldStyle = TextStylePool.Get(TextStyleIdentifiers.Bold));
			AddNode(RegularStyle = TextStylePool.Get(TextStyleIdentifiers.Regular));
			AddNode(PropertyStyle = TextStylePool.Get(TextStyleIdentifiers.PropertyColor));
			SetDefaultStyle(RegularStyle);
		}

		public TextStyle GetDefaultStyle() => Nodes[0] as TextStyle;

		public void SetDefaultStyle(TextStyle style)
		{
			if (Nodes.Contains(style)) {
				style.Unlink();
			}
			PushNode(style);
			AdjustWidthToText();
		}

		public void AdjustWidthToText()
		{
			Width = 1024.0f;
			MinMaxWidth = Width = MeasureText().Width + ExtraWidth;
		}

		public static string Stylize(string text, TextStyleIdentifier id) => $"<{id}>{text}</{id}>";
	}
}
