using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Lime.NanoVG
{
	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	public struct FillParams
	{
		public Vector4 ScissorU;
		public Vector4 ScissorV;
		public Vector4 ScissorT;
		public Vector4 PaintU;
		public Vector4 PaintV;
		public Vector4 PaintT;
		public Vector4 InnerCol;
		public Vector4 OuterCol;
		public Vector2 ScissorExt;
		public Vector2 ScissorScale;
		public Vector2 Extent;
		public float Radius;
		public float Feather;
		public float StrokeMult;
		public float StrokeThr;
		public float TexType;
		public float Type;
	}

	public unsafe class Material : IMaterial
	{
		private readonly ShaderParams[] shaderParamsArray;
		private readonly ShaderParams shaderParams;
		private readonly ShaderParamKey<Vector4> paramArrayKey;

		public string Id { get; set; }
		public int PassCount => 1;

		public FillParams FillParams;

		public Material()
		{
			shaderParams = new ShaderParams();
			shaderParamsArray = new[] { Lime.Renderer.GlobalShaderParams, shaderParams };
			paramArrayKey = shaderParams.GetParamKey<Vector4>("frag");
		}

		public void Apply(int pass)
		{
			fixed (FillParams* p = &FillParams) {
				shaderParams.Set(paramArrayKey, (Vector4*)p, 11);
			}
			PlatformRenderer.SetBlendState(Blending.PremultipliedAlpha.GetBlendState());
			PlatformRenderer.SetShaderProgram(ShaderProgram.GetInstance());
			PlatformRenderer.SetShaderParams(shaderParamsArray);
		}

		public void Invalidate()
		{
		}
	}

	public class ShaderProgram : Lime.ShaderProgram
	{
		private static string vertexShaderText = @"
			attribute vec4 inPos;
			attribute vec4 inColor;
			attribute vec2 inTexCoords1;
			varying vec4 color;
			varying vec2 texCoords1;
			varying vec2 pos;
			uniform mat4 matProjection;
			void main()
			{
				pos = inPos.xy;
				gl_Position = matProjection * inPos;
				color = inColor;
				texCoords1 = inTexCoords1;
			}";

		private static readonly string fragmentShaderText = @"
			precision highp float;
			uniform vec4 frag[11];
			uniform sampler2D tex;
			varying vec2 texCoords1;
			varying vec2 pos;

			#define scissorMat mat3(frag[0].xyz, frag[1].xyz, frag[2].xyz)
			#define paintMat mat3(frag[3].xyz, frag[4].xyz, frag[5].xyz)
			#define innerCol frag[6]
			#define outerCol frag[7]
			#define scissorExt frag[8].xy
			#define scissorScale frag[8].zw
			#define extent frag[9].xy
			#define radius frag[9].z
			#define feather frag[9].w
			#define strokeMult frag[10].x
			#define strokeThr frag[10].y
			#define texType int(frag[10].z)
			#define type int(frag[10].w)

			float sdroundrect(vec2 pt, vec2 ext, float rad)
			{
				vec2 ext2 = ext - vec2(rad, rad);
				vec2 d = abs(pt) - ext2;
				return min(max(d.x, d.y), 0.0) + length(max(d, 0.0)) - rad;
			}
			
			// Scissoring
			float scissorMask(vec2 p)
			{
				vec2 sc = (abs((scissorMat * vec3(p, 1.0)).xy) - scissorExt);
				sc = vec2(0.5, 0.5) - sc * scissorScale;
				return clamp(sc.x, 0.0, 1.0) * clamp(sc.y, 0.0, 1.0);
			}

			// Stroke - from [0..1] to clipped pyramid, where the slope is 1px.
			// pow() makes edges more smooth.
			float strokeMask() 
			{
				return pow(min(1.0, (1.0 - abs(texCoords1.x * 2.0 - 1.0)) * strokeMult) * min(1.0, texCoords1.y), 0.7);
			}
			
			void main(void) 
			{
			    vec4 result;
				float scissor = scissorMask(pos);
				float strokeAlpha = strokeMask();
				if (strokeAlpha < strokeThr) {
					discard;
				}
				if (type == 0) {	// Gradient
					// Calculate gradient color using box gradient
					vec2 pt = (paintMat * vec3(pos, 1.0)).xy;
					float d = clamp((sdroundrect(pt, extent, radius) + feather * 0.5) / feather, 0.0, 1.0);
					vec4 color = mix(innerCol, outerCol, d);
					// Combine alpha
					color *= strokeAlpha * scissor;
					result = color;
				} else if (type == 1) {		// Image
					// Calculate color from texture
					vec2 pt = (paintMat * vec3(pos, 1.0)).xy / extent;
					vec4 color = texture2D(tex, pt);
					if (texType == 1) {
						color = vec4(color.xyz * color.w, color.w);
					}
					if (texType == 2) { 
						color = vec4(color.x);
					}
					// Apply color tint and alpha.
					color *= innerCol;
					// Combine alpha
					color *= strokeAlpha * scissor;
					result = color;
				} else if (type == 2) {		// Stencil fill
					result = vec4(1, 1, 1, 1);
				} else if (type == 3) {		// Textured tris
					vec4 color = texture2D(tex, texCoords1);
					if (texType == 1) {
						color = vec4(color.xyz * color.w, color.w);
					}
					if (texType == 2) {
						color = vec4(color.x);
					}
					color *= scissor;
					result = color * innerCol;
				}
				gl_FragColor = result;
			}";

		private ShaderProgram()
			: base(CreateShaders(), ShaderPrograms.Attributes.GetLocations(), GetSamplers())
		{
		}

		private static ShaderProgram instance;

		public static ShaderProgram GetInstance() => instance ??= new ShaderProgram();

		private static IEnumerable<Sampler> GetSamplers()
		{
			yield return new Sampler { Name = "tex", Stage = 0 };
		}

		private static IEnumerable<Shader> CreateShaders()
		{
			yield return new VertexShader(vertexShaderText);
			yield return new FragmentShader(fragmentShaderText);
		}
	}
}
