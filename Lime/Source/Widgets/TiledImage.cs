using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Yuzu;

namespace Lime
{
	[TangerineRegisterNode(Order = 31)]
	[TangerineVisualHintGroup("/All/Nodes/Images")]
	public class TiledImage : Widget
	{
		private bool skipRender;
		private ITexture texture;
		private IMaterial material;

		[YuzuMember]
		[YuzuSerializeIf(nameof(IsNotRenderTexture))]
#if TANGERINE
		[TangerineKeyframeColor(15)]
		[TangerineOnPropertySet(nameof(OnSetTextureViaTangerine))]
		[TangerineTileImageTexture]
#endif // TANGERINE
		public override sealed ITexture Texture
		{
			get { return texture; }
			set {
				if (texture != value) {
					texture = value;
					material = null;
					Window.Current?.Invalidate();
				}
			}
		}

#if TANGERINE
		private void OnSetTextureViaTangerine()
		{
			TileSize = (Vector2)texture.ImageSize;
		}
#endif // TANGERINE

		[YuzuMember]
		[TangerineKeyframeColor(16)]
		[TangerineRatioInfo(typeof(TiledImage))]
        [TangerineSizeInfo(typeof(TiledImage))]
		public Vector2 TileSize { get; set; }

		[YuzuMember]
		[TangerineKeyframeColor(17)]
		public Vector2 TileOffset { get; set; }

		[YuzuMember]
		[TangerineKeyframeColor(18)]
		public bool TileRounding { get; set; }

#if TANGERINE
		[TangerineInspect]
		public bool IsTiledAlongX
		{
			get => TileSize.X != 0.0f;
			set
			{
				if (value) {
					TileSize = new Vector2(savedTileSizeX, TileSize.Y);
				} else {
					savedTileSizeX = TileSize.X;
					TileSize = new Vector2(0.0f, TileSize.Y);
				}
			}
		}

		[TangerineInspect]
		public bool IsTiledAlongY
		{
			get => TileSize.Y != 0.0f;
			set {
				if (value) {
					TileSize = new Vector2(TileSize.X, savedTileSizeY);
				} else {
					savedTileSizeY = TileSize.Y;
					TileSize = new Vector2(TileSize.X, 0.0f);
				}
			}
		}

		private float savedTileSizeX;
		private float savedTileSizeY;
#endif // TANGERINE

		public IMaterial CustomMaterial { get; set; }

		public TiledImage()
		{
			Presenter = DefaultPresenter.Instance;
			TileOffset = Vector2.Zero;
			TileSize = Vector2.Zero;
			HitTestMethod = HitTestMethod.Contents;
			var texture = new SerializableTexture();
			Texture = texture;
		}

		public TiledImage(ITexture texture)
		{
			Presenter = DefaultPresenter.Instance;
			TileOffset = Vector2.Zero;
			TileSize = Vector2.Zero;
			Texture = texture;
			HitTestMethod = HitTestMethod.Contents;
			Size = (Vector2)texture.ImageSize;
		}

		public TiledImage(string texturePath)
			: this(new SerializableTexture(texturePath))
		{
		}

		public override Vector2 CalcContentSize()
		{
			return (Vector2)Texture.ImageSize;
		}

		public override void AddToRenderChain(RenderChain chain)
		{
			if (GloballyVisible && !skipRender && ClipRegionTest(chain.ClipRegion)) {
				AddSelfToRenderChain(chain, Layer);
			}
			skipRender = false;
		}

		internal protected override bool PartialHitTestByContents(ref HitTestArgs args)
		{
			Vector2 localPoint = LocalToWorldTransform.CalcInversed().TransformVector(args.Point);
			Vector2 size = Size;
			if (size.X < 0) {
				localPoint.X = -localPoint.X;
				size.X = -size.X;
			}
			if (size.Y < 0) {
				localPoint.Y = -localPoint.Y;
				size.Y = -size.Y;
			}
			if (localPoint.X >= 0 && localPoint.Y >= 0 && localPoint.X < size.X && localPoint.Y < size.Y) {
				var UV1 = CalcUV1();
				float u = TileOffset.X + (UV1.X - TileOffset.X) * (localPoint.X / size.X);
				float v = TileOffset.Y + (UV1.Y - TileOffset.Y) * (localPoint.Y / size.Y);
				int tu = (int)(Texture.ImageSize.Width * u);
				int tv = (int)(Texture.ImageSize.Height * v);
				return !Texture.IsTransparentPixel(tu, tv);
			} else {
				return false;
			}
		}

		protected internal override Lime.RenderObject GetRenderObject()
		{
			if (material == null || CleanDirtyFlags(DirtyFlags.Material)) {
				material = WidgetMaterial.GetInstance(GlobalBlending, GlobalShader, 1);
			}
			var UV1 = CalcUV1();
			var ro = RenderObjectPool<RenderObject>.Acquire();
			ro.CaptureRenderState(this);
			ro.Texture = Texture;
			ro.Material = CustomMaterial ?? material;
			ro.TileOffset = TileOffset;
			ro.UV1 = UV1;
			ro.Color = GlobalColor;
			ro.Position = ContentPosition;
			ro.Size = ContentSize;
			return ro;
		}

		private Vector2 CalcUV1()
		{
			var UV1 = new Vector2 {
				X = TileSize.X == 0.0f ? 1.0f : Size.X / TileSize.X,
				Y = TileSize.Y == 0.0f ? 1.0f : Size.Y / TileSize.Y
			};
			if (TileRounding) {
				UV1.X = (float)Math.Round(UV1.X);
				UV1.Y = (float)Math.Round(UV1.Y);
			}
			UV1 += TileOffset;
			return UV1;
		}

		public bool IsNotRenderTexture()
		{
			return !(texture is RenderTexture);
		}

		private class RenderObject : WidgetRenderObject
		{
			public ITexture Texture;
			public IMaterial Material;
			public Vector2 TileOffset;
			public Vector2 UV1;
			public Color4 Color;
			public Vector2 Position;
			public Vector2 Size;

			public override void Render()
			{
				PrepareRenderState();
				Renderer.DrawSprite(Texture, null, Material, Color, Position, Size, TileOffset, UV1, Vector2.Zero, Vector2.Zero);
			}

			protected override void OnRelease()
			{
				Texture = null;
				Material = null;
			}
		}
	}
}
