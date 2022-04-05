using System;
using Lime;

namespace Tangerine.Core
{
	public static class WidgetExtensions
	{
		[ThreadStatic]
		private static RenderObjectList renderObjects;

		public static void Render(this RenderChain renderChain)
		{
			if (renderObjects == null) {
				renderObjects = new RenderObjectList();
			}
			try {
				renderChain.GetRenderObjects(renderObjects);
				renderObjects.Render();
			} finally {
				renderObjects.Clear();
			}
		}

		public static void RenderAndClear(this RenderChain renderChain)
		{
			renderChain.Render();
			renderChain.Clear();
		}

		public static void PrepareRendererState(this Widget widget)
		{
			Renderer.Transform1 = widget.LocalToWorldTransform;
			Renderer.Blending = widget.GlobalBlending;
			Renderer.Shader = widget.GlobalShader;
		}

		public static void RenderToTexture(
			this Widget widget,
			ITexture texture,
			RenderChain renderChain,
			Vector2 leftTop,
			Vector2 rightBottom,
			bool clearRenderTarget = true
		) {
			if (widget.Width > 0 && widget.Height > 0) {
				texture.SetAsRenderTarget();
				Renderer.PushState(
					RenderState.ScissorState |
					RenderState.View |
					RenderState.World |
					RenderState.View |
					RenderState.Projection |
					RenderState.DepthState |
					RenderState.CullMode |
					RenderState.Transform2);
				Renderer.ScissorState = ScissorState.ScissorDisabled;
				Renderer.Viewport = new Viewport(0, 0, texture.ImageSize.Width, texture.ImageSize.Height);
				if (clearRenderTarget) {
					Renderer.Clear(new Color4(0, 0, 0, 0));
				}
				Renderer.World = Matrix44.Identity;
				Renderer.View = Matrix44.Identity;
				Renderer.SetOrthogonalProjection(leftTop, rightBottom);
				Renderer.DepthState = DepthState.DepthDisabled;
				Renderer.CullMode = CullMode.None;
				Renderer.Transform2 = widget.LocalToWorldTransform.CalcInversed();
				renderChain.RenderAndClear();
				Renderer.PopState();
				texture.RestoreRenderTarget();
			}
		}

		public static Bitmap ToBitmap(this Widget widget, Rectangle bounds)
		{
			var pixelScale = Window.Current.PixelScale;
			var scaledWidth = (int)(bounds.Width * pixelScale);
			var scaledHeight = (int)(bounds.Height * pixelScale);
			var savedScale = widget.Scale;
			var savedPosition = widget.Position;
			var savedPivot = widget.Pivot;

			try {
				widget.Scale = Vector2.One;
				widget.Position = Vector2.Zero;
				widget.Pivot = Vector2.Zero;

				using var texture = new RenderTexture(scaledWidth, scaledHeight);
				var renderChain = new RenderChain();
				widget.RenderChainBuilder?.AddToRenderChain(renderChain);
				widget.RenderToTexture(
					texture,
					renderChain,
					new Vector2(bounds.Left, bounds.Top) * pixelScale,
					new Vector2(bounds.Right, bounds.Bottom) * pixelScale
				);
				return new Bitmap(texture.GetPixels(), scaledWidth, scaledHeight);
			} finally {
				widget.Scale = savedScale;
				widget.Position = savedPosition;
				widget.Pivot = savedPivot;
			}
		}

		public static Bitmap ToBitmap(this Widget widget)
		{
			return ToBitmap(widget, new Rectangle(Vector2.Zero, widget.Size));
		}

		public static void AddTransactionClickHandler(this Button button, Action clicked)
		{
			button.Clicked += () => {
				var history = Document.Current.History;
				using (history.BeginTransaction()) {
					clicked();
					history.CommitTransaction();
				}
			};
		}

		public static float Left(this Widget widget) => widget.X;
		public static float Right(this Widget widget) => widget.X + widget.Width;
		public static float Top(this Widget widget) => widget.Y;
		public static float Bottom(this Widget widget) => widget.Y + widget.Height;

		public static bool IsPropertyReadOnly(this Widget widget, string property) =>
			PropertyAttributes<TangerineReadOnlyAttribute>.Get(widget.GetType(), property) != null ||
			(ClassAttributes<TangerineReadOnlyPropertiesAttribute>.Get(widget.GetType())?.Contains(property) ?? false);
	}
}
