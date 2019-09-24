using Lime;

namespace Tangerine.UI.SceneView.PolygonMesh
{
	public static class Utils
	{
		public static void RenderVertex(
			Vector2 pos, float backgroundRadius, float radius, Color4 backgroundColor, Color4 foregroundColor)
		{
			Renderer.DrawRound(
				pos,
				backgroundRadius,
				numSegments: 32,
				backgroundColor,
				backgroundColor
			);
			Renderer.DrawRound(
				pos,
				radius,
				numSegments: 32,
				foregroundColor,
				foregroundColor
			);
		}

		public static void RenderLine(
			Vector2 p1, Vector2 p2, Vector2 backgroundSize, Vector2 foregroundSize, Color4 backgroundColor, Color4 foregroundColor, bool isDashed = false)
		{
			if (isDashed) {
				Renderer.DrawDashedLine(
					p1,
					p2,
					backgroundColor,
					backgroundSize
				);
				Renderer.DrawDashedLine(
					p1,
					p2,
					foregroundColor,
					foregroundSize
				);
			} else {
				Renderer.DrawLine(
					p1,
					p2,
					backgroundColor,
					backgroundSize.X
				);
				Renderer.DrawLine(
					p1,
					p2,
					foregroundColor,
					foregroundSize.X
				);
			}
		}

		public static void RenderTriangle(Vector2 p1, Vector2 p2, Vector2 p3, Color4 color)
		{
			var size = 2;
			var texture = new Texture2D();
			var image = new Color4[size * size];
			for (int y = 0; y < size; ++y) {
				for (int x = 0; x < size; ++x) {
					image[y * size + x] = color.Transparentify(0.8f);
				}
			}
			texture.LoadImage(image, size, size);
			var vertices = new[] {
				new Vertex() { Pos = p1, Color = color },
				new Vertex() { Pos = p2, Color = color },
				new Vertex() { Pos = p3, Color = color },
			};
			Renderer.DrawTriangleStrip(texture, vertices, vertices.Length);
		}
	}
}
