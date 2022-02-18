using System;
using System.Runtime.CompilerServices;

namespace Lime.NanoVG
{
	internal class RenderingBackend : IRenderingBackend
	{
		private readonly Material material = new Material();

		private readonly StencilState fillStencilState = new StencilState
		{
			Enable = true,
			WriteMask = 0xff,
			ReferenceValue = 0,
			ReadMask = 0xff,
			FrontFaceComparison = CompareFunc.Always,
			FrontFaceFail = StencilOp.Keep,
			FrontFaceDepthFail = StencilOp.Keep,
			FrontFacePass = StencilOp.Increment,
			BackFaceComparison = CompareFunc.Always,
			BackFaceFail = StencilOp.Keep,
			BackFaceDepthFail = StencilOp.Keep,
			BackFacePass = StencilOp.Decrement
		};
		
		private readonly StencilState applyStencilState = new StencilState
		{
			Enable = true,
			WriteMask = 0xff,
			ReferenceValue = 0,
			ReadMask = 0xff,
			FrontFaceComparison = CompareFunc.NotEqual,
			FrontFaceFail = StencilOp.Zero,
			FrontFaceDepthFail = StencilOp.Zero,
			FrontFacePass = StencilOp.Zero,
			BackFaceComparison = CompareFunc.NotEqual,
			BackFaceFail = StencilOp.Zero,
			BackFaceDepthFail = StencilOp.Zero,
			BackFacePass = StencilOp.Zero
		};

		public int CreateTexture(TextureType type, int width, int height, ImageFlags imageFlags, byte[] data)
		{
			throw new NotSupportedException();
		}

		public void DeleteTexture(int image)
		{
			throw new NotSupportedException();
		}

		public void UpdateTexture(int image, int x, int y, int width, int height, byte[] data)
		{
			throw new NotSupportedException();
		}

		public void GetTextureSize(int image, out int width, out int height)
		{
			throw new NotSupportedException();
		}

		private readonly Vertex[] quad = new Vertex[4];

		public void RenderFill(ref Paint paint, ref Scissor scissor, float fringe, Rectangle bounds,
			ArraySegment<Path> paths)
		{
			var isConvex = paths.Count == 1 && paths.Array[paths.Offset].Convex == 1;
			RenderingType renderingType;
			if (isConvex) {
				renderingType = paint.Image != 0 ? RenderingType.FillImage : RenderingType.FillGradient;
			} else {
				Renderer.ColorWriteEnabled = ColorWriteMask.None;
				Renderer.StencilState = fillStencilState;
				renderingType = RenderingType.FillStencil;
			}
			for (var i = 0; i < paths.Count; ++i) {
				var path = paths.Array[paths.Offset + i];
				if (path.Fill != null) {
					RenderTriangles(
						ref paint, 
						ref scissor, 
						fringe,
						fringe, 
						renderingType, 
						path.Fill.Value, 
						PrimitiveType.TriangleFan
					);
				}
			}
			if (!isConvex) {
				Renderer.ColorWriteEnabled = ColorWriteMask.All;
				Renderer.StencilState = applyStencilState;
				quad[0].Pos = new Vector2(bounds.BX, bounds.BY);
				quad[1].Pos = new Vector2(bounds.BX, bounds.AY);
				quad[2].Pos = new Vector2(bounds.AX, bounds.BY);
				quad[3].Pos = new Vector2(bounds.AX, bounds.AY);
				quad[0].UV1 = quad[1].UV1 = quad[2].UV1 = quad[3].UV1 = new Vector2(0.5f, 1.0f);
				RenderTriangles(
					ref paint, 
					ref scissor, 
					fringe, 
					fringe,
					RenderingType.FillGradient, 
					new ArraySegment<Vertex>(quad), 
					PrimitiveType.TriangleStrip
				);
				Renderer.StencilState = StencilState.Default;
			}
			// Render antialiased outline
			renderingType = paint.Image != 0 ? RenderingType.FillImage : RenderingType.FillGradient;
			for (var i = 0; i < paths.Count; ++i) {
				var path = paths.Array[paths.Offset + i];
				if (path.Stroke != null) {
					RenderTriangles(
						ref paint,
						ref scissor, 
						fringe,
						fringe,
						renderingType, 
						path.Stroke.Value, 
						PrimitiveType.TriangleStrip
					);
				}
			}
		}

		public void RenderStroke(ref Paint paint, ref Scissor scissor, float fringe, float strokeWidth, ArraySegment<Path> paths)
		{
			for (var i = 0; i < paths.Count; ++i)  {
				var path = paths.Array[paths.Offset + i];
				if (path.Stroke != null) {
					RenderTriangles(
						ref paint, 
						ref scissor,
						strokeWidth, 
						fringe, 
						paint.Image != 0 ? RenderingType.FillImage : RenderingType.FillGradient,
						path.Stroke.Value,
						PrimitiveType.TriangleStrip
					);
				}
			}
		}

		private enum RenderingType
		{
			FillGradient,
			FillImage,
			FillStencil,
		}

		enum PrimitiveType
		{
			TriangleFan,
			TriangleStrip
		}
		
		private unsafe void RenderTriangles(
			ref Paint paint,
			ref Scissor scissor,
			float width, 
			float fringe,
			RenderingType renderingType,
			ArraySegment<Vertex> vertices,
			PrimitiveType primitiveType
		) {
			if (vertices.Count < 3) {
				return;
			}
			var innerColor = Color4.PremulAlpha(paint.InnerColor);
			var outerColor = Color4.PremulAlpha(paint.OuterColor);
			var strokeMult = (width * 0.5f + fringe * 0.5f) / fringe;
			var scissorTransform = new Transform();
			var scissorExt = new Vector2();
			var scissorScale = new Vector2();
			if (scissor.Extent.X < -0.5f || scissor.Extent.Y < -0.5f) {
				scissorTransform.Zero();
				scissorExt.X = 1.0f;
				scissorExt.Y = 1.0f;
				scissorScale.X = 1.0f;
				scissorScale.Y = 1.0f;
			} else {
				scissorTransform = scissor.Transform.BuildInverse();
				scissorExt.X = scissor.Extent.X;
				scissorExt.Y = scissor.Extent.Y;
				scissorScale.X =
					MathF.Sqrt(
						scissor.Transform.T1 * scissor.Transform.T1 +
						scissor.Transform.T3 * scissor.Transform.T3
					) / fringe;
				scissorScale.Y =
					MathF.Sqrt(
						scissor.Transform.T2 * scissor.Transform.T2 + 
					    scissor.Transform.T4 * scissor.Transform.T4
					) / fringe;
			}
			var transform = paint.Transform.BuildInverse();
			var p = new FillParams();
			p.ScissorU.X = scissorTransform.T1;
			p.ScissorU.Y = scissorTransform.T2;
			p.ScissorV.X = scissorTransform.T3;
			p.ScissorV.Y = scissorTransform.T4;
			p.ScissorT.X = scissorTransform.T5;
			p.ScissorT.Y = scissorTransform.T6;
			p.PaintU.X = transform.T1;
			p.PaintU.Y = transform.T2;
			p.PaintV.X = transform.T3;
			p.PaintV.Y = transform.T4;
			p.PaintT.X = transform.T5;
			p.PaintT.Y = transform.T6;
			p.InnerCol = innerColor.ToVector4();
			p.OuterCol = outerColor.ToVector4();
			p.ScissorExt = scissorExt;
			p.ScissorScale = scissorScale;
			p.Extent = paint.Extent;
			p.Radius = paint.Radius;
			p.Feather = paint.Feather;
			p.StrokeMult = strokeMult;
			p.StrokeThr = -1;
			p.TexType = 0;
			p.Type = (float)renderingType;
			var paramsChanged = false;
			var p1 = (long*)Unsafe.AsPointer(ref material.FillParams);
			var p2 = (long*)&p;
			for (int i = sizeof(FillParams) / 8; i > 0; i--) {
				if (*p1++ != *p2++) {
					paramsChanged = true;
					break;
				}
			}
			if (paramsChanged) {
				Renderer.Flush();
			}
			material.FillParams = p;
			if (primitiveType == PrimitiveType.TriangleFan) {
				Renderer.DrawTriangleFan(null, null, material, vertices.Array, vertices.Count, vertices.Offset);
			} else {
				Renderer.DrawTriangleStrip(null, null, material, vertices.Array, vertices.Count, vertices.Offset);
			}
		}
	}
}