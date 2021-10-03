using Lime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Yuzu;

namespace Tests.Types
{
	[TangerineRegisterNode]
	[TangerineMenuPath("Custom/Isometric/")]
	public class IsometricMesh : Widget
	{
		[YuzuMember]
		public Face LeftFace { get; set; } = new Face();

		[YuzuMember]
		public Face RightFace { get; set; } = new Face();

		[YuzuMember]
		public Face TopFace { get; set; } = new Face();


		[YuzuMember]
		[TangerineStaticProperty]
		[TangerineValidRange(0.0f, float.MaxValue)]
		public float IsometricAspect { get; set; } = 2.0f;

		private bool isEmpty;

		private Vector2 appliedSize;
		private Vector2 appliedPivot;

		protected override void OnBuilt()
		{
			base.OnBuilt();
			ApplyChanges();
			Updating += Update;
		}

		protected void Update(float dt)
		{
			TryApplyChanges();
		}

		protected override RenderObject GetRenderObject()
		{
			if (isEmpty) {
				return null;
			}

			var meshRenderObject = RenderObjectPool<MeshRenderObject>.Acquire();

			var material = WidgetMaterial.GetInstance(GlobalBlending, GlobalShader, 1);
			var color = GlobalColor;

			meshRenderObject.Left = LeftFace.GetRenderObject(material, color, LocalToWorldTransform);
			meshRenderObject.Right = RightFace.GetRenderObject(material, color, LocalToWorldTransform);
			meshRenderObject.Top = TopFace.GetRenderObject(material, color, LocalToWorldTransform);

			return meshRenderObject;
		}

		protected override bool PartialHitTestByContents(ref HitTestArgs args)
		{
			var localPoint = LocalToWorldTransform.CalcInversed().TransformVector(args.Point);

			var size = Size;
			if (size.X < 0) {
				localPoint.X = -localPoint.X;
				size.X = -size.X;
			}
			if (size.Y < 0) {
				localPoint.Y = -localPoint.Y;
				size.Y = -size.Y;
			}

			if (!(localPoint.X >= 0 && localPoint.Y >= 0 && localPoint.X < size.X && localPoint.Y < size.Y)) {
				return false;
			}

			return
				LeftFace.HitTestByContents(localPoint) ||
				RightFace.HitTestByContents(localPoint) ||
				TopFace.HitTestByContents(localPoint);
		}

		private void TryApplyChanges()
		{
			if (appliedSize == Size && appliedPivot == Pivot) {
				return;
			}

			ApplyChanges();
		}

		private void ApplyChanges()
		{
			appliedSize = Size;
			appliedPivot = Pivot;

			var nullableSimplifiedBox = CalculateSimplifiedBox();

			if (nullableSimplifiedBox == null) {
				isEmpty = true;
				return;
			}
			isEmpty = false;

			var simplifiedBox = nullableSimplifiedBox.Value;

			LeftFace.Calculate(
				simplifiedBox.LeftTop,
				simplifiedBox.ControlPoint,
				simplifiedBox.LeftBottom
			);

			RightFace.Calculate(
				simplifiedBox.ControlPoint,
				simplifiedBox.RightTop,
				simplifiedBox.ProjectionControlPoint
			);

			TopFace.Calculate(
				simplifiedBox.Top,
				simplifiedBox.RightTop,
				simplifiedBox.LeftTop
			);
		}

		public class Face
		{
			public enum RotationType
			{
				Deg0,
				Deg90,
				Deg180,
				Deg270,
			}

			public enum FlippingType
			{
				None,
				Vertical,
				Horizontal
			}

			private readonly NinePatchHelper ninePatchHelper = new NinePatchHelper();

			[YuzuMember]
			[TangerineStaticProperty]
			public bool Enable
			{
				get => ninePatchHelper.Enable;
				set => ninePatchHelper.Enable = value;
			}


			[YuzuMember]
			[TangerineStaticProperty]
			public ITexture Texture
			{
				get => ninePatchHelper.Texture;
				set
				{
					if (ninePatchHelper.Texture != value) {
						ninePatchHelper.Texture = value;
					}
				}
			}

			[YuzuMember]
			[TangerineStaticProperty]
			public Color4 TopLeft
			{
				get => ninePatchHelper.TopLeft;
				set => ninePatchHelper.TopLeft = value;
			}

			[YuzuMember]
			[TangerineStaticProperty]
			public Color4 TopRight
			{
				get => ninePatchHelper.TopRight;
				set => ninePatchHelper.TopRight = value;
			}

			[YuzuMember]
			[TangerineStaticProperty]
			public Color4 BottomRight
			{
				get => ninePatchHelper.BottomRight;
				set => ninePatchHelper.BottomRight = value;
			}

			[YuzuMember]
			[TangerineStaticProperty]
			public Color4 BottomLeft
			{
				get => ninePatchHelper.BottomLeft;
				set => ninePatchHelper.BottomLeft = value;
			}

			[YuzuMember]
			[TangerineStaticProperty]
			public ColorRenderingType ColorRendering
			{
				get => ninePatchHelper.ColorRendering;
				set => ninePatchHelper.ColorRendering = value;
			}

			[YuzuMember]
			[TangerineStaticProperty]
			public NinePatchHelper.RenderType Type
			{
				get => ninePatchHelper.Type;
				set => ninePatchHelper.Type = value;
			}

			[YuzuMember]
			[TangerineStaticProperty]
			public BorderDescription Border
			{
				get => ninePatchHelper.Border;
				set => ninePatchHelper.Border = value;
			}

			[YuzuMember]
			[TangerineStaticProperty]
			public Vector2 TextureSize
			{
				get => ninePatchHelper.TextureSize;
				set => ninePatchHelper.TextureSize = value;
			}

			[YuzuMember]
			[TangerineStaticProperty]
			public RotationType Rotation { get; set; }

			[YuzuMember]
			[TangerineStaticProperty]
			public FlippingType Flipping { get; set; }

			internal void Calculate(Vector2 v0, Vector2 v1, Vector2 v2)
			{
				Vector2 useV0;
				Vector2 useV1;
				Vector2 useV2;
				switch (Rotation) {
					default:
						useV0 = v0;
						useV1 = v1;
						useV2 = v2;
						break;
					case RotationType.Deg90:
						useV0 = v2;
						useV1 = v0;
						useV2 = v0 + (v1 - v0) + (v2 - v0);
						break;
					case RotationType.Deg180:
						useV0 = v0 + (v1 - v0) + (v2 - v0);
						useV1 = v2;
						useV2 = v1;
						break;
					case RotationType.Deg270:
						useV0 = v1;
						useV1 = v0 + (v1 - v0) + (v2 - v0);
						useV2 = v0;
						break;
				}
				v0 = useV0;
				v1 = useV1;
				v2 = useV2;
				switch (Flipping) {
					default:
						useV0 = v0;
						useV1 = v1;
						useV2 = v2;
						break;
					case FlippingType.Horizontal:
						useV0 = v1;
						useV1 = v0;
						useV2 = v0 + (v1 - v0) + (v2 - v0);
						break;
					case FlippingType.Vertical:
						useV0 = v2;
						useV1 = v0 + (v1 - v0) + (v2 - v0);
						useV2 = v0;
						break;
				}
				ninePatchHelper.SetCorners(useV0, useV1, useV2);
				ninePatchHelper.ApplyChanges();
			}

			internal RenderObject GetRenderObject(
				WidgetMaterial material, Color4 color, Matrix32 localToWorldTransform
			) {
				return ninePatchHelper.GetRenderObject(material, color, localToWorldTransform);
			}

			internal bool HitTestByContents(Vector2 point)
			{
				return ninePatchHelper.HitTestByContents(point);
			}
		}

		private class MeshRenderObject : RenderObject
		{
			public RenderObject Left;
			public RenderObject Right;
			public RenderObject Top;

			public override void Render()
			{
				Left?.Render();
				Right?.Render();
				Top?.Render();
			}

			protected override void OnRelease()
			{
				base.OnRelease();

				Left?.Release();
				Right?.Release();
				Top?.Release();

				Left = null;
				Right = null;
				Top = null;
			}
		}

		public class NinePatchHelper
		{
			public enum RenderType
			{
				Tiled,
				Sliced
			}

			public bool Enable = true;
			public ITexture Texture = null;
			public Vector2 TextureSize = Vector2.Zero;
			public RenderType Type = RenderType.Sliced;
			public ColorRenderingType ColorRendering = ColorRenderingType.Speed;
			public BorderDescription Border = new BorderDescription();
			public Color4 TopLeft = Color4.White;
			public Color4 TopRight = Color4.White;
			public Color4 BottomRight = Color4.White;
			public Color4 BottomLeft = Color4.White;

			private Vector2 corner0;
			private Vector2 corner1;
			private Vector2 corner2;

			private Markup markup;

			/// <summary>
			/// Must be recreated each rebuild to be immutable for rendering purpose.
			/// </summary>
			private IReadOnlyList<Piece> pieces = new List<Piece>();

			public void SetCorners(Vector2 v0, Vector2 v1, Vector2 v2)
			{
				corner0 = v0;
				corner1 = v1;
				corner2 = v2;
			}

			public void ApplyChanges()
			{
				if (Texture == null) {
					return;
				}

				markup = new Markup(corner0, corner1, corner2, Border, TextureSize);
				pieces = CalculatePieces(
					TextureSize,
					Type,
					markup,
					Border,
					TopLeft, TopRight, BottomLeft, BottomRight
				);
			}

			public RenderObject GetRenderObject(
				WidgetMaterial material, Color4 color, Matrix32 localToWorldTransform
			) {
				if (!Enable || !markup.IsCorrectMesh || Texture == null) {
					return null;
				}

				var renderObject = RenderObjectPool<NinePatchRenderObject>.Acquire();
				renderObject.Setup(localToWorldTransform, Texture, color, material, pieces, ColorRendering);
				return renderObject;
			}

			public bool HitTestByContents(Vector2 point)
			{
				if (!Enable || !markup.IsCorrectMesh || Texture == null) {
					return false;
				}

				if (!Math2d.HitTest(
					point, markup.V0, markup.V3, markup.V15, markup.V12
				)) {
					return false;
				}

				foreach (var piece in pieces) {
					var hitTest = piece.HitTest(point, Texture);
					if (hitTest != null) {
						return hitTest.Value;
					}
				}

				return false;
			}


			private static IReadOnlyList<Piece> CalculatePieces(
				Vector2 textureSize,
				RenderType renderType,
				Markup markup,
				BorderDescription borderDescription,
				Color4 topLeftColor,
				Color4 topRightColor,
				Color4 bottomLeftColor,
				Color4 bottomRightColor
			) {
				var resultFaceParts = new List<Piece>();

				var vertexColor0 = topLeftColor;
				var vertexColor3 = topRightColor;
				var vertexColor12 = bottomLeftColor;
				var vertexColor15 = bottomRightColor;

				var vertexColor1 = Color4.Lerp(borderDescription.Left, vertexColor0, vertexColor3);
				var vertexColor2 = Color4.Lerp(borderDescription.Right, vertexColor3, vertexColor0);
				var vertexColor4 = Color4.Lerp(borderDescription.Top, vertexColor0, vertexColor12);
				var vertexColor7 = Color4.Lerp(borderDescription.Top, vertexColor3, vertexColor15);
				var vertexColor8 = Color4.Lerp(borderDescription.Bottom, vertexColor12, vertexColor0);
				var vertexColor11 = Color4.Lerp(borderDescription.Bottom, vertexColor15, vertexColor3);
				var vertexColor13 = Color4.Lerp(borderDescription.Left, vertexColor12, vertexColor15);
				var vertexColor14 = Color4.Lerp(borderDescription.Right, vertexColor15, vertexColor12);

				var vertexColor5 = Color4.Lerp(borderDescription.Top, vertexColor1, vertexColor13);
				var vertexColor9 = Color4.Lerp(borderDescription.Bottom, vertexColor13, vertexColor1);
				var vertexColor6 = Color4.Lerp(borderDescription.Top, vertexColor2, vertexColor14);
				var vertexColor10 = Color4.Lerp(borderDescription.Bottom, vertexColor14, vertexColor2);

				if (borderDescription.Left > 0 && borderDescription.Top > 0) {
					resultFaceParts.Add(
						new Piece(
							leftTop: (markup.V0, Vector2.Zero, vertexColor0),
							rightTop: (markup.V1, new Vector2(borderDescription.Left, 0), vertexColor1),
							leftBottom: (markup.V4, new Vector2(0, borderDescription.Top), vertexColor4),
							rightBottom: (markup.V5, new Vector2(borderDescription.Left, borderDescription.Top), vertexColor5)
						)
					);
				}

				if (borderDescription.Right > 0 && borderDescription.Top > 0) {
					resultFaceParts.Add(
						new Piece(
							leftTop: (markup.V2, new Vector2(1 - borderDescription.Right, 0), vertexColor2),
							rightTop: (markup.V3, new Vector2(1, 0), vertexColor3),
							leftBottom: (markup.V6, new Vector2(1 - borderDescription.Right, borderDescription.Top), vertexColor6),
							rightBottom: (markup.V7, new Vector2(1, borderDescription.Top), vertexColor7)
						)
					);
				}

				if (borderDescription.Left > 0 && borderDescription.Bottom > 0) {
					resultFaceParts.Add(
						new Piece(
							leftTop: (markup.V8, new Vector2(0, 1 - borderDescription.Bottom), vertexColor8),
							rightTop: (markup.V9, new Vector2(borderDescription.Left, 1 - borderDescription.Bottom), vertexColor9),
							leftBottom: (markup.V12, new Vector2(0, 1), vertexColor12),
							rightBottom: (markup.V13, new Vector2(borderDescription.Left, 1), vertexColor13)
						)
					);
				}

				if (borderDescription.Right > 0 && borderDescription.Bottom > 0) {
					resultFaceParts.Add(
						new Piece(
							leftTop: (
								markup.V10,
								new Vector2(1 - borderDescription.Right, 1 - borderDescription.Bottom),
								vertexColor10
							),
							rightTop: (markup.V11, new Vector2(1, 1 - borderDescription.Bottom), vertexColor11),
							leftBottom: (markup.V14, new Vector2(1 - borderDescription.Right, 1), vertexColor14),
							rightBottom: (markup.V15, new Vector2(1, 1), vertexColor15)
						)
					);
				}

				switch (renderType) {
					case RenderType.Sliced:
						if (borderDescription.Left > 0) {
							resultFaceParts.Add(
								new Piece(
									leftTop: (markup.V4, new Vector2(0, borderDescription.Top), vertexColor4),
									rightTop: (markup.V5, new Vector2(borderDescription.Left, borderDescription.Top), vertexColor5),
									leftBottom: (markup.V8, new Vector2(0, 1 - borderDescription.Bottom), vertexColor8),
									rightBottom: (
										markup.V9,
										new Vector2(borderDescription.Left, 1 - borderDescription.Bottom),
										vertexColor9
									)
								)
							);
						}

						if (borderDescription.Top > 0) {
							resultFaceParts.Add(
								new Piece(
									leftTop: (markup.V1, new Vector2(borderDescription.Left, 0), vertexColor1),
									rightTop: (markup.V2, new Vector2(1 - borderDescription.Right, 0), vertexColor2),
									leftBottom: (markup.V5, new Vector2(borderDescription.Left, borderDescription.Top), vertexColor5),
									rightBottom: (
										markup.V6,
										new Vector2(1 - borderDescription.Right, borderDescription.Top),
										vertexColor6
									)
								)
							);
						}

						resultFaceParts.Add(
							new Piece(
								leftTop: (markup.V5, new Vector2(borderDescription.Left, borderDescription.Top), vertexColor5),
								rightTop: (
									markup.V6,
									new Vector2(1 - borderDescription.Right, borderDescription.Top),
									vertexColor6),
								leftBottom: (
									markup.V9,
									new Vector2(borderDescription.Left, 1 - borderDescription.Bottom),
									vertexColor9),
								rightBottom: (
									markup.V10,
									new Vector2(1 - borderDescription.Right, 1 - borderDescription.Bottom),
									vertexColor10
								)
							)
						);

						if (borderDescription.Bottom > 0) {
							resultFaceParts.Add(
								new Piece(
									leftTop: (
										markup.V9,
										new Vector2(borderDescription.Left, 1 - borderDescription.Bottom),
										vertexColor9
									),
									rightTop: (
										markup.V10,
										new Vector2(1 - borderDescription.Right, 1 - borderDescription.Bottom),
										vertexColor10
									),
									leftBottom: (markup.V13, new Vector2(borderDescription.Left, 1), vertexColor13),
									rightBottom: (markup.V14, new Vector2(1 - borderDescription.Right, 1), vertexColor14)
								)
							);
						}

						if (borderDescription.Right > 0) {
							resultFaceParts.Add(
								new Piece(
									leftTop: (
										markup.V6,
										new Vector2(1 - borderDescription.Right, borderDescription.Top),
										vertexColor6
									),
									rightTop: (markup.V7, new Vector2(1, borderDescription.Top), vertexColor7),
									leftBottom: (
										markup.V10,
										new Vector2(1 - borderDescription.Right, 1 - borderDescription.Bottom),
										vertexColor10
									),
									rightBottom: (markup.V11, new Vector2(1, 1 - borderDescription.Bottom), vertexColor11)
								)
							);
						}

						break;
					case RenderType.Tiled: {
						int countX = 0;
						int countY = 0;
						var deltaX = Vector2.Zero;
						var deltaY = Vector2.Zero;
						float remainderX = 0.0f;
						float remainderY = 0.0f;
						var vector48 = markup.V8 - markup.V4;
						if (vector48 != Vector2.Zero) {
							deltaY =
								Mathf.Max(1 - borderDescription.Top - borderDescription.Bottom, 0.01f) *
								textureSize.Y *
								vector48.Normalized;
							float coefficientY = vector48.Length / deltaY.Length;
							countY = coefficientY.Truncate();
							remainderY = coefficientY - countY;
							for (int i = 0; i < countY + 1; i++) {
								float factor = 1.0f;
								if (i == countY) {
									factor = remainderY;
								}

								resultFaceParts.Add(
									new Piece(
										leftTop: (
											markup.V4 + i * deltaY,
											new Vector2(0, borderDescription.Top),
											Color4.Lerp(i * deltaY.Length / vector48.Length, vertexColor4, vertexColor8)
										),
										rightTop: (
											markup.V5 + i * deltaY,
											new Vector2(borderDescription.Left, borderDescription.Top),
											Color4.Lerp(i * deltaY.Length / vector48.Length, vertexColor5, vertexColor9)
										),
										leftBottom: (
											markup.V4 + (i + factor) * deltaY,
											new Vector2(
												0,
												(1 - borderDescription.Bottom - borderDescription.Top) * factor +
												borderDescription.Top
											),
											Color4.Lerp((i + factor) * deltaY.Length / vector48.Length, vertexColor4, vertexColor8)
										),
										rightBottom: (
											markup.V5 + (i + factor) * deltaY,
											new Vector2(
												borderDescription.Left,
												(1 - borderDescription.Bottom - borderDescription.Top) * factor +
												borderDescription.Top
											),
											Color4.Lerp((i + factor) * deltaY.Length / vector48.Length, vertexColor5, vertexColor9)
										)
									)
								);

								resultFaceParts.Add(
									new Piece(
										leftTop: (
											markup.V6 + i * deltaY,
											new Vector2(1 - borderDescription.Right, borderDescription.Top),
											Color4.Lerp(i * deltaY.Length / vector48.Length, vertexColor6, vertexColor10)
										),
										rightTop: (
											markup.V7 + i * deltaY,
											new Vector2(1, borderDescription.Top),
											Color4.Lerp(i * deltaY.Length / vector48.Length, vertexColor7, vertexColor11)
										),
										leftBottom: (
											markup.V6 + (i + factor) * deltaY,
											new Vector2(
												1 - borderDescription.Right,
												(1 - borderDescription.Bottom - borderDescription.Top) * factor +
												borderDescription.Top
											),
											Color4.Lerp((i + factor) * deltaY.Length / vector48.Length, vertexColor6, vertexColor10)

										),
										rightBottom: (
											markup.V7 + (i + factor) * deltaY,
											new Vector2(
												1,
												(1 - borderDescription.Bottom - borderDescription.Top) * factor +
												borderDescription.Top
											),
											Color4.Lerp((i + factor) * deltaY.Length / vector48.Length, vertexColor7, vertexColor11)
										)
									)
								);
							}
						}

						var vector12 = markup.V2 - markup.V1;
						if (vector12 != Vector2.Zero) {
							deltaX =
								Mathf.Max(1 - borderDescription.Left - borderDescription.Right, 0.01f) *
								textureSize.X *
								vector12.Normalized;
							float coefficientX = vector12.Length / deltaX.Length;
							countX = coefficientX.Truncate();
							remainderX = coefficientX - countX;
							for (int i = 0; i < countX + 1; i++) {
								float factor = 1.0f;
								if (i == countX) {
									factor = remainderX;
								}

								resultFaceParts.Add(
									new Piece(
										leftTop: (
											markup.V1 + i * deltaX,
											new Vector2(borderDescription.Left, 0),
											Color4.Lerp(i * deltaX.Length / vector12.Length, vertexColor1, vertexColor2)
										),
										rightTop: (
											markup.V1 + (i + factor) * deltaX,
											new Vector2(
												(1 - borderDescription.Right - borderDescription.Left) * factor +
												borderDescription.Left, 0
											),
											Color4.Lerp((i + factor) * deltaX.Length / vector12.Length, vertexColor1, vertexColor2)
										),
										leftBottom: (
											markup.V5 + i * deltaX,
											new Vector2(borderDescription.Left, borderDescription.Top),
											Color4.Lerp(i * deltaX.Length / vector12.Length, vertexColor5, vertexColor6)
										),
										rightBottom: (
											markup.V5 + (i + factor) * deltaX,
											new Vector2(
												(1 - borderDescription.Right - borderDescription.Left) * factor +
												borderDescription.Left,
												borderDescription.Top
											),
											Color4.Lerp((i + factor) * deltaX.Length / vector12.Length, vertexColor5, vertexColor6)
										)
									)
								);

								resultFaceParts.Add(
									new Piece(
										leftTop: (
											markup.V9 + i * deltaX,
											new Vector2(borderDescription.Left, 1 - borderDescription.Bottom),
											Color4.Lerp(i * deltaX.Length / vector12.Length, vertexColor9, vertexColor10)
										),
										rightTop: (
											markup.V13 + i * deltaX,
											new Vector2(borderDescription.Left, 1),
											Color4.Lerp(i * deltaX.Length / vector12.Length, vertexColor13, vertexColor14)
										),
										leftBottom: (
											markup.V9 + (i + factor) * deltaX,
											new Vector2(
												(1 - borderDescription.Right - borderDescription.Left) * factor +
												borderDescription.Left,
												1 - borderDescription.Bottom
											),
											Color4.Lerp((i + factor) * deltaX.Length / vector12.Length, vertexColor9, vertexColor10)
										),
										rightBottom: (
											markup.V13 + (i + factor) * deltaX,
											new Vector2(
												(1 - borderDescription.Right - borderDescription.Left) * factor +
												borderDescription.Left, 1
											),
											Color4.Lerp((i + factor) * deltaX.Length / vector12.Length, vertexColor13, vertexColor14)
										)
									)
								);
							}
						}

						if (vector12 != Vector2.Zero && vector48 != Vector2.Zero) {
							for (int i = 0; i < countX + 1; i++) {
								float factorX = 1.0f;
								if (i == countX) {
									factorX = remainderX;
								}

								for (int j = 0; j < countY + 1; j++) {
									float factorY = 1.0f;
									if (j == countY) {
										factorY = remainderY;
									}

									var startRector = markup.V5 + i * deltaX + j * deltaY;
									var leftTopColor = Color4.Lerp(
										j * deltaY.Length / vector48.Length,
										Color4.Lerp(i * deltaX.Length / vector12.Length, vertexColor5, vertexColor6),
										Color4.Lerp(i * deltaX.Length / vector12.Length, vertexColor9, vertexColor10)
									);
									var rightTopColor = Color4.Lerp(
										j * deltaY.Length / vector48.Length,
										Color4.Lerp((i + factorX) * deltaX.Length / vector12.Length, vertexColor5, vertexColor6),
										Color4.Lerp((i + factorX) * deltaX.Length / vector12.Length, vertexColor9, vertexColor10)
									);
									var leftBottomColor = Color4.Lerp(
										(j + factorY) * deltaY.Length / vector48.Length,
										Color4.Lerp(i * deltaX.Length / vector12.Length, vertexColor5, vertexColor6),
										Color4.Lerp(i * deltaX.Length / vector12.Length, vertexColor9, vertexColor10)
									);
									var rightBottomColor = Color4.Lerp(
										(j + factorY) * deltaY.Length / vector48.Length,
										Color4.Lerp((i + factorX) * deltaX.Length / vector12.Length, vertexColor5, vertexColor6),
										Color4.Lerp((i + factorX) * deltaX.Length / vector12.Length, vertexColor9, vertexColor10)
									);

									resultFaceParts.Add(
										new Piece(
											leftTop: (
												startRector,
												new Vector2(borderDescription.Left, borderDescription.Top),
												leftTopColor
											),
											rightTop: (
												startRector + factorX * deltaX,
												new Vector2(
													(1 - borderDescription.Right - borderDescription.Left) * factorX +
													borderDescription.Left,
													borderDescription.Top
												),
												rightTopColor
											),
											leftBottom: (
												startRector + factorY * deltaY,
												new Vector2(
													borderDescription.Left,
													(1 - borderDescription.Bottom - borderDescription.Top) * factorY +
													borderDescription.Top
												),
												leftBottomColor
											),
											rightBottom: (
												startRector + factorY * deltaY + factorX * deltaX,
												new Vector2(
													(1 - borderDescription.Right - borderDescription.Left) * factorX +
													borderDescription.Left,
													(1 - borderDescription.Bottom - borderDescription.Top) * factorY +
													borderDescription.Top
												),
												rightBottomColor
											)
										)
									);
								}
							}
						}

						break;
					}
				}

				return resultFaceParts;
			}
		}

		private readonly struct Piece
		{
			private readonly (Vector2 V, Vector2 UV, Color4 Color) leftTop;
			private readonly (Vector2 V, Vector2 UV, Color4 Color) rightTop;
			private readonly (Vector2 V, Vector2 UV, Color4 Color) leftBottom;
			private readonly (Vector2 V, Vector2 UV, Color4 Color) rightBottom;

			private readonly bool isValid;

			public Piece(
				(Vector2 V, Vector2 UV, Color4 Color) leftTop,
				(Vector2 V, Vector2 UV, Color4 Color) rightTop,
				(Vector2 V, Vector2 UV, Color4 Color) leftBottom,
				(Vector2 V, Vector2 UV, Color4 Color) rightBottom
			) {
				this.leftTop = leftTop;
				this.rightTop = rightTop;
				this.leftBottom = leftBottom;
				this.rightBottom = rightBottom;

				if (this.leftTop == this.rightTop || this.leftBottom == this.rightBottom) {
					isValid = false;
					return;
				}

				isValid = true;
			}

			public void Render(
				ITexture texture,
				Color4 color,
				IMaterial material,
				ColorRenderingType colorRendering,
				Vertex[] vertices
			) {
				if (!isValid) {
					return;
				}

				switch (colorRendering) {
					case ColorRenderingType.Speed:
						vertices[0] = new Vertex {
							Pos = leftTop.V,
							Color = color * leftTop.Color,
							UV1 = leftTop.UV,
							UV2 = Vector2.Zero
						};
						vertices[1] = new Vertex {
							Pos = rightTop.V,
							Color = color * rightTop.Color,
							UV1 = rightTop.UV,
							UV2 = Vector2.Zero
						};
						vertices[2] = new Vertex {
							Pos = leftBottom.V,
							Color = color * leftBottom.Color,
							UV1 = leftBottom.UV,
							UV2 = Vector2.Zero
						};
						vertices[3] = new Vertex {
							Pos = rightBottom.V,
							Color = color * rightBottom.Color,
							UV1 = rightBottom.UV,
							UV2 = Vector2.Zero
						};
						Renderer.DrawTriangleStrip(texture, null, material, vertices, 4);
						break;
					case ColorRenderingType.Quality:
						vertices[1] = new Vertex {
							Pos = leftTop.V,
							Color = color * leftTop.Color,
							UV1 = leftTop.UV,
							UV2 = Vector2.Zero
						};
						vertices[2] = new Vertex {
							Pos = leftBottom.V,
							Color = color * leftBottom.Color,
							UV1 = leftBottom.UV,
							UV2 = Vector2.Zero
						};
						vertices[3] = new Vertex {
							Pos = rightBottom.V,
							Color = color * rightBottom.Color,
							UV1 = rightBottom.UV,
							UV2 = Vector2.Zero
						};
						vertices[4] = new Vertex {
							Pos = rightTop.V,
							Color = color * rightTop.Color,
							UV1 = rightTop.UV,
							UV2 = Vector2.Zero
						};
						vertices[5] = vertices[1];
						vertices[0] = CalculateCenterVertex();
						Renderer.DrawTriangleFan(texture, null, material, vertices, 6);
						break;
					default:
						throw new ArgumentOutOfRangeException(nameof(colorRendering), colorRendering, null);
				}

				Vertex CalculateCenterVertex()
				{
					var v = new Vertex();
					var colorAR = Vector2.Zero;
					var colorGB = Vector2.Zero;
					for (int t = 1; t < 5; t++) {
						var p = vertices[t];
						v.UV1 += p.UV1;
						v.Pos += p.Pos;
						colorAR.X += p.Color.A;
						colorAR.Y += p.Color.R;
						colorGB.X += p.Color.G;
						colorGB.Y += p.Color.B;
					}
					var k = new Vector2(0.25f, 0.25f);
					colorAR *= k;
					colorGB *= k;
					v.Color = new Color4((byte)colorAR.Y, (byte)colorGB.X, (byte)colorGB.Y, (byte)colorAR.X);
					v.UV1 *= k;
					v.Pos *= k;
					return v;
				}
			}

			public bool? HitTest(Vector2 point, ITexture texture)
			{
				if (!Math2d.HitTest(point, leftTop.V, rightTop.V, rightBottom.V, leftBottom.V)) {
					return null;
				}

				var topVector = rightTop.V - leftTop.V;
				var leftVector = leftBottom.V - leftTop.V;
				var pointVector = point - leftTop.V;

				Vector2 top;
				Vector2 left;

				if (topVector.Y == 0 && leftVector.X == 0) {
					top = new Vector2(topVector.X, 0);
					left = new Vector2(0, leftVector.Y);
				} else if (topVector.X == 0) {
					float y = pointVector.Y - pointVector.X * leftVector.Y / leftVector.X;
					top = new Vector2(0, y);
					left = pointVector - top;
				} else if (topVector.Y == 0) {
					float x = pointVector.X - pointVector.Y * leftVector.X / leftVector.Y;
					left = new Vector2(x, 0);
					top = pointVector - left;
				} else if (leftVector.X == 0) {
					float y = pointVector.Y - pointVector.X * topVector.Y / topVector.X;
					left = new Vector2(0, y);
					top = pointVector - left;
				} else if (leftVector.Y == 0) {
					float x = pointVector.X - pointVector.Y * topVector.X / topVector.Y;
					top = new Vector2(x, 0);
					left = pointVector - top;
				} else {
					float x =
						(pointVector.Y - leftVector.Y / leftVector.X * pointVector.X) /
						(topVector.Y / topVector.X - leftVector.Y / leftVector.X);
					float y = topVector.Y / topVector.X * x;
					top = new Vector2(x, y);
					left = pointVector - top;
				}

				float topVectorLength = topVector.Length;
				float leftVectorLength = leftVector.Length;

				float u = leftTop.UV.X + (topVectorLength == 0 ? 0 : top.Length / topVectorLength) *
					(rightTop.UV.X - leftTop.UV.X);
				float v = leftTop.UV.Y + (leftVectorLength == 0 ? 0 : left.Length / leftVectorLength) *
					(leftBottom.UV.Y - leftTop.UV.Y);

				int tu = (int)(texture.ImageSize.Width * u);
				int tv = (int)(texture.ImageSize.Height * v);

				return !texture.IsTransparentPixel(tu, tv);
			}
		}

		public enum ColorRenderingType
		{
			Speed,
			Quality
		}

		public static class Math2d
		{
			public static bool HitTest(
				Vector2 point,
				Vector2 leftTop,
				Vector2 rightTop,
				Vector2 rightBottom,
				Vector2 leftBottom
			) {
				float v1 =
					(point.X - leftTop.X) * (rightTop.Y - leftTop.Y) -
					(point.Y - leftTop.Y) * (rightTop.X - leftTop.X);
				float v2 =
					(point.X - rightTop.X) * (rightBottom.Y - rightTop.Y) -
					(point.Y - rightTop.Y) * (rightBottom.X - rightTop.X);
				float v3 =
					(point.X - rightBottom.X) * (leftBottom.Y - rightBottom.Y) -
					(point.Y - rightBottom.Y) * (leftBottom.X - rightBottom.X);
				float v4 =
					(point.X - leftBottom.X) * (leftTop.Y - leftBottom.Y) -
					(point.Y - leftBottom.Y) * (leftTop.X - leftBottom.X);

				int sign1 = v1.Sign();
				int sign2 = v2.Sign();
				int sign3 = v3.Sign();
				int sign4 = v4.Sign();
				return sign1 == sign2 && sign2 == sign3 && sign3 == sign4;
			}
		}

		private readonly struct Markup
		{
			public readonly Vector2 V0;
			public readonly Vector2 V1;
			public readonly Vector2 V2;
			public readonly Vector2 V3;
			public readonly Vector2 V4;
			public readonly Vector2 V5;
			public readonly Vector2 V6;
			public readonly Vector2 V7;
			public readonly Vector2 V8;
			public readonly Vector2 V9;
			public readonly Vector2 V10;
			public readonly Vector2 V11;
			public readonly Vector2 V12;
			public readonly Vector2 V13;
			public readonly Vector2 V14;
			public readonly Vector2 V15;
			public readonly bool IsCorrectMesh;

			public Markup(Vector2 v0, Vector2 v1, Vector2 v2, BorderDescription border, Vector2 textureSize)
			{
				V0 = Vector2.Zero;
				V1 = Vector2.Zero;
				V2 = Vector2.Zero;
				V3 = Vector2.Zero;
				V4 = Vector2.Zero;
				V5 = Vector2.Zero;
				V6 = Vector2.Zero;
				V7 = Vector2.Zero;
				V8 = Vector2.Zero;
				V9 = Vector2.Zero;
				V10 = Vector2.Zero;
				V11 = Vector2.Zero;
				V12 = Vector2.Zero;
				V13 = Vector2.Zero;
				V14 = Vector2.Zero;
				V15 = Vector2.Zero;
				IsCorrectMesh = false;

				V0 = v0;
				var vector03 = v1 - v0;
				if (vector03 == Vector2.Zero) {
					return;
				}

				var vector01 = border.Left * textureSize.X * vector03.Normalized;
				V1 = vector01 + v0;
				var vector02 = vector03 - border.Right * textureSize.X * vector03.Normalized;
				if (vector01.Length > vector02.Length) {
					return;
				}

				V2 = vector02 + v0;
				V3 = v1;
				var vector012 = v2 - v0;
				if (vector012 == Vector2.Zero) {
					return;
				}

				var vector04 = border.Top * textureSize.Y * vector012.Normalized;
				var vector08 = vector012 - border.Bottom * textureSize.Y * vector012.Normalized;
				if (vector04.Length > vector08.Length) {
					return;
				}

				V4 = vector04 + v0;
				V5 = vector01 + vector04 + v0;
				V6 = vector02 + vector04 + v0;
				V7 = vector03 + vector04 + v0;
				V8 = vector08 + v0;
				V9 = vector01 + vector08 + v0;
				V10 = vector02 + vector08 + v0;
				V11 = vector03 + vector08 + v0;
				V12 = v2;
				V13 = vector01 + vector012 + v0;
				V14 = vector02 + vector012 + v0;
				V15 = vector03 + vector012 + v0;
				IsCorrectMesh = true;
			}
		}

		public class BorderDescription
		{
			private const float MinValue = 0.0f;
			private const float MaxValue = 1.0f;

			private float left;
			private float right;
			private float top;
			private float bottom;

			[YuzuMember]
			[TangerineValidRange(0.0f, 1.0f, WarningLevel = ValidationResult.Error)]
			public float Left
			{
				get => left;
				set
				{
					left = value;
					FixPairValues(ref left, ref right);
				}
			}

			[YuzuMember]
			[TangerineValidRange(0.0f, 1.0f, WarningLevel = ValidationResult.Error)]
			public float Right
			{
				get => right;
				set
				{
					right = value;
					FixPairValues(ref right, ref left);
				}
			}

			[YuzuMember]
			[TangerineValidRange(0.0f, 1.0f, WarningLevel = ValidationResult.Error)]
			public float Top
			{
				get => top;
				set
				{
					top = value;
					FixPairValues(ref top, ref bottom);
				}
			}

			[YuzuMember]
			[TangerineValidRange(0.0f, 1.0f, WarningLevel = ValidationResult.Error)]
			public float Bottom
			{
				get => bottom;
				set
				{
					bottom = value;
					FixPairValues(ref bottom, ref top);
				}
			}

			private static void FixPairValues(ref float primaryValue, ref float secondaryValue)
			{
				if (primaryValue > MaxValue) {
					primaryValue = MaxValue;
				}

				if (primaryValue < MinValue) {
					primaryValue = MinValue;
				}

				if (primaryValue + secondaryValue > 1.0f) {
					secondaryValue = 1.0f - primaryValue;
				}
			}
		}

		private class NinePatchRenderObject : RenderObject
		{
			private Matrix32 localToWorldTransform;

			private IMaterial material;
			private Color4 color;

			private ITexture texture;
			private IReadOnlyList<Piece> pieces;
			private ColorRenderingType colorRendering;

			private readonly Vertex[] vertices = new Vertex[6];

			public void Setup(
				Matrix32 localToWorldTransform,
				ITexture texture,
				Color4 color,
				IMaterial material,
				IReadOnlyList<Piece> pieces,
				ColorRenderingType colorRendering
			) {
				this.localToWorldTransform = localToWorldTransform;
				this.material = material;
				this.texture = texture;
				this.pieces = pieces;
				this.color = color;
				this.colorRendering = colorRendering;
			}

			protected override void OnRelease()
			{
				texture = null;
				pieces = null;
			}

			public override void Render()
			{
				Renderer.Transform1 = localToWorldTransform;

				foreach (var piece in pieces) {
					piece.Render(texture, color, material, colorRendering, vertices);
				}
			}
		}

		private struct SimplifiedBox
		{
			public Vector2 ControlPoint;
			public Vector2 ProjectionControlPoint;

			public Vector2 LeftTop;
			public Vector2 LeftBottom;
			public Vector2 RightTop;
			public Vector2 Top;
		}

		SimplifiedBox? CalculateSimplifiedBox()
		{
			if (Pivot.X < 0.0f || Pivot.X > 1.0f || Pivot.Y < 0.0f || Pivot.Y > 1.0f || IsometricAspect < 0.0f) {
				return null;
			}

			var resultSimplifiedBox = new SimplifiedBox();

			var controlPoint = new Vector2(Size.X * Pivot.X, Size.Y * Pivot.Y);
			resultSimplifiedBox.ControlPoint = controlPoint;

			var projectionControlPoint = new Vector2(controlPoint.X, Size.Y);
			resultSimplifiedBox.ProjectionControlPoint = projectionControlPoint;

			if (controlPoint.Y - controlPoint.X / IsometricAspect >= 0) {
				resultSimplifiedBox.LeftTop = new Vector2(0, controlPoint.Y - controlPoint.X / IsometricAspect);
				resultSimplifiedBox.LeftBottom = new Vector2(
					0, projectionControlPoint.Y - projectionControlPoint.X / IsometricAspect
				);

				if (resultSimplifiedBox.LeftTop.Y * IsometricAspect < Size.X - controlPoint.X) {
					resultSimplifiedBox.Top = new Vector2(resultSimplifiedBox.LeftTop.Y * IsometricAspect, 0);
					var delta = new Vector2(resultSimplifiedBox.Top.X, resultSimplifiedBox.LeftTop.Y);
					resultSimplifiedBox.RightTop = resultSimplifiedBox.ControlPoint + new Vector2(delta.X, -delta.Y);
				} else {
					var delta = new Vector2(Size.X - controlPoint.X, (Size.X - controlPoint.X) / IsometricAspect);
					resultSimplifiedBox.RightTop = resultSimplifiedBox.ControlPoint + new Vector2(delta.X, -delta.Y);
					resultSimplifiedBox.Top =
						resultSimplifiedBox.RightTop - (controlPoint - resultSimplifiedBox.LeftTop);
				}

				return resultSimplifiedBox;
			}

			if (controlPoint.Y - (Size.X - controlPoint.X) / IsometricAspect >= 0) {
				var delta = new Vector2(Size.X - controlPoint.X, (Size.X - controlPoint.X) / IsometricAspect);
				resultSimplifiedBox.RightTop = new Vector2(controlPoint.X + delta.X, controlPoint.Y - delta.Y);

				if (resultSimplifiedBox.RightTop.Y * IsometricAspect < controlPoint.X) {
					resultSimplifiedBox.Top = new Vector2(Size.X - resultSimplifiedBox.RightTop.Y * IsometricAspect, 0);
					delta = new Vector2(Size.X - resultSimplifiedBox.Top.X, resultSimplifiedBox.RightTop.Y);
					resultSimplifiedBox.LeftTop = controlPoint - delta;
					resultSimplifiedBox.LeftBottom = projectionControlPoint - delta;
				} else {
					delta = new Vector2(controlPoint.X, controlPoint.X / IsometricAspect);
					resultSimplifiedBox.LeftTop = controlPoint - delta;
					resultSimplifiedBox.LeftBottom = projectionControlPoint - delta;
					resultSimplifiedBox.Top = resultSimplifiedBox.RightTop - delta;
				}

				return resultSimplifiedBox;
			}

			return null;
		}
	}
}
