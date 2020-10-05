#if PROFILER
using System;
using System.Collections.Generic;

namespace Lime.Profiler.Graphics
{
	/// <summary>
	/// Switch for overdraw mode.
	/// </summary>
	public static class Overdraw
	{
		/// <summary>
		/// Use to enable or disable overdraw mode.
		/// </summary>
		public static bool Enabled { get; set; }

		/// <summary>
		/// Use to determine if overdraw is enabled on the update thread.
		/// </summary>
		public static bool EnabledAtUpdateThread { get; private set; }

		/// <summary>
		/// Use to determine if overdraw is enabled on the render thread.
		/// </summary>
		public static bool EnabledAtRenderThread { get; private set; }

		/// <summary>
		/// Use to enable or disable overdraw metric.
		/// </summary>
		/// <remarks>
		/// Obtaining a metric is a very expensive operation.
		/// Turn off to increase performance.
		/// </remarks>
		public static bool MetricRequired { get; set; }

		/// <summary>
		/// Use to determine if overdraw metric required is enabled on the update thread.
		/// </summary>
		public static bool MetricRequiredAtUpdateThread { get; private set; }

		/// <summary>
		/// Use to determine if overdraw metric required is enabled on the render thread.
		/// </summary>
		public static bool MetricRequiredAtRenderThread { get; private set; }

		/// <summary>
		/// Invoked when the metric is generated for the next frame.
		/// Params: avg overdraw, pixels count.
		/// </summary>
		public static event Action<float, int> MetricCreated;

		public static void InvokeMetricCreated(float avgOverdraw, int pixelsCount) =>
			MetricCreated?.Invoke(avgOverdraw, pixelsCount);

		public static void UpdateStarted()
		{
			EnabledAtUpdateThread = Enabled;
			MetricRequiredAtUpdateThread = MetricRequired;
		}

		/// <summary>
		/// Occurs after update and before render. The previous render is guaranteed to be completed.
		/// </summary>
		public static void Sync()
		{
			EnabledAtRenderThread = EnabledAtUpdateThread;
			MetricRequiredAtRenderThread = MetricRequiredAtUpdateThread;
		}
	}

	public static class OverdrawForeground
	{
		private static RenderObjectList freeRenderList = new RenderObjectList();
		private static RenderObjectList preparedRenderList = new RenderObjectList();
		public static RenderChain RenderChain { get; private set; } = new RenderChain();

		/// <summary>
		/// Occurs after update and before render. The previous render is guaranteed to be completed.
		/// </summary>
		public static void Sync()
		{
			var list = preparedRenderList;
			preparedRenderList = freeRenderList;
			freeRenderList = list;
		}

		/// <remarks>
		/// Call before Sync().
		/// </remarks>
		public static void GetRenderObjects()
		{
			if (Overdraw.EnabledAtUpdateThread) {
				freeRenderList.Clear();
				RenderChain.GetRenderObjects(freeRenderList);
				RenderChain.Clear();
			}
		}

		/// <summary>
		/// Draw OverdrawForeground.
		/// </summary>
		public static void Render() => preparedRenderList.Render();
	}

	/// <summary>
	/// Forces the Renderer to use the Overdraw version of the materials.
	/// </summary>
	public static class OverdrawMaterialsScope
	{
		private static int scopesCounter = 0;

		/// <summary>
		/// Indicates whether we are inside the OverdrawMaterialsScope or not.
		/// </summary>
		public static bool IsInside => scopesCounter > 0;

		public static void Enter() => ++scopesCounter;
		public static void Leave() => --scopesCounter;
	}

	public static class OverdrawShaderProgram
	{
		/// <summary>
		/// Since the recording and storage of information about overdraw is carried out
		/// using the red channel of the rendering target, 256 states are available to us.
		/// </summary>
		public const int StatesCount = 256;

		/// <summary>
		/// These 256 states are represented in the range from 0 to 1.
		/// </summary>
		public static readonly float Step = 1f / StatesCount;

		public static readonly BlendState DefaultBlending;

		static OverdrawShaderProgram()
		{
			DefaultBlending = new BlendState {
				Enable = true,
				BlendFunc = BlendFunc.Add,
				SrcBlend = Blend.One,
				DstBlend = Blend.One
			};
		}
	}

	public class RenderTargetsQueue
	{
		private readonly Queue<RenderTexture> freeTargets = new Queue<RenderTexture>();

		/// <summary>
		/// Creates a new or reuses a free OverdrawRenderTarget.
		/// </summary>
		/// <param name="size">Render target size.</param>
		public RenderTexture Acquire(Size size)
		{
			var renderTarget = freeTargets.Count == 0 ? null : freeTargets.Dequeue();
			if (renderTarget == null || renderTarget.ImageSize != size) {
				renderTarget?.Dispose();
				renderTarget = new RenderTexture(size.Width, size.Height);
			}
			return renderTarget;
		}

		/// <summary>
		/// Add the render target to the reuse queue.
		/// </summary>
		public void Free(RenderTexture renderTarget) => freeTargets.Enqueue(renderTarget);
	}

	/// <summary>
	/// Allows you to interpret the overdraw results.
	/// </summary>
	public static class OverdrawInterpreter
	{
		/// <summary>
		/// Default gradient to interpret the overdraw results.
		/// </summary>
		public static readonly ColorGradient DefaultGradient = GetDefaultGradient();

		private static ColorGradient gradient;
		private static Texture2D gradientTexture;
		private static Color4[] gradientRasterizationTarget;

		static OverdrawInterpreter()
		{
			gradientTexture = new Texture2D {
				 TextureParams = new TextureParams {
					 WrapMode = TextureWrapMode.Clamp,
					 MinMagFilter = TextureFilter.Nearest,
				 }
			};
			Gradient = DefaultGradient;
		}

		/// <summary>
		/// Gets and sets the gradient to interpret the overdraw results.
		/// </summary>
		public static ColorGradient Gradient
		{
			get { return gradient; }
			set {
				gradient = value;
				if (gradientRasterizationTarget == null) {
					gradientRasterizationTarget = new Color4[OverdrawShaderProgram.StatesCount];
				}
				gradient.Rasterize(ref gradientRasterizationTarget);
				gradientTexture.LoadImage(gradientRasterizationTarget, OverdrawShaderProgram.StatesCount, 1);
			}
		}

		/// <summary>
		/// Ensures that the buffer is large enough to accommodate all the pixels in the texture.
		/// </summary>
		/// <param name="rawResults">The texture that determines the size of the buffer.</param>
		/// <param name="buffer">The buffer to be resized.</param>
		public static void EnsureEnoughBufferSize(RenderTexture rawResults, ref Color4[] buffer)
		{
			int requiredPixelsCount = rawResults.PixelsCount;
			if (buffer.Length < requiredPixelsCount) {
				Array.Resize(ref buffer, requiredPixelsCount);
			}
		}

		/// <summary>
		/// Returns average overdraw.
		/// </summary>
		public static float GetAverageOverdraw(Color4[] pixelsBuffer, int pixelsCount)
		{
			float counter = 0;
			unchecked {
				for (int i = 0; i < pixelsCount; ++i) {
					counter += pixelsBuffer[i].R;
				}
			}
			return counter / pixelsCount;
		}

		/// <summary>
		/// This method draws a texture and maps its red channel to the <see cref="Gradient"/>.
		/// Initially, the overdraw results are represented by the red channel.
		/// </summary>
		/// <param name="rawResults">The buffer into which the objects were rendered.</param>
		/// <param name="transform">Transform in which you want to draw the buffer.</param>
		/// <param name="imageSize">Size to stretch the image.</param>
		public static void DrawResults(RenderTexture rawResults, Matrix32 transform, Size imageSize)
		{
			Renderer.PushState(RenderState.Transform1);
			Renderer.Transform1 = transform;
			Renderer.DrawSprite(
				texture1: rawResults, texture2: gradientTexture,
				material: Material.Instance, color: Color4.Black,
				position: Vector2.Zero, size: (Vector2)imageSize,
				uv0t1: Vector2.Zero, uv1t1: Vector2.One, uv0t2: Vector2.Zero, uv1t2: Vector2.Zero);
			Renderer.PopState();
		}

		private static ColorGradient GetDefaultGradient()
		{
			int statesCount = OverdrawShaderProgram.StatesCount;
			var gradient = new ColorGradient();
			gradient.Add(new GradientControlPoint(new Color4(0, 0, 0, 255), 0f / statesCount));
			gradient.Add(new GradientControlPoint(new Color4(0, 0, 102, 255), 1f / statesCount));
			gradient.Add(new GradientControlPoint(new Color4(0, 76, 255, 255), 2f / statesCount));
			gradient.Add(new GradientControlPoint(new Color4(0, 178, 102, 255), 3f / statesCount));
			gradient.Add(new GradientControlPoint(new Color4(0, 255, 0, 255), 4f / statesCount));
			gradient.Add(new GradientControlPoint(new Color4(204, 204, 0, 255), 5f / statesCount));
			gradient.Add(new GradientControlPoint(new Color4(255, 76, 0, 255), 8f / statesCount));
			gradient.Add(new GradientControlPoint(new Color4(178, 0, 0, 255), 11f / statesCount));
			gradient.Add(new GradientControlPoint(new Color4(127, 0, 127, 255), 16f / statesCount));
			gradient.Add(new GradientControlPoint(new Color4(178, 76, 178, 255), 23f / statesCount));
			gradient.Add(new GradientControlPoint(new Color4(255, 229, 229, 255), 28f / statesCount));
			gradient.Add(new GradientControlPoint(new Color4(255, 255, 255, 255), 38f / statesCount));
			return gradient;
		}

		private class Material : IMaterial
		{
			private static Material instance;
			public static Material Instance => instance ?? (instance = new Material());

			private static readonly BlendState blending;

			static Material()
			{
				blending = new BlendState {
					Enable    = true,
					BlendFunc = BlendFunc.Add,
					SrcBlend  = Blend.One,
					DstBlend  = Blend.Zero
				};
			}

			public string Id { get; set; }
			public int PassCount => 1;

			private readonly ShaderParams[] shaderParamsArray;
			private readonly ShaderParams shaderParams;

			private Material()
			{
				shaderParams = new ShaderParams();
				shaderParamsArray = new[] { Renderer.GlobalShaderParams, shaderParams };
			}

			public void Apply(int pass)
			{
				PlatformRenderer.SetBlendState(blending);
				PlatformRenderer.SetShaderProgram(ShaderProgram.Instance);
				PlatformRenderer.SetShaderParams(shaderParamsArray);
			}

			public void Invalidate() { }

			public class ShaderProgram : Lime.ShaderProgram
			{
				private static ShaderProgram instance;
				public static ShaderProgram Instance => instance ?? (instance = new ShaderProgram());

				private const string VertexShader = @"
					attribute vec4 inPos;
					attribute vec4 inColor;
					attribute vec2 inTexCoords1;

					uniform mat4 matProjection;

					varying lowp vec2 texCoords1;

					void main()
					{
						gl_Position = matProjection * inPos;
						texCoords1 = inTexCoords1;
					}";

				private const string FragmentShader = @"
					uniform lowp sampler2D tex1;
					uniform lowp sampler2D tex2;

					varying lowp vec2 texCoords1;

					void main()
					{
						highp float value = texture2D(tex1, texCoords1).r;
						gl_FragColor = texture2D(tex2, vec2(value, 0));
					}";

				private ShaderProgram() : base(
					CreateShaders(),
					ShaderPrograms.Attributes.GetLocations(),
					ShaderPrograms.GetSamplers(),
					ShaderProgram.Empty)
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
}
#endif // PROFILER
