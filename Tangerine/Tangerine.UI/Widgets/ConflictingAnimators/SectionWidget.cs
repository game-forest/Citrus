using Lime;

namespace Tangerine.UI.Widgets.ConflictingAnimators
{
	public class SectionWidget : Widget
	{
		public readonly ThemedExpandButton ExpandButton;
		public readonly DescriptionWidget Description;
		public readonly Frame Content;

		private Spacer Margin => Spacer.HSpacer(
			DescriptionWidget.IconSize +
			DescriptionWidget.IconRightPadding +
			ExpandButton?.Width ?? 0.0f +
			ExpandButton?.Padding.Right ?? 0.0f
		);

		public SectionWidget(string text, ITexture iconTexture)
		{
			Layout = new VBoxLayout { Spacing = 4 };

			ExpandButton = new ThemedExpandButton();
			Description = new DescriptionWidget(text, iconTexture);
			ExpandButton.Clicked += OnExpandButtonClicked;
			Description.Caption.RegularStyle.TextColor = Theme.Colors.BlackText;

			AddNode(CreateHeader());
			AddNode(Content = CreateContent());
		}

		public void AddItem(SectionItemWidget sectionItem)
		{
			Content.AddNode(new Widget {
				Layout = new HBoxLayout(),
				Nodes = {
					Margin,
					sectionItem,
				},
			});
		}

		private Frame CreateHeader()
		{
			return new Frame {
				Layout = new HBoxLayout { Spacing = 2 },
				Padding = new Thickness(2),
				Nodes = {
					ExpandButton,
					Description,
				}
			};
		}
		private Frame CreateContent()
		{
			return new Frame {
				Layout = new VBoxLayout { Spacing = 16 },
				Padding = new Thickness(8),
				Visible = ExpandButton.Expanded,
			};
		}

		private void OnExpandButtonClicked() => Content.Visible = ExpandButton.Expanded;
	}
}
