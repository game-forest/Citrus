using System.Collections.Generic;
using System.Text;
using Yuzu;

namespace Lime
{
	[TangerineRegisterNode(Order = 7)]
	[TangerineAllowedParentTypes(typeof(ParticleEmitter))]
	public class ParticleModifier : Node
	{
		[YuzuMember]
		[TangerineKeyframeColor(18)]
		[TangerineRatioInfo(typeof(ParticleModifier))]
		[TangerineSizeInfo(typeof(ParticleModifier))]
		public Vector2 Size { get; set; }

		[YuzuMember]
		[TangerineKeyframeColor(19)]
		public Vector2 Scale { get; set; }

		[YuzuMember]
		[TangerineKeyframeColor(20)]
		public float Velocity { get; set; }

		[YuzuMember]
		[TangerineKeyframeColor(21)]
		public float Spin { get; set; }

		[YuzuMember]
		[TangerineKeyframeColor(22)]
		public float AngularVelocity { get; set; }

		[YuzuMember]
		[TangerineKeyframeColor(23)]
		public float GravityAmount { get; set; }

		[YuzuMember]
		[TangerineKeyframeColor(24)]
		public float WindAmount { get; set; }

		[YuzuMember]
		[TangerineKeyframeColor(25)]
		public float MagnetAmount { get; set; }

		[YuzuMember]
		[TangerineKeyframeColor(9)]
		public Color4 Color { get; set; }

		[YuzuMember]
		[TangerineKeyframeColor(11)]
		public int FirstFrame { get; set; }

		[YuzuMember]
		[TangerineKeyframeColor(12)]
		public int LastFrame { get; set; }

		[YuzuMember]
		[TangerineKeyframeColor(13)]
		public float AnimationFps { get; set; }

		[YuzuMember]
		[TangerineKeyframeColor(14)]
		public bool LoopedAnimation { get; set; }

		private ITexture texture = new SerializableTexture();

		[YuzuMember]
		[TangerineKeyframeColor(27)]
		public ITexture Texture { get { return texture; } set { texture = value; textures = null; } }

		public ParticleModifier()
		{
			RenderChainBuilder = null;
			Size = Widget.DefaultWidgetSize;
			Scale = Vector2.One;
			Velocity = 1;
			Spin = 1;
			AngularVelocity = 1;
			WindAmount = 1;
			GravityAmount = 1;
			MagnetAmount = 1;
			Color = Color4.White;
			FirstFrame = 1;
			LastFrame = 1;
			AnimationFps = 20;
			LoopedAnimation = true;
		}

		private static bool ChangeTextureFrameIndex(ref string path, int frame)
		{
			if (frame < 0 || frame > 99)
				return false;
			int i = path.Length;
			//for (; i >= 0; i--)
			//	if (path[i] == '.')
			//		break;
			if (i < 2)
				return false;
			if (char.IsDigit(path, i - 1) && char.IsDigit(path, i - 2)) {
				var s = new StringBuilder(path);
				s[i - 1] = (char)(frame % 10 + '0');
				s[i - 2] = (char)(frame / 10 + '0');
				path = s.ToString();
				return true;
			}
			return false;
		}

		private List<SerializableTexture> textures;

		public ITexture GetTexture(int index)
		{
			if (FirstFrame == LastFrame) {
				return texture;
			}
			if (textures == null && texture is SerializableTexture st) {
				textures = new List<SerializableTexture>();
				var path = st.SerializationPath;
				for (int i = 0; i < 100; i++) {
					if (!ChangeTextureFrameIndex(ref path, i))
						break;
					if (AssetBundle.Current.FileExists(path + ".atlasPart") ||
						AssetBundle.Current.FileExists(path + ".png")
					) {
						var t = new SerializableTexture(path);
						textures.Add(t);
					} else if (i > 0)
						break;
				}
			}
			if (textures.Count == 0)
				return texture;
			index = Mathf.Clamp(index, 0, textures.Count - 1);
			return textures[index];
		}

		public override void AddToRenderChain(RenderChain chain)
		{
		}
	}
}
