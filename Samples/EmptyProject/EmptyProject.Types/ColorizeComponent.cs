using Lime;
using System;
using Yuzu;

namespace EmptyProject.Types
{
	public class ColorizeMaterial : IMaterial
	{
		private readonly ShaderParams shaderParams;
		private readonly ShaderParams[] shaderParamsArray;

		public string Id { get; set; }
		public int PassCount => 1;

		private static class HighlightShader
		{
			private const string VertexShader = @"
				attribute vec4 inPos;
				attribute vec4 inColor;
				attribute vec2 inTexCoords1;

				uniform mat4 matProjection;

				varying lowp vec4 color;
				varying lowp vec2 texCoords;

				void main()
				{
					gl_Position = matProjection * inPos;
					texCoords = inTexCoords1;
					color = inColor;
				}";

			private const string FragmentShader = @"
				varying lowp vec2 texCoords;
				varying lowp vec4 color;

				uniform lowp sampler2D tex1;

				const lowp vec3 one = vec3(1.0);
				const lowp vec3 two = vec3(2.0);
				const lowp vec3 half3 = vec3(0.5);

				void main()
				{
					lowp vec4 dst = texture2D(tex1, texCoords);
					gl_FragColor.rgb = mix(
						one - two * (one - dst.rgb) * (one - color.rgb),
						two * dst.rgb * color.rgb,
						step(dst.rgb, half3)
					);

					gl_FragColor.a = color.a * dst.a;
				}";

			public static ShaderProgram Instance { get; } = Create();

			private static ShaderProgram Create()
			{
				var shaders = new Shader[] {
					new VertexShader(VertexShader),
					new FragmentShader(FragmentShader)
				};

				return new ShaderProgram(
					shaders,
					ShaderPrograms.Attributes.GetLocations(),
					ShaderPrograms.GetSamplers()
				);
			}
		}

		public ColorizeMaterial()
		{
			shaderParams = new ShaderParams();
			shaderParamsArray = new[] { Renderer.GlobalShaderParams, shaderParams };
		}

		public void Apply(int pass)
		{
			PlatformRenderer.SetShaderProgram(HighlightShader.Instance);
			PlatformRenderer.SetShaderParams(shaderParamsArray);
			PlatformRenderer.SetBlendState(Blending.Alpha.GetBlendState());
		}

		public void Invalidate()
		{
		}
	}

	[TangerineRegisterComponent]
	[MutuallyExclusiveDerivedComponents]
	[TangerineMenuPath("Effects/")]
	[TangerineTooltip(
		"Paints gray image with selected Color hue."
	)]
	public class ColorizeComponent : MaterialComponent<ColorizeMaterial>
	{
		public ColorizeComponent()
		{ }
	}
}
