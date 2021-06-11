#if WIN
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using WinFormsCloseReason = System.Windows.Forms.CloseReason;

namespace Lime
{
	public class Window : CommonWindow, IWindow
	{
		private ManualResetEvent renderReady = new ManualResetEvent(false);
		private ManualResetEvent renderCompleted = new ManualResetEvent(true);

		private Thread renderThread;
		private CancellationTokenSource renderThreadTokenSource;
		private CancellationToken renderThreadToken;

		// This line only suppresses warning: "Window.Current: a name can be simplified".
		public new static IWindow Current => CommonWindow.Current;

		// We must perform no more than single render per update.
		// So we defer Invalidate() calls until next Update().
		private enum RenderingState
		{
			Updated,
			RenderDeferred,
			Rendered,
		}
		private readonly System.Windows.Forms.Timer timer;
		private RenderControl renderControl;
		private Form form;
		private Stopwatch stopwatch;
		private bool active;
		private RenderingState renderingState = RenderingState.Rendered;
		private Point lastMousePosition;
		private bool isInvalidated;
		private bool vSync;
		private bool shouldCleanDroppedFiles;

		public WindowInput Input { get; private set; }

		public bool Active => active;
		public Form Form => form;

		public string Title
		{
			get { return form.Text; }
			set { form.Text = value; }
		}

		public WindowState State
		{
			get
			{
				if (form.FormBorderStyle == FormBorderStyle.None) {
					return WindowState.Fullscreen;
				} else if (form.WindowState == FormWindowState.Maximized) {
					return WindowState.Maximized;
				} else if (form.WindowState == FormWindowState.Minimized) {
					return WindowState.Minimized;
				} else {
					return WindowState.Normal;
				}
			}

			set
			{
				if (value == WindowState.Fullscreen) {
					form.WindowState = FormWindowState.Normal;
					form.FormBorderStyle = FormBorderStyle.None;
					form.WindowState = FormWindowState.Maximized;
				} else {
					form.FormBorderStyle = borderStyle;
					if (value == WindowState.Maximized) {
						form.WindowState = FormWindowState.Maximized;
					} else if (value == WindowState.Minimized) {
						form.WindowState = FormWindowState.Minimized;
					} else {
						form.WindowState = FormWindowState.Normal;
					}
				}
			}
		}

		public bool FixedSize
		{
			get
			{
				return borderStyle != FormBorderStyle.Sizable;
			}

			set
			{
				if (value && borderStyle == FormBorderStyle.Sizable) {
					borderStyle = FormBorderStyle.FixedSingle;
				} else if (!value && borderStyle == FormBorderStyle.FixedSingle) {
					borderStyle = FormBorderStyle.Sizable;
				}

				if (form.FormBorderStyle != FormBorderStyle.None) {
					form.FormBorderStyle = borderStyle;
				}
				form.MaximizeBox = !FixedSize;
			}
		}

		public bool Fullscreen
		{
			get
			{
				return State == WindowState.Fullscreen;
			}

			set
			{
				if (value && State == WindowState.Fullscreen || !value && State != WindowState.Fullscreen) {
					return;
				}
				State = value ? WindowState.Fullscreen : WindowState.Normal;
			}
		}

		public Vector2 ClientPosition
		{
			get { return SDToLime.Convert(renderControl.PointToScreen(new Point(0, 0)), PixelScale); }
			set { DecoratedPosition = value + DecoratedPosition - ClientPosition; }
		}

		public Vector2 ClientSize
		{
			get { return SDToLime.Convert(renderControl.ClientSize, PixelScale); }
			set { DecoratedSize = value + DecoratedSize - ClientSize; }
		}

		public Vector2 DecoratedPosition
		{
			get { return SDToLime.Convert(form.Location, PixelScale); }
			set { form.Location = LimeToSD.ConvertToPoint(value, PixelScale); }
		}

		public Vector2 DecoratedSize
		{
			get { return SDToLime.Convert(form.Size, PixelScale); }
			set { form.Size = LimeToSD.ConvertToSize(value, PixelScale); }
		}

		public Vector2 MinimumDecoratedSize
		{
			get { return SDToLime.Convert(form.MinimumSize, PixelScale); }
			set { form.MinimumSize = LimeToSD.ConvertToSize(value, PixelScale); }
		}

		public Vector2 MaximumDecoratedSize
		{
			get { return SDToLime.Convert(form.MaximumSize, PixelScale); }
			set { form.MaximumSize = LimeToSD.ConvertToSize(value, PixelScale); }
		}

		public Vector2 WorldToWindow(Vector2 wp)
		{
			var sp = LimeToSD.ConvertToPoint(wp, PixelScale);
			return new Vector2(sp.X + renderControl.Left, sp.Y + renderControl.Top);
		}

		public Vector2 LocalToDesktop(Vector2 localPosition)
		{
			return SDToLime.Convert(
				renderControl.PointToScreen(LimeToSD.ConvertToPoint(localPosition, PixelScale)),
				PixelScale
			);
		}

		public Vector2 DesktopToLocal(Vector2 desktopPosition)
		{
			return SDToLime.Convert(
				renderControl.PointToClient(LimeToSD.ConvertToPoint(desktopPosition, PixelScale)),
				PixelScale
			);
		}

		public bool AsyncRendering { get; }

		public float UnclampedDelta { get; private set; }

		FPSCounter fpsCounter = new FPSCounter();
		public float FPS { get { return fpsCounter.FPS; } }

		[Obsolete("Use FPS property instead", true)]
		public float CalcFPS() { return fpsCounter.FPS; }

		public bool Visible
		{
			get { return form.Visible; }
			set
			{
				RaiseVisibleChanging(value, false);
				form.Visible = value;
			}
		}

		private MouseCursor cursor;
		public MouseCursor Cursor
		{
			get { return cursor; }
			set
			{
				cursor = value;
				if (form.Cursor != value.NativeCursor) {
					form.Cursor = value.NativeCursor;
				}
			}
		}

		public float PixelScale { get; private set; }

		public void Center()
		{
			var screen = Screen.FromControl(form).WorkingArea;
			var x = (int)((screen.Width / PixelScale - DecoratedSize.X) / 2);
			var y = (int)((screen.Height / PixelScale - DecoratedSize.Y) / 2);
			var position = new Vector2(screen.X + x, screen.Y + y);
			DecoratedPosition = position;
		}

		public void Close()
		{
			RaiseVisibleChanging(false, false);
			form.Close();
		}

		private class OpenGLRenderControl : RenderControl
		{
			private OpenTK.Platform.IWindowInfo windowInfo;
			private OpenTK.Graphics.GraphicsMode graphicsMode;
			private OpenTK.Graphics.GraphicsContext graphicsContext;
			private OpenTK.Graphics.GraphicsContextFlags graphicsContextFlags;
			private int major;
			private int minor;
			private static Graphics.Platform.OpenGL.PlatformRenderContext platformRenderContext;

			static OpenGLRenderControl()
			{
				OpenTK.Graphics.GraphicsContext.ShareContexts = true;
			}

			public OpenGLRenderControl(OpenTK.Graphics.GraphicsMode graphicsMode, int major, int minor, OpenTK.Graphics.GraphicsContextFlags graphicsContextFlags)
			{
				this.graphicsMode = graphicsMode;
				this.graphicsContextFlags = graphicsContextFlags;
				this.major = major;
				this.minor = minor;
			}

			protected override void OnHandleCreated(EventArgs e)
			{
				base.OnHandleCreated(e);
				windowInfo = OpenTK.Platform.Utilities.CreateWindowsWindowInfo(Handle);
				graphicsContext = new OpenTK.Graphics.GraphicsContext(graphicsMode, windowInfo, major, minor, graphicsContextFlags);
				graphicsContext.MakeCurrent(windowInfo);
				graphicsContext.LoadAll();
				graphicsContext.SwapInterval = VSync ? 1 : 0;
				if (platformRenderContext == null) {
					platformRenderContext = new Graphics.Platform.OpenGL.PlatformRenderContext();
					PlatformRenderer.Initialize(platformRenderContext);
				}
				graphicsContext.MakeCurrent(null);
			}

			protected override void OnVSyncChanged()
			{
				graphicsContext.MakeCurrent(windowInfo);
				graphicsContext.SwapInterval = VSync ? 1 : 0;
				graphicsContext.MakeCurrent(null);
			}

			protected override void OnHandleDestroyed(EventArgs e)
			{
				base.OnHandleDestroyed(e);
				if (graphicsContext != null) {
					graphicsContext.Dispose();
					graphicsContext = null;
				}
				if (windowInfo != null) {
					windowInfo.Dispose();
					windowInfo = null;
				}
			}

			protected override void OnSizeChanged(EventArgs e)
			{
				base.OnSizeChanged(e);
				graphicsContext.Update(windowInfo);
			}

			public override void Begin()
			{
				graphicsContext.MakeCurrent(windowInfo);
				platformRenderContext.Begin(0);
			}

			public override void SwapBuffers()
			{
				platformRenderContext.End();
				graphicsContext.SwapBuffers();
			}

			public override void UnbindContext()
			{
				OpenTK.Graphics.GraphicsContext.CurrentContext?.MakeCurrent(null);
			}
		}

		private class VulkanRenderControl : RenderControl
		{
			private static Graphics.Platform.Vulkan.PlatformRenderContext platformRenderContext;

			private Graphics.Platform.Vulkan.Swapchain swapchain;
			private bool canRender;

			public override bool CanRender => canRender;

			protected override void OnHandleCreated(EventArgs e)
			{
				base.OnHandleCreated(e);
				if (platformRenderContext == null) {
					platformRenderContext = new Graphics.Platform.Vulkan.PlatformRenderContext();
					PlatformRenderer.Initialize(platformRenderContext);
				}
				RecreateSwapchain();
			}

			private void RecreateSwapchain()
			{
				swapchain?.Dispose();
				swapchain = new Graphics.Platform.Vulkan.Swapchain(platformRenderContext, Handle, ClientSize.Width, ClientSize.Height, VSync);
			}

			protected override void OnHandleDestroyed(EventArgs e)
			{
				swapchain.Dispose();
				base.OnHandleDestroyed(e);
			}

			protected override void SetBoundsCore(int x, int y, int width, int height, BoundsSpecified specified)
			{
				base.SetBoundsCore(x, y, width, height, specified);
				canRender = false;
				if (width != 0 && height != 0) {
					swapchain.Resize(ClientSize.Width, ClientSize.Height);
					canRender = true;
				}
			}

			public override void Begin()
			{
				platformRenderContext.Begin(swapchain);
			}

			public override void SwapBuffers()
			{
				platformRenderContext.Present();
			}

			public override void UnbindContext()
			{
			}

			protected override void OnVSyncChanged()
			{
				RecreateSwapchain();
			}
		}

		private abstract class RenderControl : UserControl
		{
			private bool vSync = true;

			public bool VSync
			{
				get => vSync;
				set
				{
					if (vSync != value) {
						BeforeVSyncChanged?.Invoke();
						vSync = value;
						OnVSyncChanged();
					}
				}
			}

			public virtual bool CanRender => true;

			public event Action BeforeBoundsChanged;
			public event Action BeforeVSyncChanged;

			public RenderControl()
			{
				SetStyle(ControlStyles.Opaque, true);
				SetStyle(ControlStyles.UserPaint, true);
				SetStyle(ControlStyles.AllPaintingInWmPaint, true);
				DoubleBuffered = false;
			}

			protected override CreateParams CreateParams
			{
				get {
					const int CS_VREDRAW = 0x1;
					const int CS_HREDRAW = 0x2;
					const int CS_OWNDC = 0x20;
					var cp = base.CreateParams;
					cp.ClassStyle |= CS_VREDRAW | CS_HREDRAW | CS_OWNDC;
					return cp;
				}
			}

			protected abstract void OnVSyncChanged();

			// Without this at least Left, Right, Up, Down and Tab keys are not submitted OnKeyDown
			protected override bool IsInputKey(Keys keyData)
			{
				return true;
			}

			protected override void SetBoundsCore(int x, int y, int width, int height, BoundsSpecified specified)
			{
				BeforeBoundsChanged?.Invoke();
				base.SetBoundsCore(x, y, width, height, specified);
			}

			public abstract void Begin();
			public abstract void SwapBuffers();
			public abstract void UnbindContext();
		}

		private static RenderControl CreateRenderControl(RenderingBackend backend)
		{
			if (backend == RenderingBackend.Vulkan) {
				return new VulkanRenderControl();
			} else {
				var flags = backend == RenderingBackend.ES20
					? OpenTK.Graphics.GraphicsContextFlags.Embedded
					: OpenTK.Graphics.GraphicsContextFlags.Default;
				return new OpenGLRenderControl(new OpenTK.Graphics.GraphicsMode(32, 16, 8), 2, 0, flags);
			}
		}

		public Window()
			: this(new WindowOptions())
		{
		}

		FormBorderStyle borderStyle;
		private WindowState prevWindowState;

		public Window(WindowOptions options)
		{
			if (Application.MainWindow != null && Application.RenderingBackend == RenderingBackend.ES20) {
				// ES20 doesn't allow multiple contexts for now, because of a bug in OpenTK
				throw new Lime.Exception("Attempt to create a second window for ES20 rendering backend. Use OpenGL backend instead.");
			}
			if (options.UseTimer && options.AsyncRendering) {
				throw new Lime.Exception("Can't use both timer and async rendering");
			}
			if (options.Type == WindowType.Tool) {
				form = new ToolForm();
			} else if (options.Type == WindowType.ToolTip) {
				form = new ToolTipForm();
			} else {
				form = new Form();
			}
			Input  = new WindowInput(this);
			using (var graphics = form.CreateGraphics()) {
				PixelScale = CalcPixelScale(graphics.DpiX);
			}
			if (options.Style == WindowStyle.Borderless) {
				borderStyle = FormBorderStyle.None;
			} else {
				borderStyle = options.FixedSize ? FormBorderStyle.FixedSingle : FormBorderStyle.Sizable;
			}
			form.FormBorderStyle = borderStyle;
			form.MaximizeBox = !options.FixedSize;
			if (options.MinimumDecoratedSize != Vector2.Zero) {
				MinimumDecoratedSize = options.MinimumDecoratedSize;
			}
			if (options.MaximumDecoratedSize != Vector2.Zero) {
				MaximumDecoratedSize = options.MaximumDecoratedSize;
			}
			renderControl = CreateRenderControl(Application.RenderingBackend);
			renderControl.CreateControl();
			renderControl.UnbindContext();
			renderControl.Dock = DockStyle.Fill;
			renderControl.Paint += OnPaint;
			renderControl.KeyDown += OnKeyDown;
			renderControl.KeyUp += OnKeyUp;
			renderControl.KeyPress += OnKeyPress;
			renderControl.MouseDown += OnMouseDown;
			renderControl.MouseUp += OnMouseUp;
			renderControl.Resize += OnResize;
			renderControl.MouseWheel += OnMouseWheel;
			renderControl.MouseEnter += (sender, args) => {
				Application.WindowUnderMouse = this;
			};
			renderControl.MouseLeave += (sender, args) => {
				if (Application.WindowUnderMouse == this) {
					Application.WindowUnderMouse = null;
				}
			};
			renderControl.BeforeBoundsChanged += WaitForRendering;
			renderControl.BeforeVSyncChanged += WaitForRendering;
			form.Move += OnMove;
			form.Activated += OnActivated;
			form.Deactivate += OnDeactivate;
			form.FormClosing += OnClosing;
			form.FormClosed += OnClosed;
			if (options.Type != WindowType.ToolTip) {
				form.Shown += OnShown;
			}
			active = Form.ActiveForm == form;

			if (options.UseTimer) {
				timer = new System.Windows.Forms.Timer {
					Interval = (int)(1000.0 / 65),
					Enabled = true,
				};
				timer.Tick += OnTick;
			} else {
				vSync = options.VSync;
				renderControl.VSync = vSync;
				lock (WindowIdleUpdater.UpdateOnIdleWindows) {
					WindowIdleUpdater.UpdateOnIdleWindows.Add(this);
				}
			}

			form.Controls.Add(renderControl);
			stopwatch = new Stopwatch();
			stopwatch.Start();

			if (options.Icon != null) {
				form.Icon = (System.Drawing.Icon)options.Icon;
			}
			Cursor = MouseCursor.Default;
			Title = options.Title;
			ClientSize = options.ClientSize;
			if (options.Visible) {
				Visible = true;
			}
			if (options.Screen != null && options.Screen >= 0 && Screen.AllScreens.Length > options.Screen) {
				form.Location = GetCenter(Screen.AllScreens[options.Screen.Value].WorkingArea);
			}
			if (options.Centered) {
				Center();
			}
			if (Application.MainWindow == null) {
				Application.MainWindow = this;
				Closing += reason => Application.DoExiting();
				Closed += Application.DoExited;
			} else {
				Form.Owner = Application.MainWindow.Form;
				Form.StartPosition = FormStartPosition.CenterParent;
			}
			AsyncRendering = options.AsyncRendering;
			if (AsyncRendering) {
				renderThreadTokenSource = new CancellationTokenSource();
				renderThreadToken = renderThreadTokenSource.Token;
				renderThread = new Thread(RenderLoop);
				renderThread.IsBackground = true;
				renderThread.Start();
			}
			Application.Windows.Add(this);
		}

		private void OnShown(object sender, EventArgs e) => renderControl.Focus();

		public override bool VSync
		{
			get
			{
				return vSync;
			}

			set
			{
				if (vSync != value && timer == null) {
					vSync = value;
					renderControl.VSync = value;
				}
			}
		}

		public void ShowModal()
		{
			using (Context.Activate().Scoped()) {
				RaiseVisibleChanging(true, true);
				form.ShowDialog();
				RaiseVisibleChanging(false, true);
			};
		}

		public void Activate()
		{
			form.Activate();
		}

		/// <summary>
		/// Gets the display device containing the largest portion of this window.
		/// </summary>
		public IDisplay Display => Lime.Display.GetDisplay(Screen.FromControl(form));

		private static Point GetCenter(System.Drawing.Rectangle rect)
		{
			return new Point(
				rect.X + (rect.Width / 2),
				rect.Y + (rect.Height / 2)
			);
		}

		private static float CalcPixelScale(float dpi)
		{
			// Round DPI to prevent artifacts on UI applications
			return (int)(dpi * 1f / 96f + 0.5f);
		}

		private void OnMouseWheel(object sender, MouseEventArgs e)
		{
			Input.SetWheelScrollAmount(e.Delta);
		}

		private void OnClosed(object sender, FormClosedEventArgs e)
		{
			if (AsyncRendering) {
				renderThreadTokenSource.Cancel();
				renderReady.Set();
				renderThread.Join();
			}
			RaiseClosed();
			Application.Windows.Remove(this);
			if (this == Application.MainWindow) {
				System.Windows.Forms.Application.Exit();
			}
			if (timer == null) {
				lock (WindowIdleUpdater.UpdateOnIdleWindows) {
					WindowIdleUpdater.UpdateOnIdleWindows.Remove(this);
				}
			} else {
				timer.Dispose();
			}
		}

		private void OnClosing(object sender, FormClosingEventArgs e)
		{
			CloseReason reason;
			switch (e.CloseReason) {
				case WinFormsCloseReason.None:
					reason = CloseReason.Unknown;
					break;
				case WinFormsCloseReason.WindowsShutDown:
				case WinFormsCloseReason.MdiFormClosing:
				case WinFormsCloseReason.TaskManagerClosing:
				case WinFormsCloseReason.FormOwnerClosing:
				case WinFormsCloseReason.ApplicationExitCall:
					reason = CloseReason.MainWindowClosing;
					break;
				case WinFormsCloseReason.UserClosing:
					reason = CloseReason.UserClosing;
					break;
				default:
					throw new ArgumentOutOfRangeException();
			}
			e.Cancel = !RaiseClosing(reason);
		}

		private void OnMove(object sender, EventArgs e)
		{
			bool hasBeenMinimized, hasBeenRestored;
			HasWindowStateChanged(out hasBeenMinimized, out hasBeenRestored);

			// We should ignore this event after minimize or restore.
			// Calling to RaiseMoved() after minimize can lead to various bugs because window position is negative
			if (!hasBeenRestored && !hasBeenMinimized) {
				RaiseMoved();
			}
		}

		private void OnActivated(object sender, EventArgs e)
		{
			if (!active) {
				active = true;
				RaiseActivated();
			}
		}

		private void OnDeactivate(object sender, EventArgs e)
		{
			if (active) {
				active = false;
				// Clearing key state on deactivate is required so no keys will get stuck.
				// If, for some reason, you need to transfer key state between windows use InputSimulator to hack it. See docking implementation in Tangerine.
				Input.ClearKeyState();
				RaiseDeactivated();
			}
		}

		private void OnResize(object sender, EventArgs e)
		{
			bool hasBeenMinimized, hasBeenRestored;
			HasWindowStateChanged(out hasBeenMinimized, out hasBeenRestored);
			prevWindowState = State;

			// This will produce extra invokes, but will keep "active" flag in consistant state when minimizing app by
			// clicking on taskbar icon
			if (hasBeenRestored) {
				OnActivated(this, EventArgs.Empty);
			}
			if (hasBeenMinimized) {
				OnDeactivate(this, EventArgs.Empty);
			}

			// We should ignore this event after minimize or restore.
			// Calling to RaiseResized() after minimize can lead to various bugs because window size is 0x0
			if (!hasBeenRestored && !hasBeenMinimized) {
				RaiseResized(deviceRotated: false);
			}
		}

		private void HasWindowStateChanged(out bool hasBeenMinimized, out bool hasBeenRestored)
		{
			hasBeenMinimized = prevWindowState != WindowState.Minimized && State == WindowState.Minimized;
			hasBeenRestored = prevWindowState == WindowState.Minimized && State != WindowState.Minimized;
		}

		private void OnMouseDown(object sender, MouseEventArgs e)
		{
			if (e.Button == MouseButtons.Left) {
				Input.SetKeyState(Key.Mouse0, true);
				Input.SetKeyState(Key.Touch0, true);
				if (e.Clicks == 2) {
					Input.SetKeyState(Key.Mouse0DoubleClick, true);
				}
			} else if (e.Button == MouseButtons.Right) {
				Input.SetKeyState(Key.Mouse1, true);
				if (e.Clicks == 2) {
					Input.SetKeyState(Key.Mouse1DoubleClick, true);
				}
			} else if (e.Button == MouseButtons.Middle) {
				Input.SetKeyState(Key.Mouse2, true);
			}
			else if (e.Button == MouseButtons.XButton1) {
				Input.SetKeyState(Key.MouseBack, true);
			}
			else if (e.Button == MouseButtons.XButton2) {
				Input.SetKeyState(Key.MouseForward, true);
			}
			Input.SetKeyState(Key.Control, Control.ModifierKeys.HasFlag(Keys.Control));
			Input.SetKeyState(Key.Shift, Control.ModifierKeys.HasFlag(Keys.Shift));
			Input.SetKeyState(Key.Alt, Control.ModifierKeys.HasFlag(Keys.Alt));
			Input.SetKeyState(Key.Win, Control.ModifierKeys.HasFlag(Keys.LWin) || Control.ModifierKeys.HasFlag(Keys.RWin));
		}

		private void OnMouseUp(object sender, MouseEventArgs e)
		{
			if (e.Button == MouseButtons.Left) {
				Input.SetKeyState(Key.Mouse0, false);
				Input.SetKeyState(Key.Touch0, false);
				Input.SetKeyState(Key.Mouse0DoubleClick, false);
			} else if (e.Button == MouseButtons.Right) {
				Input.SetKeyState(Key.Mouse1, false);
				Input.SetKeyState(Key.Mouse1DoubleClick, false);
			} else if (e.Button == MouseButtons.Middle) {
				Input.SetKeyState(Key.Mouse2, false);
			}
			else if (e.Button == MouseButtons.XButton1) {
				Input.SetKeyState(Key.MouseBack, false);
			}
			else if (e.Button == MouseButtons.XButton2) {
				Input.SetKeyState(Key.MouseForward, false);
			}
		}

		private void RefreshMousePosition()
		{
			if (lastMousePosition == Control.MousePosition) {
				return;
			}
			lastMousePosition = Control.MousePosition;
			Application.Input.DesktopMousePosition = SDToLime.Convert(lastMousePosition, PixelScale);
			Application.Input.SetDesktopTouchPosition(0, Application.Input.DesktopMousePosition);
		}

		internal bool OnIdleUpdate()
		{
			var wasUpdated = Update();
			return wasUpdated && renderingState != RenderingState.Updated;
		}

		private void OnTick(object sender, EventArgs e)
		{
			Update();
		}

		private void OnKeyDown(object sender, KeyEventArgs e)
		{
			var k = TranslateKey(e.KeyCode);
			if (k != Key.Unknown) {
				Input.SetKeyState(k, true);
			}
			if ((e.Modifiers & Keys.Shift) != 0) {
				Input.SetKeyState(Key.Shift, true);
			}
			if ((e.Modifiers & Keys.Alt) != 0) {
				Input.SetKeyState(Key.Alt, true);
			}
			if ((e.Modifiers & Keys.Control) != 0) {
				Input.SetKeyState(Key.Control, true);
			}
		}

		private void OnKeyUp(object sender, KeyEventArgs e)
		{
			var k = TranslateKey(e.KeyCode);
			if (k != Key.Unknown) {
				Input.SetKeyState(k, false);
			}
		}

		private void OnKeyPress(object sender, KeyPressEventArgs e)
		{
			Input.TextInput += e.KeyChar;
		}

		private void RenderLoop()
		{
			while (true) {
				renderReady.WaitOne();
				renderReady.Reset();
				if (renderThreadToken.IsCancellationRequested) {
					return;
				}
				renderControl.Begin();
				RaiseRendering();
				renderControl.SwapBuffers();
				renderControl.UnbindContext();
				renderCompleted.Set();
			}
		}

		public void WaitForRendering()
		{
			if (AsyncRendering) {
				renderCompleted.WaitOne();
			}
		}

		private void OnPaint(object sender, PaintEventArgs e)
		{
			switch (renderingState) {
				case RenderingState.Updated:
					PixelScale = CalcPixelScale(e.Graphics.DpiX);
					if (!AsyncRendering && renderControl.IsHandleCreated && form.Visible && !renderControl.IsDisposed && renderControl.CanRender) {
						renderControl.Begin();
						RaiseRendering();
						renderControl.SwapBuffers();
					}
					renderingState = RenderingState.Rendered;
					break;
				case RenderingState.Rendered:
					renderingState = RenderingState.RenderDeferred;
					break;
				case RenderingState.RenderDeferred:
					break;
			}
		}

		private bool Update()
		{
			var wasInvalidated = isInvalidated;
			isInvalidated = false;
			if (!form.Visible || !form.CanFocus || !renderControl.IsHandleCreated) {
				return false;
			}
			UnclampedDelta = (float)stopwatch.Elapsed.TotalSeconds;
			float delta = Mathf.Clamp(UnclampedDelta, 0, Application.MaxDelta);
			stopwatch.Restart();
			if (this == Application.MainWindow && Application.MainMenu != null) {
				Application.MainMenu.Refresh();
			}
			fpsCounter.Refresh();
			// Refresh mouse position of every frame to make HitTest work properly if mouse is outside of the screen.
			RefreshMousePosition();
			if (active || Input.IsSimulationRunning) {
				Input.ProcessPendingInputEvents(delta);
			}
			if (Input.IsSimulationRunning) {
				Application.Input.Simulator.OnProcessingPendingInputEvents();
			}
			RaiseUpdating(delta);
			if (this == Application.MainWindow) {
				AudioSystem.Update();
			}
			if (active || Input.IsSimulationRunning) {
				Input.CopyKeysState();
				Input.TextInput = null;
			}
			if (wasInvalidated || renderingState == RenderingState.RenderDeferred) {
				renderControl.Invalidate();
			}
			// We give one update cycle to handle files drop
			// (files dropped event may be fired inside update)
			if (Input.DroppedFiles.Count > 0 || shouldCleanDroppedFiles) {
				if (shouldCleanDroppedFiles) {
					Input.DroppedFiles.Clear();
				}
				shouldCleanDroppedFiles = !shouldCleanDroppedFiles;
			}
			renderingState = renderControl.CanRender ? RenderingState.Updated : RenderingState.Rendered;
			WaitForRendering();
			if (renderControl.CanRender) {
				RaiseSync();
				if (AsyncRendering) {
					renderCompleted.Reset();
					renderReady.Set();
				}
			}
			return true;
		}

		private static Key TranslateKey(Keys key)
		{
			switch (key) {
				case Keys.Oem1:
					return Key.Semicolon;
				case Keys.Oem2:
					return Key.Slash;
				case Keys.Oem7:
					return Key.Quote;
				case Keys.Oem4:
					return Key.LBracket;
				case Keys.Oem6:
					return Key.RBracket;
				case Keys.Oem5:
					return Key.BackSlash;
				case Keys.D0:
					return Key.Number0;
				case Keys.D1:
					return Key.Number1;
				case Keys.D2:
					return Key.Number2;
				case Keys.D3:
					return Key.Number3;
				case Keys.D4:
					return Key.Number4;
				case Keys.D5:
					return Key.Number5;
				case Keys.D6:
					return Key.Number6;
				case Keys.D7:
					return Key.Number7;
				case Keys.D8:
					return Key.Number8;
				case Keys.D9:
					return Key.Number9;
				case Keys.Oem3:
					return Key.Tilde;
				case Keys.Q:
					return Key.Q;
				case Keys.W:
					return Key.W;
				case Keys.E:
					return Key.E;
				case Keys.R:
					return Key.R;
				case Keys.T:
					return Key.T;
				case Keys.Y:
					return Key.Y;
				case Keys.U:
					return Key.U;
				case Keys.I:
					return Key.I;
				case Keys.O:
					return Key.O;
				case Keys.P:
					return Key.P;
				case Keys.A:
					return Key.A;
				case Keys.S:
					return Key.S;
				case Keys.D:
					return Key.D;
				case Keys.F:
					return Key.F;
				case Keys.G:
					return Key.G;
				case Keys.H:
					return Key.H;
				case Keys.J:
					return Key.J;
				case Keys.K:
					return Key.K;
				case Keys.L:
					return Key.L;
				case Keys.Z:
					return Key.Z;
				case Keys.X:
					return Key.X;
				case Keys.C:
					return Key.C;
				case Keys.V:
					return Key.V;
				case Keys.B:
					return Key.B;
				case Keys.N:
					return Key.N;
				case Keys.M:
					return Key.M;
				case Keys.F1:
					return Key.F1;
				case Keys.F2:
					return Key.F2;
				case Keys.F3:
					return Key.F3;
				case Keys.F4:
					return Key.F4;
				case Keys.F5:
					return Key.F5;
				case Keys.F6:
					return Key.F6;
				case Keys.F7:
					return Key.F7;
				case Keys.F8:
					return Key.F8;
				case Keys.F9:
					return Key.F9;
				case Keys.F10:
					return Key.F10;
				case Keys.F11:
					return Key.F11;
				case Keys.F12:
					return Key.F12;
				case Keys.Left:
					return Key.Left;
				case Keys.Right:
					return Key.Right;
				case Keys.Up:
					return Key.Up;
				case Keys.Down:
					return Key.Down;
				case Keys.Space:
					return Key.Space;
				case Keys.Return:
					return Key.Enter;
				case Keys.Delete:
					return Key.Delete;
				case Keys.Insert:
					return Key.Insert;
				case Keys.Back:
					return Key.BackSpace;
				case Keys.PageUp:
					return Key.PageUp;
				case Keys.PageDown:
					return Key.PageDown;
				case Keys.Home:
					return Key.Home;
				case Keys.End:
					return Key.End;
				case Keys.Pause:
					return Key.Pause;
				case Keys.Menu:
					return Key.Alt;
				case Keys.ControlKey:
					return Key.Control;
				case Keys.ShiftKey:
					return Key.Shift;
				case Keys.Apps:
					return Key.Menu;
				case Keys.Tab:
					return Key.Tab;
				case Keys.Escape:
					return Key.Escape;
				case Keys.Oemplus:
					return Key.EqualsSign;
				case Keys.OemMinus:
					return Key.Minus;
				case Keys.OemPeriod:
					return Key.Period;
				case Keys.Oemcomma:
					return Key.Comma;
				case Keys.NumPad0:
					return Key.Keypad0;
				case Keys.NumPad1:
					return Key.Keypad1;
				case Keys.NumPad2:
					return Key.Keypad2;
				case Keys.NumPad3:
					return Key.Keypad3;
				case Keys.NumPad4:
					return Key.Keypad4;
				case Keys.NumPad5:
					return Key.Keypad5;
				case Keys.NumPad6:
					return Key.Keypad6;
				case Keys.NumPad7:
					return Key.Keypad7;
				case Keys.NumPad8:
					return Key.Keypad8;
				case Keys.NumPad9:
					return Key.Keypad9;
				case Keys.Multiply:
					return Key.KeypadMultiply;
				case Keys.Add:
					return Key.KeypadPlus;
				case Keys.Decimal:
					return Key.KeypadDecimal;
				case Keys.Subtract:
					return Key.KeypadMinus;
				case Keys.Divide:
					return Key.KeypadDivide;

				default:
					return Key.Unknown;
			}
		}

		public void Invalidate()
		{
			isInvalidated = true;
		}

		internal void SetMenu(Menu menu)
		{
			if (form.MainMenuStrip != null) {
				form.Controls.Remove(form.MainMenuStrip);
				form.MainMenuStrip = null;
			}
			if (menu != null) {
				menu.Refresh();
				form.Controls.Add(menu.NativeMainMenu);
				form.MainMenuStrip = menu.NativeMainMenu;
			}
		}

		public bool AllowDropFiles
		{
			get { return form.AllowDrop; }
			set
			{
				if (form.AllowDrop != value) {
					form.AllowDrop = value;
					if (value) {
						form.DragEnter += Form_DragEnter;
						form.DragDrop += Form_DragDrop;
						form.QueryContinueDrag += Form_QueryContinueDrag;
					} else {
						form.DragEnter -= Form_DragEnter;
						form.DragDrop -= Form_DragDrop;
						form.QueryContinueDrag -= Form_QueryContinueDrag;
					}
				}
			}
		}

		private void Form_QueryContinueDrag(object sender, QueryContinueDragEventArgs e)
		{
			if (e.Action == DragAction.Drop) {
				Input.SetKeyState(Key.Mouse0, false);
				Input.SetKeyState(Key.Mouse1, false);
				Input.SetKeyState(Key.Mouse2, false);
				Input.SetKeyState(Key.Touch0, false);
				Input.SetKeyState(Key.Touch1, false);
				Input.SetKeyState(Key.Touch2, false);
				Input.SetKeyState(Key.Touch3, false);
			}
		}

		private void Form_DragEnter(object sender, DragEventArgs e)
		{
			if (e.Data.GetDataPresent(DataFormats.FileDrop)) {
				e.Effect = DragDropEffects.All;
			}
		}

		private void Form_DragDrop(object sender, DragEventArgs e)
		{
			var files = ((string[])e.Data.GetData(DataFormats.FileDrop, false));
			using (Context.Activate().Scoped()) {
				Application.WindowUnderMouse = this;
				FilesDropped?.Invoke(files);
				Input.DroppedFiles.AddRange(files);
			}
		}

		public event Action<IEnumerable<string>> FilesDropped;
		public void DragFiles(string[] filenames)
		{
			var dragObject = new DataObject(DataFormats.FileDrop, filenames);
			form.DoDragDrop(dragObject, DragDropEffects.All);
		}

		private static class WindowIdleUpdater
		{
			public static List<Window> UpdateOnIdleWindows = new List<Window>();

			static WindowIdleUpdater()
			{
				System.Windows.Forms.Application.Idle += OnBecomeIdle;
			}

			private static void OnBecomeIdle(object sender, EventArgs e)
			{
				var restartLoop = false;
				do {
					restartLoop = false;
					Window[] windows;
					lock (UpdateOnIdleWindows) {
						windows = UpdateOnIdleWindows.ToArray();
					}
					foreach (var window in windows) {
						// check if it was not closed by previuos window
						lock (UpdateOnIdleWindows) {
							if (!UpdateOnIdleWindows.Contains(window)) {
								continue;
							}
						}
						restartLoop |= window.OnIdleUpdate();
					}
				} while (IsApplicationIdle() && restartLoop);
			}

			private static bool IsApplicationIdle()
			{
				return PeekMessage(out NativeMessage result, (IntPtr)0, (uint)0, (uint)0, (uint)0) == 0;
			}

			[StructLayout(LayoutKind.Sequential)]
			public struct NativeMessage
			{
				public IntPtr Handle;
				public uint Message;
				public IntPtr WParameter;
				public IntPtr LParameter;
				public uint Time;
				public Point Location;
			}

			[DllImport("user32.dll")]
			private static extern int PeekMessage(out NativeMessage message, IntPtr window, uint filterMin, uint filterMax, uint remove);
		}
	}

	static class SDToLime
	{
		public static Vector2 Convert(Point p, float pixelScale)
		{
			return new Vector2(p.X, p.Y) / pixelScale;
		}
		public static Vector2 Convert(System.Drawing.Size p, float pixelScale)
		{
			return new Vector2(p.Width, p.Height) / pixelScale;
		}
	}

	static class LimeToSD
	{
		public static Point ConvertToPoint(Vector2 p, float pixelScale)
		{
			return (Point)ConvertToSize(p, pixelScale);
		}
		public static System.Drawing.Size ConvertToSize(Vector2 p, float pixelScale)
		{
			p = (p * pixelScale);
			return new System.Drawing.Size(p.X.Round(), p.Y.Round());
		}
	}

	public class ToolForm : Form
	{
		public ToolForm() : base()
		{
			ShowInTaskbar = false;
		}

		protected override CreateParams CreateParams
		{
			get {
				CreateParams cp = base.CreateParams;
				cp.Style &= ~0x20000;
				return cp;
			}
		}
	}

	public class ToolTipForm : Form
	{
		public ToolTipForm() : base()
		{
			ShowInTaskbar = false;
			DoubleBuffered = true;
		}

		protected override CreateParams CreateParams
		{
			get
			{
				CreateParams cp = base.CreateParams;
				// WS_POPUP | TTS_NOPREFIX | TTS_ALWAYSTIP
				cp.Style = unchecked((int)0x80000000) | 0x01 | 0x02;
				// WS_EX_TOOLWINDOW | WS_EX_TOPMOST
				cp.ExStyle = 0x00000080 | 0x00000008;
				return cp;
			}
		}

		protected override bool ShowWithoutActivation => true;
	}
}
#endif
