using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
#if PROFILER
using Lime.Profiler.Graphics;
#endif // PROFILER

namespace Lime
{
	/// <summary>
	/// Root of the widgets hierarchy.
	/// </summary>
	[YuzuDontGenerateDeserializer]
	public class WindowWidget : Widget
	{
		private RenderObjectList renderObjectList1 = new RenderObjectList();
		private RenderObjectList renderObjectList2 = new RenderObjectList();

		private bool windowActivated;
		private Widget lastFocused;
		protected readonly RenderChain renderChain;

#if !TANGERINE && PROFILER
		private readonly RenderTargetQueue renderTargetManager;
		private Color4[] overdrawPixels;

		private Action overdrawBegin;
		private Action overdrawEnd;
#endif // !TANGERINE && PROFILER

		public IWindow Window { get; private set; }
		public WidgetContext WidgetContext { get; private set; }

		public WindowWidget(IWindow window)
		{
#if !TANGERINE && PROFILER
			if (window == Application.MainWindow) {
				renderTargetManager = new RenderTargetQueue();
				overdrawPixels = new Color4[1920 * 1080];
			}
#endif // !TANGERINE && PROFILER
			WidgetContext = new WidgetContext(this);
			LayoutManager = new LayoutManager();
			CreateManager(LayoutManager, WidgetContext).RootNodes.Add(this);
			Window = window;
			Window.Context = new CombinedContext(Window.Context, WidgetContext);
			renderChain = new RenderChain();
			window.Activated += () => windowActivated = true;
			window.Sync += Sync;
		}

		protected virtual NodeManager CreateManager(LayoutManager layoutManager, WidgetContext widgetContext)
		{
			var services = new ServiceRegistry();
			services.Add(new BehaviorSystem());
			services.Add(layoutManager);
			services.Add(CommandHandlerList.Global);
			services.Add(widgetContext);
			if (Application.EnableParticleLimiter) {
				services.Add(new ParticleLimiter());
			}

			var manager = new NodeManager(services);
			manager.Processors.Add(new GestureProcessor());
			manager.Processors.Add(new BehaviorSetupProcessor());
			manager.Processors.Add(new BehaviorUpdateProcessor(typeof(PreEarlyUpdateStage)));
			manager.Processors.Add(new BehaviorUpdateProcessor(typeof(EarlyUpdateStage)));
			manager.Processors.Add(new BehaviorUpdateProcessor(typeof(PostEarlyUpdateStage)));
			// All context specific commands (e.g. EditBox Copy/Paste) should be processed and consumed before this processor.
			// This processor is intended to process application wide commands registered in CommandHandlerList.Global
			manager.Processors.Add(new CommandProcessor());
			manager.Processors.Add(new AnimationProcessor());
			manager.Processors.Add(new BehaviorUpdateProcessor(typeof(AfterAnimationStage)));
			manager.Processors.Add(new LayoutProcessor());
			if (Application.EnableParticleLimiter) {
				manager.Processors.Add(new ParticleLimitPreLateUpdateProcessor());
			}
			manager.Processors.Add(new BehaviorUpdateProcessor(typeof(PreLateUpdateStage)));
			manager.Processors.Add(new BehaviorUpdateProcessor(typeof(LateUpdateStage)));
			manager.Processors.Add(new BehaviorUpdateProcessor(typeof(PostLateUpdateStage)));
			if (Application.EnableParticleLimiter) {
				manager.Processors.Add(new ParticleLimitPostLateUpdateProcessor());
			}
			return manager;
		}

		protected virtual bool ContinuousRendering() { return true; }

		private bool prevAnyCaptureKeyPressed;
		private AssetBundle assetBundle;

		private void Sync()
		{
			assetBundle = AssetBundle.Initialized ? AssetBundle.Current : null;
			Toolbox.Swap(ref renderObjectList1, ref renderObjectList2);
#if PROFILER
			if (Window == Application.MainWindow) {
				Overdraw.Sync();
				OverdrawForeground.Sync();
			}
#endif // PROFILER
		}

		public void PrepareToRender()
		{
			renderObjectList1.Clear();
			renderChain.GetRenderObjects(renderObjectList1);
#if PROFILER
			if (Window == Application.MainWindow) {
				OverdrawForeground.GetRenderObjects();
			}
#endif // PROFILER
		}

		public virtual void Update(float delta)
		{
#if PROFILER
			if (Window == Application.MainWindow) {
				Overdraw.UpdateStarted();
			}
#endif // PROFILER

			if (ContinuousRendering()) {
				Window.Invalidate();
			}
			var context = WidgetContext.Current;

			// Find the node under mouse, using the render chain built on one frame before.
			context.NodeUnderMouse = LookForNodeUnderMouse(renderChain);

			// Assign NodeCapturedByMouse if any mouse button was pressed or files were dropped.
			var anyCaptureKeyPressed = IsAnyCaptureKeyPressed();
			if (!prevAnyCaptureKeyPressed && anyCaptureKeyPressed || Window.Input.DroppedFiles.Count > 0) {
				context.NodeCapturedByMouse = context.NodeUnderMouse;
			} else if (
				!anyCaptureKeyPressed
				|| (!(context.NodeCapturedByMouse as Widget)?.GloballyVisible ?? false)
				|| (!(context.NodeCapturedByMouse as Node3D)?.GloballyVisible ?? false)
			) {
				// Set NodeCapturedByMouse to null if all mouse buttons are released or widget became invisible.
				context.NodeCapturedByMouse = null;
			}
			prevAnyCaptureKeyPressed = anyCaptureKeyPressed;

			// Update the widget hierarchy.
			context.MouseCursor = MouseCursor.Default;
			Manager.Update(delta);
			Window.Cursor = context.MouseCursor;

			if (Window.Input.WasKeyPressed(Key.DismissSoftKeyboard)) {
				SetFocus(null);
			}

			ManageFocusOnWindowActivation();

			// Rebuild the render chain.
			renderChain.Clear();
			renderChain.ClipRegion = new Rectangle(Vector2.Zero, Size);
			RenderChainBuilder?.AddToRenderChain(renderChain);
		}

		public void RenderAll()
		{
			if (assetBundle != null) {
				AssetBundle.SetCurrent(assetBundle, resetTexturePool: false);
			}
#if !TANGERINE && PROFILER
			RenderTexture renderTexture = null;
			if (Overdraw.EnabledAtRenderThread && Window == Application.MainWindow) {
				renderTexture = renderTargetManager.Acquire((Size)GetViewport().Size);
				renderTexture.SetAsRenderTarget();
				Renderer.Clear(Color4.Zero);
				OverdrawMaterialScope.Enter();
			}
#endif // !TANGERINE && PROFILER
			Render(renderObjectList2);
#if !TANGERINE && PROFILER
			if (Overdraw.EnabledAtRenderThread && Window == Application.MainWindow) {
				OverdrawMaterialScope.Leave();
				renderTexture.RestoreRenderTarget();
				if (Overdraw.MetricRequiredAtRenderThread) {
					OverdrawInterpreter.EnsureEnoughBufferSize(renderTexture, ref overdrawPixels);
					renderTexture.GetPixels(overdrawPixels);
					int pixelCount = renderTexture.PixelCount;
					float averageOverdraw = OverdrawInterpreter.GetAverageOverdraw(overdrawPixels, pixelCount);
					Overdraw.InvokeMetricCreated(averageOverdraw, pixelCount);
				}
				renderTargetManager.Free(renderTexture);
				Renderer.PushState(RenderState.Projection);
				var windowSize = (Size)Window.ClientSize;
				Renderer.SetOrthogonalProjection(0, 0, windowSize.Width, windowSize.Height);
				OverdrawInterpreter.DrawResults(renderTexture, Matrix32.Identity, windowSize);
				Renderer.PopState();
				OverdrawForeground.Render();
			}
#endif // !TANGERINE && PROFILER
		}

		private bool IsAnyCaptureKeyPressed()
		{
			foreach (var key in WidgetContext.NodeCaptureKeys) {
				if (Window.Input.IsKeyPressed(key)) {
					return true;
				}
			}
			return false;
		}

		private void ManageFocusOnWindowActivation()
		{
			if (Window.Active) {
				lastFocused = null;
				if (Widget.Focused != null && Widget.Focused.SameOrDescendantOf(this)) {
					lastFocused = Widget.Focused;
				}
			}
			if (windowActivated) {
				windowActivated = false;
				if (lastFocused == null || !lastFocused.GloballyVisible || !lastFocused.SameOrDescendantOf(this)) {
					// Looking the first focus scope widget on the window and make it focused.
					lastFocused = Descendants.OfType<Widget>().FirstOrDefault(i => i.FocusScope != null && i.GloballyVisible);
				}
				Widget.SetFocus(lastFocused);
			}
		}

		private Node LookForNodeUnderMouse(RenderChain renderChain)
		{
#if iOS || ANDROID
			if (
				!Toolbox.IsMouseWheelSupported() &&
				!Window.Input.IsTouching(0) && !Window.Input.WasTouchEnded(0)
			) {
				return null;
			}
#endif
			var hitTestArgs = new HitTestArgs(Window.Input.MousePosition);
			renderChain.HitTest(ref hitTestArgs);
			var n = hitTestArgs.Node;
			if (
				n != null &&
				WidgetInput.InputScopeStack.Top != null &&
				!n.SameOrDescendantOf(WidgetInput.InputScopeStack.Top)
			) {
				n = null;
			}
			return n;
		}

		protected virtual void Render(RenderObjectList renderObjects)
		{
			Renderer.Viewport = new Viewport(GetViewport());
			renderObjects.Render();
		}

		public WindowRect GetViewport()
		{
			return new WindowRect {
				X = 0, Y = 0,
				Width = (int)(Window.ClientSize.X * Window.PixelScale),
				Height = (int)(Window.ClientSize.Y * Window.PixelScale)
			};
		}

		public Matrix44 GetProjection() => Matrix44.CreateOrthographicOffCenter(0, Width, Height, 0, -50, 50);
	}

	[YuzuDontGenerateDeserializer]
	public class DefaultWindowWidget : WindowWidget
	{
		public bool LayoutBasedWindowSize { get; set; }

		public DefaultWindowWidget(IWindow window)
			: base(window)
		{
			window.Rendering += () => {
				Renderer.BeginFrame();
				Renderer.Projection = GetProjection();
				RenderAll();
				Renderer.EndFrame();
			};
			window.Updating += UpdateAndResize;
			window.Resized += deviceRotated => UpdateAndResize(0);
			window.VisibleChanging += Window_VisibleChanging;
		}

		private void Window_VisibleChanging(bool showing, bool modal)
		{
			if (modal && showing) {
				Input.RestrictScope();
			}
			if (!showing)
				Input.DerestrictScope();{
			}
			if (showing) {
				UpdateAndResize(0);
			}
		}

		private void UpdateAndResize(float delta)
		{
			if (LayoutBasedWindowSize) {
				Update(delta); // Update widgets in order to deduce EffectiveMinSize.
				Size = Window.ClientSize = EffectiveMinSize;
			} else {
				Size = Window.ClientSize;
				Update(delta);
			}
			PrepareToRender();
		}
	}

	[YuzuDontGenerateDeserializer]
	public class InvalidableWindowWidget : DefaultWindowWidget
	{
		public bool RedrawMarkVisible { get; set; }

		public InvalidableWindowWidget(IWindow window)
			: base(window)
		{
		}

		protected override bool ContinuousRendering() { return false; }

		protected override void Render(RenderObjectList renderObjects)
		{
			base.Render(renderObjects);
			if (RedrawMarkVisible) {
				RenderRedrawMark();
			}
		}

		void RenderRedrawMark()
		{
			Renderer.Transform1 = Matrix32.Identity;
			Renderer.Blending = Blending.Alpha;
			Renderer.Shader = ShaderId.Diffuse;
			Renderer.DrawRect(Vector2.Zero, Vector2.One * 4, RandomColor());
		}

		private Color4 RandomColor()
		{
			return new Color4(RandomByte(), RandomByte(), RandomByte());
		}

		private byte RandomByte()
		{
			return (byte)Mathf.RandomInt(0, 255);
		}
	}
}
