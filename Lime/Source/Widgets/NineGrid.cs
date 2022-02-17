using System;
using Yuzu;

namespace Lime
{
	[TangerineRegisterNode(Order = 13)]
	[TangerineVisualHintGroup("/All/Nodes/Images")]
	public class NineGrid : Widget
	{
		[YuzuMember]
		[TangerineKeyframeColor(14)]
		public override ITexture Texture { get; set; }

		[YuzuMember]
		[TangerineKeyframeColor(15)]
		public float LeftOffset { get; set; }

		[YuzuMember]
		[TangerineKeyframeColor(16)]
		public float RightOffset { get; set; }

		[YuzuMember]
		[TangerineKeyframeColor(17)]
		public float TopOffset { get; set; }

		[YuzuMember]
		[TangerineKeyframeColor(18)]
		public float BottomOffset { get; set; }

		public struct Part
		{
			public Rectangle Rect;
			public Rectangle UV;
		}

		public NineGrid()
		{
			Presenter = DefaultPresenter.Instance;
			HitTestMethod = HitTestMethod.Contents;
			Texture = new SerializableTexture();
		}

		public static void BuildLayout(
			Part[] layout,
			Vector2 textureSize,
			float leftOffset,
			float rightOffset,
			float topOffset,
			float bottomOffset,
			Vector2 size
		) {
			float leftPart = leftOffset * textureSize.X;
			float topPart = topOffset * textureSize.Y;
			float rightPart = rightOffset * textureSize.X;
			float bottomPart = bottomOffset * textureSize.Y;

			float tx0 = 0;
			float tx1 = leftOffset;
			float tx2 = 1 - rightOffset;
			float tx3 = 1;

			float ty0 = 0;
			float ty1 = topOffset;
			float ty2 = 1 - bottomOffset;
			float ty3 = 1;

			Vector2 gridSize = size;
			bool flipX = false;
			bool flipY = false;
			if (gridSize.X < 0) {
				gridSize.X = -gridSize.X;
				flipX = true;
			}
			if (gridSize.Y < 0) {
				gridSize.Y = -gridSize.Y;
				flipY = true;
			}
			// If grid width less than texture width, then uniform scale texture by width.
			if (gridSize.X < textureSize.X) {
				leftPart = rightPart = 0;
				tx0 = tx1 = 0;
				tx2 = tx3 = 1;
			}
			// If grid height less than texture height, then uniform scale texture by height.
			if (gridSize.Y < textureSize.Y) {
				topPart = bottomPart = 0;
				ty0 = ty1 = 0;
				ty2 = ty3 = 1;
			}
			// Corners
			layout[0].Rect = new Rectangle(0, 0, leftPart, topPart);
			layout[0].UV = new Rectangle(tx0, ty0, tx1, ty1);
			layout[1].Rect = new Rectangle(gridSize.X - rightPart, 0, gridSize.X, topPart);
			layout[1].UV = new Rectangle(tx2, ty0, tx3, ty1);
			layout[2].Rect = new Rectangle(0, gridSize.Y - bottomPart, leftPart, gridSize.Y);
			layout[2].UV = new Rectangle(tx0, ty2, tx1, ty3);
			layout[3].Rect = new Rectangle(gridSize.X - rightPart, gridSize.Y - bottomPart, gridSize.X, gridSize.Y);
			layout[3].UV = new Rectangle(tx2, ty2, tx3, ty3);
			// Central part
			layout[4].Rect = new Rectangle(leftPart, topPart, gridSize.X - rightPart, gridSize.Y - bottomPart);
			layout[4].UV = new Rectangle(tx1, ty1, tx2, ty2);
			// Sides
			layout[5].Rect = new Rectangle(leftPart, 0, gridSize.X - rightPart, topPart);
			layout[5].UV = new Rectangle(tx1, ty0, tx2, ty1);
			layout[6].Rect = new Rectangle(leftPart, gridSize.Y - bottomPart, gridSize.X - rightPart, gridSize.Y);
			layout[6].UV = new Rectangle(tx1, ty2, tx2, ty3);
			layout[7].Rect = new Rectangle(0, topPart, leftPart, gridSize.Y - bottomPart);
			layout[7].UV = new Rectangle(tx0, ty1, tx1, ty2);
			layout[8].Rect = new Rectangle(gridSize.X - rightPart, topPart, gridSize.X, gridSize.Y - bottomPart);
			layout[8].UV = new Rectangle(tx2, ty1, tx3, ty2);
			for (int i = 0; i < 9; i++) {
				if (flipX) {
					layout[i].Rect.AX = -layout[i].Rect.AX;
					layout[i].Rect.BX = -layout[i].Rect.BX;
				}
				if (flipY) {
					layout[i].Rect.AY = -layout[i].Rect.AY;
					layout[i].Rect.BY = -layout[i].Rect.BY;
				}
			}
		}

		public override void AddToRenderChain(RenderChain chain)
		{
			if (GloballyVisible && ClipRegionTest(chain.ClipRegion)) {
				AddSelfAndChildrenToRenderChain(chain, Layer);
			}
		}

		protected internal override Lime.RenderObject GetRenderObject()
		{
			var ro = RenderObjectPool<RenderObject>.Acquire();
			ro.CaptureRenderState(this);
			ro.Texture = Texture;
			ro.LeftOffset = LeftOffset;
			ro.RightOffset = RightOffset;
			ro.TopOffset = TopOffset;
			ro.BottomOffset = BottomOffset;
			ro.Size = Size;
			ro.Color = GlobalColor;
			return ro;
		}

		private Part[] parts;

		protected internal override bool PartialHitTestByContents(ref HitTestArgs args)
		{
			parts = parts ?? new Part[9];
			BuildLayout(parts, (Vector2)Texture.ImageSize, LeftOffset, RightOffset, TopOffset, BottomOffset, Size);
			for (int i = 0; i < parts.Length; i++) {
				if (PartHitTest(parts[i], args.Point)) {
					return true;
				}
			}
			return false;
		}

		private bool PartHitTest(Part part, Vector2 point)
		{
			point = LocalToWorldTransform.CalcInversed().TransformVector(point);
			if (part.Rect.BX < part.Rect.AX) {
				part.Rect.AX = -part.Rect.AX;
				part.Rect.BX = -part.Rect.BX;
				point.X = -point.X;
			}
			if (part.Rect.BY < part.Rect.AY) {
				part.Rect.AY = -part.Rect.AY;
				part.Rect.BY = -part.Rect.BY;
				point.Y = -point.Y;
			}
			if (
				point.X >= part.Rect.AX && point.Y >= part.Rect.AY && point.X < part.Rect.BX && point.Y < part.Rect.BY
			) {
				float uf = (point.X - part.Rect.AX) / part.Rect.Width * part.UV.Width + part.UV.AX;
				float vf = (point.Y - part.Rect.AY) / part.Rect.Height * part.UV.Height + part.UV.AY;
				int ui = (int)(Texture.ImageSize.Width * uf);
				int vi = (int)(Texture.ImageSize.Height * vf);
				return !Texture.IsTransparentPixel(ui, vi);
			}
			return false;
		}

		private class RenderObject : WidgetRenderObject
		{
			private Part[] parts = new Part[9];

			public ITexture Texture;
			public float LeftOffset;
			public float RightOffset;
			public float TopOffset;
			public float BottomOffset;
			public Vector2 Size;
			public Color4 Color;

			public override void Render()
			{
				BuildLayout(parts, (Vector2)Texture.ImageSize, LeftOffset, RightOffset, TopOffset, BottomOffset, Size);
				PrepareRenderState();
				foreach (var part in parts) {
					Renderer.DrawSprite(Texture, Color, part.Rect.A, part.Rect.Size, part.UV.A, part.UV.B);
				}
			}

			protected override void OnRelease()
			{
				Texture = null;
			}
		}
	}
}
