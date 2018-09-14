using Yuzu;

namespace Lime
{
	// TODO: Attribute to exclude components with custom presenters and/or custom render chain builders
	[TangerineRegisterComponent]
	[AllowedComponentOwnerTypes(typeof(Widget))]
	public class PostProcessingComponent : NodeComponent
	{
		private const float MinimumTextureScaling = 0.01f;
		private const float MaximumTextureScaling = 1f;
		private const float MaximumBlurRadius = 10f;
		private const float MaximumGammaCorrection = 10f;

		internal BlurMaterial BlurMaterial { get; private set; } = new BlurMaterial();
		internal BloomMaterial BloomMaterial { get; private set; } = new BloomMaterial();

		private PostProcessingPresenter presenter = new PostProcessingPresenter();
		private IPresenter savedPresenter;
		private PostProcessingRenderChainBuilder renderChainBuilder = new PostProcessingRenderChainBuilder();
		private IRenderChainBuilder savedRenderChainBuilder;
		private float blurRadius = 1f;
		private float blurTextureScaling = 1f;
		private float blurAlphaCorrection = 1f;
		private float bloomStrength = 1f;
		private float bloomBrightThreshold = 1f;
		private Vector3 bloomGammaCorrection = Vector3.One;
		private float bloomTextureScaling = 0.5f;

		[YuzuMember]
		[TangerineGroup("Blur effect")]
		public float BlurRadius
		{
			get => blurRadius;
			set => blurRadius = Mathf.Clamp(value, 0f, MaximumBlurRadius);
		}

		[YuzuMember]
		[TangerineGroup("Blur effect")]
		public float BlurTextureScaling
		{
			get => blurTextureScaling;
			set => blurTextureScaling = Mathf.Clamp(value, MinimumTextureScaling, MaximumTextureScaling);
		}

		[YuzuMember]
		[TangerineGroup("Blur effect")]
		public float BlurAlphaCorrection
		{
			get => blurAlphaCorrection;
			set => blurAlphaCorrection = Mathf.Clamp(value, 1f, MaximumGammaCorrection);
		}

		[YuzuMember]
		[TangerineGroup("Blur effect")]
		public Color4 BlurBackgroundColor { get; set; } = new Color4(127, 127, 127, 0);

		[YuzuMember]
		[TangerineGroup("Bloom effect")]
		public bool BloomEnabled { get; set; }

		[YuzuMember]
		[TangerineGroup("Bloom effect")]
		public float BloomStrength
		{
			get => bloomStrength;
			set => bloomStrength = Mathf.Clamp(value, 0f, MaximumBlurRadius);
		}

		[YuzuMember]
		[TangerineGroup("Bloom effect")]
		public float BloomBrightThreshold
		{
			get => bloomBrightThreshold;
			set => bloomBrightThreshold = Mathf.Clamp(value, 0f, 1f);
		}

		[YuzuMember]
		[TangerineGroup("Bloom effect")]
		public Vector3 BloomGammaCorrection
		{
			get => bloomGammaCorrection;
			set => bloomGammaCorrection = new Vector3(
				Mathf.Clamp(value.X, 0f, MaximumGammaCorrection),
				Mathf.Clamp(value.Y, 0f, MaximumGammaCorrection),
				Mathf.Clamp(value.Y, 0f, MaximumGammaCorrection)
			);
		}

		[YuzuMember]
		[TangerineGroup("Bloom effect")]
		public float BloomTextureScaling
		{
			get => bloomTextureScaling;
			set => bloomTextureScaling = Mathf.Clamp(value, MinimumTextureScaling, MaximumTextureScaling);
		}

		public void GetOwnerRenderObjects(RenderChain renderChain, RenderObjectList roObjects)
		{
			Owner.RenderChainBuilder = savedRenderChainBuilder;
			Owner.Presenter = savedPresenter;
			Owner.AddToRenderChain(renderChain);
			renderChain.GetRenderObjects(roObjects);
			Owner.RenderChainBuilder = renderChainBuilder;
			Owner.Presenter = presenter;
		}

		protected override void OnOwnerChanged(Node oldOwner)
		{
			base.OnOwnerChanged(oldOwner);
			if (oldOwner != null) {
				oldOwner.Presenter = savedPresenter;
				oldOwner.RenderChainBuilder = savedRenderChainBuilder;
				renderChainBuilder.Owner = null;
			}
			if (Owner != null) {
				savedPresenter = Owner.Presenter;
				Owner.Presenter = presenter;
				savedRenderChainBuilder = Owner.RenderChainBuilder;
				Owner.RenderChainBuilder = renderChainBuilder;
				renderChainBuilder.Owner = Owner.AsWidget;
			}
		}

		public override NodeComponent Clone()
		{
			var clone = (PostProcessingComponent)base.Clone();
			clone.presenter = (PostProcessingPresenter)presenter.Clone();
			clone.renderChainBuilder = (PostProcessingRenderChainBuilder)renderChainBuilder.Clone(null);
			clone.BlurMaterial = (BlurMaterial)BlurMaterial.Clone();
			clone.BloomMaterial = (BloomMaterial)BloomMaterial.Clone();
			return clone;
		}
	}
}
