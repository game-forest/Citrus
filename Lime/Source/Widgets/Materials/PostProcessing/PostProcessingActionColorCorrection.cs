namespace Lime
{
	internal class PostProcessingActionColorCorrection : PostProcessingAction
	{
		public override bool EnabledCheck(PostProcessingRenderObject ro) => ro.HSLEnabled;
		public override PostProcessingAction.Buffer GetTextureBuffer(PostProcessingRenderObject ro)
		{
			return ro.ColorCorrectionBuffer;
		}

		public override void Do(PostProcessingRenderObject ro)
		{
			if (ro.ColorCorrectionBuffer.EqualRenderParameters(ro)) {
				ro.ProcessedTexture = ro.ColorCorrectionBuffer.Texture;
				ro.CurrentBufferSize = (Vector2)ro.ColorCorrectionBuffer.Size;
				ro.ProcessedUV1 = (Vector2)ro.ViewportSize / ro.CurrentBufferSize;
				return;
			}

			ro.PrepareOffscreenRendering(ro.Size);
			ro.ColorCorrectionMaterial.HSL = WrappedHSL(ro.HSL);
			ro.ColorCorrectionMaterial.Brightness = ro.Brightness;
			ro.ColorCorrectionMaterial.Contrast = ro.Contrast;
			ro.RenderToTexture(
				ro.ColorCorrectionBuffer.Texture,
				ro.ProcessedTexture,
				ro.ColorCorrectionMaterial,
				Color4.White,
				ro.TextureClearingColor
			);

			ro.ColorCorrectionBuffer.SetRenderParameters(ro);
			ro.MarkBuffersAsDirty = true;
			ro.ProcessedTexture = ro.ColorCorrectionBuffer.Texture;
			ro.CurrentBufferSize = (Vector2)ro.ColorCorrectionBuffer.Size;
			ro.ProcessedUV1 = (Vector2)ro.ViewportSize / ro.CurrentBufferSize;
		}

		internal new class Buffer : PostProcessingAction.Buffer
		{
			private Vector3 hsl = new Vector3(float.NaN, float.NaN, float.NaN);
			private float brightness;
			private float contrast;
			private Color4 textureClearingColor;

			public Buffer(Size size) : base(size) { }

			public bool EqualRenderParameters(PostProcessingRenderObject ro)
			{
				return !IsDirty
					&& hsl == WrappedHSL(ro.HSL)
					&& brightness == ro.Brightness
					&& contrast == ro.Contrast
					&& textureClearingColor == ro.TextureClearingColor;
			}

			public void SetRenderParameters(PostProcessingRenderObject ro)
			{
				IsDirty = false;
				hsl = WrappedHSL(ro.HSL);
				brightness = ro.Brightness;
				contrast = ro.Contrast;
				textureClearingColor = ro.TextureClearingColor;
			}
		}

		private static Vector3 WrappedHSL(Vector3 hsl)
		{
			return new Vector3(Mathf.Wrap(hsl.X, -0.5f, 0.5f), Mathf.Clamp(hsl.Y, 0f, 2f), Mathf.Clamp(hsl.Z, 0f, 2f));
		}
	}
}
