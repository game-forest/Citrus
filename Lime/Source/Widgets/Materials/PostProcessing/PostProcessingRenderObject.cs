namespace Lime
{
	internal class PostProcessingRenderObject : RenderObject
	{
		private bool wasOffscreenRenderingPrepared;

		internal ITexture ProcessedTexture;
		internal Viewport ProcessedViewport;
		internal Vector2 ProcessedUV1;
		internal Size ViewportSize;
		internal Vector2 TextureSize;
		public Vector2 CurrentBufferSize;

		public readonly RenderObjectList Objects = new RenderObjectList();
		public PostProcessingAction[] PostProcessingActions;
		public IMaterial Material;
		public Matrix32 LocalToWorldTransform;
		public Vector2 Position;
		public Vector2 Size;
		public Color4 Color;
		public Vector2 UV0;
		public Vector2 UV1;
		public PostProcessingPresenter.DebugViewMode DebugViewMode;
		public bool MarkBuffersAsDirty;
		public PostProcessingAction.Buffer SourceTextureBuffer;
		public float SourceTextureScaling;
		public PostProcessingAction.Buffer FirstTemporaryBuffer;
		public PostProcessingAction.Buffer SecondTemporaryBuffer;
		public PostProcessingActionColorCorrection.Buffer ColorCorrectionBuffer;
		public ColorCorrectionMaterial ColorCorrectionMaterial;
		public bool HSLEnabled;
		public Vector3 HSL;
		public float Brightness;
		public float Contrast;
		public PostProcessingActionBlur.Buffer BlurBuffer;
		public BlurMaterial BlurMaterial;
		public bool BlurEnabled;
		public float BlurRadius;
		public BlurShaderId BlurShader;
		public float BlurTextureScaling;
		public float BlurAlphaCorrection;
		public PostProcessingActionBloom.Buffer BloomBuffer;
		public BloomMaterial BloomMaterial;
		public bool BloomEnabled;
		public float BloomStrength;
		public BlurShaderId BloomShaderId;
		public float BloomBrightThreshold;
		public Vector3 BloomGammaCorrection;
		public float BloomTextureScaling;
		public Color4 BloomColor;
		public PostProcessingActionDistortion.Buffer DistortionBuffer;
		public DistortionMaterial DistortionMaterial;
		public bool DistortionEnabled;
		public float DistortionBarrelPincushion;
		public float DistortionChromaticAberration;
		public float DistortionRed;
		public float DistortionGreen;
		public float DistortionBlue;
		public PostProcessingActionSharpen.Buffer SharpenBuffer;
		public SharpenMaterial SharpenMaterial;
		public bool SharpenEnabled;
		public float SharpenStrength;
		public float SharpenLimit;
		public float SharpenStep;
		public PostProcessingActionNoise.Buffer NoiseBuffer;
		public NoiseMaterial NoiseMaterial;
		public bool NoiseEnabled;
		public float NoiseBrightThreshold;
		public float NoiseDarkThreshold;
		public float NoiseSoftLight;
		public Vector2 NoiseOffset;
		public Vector2 NoiseScale;
		public ITexture NoiseTexture;
		public PostProcessingActionFXAA.Buffer FXAABuffer;
		public FXAAMaterial FXAAMaterial;
		public bool FXAAEnabled;
		public float FXAALumaTreshold;
		public float FXAAMulReduce;
		public float FXAAMinReduce;
		public float FXAAMaxSpan;
		public bool OverallImpactEnabled;
		public Color4 OverallImpactColor;
		public VignetteMaterial VignetteMaterial;
		public Texture2D TransparentTexture;
		public bool VignetteEnabled;
		public float VignetteRadius;
		public float VignetteSoftness;
		public Vector2 VignetteScale;
		public Vector2 VignettePivot;
		public Color4 VignetteColor;
		public IMaterial AlphaDiffuseMaterial;
		public IMaterial AddDiffuseMaterial;
		public IMaterial OpaqueDiffuseMaterial;
		public Color4 TextureClearingColor;

		public bool IsNotDebugViewMode => DebugViewMode == PostProcessingPresenter.DebugViewMode.None;

		protected override void OnRelease()
		{
			ProcessedTexture = null;
			Objects.Clear();
			PostProcessingActions = null;
			Material = null;
			SourceTextureBuffer = null;
			FirstTemporaryBuffer = null;
			SecondTemporaryBuffer = null;
			ColorCorrectionBuffer = null;
			ColorCorrectionMaterial = null;
			BlurBuffer = null;
			BlurMaterial = null;
			BloomBuffer = null;
			BloomMaterial = null;
			DistortionBuffer = null;
			DistortionMaterial = null;
			SharpenBuffer = null;
			SharpenMaterial = null;
			NoiseBuffer = null;
			NoiseTexture = null;
			NoiseMaterial = null;
			FXAABuffer = null;
			FXAAMaterial = null;
			VignetteMaterial = null;
			TransparentTexture = null;
			AlphaDiffuseMaterial = null;
			AddDiffuseMaterial = null;
			OpaqueDiffuseMaterial = null;
		}

		public override void Render()
		{
			wasOffscreenRenderingPrepared = false;
			try {
				if (!IsNotDebugViewMode) {
					MarkBuffersAsDirty = true;
				}
				foreach (var action in PostProcessingActions) {
					var buffer = action.GetTextureBuffer(this);
					if (MarkBuffersAsDirty) {
						buffer?.MarkAsDirty();
					}
					if (action.EnabledCheck(this)) {
						action.Do(this);
						if (buffer != null) {
							buffer.WasApplied = true;
						}
					} else if (buffer?.WasApplied ?? false) {
						buffer.WasApplied = false;
						buffer.MarkAsDirty();
						MarkBuffersAsDirty = true;
					}
				}
			} finally {
				FinalizeOffscreenRendering();
			}
		}

		internal void RenderToTexture(
			ITexture renderTargetTexture,
			ITexture sourceTexture,
			IMaterial material,
			Color4 color,
			Color4 backgroundColor,
			Size? customViewportSize = null,
			Vector2? customUV1 = null
		) {
			var vs = customViewportSize ?? ViewportSize;
			var uv1 = customUV1 ?? ProcessedUV1;
			if (ProcessedViewport.Width != vs.Width || ProcessedViewport.Height != vs.Height) {
				Renderer.Viewport = ProcessedViewport = new Viewport(0, 0, vs.Width, vs.Height);
			}
			renderTargetTexture.SetAsRenderTarget();
			try {
				Renderer.Clear(backgroundColor);
				Renderer.DrawSprite(
					texture1: sourceTexture,
					texture2: null,
					material: material,
					color: color,
					position: Vector2.Zero,
					size: TextureSize,
					uv0t1: Vector2.Zero,
					uv1t1: uv1,
					uv0t2: Vector2.Zero,
					uv1t2: Vector2.Zero
				);
			} finally {
				renderTargetTexture.RestoreRenderTarget();
			}
		}

		internal void RenderTexture(ITexture texture, IMaterial customMaterial = null, Vector2? customUV1 = null)
		{
			var uv1 = customUV1 ?? ProcessedUV1;
			Renderer.Transform1 = LocalToWorldTransform;
			Renderer.DrawSprite(
				texture1: texture,
				texture2: null,
				material: customMaterial ?? Material,
				color: Color,
				position: Position,
				size: Size,
				uv0t1: UV0 * uv1,
				uv1t1: UV1 * uv1,
				uv0t2: Vector2.Zero,
				uv1t2: Vector2.Zero
			);
		}

		internal void PrepareOffscreenRendering(Vector2 orthogonalProjection)
		{
			if (wasOffscreenRenderingPrepared) {
				return;
			}
			Renderer.PushState(
				RenderState.Viewport |
				RenderState.World |
				RenderState.View |
				RenderState.Projection |
				RenderState.DepthState |
				RenderState.ScissorState |
				RenderState.CullMode |
				RenderState.Transform2
			);
			wasOffscreenRenderingPrepared = true;
			Renderer.ScissorState = ScissorState.ScissorDisabled;
			Renderer.World = Renderer.View = Matrix44.Identity;
			Renderer.SetOrthogonalProjection(Vector2.Zero, orthogonalProjection);
			Renderer.DepthState = DepthState.DepthDisabled;
			Renderer.CullMode = CullMode.None;
			Renderer.Transform2 = Matrix32.Identity;
			Renderer.Transform1 = Matrix32.Identity;
		}

		internal void FinalizeOffscreenRendering()
		{
			if (!wasOffscreenRenderingPrepared) {
				return;
			}
			Renderer.PopState();
			wasOffscreenRenderingPrepared = false;
		}
	}
}
