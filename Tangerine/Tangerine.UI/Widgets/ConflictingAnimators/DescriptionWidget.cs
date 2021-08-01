using Lime;

namespace Tangerine.UI.Widgets.ConflictingAnimators
{
	public class DescriptionWidget : Widget
	{
		public const float IconSize = 16.0f;
		public const float IconRightPadding = 5.0f;

		public readonly Image Icon;
		public readonly ThemedCaption Caption;

		public DescriptionWidget(string text, ITexture iconTexture)
		{
			Layout = new HBoxLayout { Spacing = 2 };
			Padding = new Thickness(2);

			AddNode(Icon = CreateIcon(iconTexture));
			AddNode(Caption = new ThemedCaption(text));
		}

		private Image CreateIcon(ITexture iconTexture)
		{
			return new Image {
				LayoutCell = new LayoutCell {
					Stretch = Vector2.Zero,
					Alignment = new Alignment {
						X = HAlignment.Center,
						Y = VAlignment.Center
					},
				},
				Padding = new Thickness(right: IconRightPadding),
				MinMaxSize = new Vector2(IconSize + IconRightPadding, IconSize),
				Texture = iconTexture,
			};
		}
	}
}
