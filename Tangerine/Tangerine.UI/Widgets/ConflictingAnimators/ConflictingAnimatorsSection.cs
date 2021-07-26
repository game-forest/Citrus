using Lime;
using System.Collections.Generic;

namespace Tangerine.UI
{
	public class ConflictingAnimatorsSection : Widget
	{
		protected const float IconSize = 16;
		protected const float IconRightPadding = 5;
		private static readonly ITexture IconTexture = IconPool.GetIcon("Lookup.SceneFileIcon").AsTexture;

		protected Widget header;
		protected Widget content;
		protected ThemedExpandButton expandButton;

		public readonly string Name;
		public readonly ThemedScrollView Container;
		public readonly List<ConflictingAnimatorsItem> Items = new List<ConflictingAnimatorsItem>();

		public ConflictingAnimatorsSection(string name)
		{
			Name = name;

			Layout = new VBoxLayout { Spacing = 4 };
			foreach (var widget in CreateContent()) {
				AddNode(widget);
			}
		}

		public IEnumerable<Widget> CreateContent()
		{
			header = new Widget {
				Layout = new HBoxLayout { Spacing = 2 },
				Padding = new Thickness(2),
				Nodes = {
					(expandButton = new ThemedExpandButton()),
					new Image {
						LayoutCell = new LayoutCell {
							Stretch = Vector2.Zero,
							Alignment = new Alignment { X = HAlignment.Center, Y = VAlignment.Center }
						},
						Padding = new Thickness(right: IconRightPadding),
						MinMaxSize = new Vector2(IconSize + IconRightPadding, IconSize),
						Texture = IconTexture,
					},
					new RichText {
						Text = Name,
						Padding = new Thickness(left: 5.0f),
						MinHeight = Theme.Metrics.TextHeight,
						Localizable = false,
						Color = Color4.White,
						HAlignment = HAlignment.Left,
						VAlignment = VAlignment.Center,
						OverflowMode = TextOverflowMode.Ellipsis,
						TrimWhitespaces = true,
						Nodes = {
							new TextStyle {
								Size = Theme.Metrics.TextHeight,
								TextColor = Theme.Colors.BlackText,
							}
						},
					},
				},
			};
			expandButton.Clicked += OnExpandButtonClicked;
			yield return header;
			content = new Frame {
				Layout = new VBoxLayout { Spacing = 16 },
				Padding = new Thickness(8),
				Visible = expandButton.Expanded,
			};
			yield return content;
		}

		private void OnExpandButtonClicked() => content.Visible = expandButton.Expanded;

		public void AddItem(ConflictingAnimatorsItem item)
		{
			Items.Add(item);
			content.AddNode(item);
		}
	}
}
