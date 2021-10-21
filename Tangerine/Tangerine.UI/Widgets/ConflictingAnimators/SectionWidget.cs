using Lime;

namespace Tangerine.UI.Widgets.ConflictingAnimators
{
	public class SectionWidget : Widget
	{
		public readonly ThemedExpandButton ExpandButton;
		public readonly DescriptionWidget Description;

		private readonly Widget content;

		private Spacer Margin => Spacer.HSpacer(
			DescriptionWidget.IconSize +
			DescriptionWidget.IconRightPadding +
			ExpandButton.Width +
			ExpandButton.Padding.Right
		);

		public SectionWidget(string text, ITexture iconTexture)
		{
			Layout = new VBoxLayout { Spacing = 0 };
			ExpandButton = new ThemedExpandButton() {
				Padding = new Thickness(bottom: 4)
			};
			ExpandButton.Clicked += () => content.Visible = ExpandButton.Expanded;
			Description = new DescriptionWidget(text, iconTexture);
			Description.Caption.RegularStyle.TextColor = Theme.Colors.BlackText;
			AddNode(CreateHeader());
			AddNode(content = CreateContent());
		}

		public void AddItem(SectionItemWidget sectionItem)
		{
			content.AddNode(new Widget {
				Layout = new HBoxLayout(),
				Nodes = {
					Margin,
					sectionItem,
				},
			});
		}

		private Widget CreateHeader()
		{
			return new Widget {
				Layout = new HBoxLayout { Spacing = 2 },
				Padding = new Thickness(2),
				Nodes = {
					ExpandButton,
					Description,
				}
			};
		}
		private Widget CreateContent()
		{
			return new Widget {
				Layout = new VBoxLayout { Spacing = 8 },
				Padding = new Thickness(bottom: 6),
				Visible = ExpandButton.Expanded,
			};
		}
	}
}
