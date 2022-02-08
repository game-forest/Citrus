using System;
using System.IO;
using Lime;

namespace Orange
{
	public class TextureTools
	{
		public class AtlasItem
		{
			public string Path;
			public IntRectangle AtlasRect;
			public bool Allocated;
			public CookingRules CookingRules;
			public string SourceExtension;
			public BitmapInfo BitmapInfo;
		}

		public class BitmapInfo
		{
			public int Width;
			public int Height;
			public bool HasAlpha;

			public static BitmapInfo FromBitmap(Bitmap bitmap)
			{
				return new BitmapInfo {
					Width = bitmap.Width,
					Height = bitmap.Height,
					HasAlpha = bitmap.HasAlpha,
				};
			}

			public static BitmapInfo FromFile(AssetBundle bundle, string path)
			{
				if (
					TextureConverterUtils.GetPngFileInfo(bundle, path, out var width, out var height, out var hasAlpha)
				) {
					return new BitmapInfo {
						Width = width,
						Height = height,
						HasAlpha = hasAlpha,
					};
				}
				Debug.Write($"Failed to read image info {path}");
				return null;
			}
		}

		public static Bitmap OpenAtlasItemBitmapAndRescaleIfNeeded(TargetPlatform platform, AtlasItem item)
		{
			var srcTexturePath = Path.ChangeExtension(item.Path, item.SourceExtension);
			using var stream = AssetBundle.Current.OpenFile(srcTexturePath);
			var bitmap = new Bitmap(stream);
			if (item.BitmapInfo == null) {
				if (ShouldDownscale(platform, bitmap, item.CookingRules)) {
					var newBitmap = DownscaleTexture(platform, bitmap, srcTexturePath, item.CookingRules);
					bitmap.Dispose();
					bitmap = newBitmap;
				}
				// Ensure that no image exceeded maxAtlasSize limit
				DownscaleTextureToFitAtlas(ref bitmap, srcTexturePath, item.CookingRules.MaxAtlasSize);
			}
			else if (bitmap.Width != item.BitmapInfo.Width || bitmap.Height != item.BitmapInfo.Height) {
				var newBitmap = bitmap.Rescale(item.BitmapInfo.Width, item.BitmapInfo.Height);
				bitmap.Dispose();
				bitmap = newBitmap;
			}
			return bitmap;
		}

		public static bool ShouldDownscale(TargetPlatform platform, Bitmap texture, CookingRules rules)
		{
			return ShouldDownscaleHelper(platform, texture.Width, texture.Height, rules);
		}

		public static bool ShouldDownscale(TargetPlatform platform, BitmapInfo textureInfo, CookingRules rules)
		{
			return ShouldDownscaleHelper(platform, textureInfo.Width, textureInfo.Height, rules);
		}

		public static void DownscaleTextureInfo(
			TargetPlatform platform, BitmapInfo textureInfo, string path, CookingRules rules
		) {
			DownscaleTextureHelper(
				platform, textureInfo.Width, textureInfo.Height, path, rules, out int newWidth, out int newHeight
			);
			textureInfo.Height = newHeight;
			textureInfo.Width = newWidth;
		}

		public static void DownscaleTextureToFitAtlas(ref Bitmap bitmap, string path, int maxAtlasSize)
		{
			if (
				DownscaleTextureToFitAtlasHelper(
					bitmap.Width, bitmap.Height, path, maxAtlasSize, out int newWidth, out int newHeight
				)
			) {
				var scaledBitmap = bitmap.Rescale(newWidth, newHeight);
				bitmap.Dispose();
				bitmap = scaledBitmap;
			}
		}

		public static void DownscaleTextureToFitAtlas(BitmapInfo textureInfo, string path, int maxAtlasSize)
		{
			if (
				DownscaleTextureToFitAtlasHelper(
					textureInfo.Width, textureInfo.Height, path, maxAtlasSize, out int newWidth, out int newHeight
				)
			) {
				textureInfo.Width = newWidth;
				textureInfo.Height = newHeight;
			}
		}

		public static Bitmap DownscaleTexture(TargetPlatform platform, Bitmap texture, string path, CookingRules rules)
		{
			DownscaleTextureHelper(
				platform, texture.Width, texture.Height, path, rules, out int newWidth, out int newHeight
			);
			return texture.Rescale(newWidth, newHeight);
		}

		public static void UpscaleTextureIfNeeded(ref Bitmap texture, ICookingRules rules, bool square)
		{
			if (rules.WrapMode == TextureWrapMode.Clamp) {
				return;
			}
			if (TextureConverterUtils.IsPowerOf2(texture.Width) && TextureConverterUtils.IsPowerOf2(texture.Height)) {
				return;
			}
			int newWidth = Mathf.CalcUpperPowerOfTwo(texture.Width);
			int newHeight = Mathf.CalcUpperPowerOfTwo(texture.Height);
			if (square) {
				newHeight = newWidth = Math.Max(newWidth, newHeight);
			}
			var newTexture = texture.Rescale(newWidth, newHeight);
			texture.Dispose();
			texture = newTexture;
		}

		private static bool DownscaleTextureToFitAtlasHelper(
			int width, int height, string path, int maxAtlasSize, out int newWidth, out int newHeight
		) {
			var maxWidth = maxAtlasSize;
			var maxHeight = maxAtlasSize;
			if (width <= maxWidth && height <= maxHeight) {
				newWidth = 0;
				newHeight = 0;
				return false;
			}
			newWidth = Math.Min(width, maxWidth);
			newHeight = Math.Min(height, maxHeight);
			Console.WriteLine(
				$"WARNING: '{path}' downscaled: {width}x{height} => {newWidth}x{newHeight} to fit atlas."
			);
			return true;
		}

		private static void DownscaleTextureHelper(
			TargetPlatform platform,
			int width,
			int height,
			string path,
			CookingRules rules,
			out int newWidth,
			out int newHeight
		) {
			int maxSize = rules.MaxAtlasSize;
			int scaleThreshold = platform == TargetPlatform.Android ? 32 : 256;
			var ratio = rules.TextureScaleFactor;
			if (width > maxSize || height > maxSize) {
				var max = (float)Math.Max(width, height);
				ratio *= maxSize / max;
			}
			newWidth = width;
			newHeight = height;
			if (width > scaleThreshold) {
				newWidth = Math.Min((width * ratio).Round(), maxSize);
			}
			if (height > scaleThreshold) {
				newHeight = Math.Min((height * ratio).Round(), maxSize);
			}
			Console.WriteLine(
				$"{path} downscaled: {width}x{height} => {newWidth}x{newHeight}, " +
				$"scale factor: {rules.TextureScaleFactor}"
			);
		}

		private static bool ShouldDownscaleHelper(TargetPlatform platform, int width, int height, CookingRules rules)
		{
			if (rules.TextureScaleFactor != 1.0f) {
				int scaleThreshold = platform == TargetPlatform.Android ? 32 : 256;
				if (width > scaleThreshold || height > scaleThreshold) {
					return true;
				}
			}
			return false;
		}
	}
}
