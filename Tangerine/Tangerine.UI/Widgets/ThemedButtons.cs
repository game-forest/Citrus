using Lime;

namespace Tangerine.UI
{
	public class ThemedExpandButton : ToolbarButton
	{
		private bool expanded;
		public bool Expanded
		{
			get
			{
				return expanded;
			}

			set
			{
				if (expanded != value) {
					expanded = value;
					Texture = IconPool.GetTexture(value ? "Tools.Unfolded" : "Tools.Folded");
				}
			}
		}

		public ThemedExpandButton() : base(IconPool.GetTexture("Tools.Folded"))
		{
			Highlightable = false;
			Clicked += () => { Expanded = !Expanded; };
		}
	}

	public class ThemedDeleteButton : Button
	{
		public override bool IsNotDecorated() => false;

		public ThemedDeleteButton()
		{
			var presenter = new VectorShapeButtonPresenter(new VectorShape {
				new VectorShape.Line(0.3f, 0.5f, 0.7f, 0.5f, Color4.White, 0.075f * 1.5f),
			});
			LayoutCell = new LayoutCell(Alignment.Center, stretchX: 0);
			PostPresenter = presenter;
			MinMaxSize = Theme.Metrics.CloseButtonSize;
			DefaultAnimation.AnimationEngine = new AnimationEngineDelegate {
				OnRunAnimation = (animation, markerId, animationTimeCorrection) => {
					presenter.SetState(markerId);
					return true;
				}
			};
		}
	}

	public class ThemedAddButton : Button
	{
		public override bool IsNotDecorated() => false;

		public ThemedAddButton()
		{
			var presenter = new VectorShapeButtonPresenter(new VectorShape {
				new VectorShape.Line(0.5f, 0.25f, 0.5f, 0.75f, Color4.White, 0.075f * 1.5f),
				new VectorShape.Line(0.25f, 0.5f, 0.75f, 0.5f, Color4.White, 0.075f * 1.5f),
			});
			LayoutCell = new LayoutCell(Alignment.Center, stretchX: 0);
			Presenter = presenter;
			MinMaxSize = Theme.Metrics.CloseButtonSize;
			DefaultAnimation.AnimationEngine = new AnimationEngineDelegate {
				OnRunAnimation = (animation, markerId, animationTimeCorrection) => {
					presenter.SetState(markerId);
					return true;
				}
			};
		}
	}

	public class IconRedChannelToColorButton : Button
	{
		private readonly RedChannelToColorMaterial material;

		private Vector4 defaultColor;

		public Color4 DefaultColor { set => defaultColor = value.ToVector4(); }

		private Vector4 hoverColor;

		public Color4 HoverColor { set => hoverColor = value.ToVector4(); }

		public override bool IsNotDecorated() => false;

		public IconRedChannelToColorButton(string icon, Vector2 size, Thickness padding)
		{
			MinMaxSize = size;
			LayoutCell = new LayoutCell(Alignment.Center);
			defaultColor = ColorTheme.Current.IsDark ?
				Color4.White.Darken(0.2f).ToVector4() :
				Color4.Black.Lighten(0.2f).ToVector4();
			hoverColor = ColorTheme.Current.IsDark ?
				Color4.White.ToVector4() :
				Color4.Black.ToVector4();
			material = new RedChannelToColorMaterial {
				Color = defaultColor
			};
			AddNode(new Image(IconPool.GetTexture(icon)) {
				MinMaxSize = size,
				Size = size,
				Padding = padding,
				Material = material,
			});
			Updating += delta => {
				var newColor = IsMouseOver() ? hoverColor : defaultColor;
				if (material.Color != newColor) {
					Window.Current.Invalidate();
				}
				material.Color = newColor;
			};
		}
	}
}
