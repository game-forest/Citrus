using System;

namespace Lime.NanoVG
{
	internal interface IRenderingBackend
	{
		int CreateTexture(TextureType type, int width, int height, ImageFlags imageFlags, byte[] data);
		void DeleteTexture(int image);
		void UpdateTexture(int image, int x, int y, int width, int height, byte[] data);
		void GetTextureSize(int image, out int width, out int height);
		void RenderFill(ref Paint paint, ref Scissor scissor, float fringe, Rectangle bounds, ArraySegment<Path> paths);
		void RenderStroke(ref Paint paint, ref Scissor scissor, float fringe, float strokeWidth, ArraySegment<Path> paths);
	}
}
