using System.Linq;
using Lime;
using Yuzu;

namespace EmptyProject.Types
{
	public class DissolveMaterial : IMaterial
	{
		private readonly ShaderParams[] shaderParamsArray;
		private readonly ShaderParams shaderParams;
		private readonly ShaderParamKey<Vector3> rangeKey;
		private readonly ShaderParamKey<Vector4> midColorKey;
		private readonly ShaderParamKey<Vector4> edgeColorKey;
		private readonly ShaderParamKey<Vector2> maskUV0Key;
		private readonly ShaderParamKey<Vector2> maskUV1Key;
		private readonly ShaderParamKey<Vector3> cosPartOneKey;
		private readonly ShaderParamKey<Vector4> cosPartTwoKey;
		private readonly Vector4 seriesCoefficients;
		
		private Vector3 rangeInfo;
		private Vector3 cosPartOne;
		private Vector4 cosPartTwo;
		private bool shouldCalculateParameters;

		private float glowRange = 1f;
		private float rangeOffset = 0f;
		private float smoothness = 1f;
		
		[YuzuMember]
		public Blending Blending;

		[YuzuMember]
		public Color4 MidGlowColor { get; set; } = Color4.White;

		[YuzuMember]
		public Color4 EdgeGlowColor { get; set; } = Color4.White;

		[YuzuMember]
		public float GlowRange
		{
			get => glowRange;
			set
			{
				glowRange = value;
				shouldCalculateParameters = true;
			}
		}
		
		[YuzuMember]
		public float RangeOffset
		{
			get => rangeOffset;
			set
			{
				rangeOffset = value;
				shouldCalculateParameters = true;
			}
		}

		[YuzuMember]
		public float Smoothness
		{
			get => (smoothness - 0.1f) * (1f / 0.9f);
			set
			{
				smoothness = value * 0.9f + 0.1f;
				shouldCalculateParameters = true;
			}
		}

		private void CalculateParameters()
		{
			float rangeStart = rangeOffset;
			float rangeEnd = rangeStart + glowRange;
			float rangeCenter = rangeStart + glowRange / 2;
			rangeInfo = new Vector3(rangeStart, rangeEnd, rangeCenter);
			float min = Mathf.Cos(Mathf.Pi * smoothness);
			float resultMultiplier = 1f / (1f - min);
			float resultOffset = -min;
			cosPartOne = new Vector3(
				x: 2 * Mathf.Pi * smoothness / glowRange,
				y: -Mathf.Pi * smoothness,
				z: resultMultiplier + resultMultiplier * resultOffset
			);
			cosPartTwo = resultMultiplier * seriesCoefficients;
		}
		
		[YuzuMember]
		public ITexture MaskTexture;
		
		[YuzuMember]
		public Vector2 MaskUV0 = Vector2.Zero;
		
		[YuzuMember]
		public Vector2 MaskUV1 = Vector2.One;

		public string Id { get; set; }
		
		public int PassCount => 1;

		public DissolveMaterial()
		{
			// Coefficients for the first four members of the Maclaurin series of cos(x).
			seriesCoefficients = new Vector4(
				x: -1f / Factorial(2),
				y: +1f / Factorial(4),
				z: -1f / Factorial(6),
				w: +1f / Factorial(8)
			);
			CalculateParameters();
			shaderParams = new ShaderParams();
			shaderParamsArray = new[] { Renderer.GlobalShaderParams, shaderParams };
			midColorKey = shaderParams.GetParamKey<Vector4>("midColor");
			edgeColorKey = shaderParams.GetParamKey<Vector4>("edgeColor");
			rangeKey = shaderParams.GetParamKey<Vector3>("glowRange");
			maskUV0Key = shaderParams.GetParamKey<Vector2>("maskUV0");
			maskUV1Key = shaderParams.GetParamKey<Vector2>("maskUV1");
			cosPartOneKey = shaderParams.GetParamKey<Vector3>("cosPartOne");
			cosPartTwoKey = shaderParams.GetParamKey<Vector4>("cosPartTwo");
		}

		public void Apply(int pass)
		{
			if (shouldCalculateParameters) {
				shouldCalculateParameters = false;
				CalculateParameters();
			}
			shaderParams.Set(midColorKey, MidGlowColor.ToVector4());
			shaderParams.Set(edgeColorKey, EdgeGlowColor.ToVector4());
			shaderParams.Set(rangeKey, rangeInfo);
			shaderParams.Set(maskUV1Key, MaskUV1);
			shaderParams.Set(maskUV0Key, MaskUV0);
			shaderParams.Set(cosPartOneKey, cosPartOne);
			shaderParams.Set(cosPartTwoKey, cosPartTwo);
			PlatformRenderer.SetTexture(1, MaskTexture);
			PlatformRenderer.SetBlendState(Blending.GetBlendState());
			PlatformRenderer.SetShaderProgram(DissolveMaterialShaderProgram.Instance);
			PlatformRenderer.SetShaderParams(shaderParamsArray);
		}

		public void Invalidate() { }

		private static int Factorial(int n) => Enumerable.Range(1, n).Aggregate((res, i) => res * i);
		
		public class DissolveMaterialShaderProgram : ShaderProgram
		{
			private static DissolveMaterialShaderProgram instance;
			public static DissolveMaterialShaderProgram Instance => instance ??= new DissolveMaterialShaderProgram();
			
			private const string VertexShader = @"
				attribute vec4 inPos;
				attribute vec4 inColor;
				attribute vec2 inTexCoords1;
				attribute vec2 inTexCoords2;

				uniform mat4 matProjection;
				uniform lowp vec2 maskUV0;
				uniform lowp vec2 maskUV1;

				varying lowp vec2 texCoords1;
				varying lowp vec2 texCoords2;
				varying lowp vec4 outColor;

				void main()
				{
					gl_Position = matProjection * inPos;
					texCoords1 = inTexCoords1;
					texCoords2 = mix(maskUV0, maskUV1, inTexCoords2);
					outColor = inColor;
				}";

			private const string FragmentShader = @"
				precision highp float;
				uniform lowp sampler2D tex1;
				uniform highp sampler2D tex2;
				uniform highp vec3 glowRange;
				uniform highp vec4 midColor;
				uniform highp vec4 edgeColor;
				uniform highp vec3 cosPartOne;
				uniform highp vec4 cosPartTwo;

				varying lowp vec2 texCoords1;
				varying lowp vec2 texCoords2;
				varying lowp vec4 outColor;

				// -pi <= x <= +pi
				highp float CustomCos(highp float x)
				{
					// cosPartOne.xy - transformation for the domain of the function.
					// cosPartOne.z - first member for the Maclaurin series, which is multiplied by some coefficient.
					// cosPartTwo - coefficients of the first four members of the Maclaurin series, 
					//              they are also multiplied by some other coefficient.
					x = cosPartOne.x * x + cosPartOne.y;
					highp float x2 = x * x;
					highp float x4 = x2 * x2;
					return cosPartOne.z + dot(vec4(x2, x4, x2 * x4, x4 * x4), cosPartTwo);
				}

				void main()
				{
					lowp vec4 imageColor = texture2D(tex1, texCoords1);
					highp float mask = texture2D(tex2, texCoords2).r;
					// sgr - abbreviation for (s)tep (g)low (r)ange
					// glowRange.x - start of the glowing interval
					// glowRange.y - end of the glowing interval
					// glowRange.z - middle of the glowing interval
					highp vec3 sgr = step(glowRange, vec3(mask));
					// 0 if the pixel does not belong to the glowing interval
					// 1 if the pixel belongs to the glowing interval
					lowp float glowFactor = sgr.x * (1.0 - sgr.y);
					highp float mixFactor = CustomCos(mask - glowRange.x);
					// the end of the custom cos region }
					lowp vec4 glowColor = mix(edgeColor, midColor, glowFactor * mixFactor);
					// If the mask pixel belongs to the first half of the glowing interval 
					// we must pass glowColor to gl_FragColor. If the mask pixel belongs 
					// to the second half of the interval we have to perform blending for 
					// glowColor with imageColor, and then pass the result to gl_FragColor.
					glowColor.a *= imageColor.a;
					gl_FragColor = mix(glowColor, imageColor, max(sgr.z * (1.0 - glowColor.a), sgr.y));
					gl_FragColor.a = sgr.x * mix(gl_FragColor.a, imageColor.a, sgr.z);
					gl_FragColor *= outColor;
				}";

			private DissolveMaterialShaderProgram() 
				: base(CreateShaders(), ShaderPrograms.Attributes.GetLocations(), ShaderPrograms.GetSamplers()) 
			{ }

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
