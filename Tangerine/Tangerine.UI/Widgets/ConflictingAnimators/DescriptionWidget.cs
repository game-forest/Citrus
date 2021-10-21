using Lime;

namespace Tangerine.UI.Widgets.ConflictingAnimators
{
	public class DescriptionWidget : Widget
	{
		public const float IconSize = 16.0f;
		public const float IconRightPadding = 5.0f;

		public readonly ThemedCaption Caption;

		public DescriptionWidget(string text, ITexture iconTexture)
		{
			Layout = new HBoxLayout { Spacing = 2 };
			Padding = new Thickness(horizontal: 2, vertical: 0);
			AddNode(new Image {
				LayoutCell = new LayoutCell {
					Stretch = Vector2.Zero,
					Alignment = Alignment.Center
				},
				Padding = new Thickness(right: IconRightPadding),
				MinMaxSize = new Vector2(IconSize + IconRightPadding, IconSize),
				Texture = iconTexture,
			});
			AddNode(Caption = new ThemedCaption(text));
		}
	}
}
