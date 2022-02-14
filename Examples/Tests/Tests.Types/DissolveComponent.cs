using Lime;
using Yuzu;

namespace EmptyProject.Types
{
	[TangerineMenuPath("Effects/New Dissolve")]
	[TangerineRegisterComponent]
	public class DissolveComponent : MaterialComponent<EmptyProject.Types.DissolveMaterial>
	{
		[YuzuMember]
		public Blending Blending
		{
			get => CustomMaterial.Blending;
			set => CustomMaterial.Blending = value;
		}

		[YuzuMember]
		public Color4 MidGlowColor
		{
			get => CustomMaterial.MidGlowColor;
			set => CustomMaterial.MidGlowColor = value;
		}
		
		[YuzuMember]
		public Color4 EdgeGlowColor
		{
			get => CustomMaterial.EdgeGlowColor;
			set => CustomMaterial.EdgeGlowColor = value;
		}
		
		[YuzuMember]
		[TangerineValidRange(0.05f, 1f)]
		public float GlowRange
		{
			get => CustomMaterial.GlowRange;
			set => CustomMaterial.GlowRange = value;
		}

		[YuzuMember]
		[TangerineValidRange(-1f, 2f)]
		public float RangeOffset
		{
			get => CustomMaterial.RangeOffset;
			set => CustomMaterial.RangeOffset = value;
		}

		[YuzuMember]
		[TangerineValidRange(0f, 1f)]
		public float Smoothness
		{
			get => CustomMaterial.Smoothness;
			set => CustomMaterial.Smoothness = value;
		}

		[YuzuMember]
		public ITexture MaskTexture
		{
			get => CustomMaterial.MaskTexture;
			set => CustomMaterial.MaskTexture = value;
		}
		
		[YuzuMember]
		public Vector2 MaskUV0
		{
			get => CustomMaterial.MaskUV0;
			set => CustomMaterial.MaskUV0 = value;
		}
		
		[YuzuMember]
		public Vector2 MaskUV1
		{
			get => CustomMaterial.MaskUV1;
			set => CustomMaterial.MaskUV1 = value;
		}
	}
}
