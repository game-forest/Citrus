using System;
using System.Collections.Generic;
using Lime.Graphics.Platform;

namespace Lime
{
	[YuzuDontGenerateDeserializer]
	public class RenderTexture : CommonTexture, ITexture
	{
		private IPlatformRenderTexture2D platformTexture;

		private readonly Size size = new Size(0, 0);
		private readonly Rectangle uvRect;
		private static readonly Stack<RenderTexture> renderTargetStack = new Stack<RenderTexture>();

		private TextureParams textureParams = TextureParams.Default;
		public TextureParams TextureParams
		{
			get
			{
				return textureParams;
			}
			set
			{
				if (textureParams != value) {
					textureParams = value;
					if (platformTexture != null) {
						platformTexture.SetTextureParams(textureParams);
					}
				}
			}
		}

		public Format Format { get; private set; }

		public RenderTexture(int width, int height, Format format = Format.R8G8B8A8_UNorm)
		{
			if (width <= 0) {
				throw new ArgumentOutOfRangeException("width", "width must be greater than zero");
			}
			if (height <= 0) {
				throw new ArgumentOutOfRangeException("height", "height must be greater than zero");
			}
			Format = format;
			size.Width = width;
			size.Height = height;
			uvRect = new Rectangle(0, 0, 1, 1);
			MemoryUsed = GraphicsUtility.CalculateImageDataSize(format, width, height);
		}

		public Size ImageSize => size;

		public Size SurfaceSize => size;

		public ITexture AtlasTexture => this;

		public Rectangle AtlasUVRect => uvRect;

		public ITexture AlphaTexture => null;

		public void TransformUVCoordinatesToAtlasSpace(ref Vector2 uv) { }

		private void DisposeInternal()
		{
			MemoryUsed = 0;
			if (platformTexture != null) {
				var platformTextureCopy = platformTexture;
				Window.Current.InvokeOnRendering(() => {
					platformTextureCopy.Dispose();
				});
				platformTexture = null;
			}
		}

		public override void Dispose()
		{
			DisposeInternal();
			base.Dispose();
		}

		~RenderTexture()
		{
			DisposeInternal();
		}

		public IPlatformRenderTexture2D GetPlatformTexture()
		{
			if (platformTexture == null) {
				platformTexture = PlatformRenderer.Context.CreateRenderTexture2D(
					Format, size.Width, size.Height, textureParams
				);
			}
			return platformTexture;
		}

		IPlatformTexture2D ITexture.GetPlatformTexture() => GetPlatformTexture();

		public bool IsStubTexture { get { return false; } }

		public void SetAsRenderTarget()
		{
			Renderer.Flush();
			renderTargetStack.Push(PlatformRenderer.CurrentRenderTarget);
			PlatformRenderer.SetRenderTarget(this);
		}

		public void RestoreRenderTarget()
		{
			Renderer.Flush();
			PlatformRenderer.SetRenderTarget(renderTargetStack.Pop());
		}

		public bool IsTransparentPixel(int x, int y)
		{
			return false;
		}

		public int PixelCount => size.Width * size.Height;

		/// <summary>
		/// Copies all pixels from texture.
		/// </summary>
		/// <remarks>
		/// This is a very expensive method.
		/// It requires waiting for all previous render commands to complete.
		/// All previous render commands include commands from all previous frames.
		/// </remarks>
		public unsafe Color4[] GetPixels()
		{
			var pixels = new Color4[PixelCount];
			fixed (Color4* pixelsPtr = pixels) {
				GetPlatformTexture().ReadPixels(
					format: Format.R8G8B8A8_UNorm,
					x: 0,
					y: 0,
					width: size.Width,
					height: size.Height,
					pixels: new IntPtr(pixelsPtr)
				);
			}
			return pixels;
		}

		/// <summary>
		/// Copies all pixels from texture.
		/// </summary>
		/// <remarks>
		/// This is a very expensive method.
		/// It requires waiting for all previous render commands to complete.
		/// All previous render commands include commands from all previous frames.
		/// </remarks>
		public unsafe void GetPixels(Color4[] destinationArray)
		{
			if (destinationArray.Length < PixelCount) {
				throw new InvalidOperationException();
			}
			fixed (Color4* pixelsPtr = destinationArray) {
				GetPlatformTexture().ReadPixels(
					format: Format.R8G8B8A8_UNorm,
					x: 0,
					y: 0,
					width: size.Width,
					height: size.Height,
					pixels: new IntPtr(pixelsPtr)
				);
			}
		}
	}
}
