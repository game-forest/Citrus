using System;
using System.Threading;
using Lime.Graphics.Platform;
using Yuzu;

namespace Lime
{
	// TODO: add mipmap related filters when fixing mipmaps
	public enum TextureFilter
	{
		Linear,
		Nearest,
	}

	public enum TextureWrapMode
	{
		Clamp,
		Repeat,
		MirroredRepeat,
	}

	public enum TextureMipmapMode
	{
		Linear,
		Nearest
	}

	public class TextureParams : IEquatable<TextureParams>
	{
		[YuzuMember]
		[TangerineKeyframeColor(5)]
		public TextureWrapMode WrapModeU { get; set; } = TextureWrapMode.Clamp;
		[YuzuMember]
		[TangerineKeyframeColor(6)]
		public TextureWrapMode WrapModeV { get; set; } = TextureWrapMode.Clamp;
		[YuzuMember]
		public TextureFilter MinFilter { get; set; } = TextureFilter.Linear;
		[YuzuMember]
		public TextureFilter MagFilter { get; set; } = TextureFilter.Linear;
		[YuzuMember]
		public TextureMipmapMode MipmapMode { get; set; } = TextureMipmapMode.Linear;

		public TextureWrapMode WrapMode
		{
			set => WrapModeU = WrapModeV = value;
		}

		public TextureFilter MinMagFilter
		{
			set => MinFilter = MagFilter = value;
		}

		public static TextureParams Default = new TextureParams();

		public bool Equals(TextureParams other)
		{
			return WrapModeU == other.WrapModeU &&
				WrapModeV == other.WrapModeV &&
				MinFilter == other.MinFilter &&
				MagFilter == other.MagFilter &&
				MipmapMode == other.MipmapMode;
		}
	}

	[YuzuDontGenerateDeserializer]
	public interface ITexture : IDisposable
	{
		Size ImageSize { get; }
		Size SurfaceSize { get; }
		Rectangle AtlasUVRect { get; }
		ITexture AtlasTexture { get; }
		bool IsDisposed { get; }

		void TransformUVCoordinatesToAtlasSpace(ref Vector2 uv);
		IPlatformTexture2D GetPlatformTexture();
		void SetAsRenderTarget();
		void RestoreRenderTarget();
		bool IsTransparentPixel(int x, int y);
		bool IsStubTexture { get; }
		int MemoryUsed { get; }
		TextureParams TextureParams { get; set; }

		void MaybeDiscardUnderPressure();
		Color4[] GetPixels();
	}

	public class CommonTexture
	{
		/// <summary>
		/// Gets total bytes uploaded to vram by all alive textures. Thread safe.
		/// </summary>
		public static int TotalMemoryUsed => Volatile.Read(ref totalMemoryUsed);

		private static int totalMemoryUsed;

		public static int TotalMemoryUsedMb => TotalMemoryUsed / (1024 * 1024);

		public bool IsDisposed { get; private set; }

		private int memoryUsed;

		public int MemoryUsed
		{
			get => memoryUsed;
			protected set
			{
				Interlocked.Add(ref totalMemoryUsed, value - memoryUsed);
				memoryUsed = value;
			}
		}

		public virtual void MaybeDiscardUnderPressure()
		{
		}

		public virtual void Dispose()
		{
			MemoryUsed = 0;
			IsDisposed = true;
		}
	}
}
