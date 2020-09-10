using System;
using Yuzu;

namespace Lime
{
	public class TwistMaterial : IMaterial
	{
		private readonly ShaderParams[] shaderParamsArray;
		private readonly ShaderParams shaderParams;
		private readonly ShaderParamKey<float> angleKey;
		private readonly ShaderParamKey<Vector2> uv0Key;
		private readonly ShaderParamKey<Vector2> uv1Key;
		private readonly ShaderParamKey<Vector2> pivotKey;
		private readonly ShaderParamKey<float> radiusKey;

		[YuzuMember]
		public float Angle = 0;
		[YuzuMember]
		public Vector2 UV0;
		[YuzuMember]
		public Vector2 UV1;
		[YuzuMember]
		public Blending Blending;
		[YuzuMember]
		public Vector2 Pivot = new Vector2(0.5f, 0.5f);
		[YuzuMember]
		[TangerineValidRange(0.0f, float.PositiveInfinity)]
		public float Radius = 1;

		public string Id { get; set; }
		public int PassCount => 1;

		public TwistMaterial()
		{
			shaderParams = new ShaderParams();
			shaderParamsArray = new[] { Renderer.GlobalShaderParams, shaderParams };
			angleKey = shaderParams.GetParamKey<float>("angle");
			uv0Key = shaderParams.GetParamKey<Vector2>("uv0");
			uv1Key = shaderParams.GetParamKey<Vector2>("uv1");
			pivotKey = shaderParams.GetParamKey<Vector2>("pivot");
			radiusKey = shaderParams.GetParamKey<float>("radius");
		}

		public void Apply(int pass)
		{
			shaderParams.Set(angleKey, Angle);
			shaderParams.Set(uv1Key, UV1);
			shaderParams.Set(uv0Key, UV0);
			shaderParams.Set(pivotKey, Pivot);
			// coefficient 1.11104f for backward compatibility
			shaderParams.Set(radiusKey, 1.0f / Radius * 1.11104f);
			PlatformRenderer.SetBlendState(Blending.GetBlendState());
			PlatformRenderer.SetShaderProgram(TwistShaderProgram.Instance);
			PlatformRenderer.SetShaderParams(shaderParamsArray);
		}

		public void Invalidate() { }

		public class TwistShaderProgram : ShaderProgram
		{
			private static TwistShaderProgram instance;
			public static TwistShaderProgram Instance => instance ?? (instance = new TwistShaderProgram());

			private const string VertexShader = @"
				attribute vec4 inPos;
				attribute vec4 inColor;
				attribute vec2 inTexCoords1;

				uniform mat4 matProjection;

				varying lowp vec2 texCoords1;
				varying lowp vec4 outColor;

				void main()
				{
					gl_Position = matProjection * inPos;
					texCoords1 = inTexCoords1;
					outColor = inColor;
				}";

			private const string FragmentShader = @"
				uniform lowp sampler2D tex1;
				uniform lowp float angle;
				uniform lowp vec2 uv0;
				uniform lowp vec2 uv1;
				uniform lowp vec2 pivot;
				uniform lowp float radius;

				varying lowp vec2 texCoords1;
				varying lowp vec4 outColor;

				void main()
				{
					lowp vec2 localUV = (texCoords1 - uv0) / (uv1 - uv0) - pivot;
					lowp float newAngle = angle * pow(max(0.0, 0.8 - length(localUV) * radius), 3.0);
					lowp float cosAngle = cos(newAngle);
					lowp float sinAngle = sin(newAngle);
					lowp vec2 uv = clamp(mat2(cosAngle, -sinAngle, sinAngle, cosAngle) * localUV + pivot, vec2(0), vec2(1));
					gl_FragColor = texture2D(tex1, uv0 + uv * (uv1 - uv0)) * outColor;
				}";

			private TwistShaderProgram() : base(CreateShaders(), ShaderPrograms.Attributes.GetLocations(), ShaderPrograms.GetSamplers()) { }

			private static Shader[] CreateShaders()
			{
				return new Shader[] {
					new VertexShader(VertexShader),
					new FragmentShader(FragmentShader)
				};
			}
		}
	}
}
