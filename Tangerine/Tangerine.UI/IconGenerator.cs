using System;
using System.Collections.Generic;
using System.Linq;
using Lime;

namespace Tangerine.UI
{
	public static class IconTextureGenerator
	{
		private const int IconSize = 32;
		private const float BorderSize = IconSize * 0.09375f;
		private const float FontHeight = 48;
		private const int IconTextMaxLength = 2;
		private static readonly string[] ignoringTypeNameSuffixes = {
			"Component",
			"Behavior",
		};
		private static readonly IReadOnlyList<(Color4 Common, Color4 Secondary)> colors = new List<(Color4, Color4)> {
			(Color4.Black, new Color4(241, 230, 13)),
			(new Color4(51, 99, 172), new Color4(241, 230, 13)),
			(new Color4(55, 10, 50), new Color4(244, 150, 25)),
			(new Color4(241, 230, 13), Color4.Black),
			(Color4.White, new Color4(128, 0, 128)),
			(new Color4(128, 0, 128), Color4.White),
			(new Color4(38, 41, 52), new Color4(181, 197, 136)),
			(new Color4(255, 255, 243), new Color4(3, 127, 140)),
			(new Color4(217, 215, 167), new Color4(38, 79, 115)),
			(new Color4(208, 233, 242), new Color4(89, 85, 25)),
			(new Color4(115, 57, 57), new Color4(217, 192, 145)),
			(new Color4(25, 0, 9), new Color4(255, 102, 102)),
			(new Color4(4, 3, 23), new Color4(19, 165, 209)),
			(new Color4(7, 70, 76), new Color4(234, 214, 186)),
			(new Color4(150, 243, 168), new Color4(0, 79, 168)),
			(new Color4(91, 217, 189), new Color4(34, 52, 64)),
			(new Color4(175, 185, 191), new Color4(70, 32, 34)),
			(new Color4(68, 251, 187), new Color4(62, 50, 89)),
			(new Color4(68, 204, 251), new Color4(38, 49, 64)),
			(new Color4(2, 66, 90), new Color4(215, 217, 216)),
			(new Color4(254, 155, 118), new Color4(34, 35, 63)),
			(new Color4(13, 44, 64), new Color4(178, 191, 75)),
			(new Color4(187, 205, 220), new Color4(89, 45, 7)),
		};

		private static readonly IFont font;
		private static readonly Dictionary<Type, RenderTexture> map = new Dictionary<Type, RenderTexture>();

		static IconTextureGenerator()
		{
			var fontResource = new EmbeddedResource("Tangerine.Resources.SpicyRice-Regular.ttf", "Tangerine");
			font = new DynamicFont(fontResource.GetResourceBytes());
		}

		public static ITexture GetTexture(Type type)
		{
			if (map.TryGetValue(type, out var texture)) {
				return texture;
			}

			map[type] = texture = new RenderTexture(IconSize, IconSize);
			var typeName = type.Name;
			foreach (var ignoringSuffix in ignoringTypeNameSuffixes) {
				if (typeName.EndsWith(ignoringSuffix, StringComparison.InvariantCultureIgnoreCase)) {
					typeName = typeName.Substring(0, typeName.Length - ignoringSuffix.Length);
				}
			}
			var abbreviation = typeName
				.Where(@char => char.IsUpper(@char) || char.IsNumber(@char))
				.Aggregate(string.Empty, (s, @char) => s + @char);
			var iconText = abbreviation.Length > 0 ? abbreviation : typeName;
			iconText = iconText.Substring(0, Math.Min(IconTextMaxLength, iconText.Length)).ToUpperInvariant();
			var (commonColor, secondaryColor) = colors[Math.Abs(typeName.GetHashCode()) % colors.Count];
			Render(() => {
				Renderer.PushState(
					RenderState.World |
					RenderState.View |
					RenderState.DepthState |
					RenderState.ScissorState |
					RenderState.CullMode |
					RenderState.Blending |
					RenderState.Transform2 |
					RenderState.Transform1
				);
				texture.SetAsRenderTarget();
				try {
					Renderer.ScissorState = ScissorState.ScissorDisabled;
					Renderer.World = Renderer.View = Matrix44.Identity;
					Renderer.DepthState = DepthState.DepthDisabled;
					Renderer.CullMode = CullMode.None;
					Renderer.Blending = Blending.Alpha;
					Renderer.Transform2 = Matrix32.Identity;
					Renderer.Transform1 = Matrix32.Identity;
					Renderer.Clear(ClearOptions.All, Color4.Zero);

					Renderer.DrawRect(Vector2.One * 1, Vector2.One * IconSize, secondaryColor);
					Renderer.DrawRectOutline(Vector2.One * 1, Vector2.One * IconSize, commonColor, BorderSize);

					var hBorderOffset = Vector2.Right * (IconSize * 0.0625f + BorderSize * 2);
					var textScale = CalcBestTextScale(iconText, FontHeight, Vector2.One * IconSize - hBorderOffset);
					var fontHeight = FontHeight * textScale;
					var textSize = font.MeasureTextLine(iconText, fontHeight, letterSpacing: 0);
					var textPosition = (Vector2.One * IconSize - textSize) * 0.5f;
					Renderer.DrawTextLine(font, textPosition, iconText, fontHeight, commonColor, letterSpacing: 0);
				} finally {
					texture.RestoreRenderTarget();
					Renderer.PopState();
				}
			});
			return texture;

			float CalcBestTextScale(string text, float fontHeight, Vector2 fitSize)
			{
				var minScale = 0.05f;
				var maxScale = 1.0f;
				var scale = maxScale;
				var bestScale = minScale;
				while (maxScale - minScale >= 0.05f) {
					var size = font.MeasureTextLine(text, fontHeight * scale, letterSpacing: 0);
					var fit = size.X < fitSize.X && size.Y < fitSize.Y;
					if (fit) {
						minScale = scale;
						bestScale = Mathf.Max(bestScale, scale);
					} else {
						maxScale = scale;
					}
					scale = (minScale + maxScale) / 2;
				}
				return bestScale;
			}
		}

		private static void Render(Action render)
		{
			var cp = WidgetContext.Current.Root.CompoundPresenter;
			cp.Add(new SingleUsePresenter(cp, render));
			Window.Current.Invalidate();
		}

		private class SingleUsePresenter : IPresenter
		{
			private readonly CompoundPresenter compoundPresenter;
			private readonly Action renderAction;
			private volatile bool wasRendered;

			public SingleUsePresenter(CompoundPresenter compoundPresenter, Action renderAction)
			{
				this.compoundPresenter = compoundPresenter;
				this.renderAction = renderAction;
			}

			public Lime.RenderObject GetRenderObject(Node node)
			{
				if (wasRendered) {
					compoundPresenter.Remove(this);
					return null;
				}
				var ro = RenderObjectPool<RenderObject>.Acquire();
				ro.RenderAction = () => {
					if (!wasRendered) {
						renderAction?.Invoke();
						wasRendered = true;
					}
				};
				return ro;
			}

			public bool PartialHitTest(Node node, ref HitTestArgs args) => false;

			private class RenderObject : Lime.RenderObject
			{
				public Action RenderAction;

				public override void Render() => RenderAction.Invoke();

				protected override void OnRelease() => RenderAction = null;
			}
		}
	}
}
