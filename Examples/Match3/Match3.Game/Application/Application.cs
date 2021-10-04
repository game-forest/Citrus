using System;
using Match3.Debug;
using Match3.Dialogs;
using Lime;
using System.IO;

namespace Match3.Application
{
	public class Application
	{
		public static Application Instance;

		public const string ApplicationName = "Match3";
		public static readonly Vector2 DefaultWorldSize = new Vector2(1024, 768);

		public static void Initialize()
		{
			Instance = new Application();
			Instance.Load();
		}

		private void Load()
		{
			World = CreateWorld();

			AppData.Load();
			AssetBundle.Current = CreateAssetBundle();
			Profile.Instance = new Profile();

			LoadDictionary();
			SetWindowSize();

			if (AppData.Instance.EnableSplashScreen) {
				The.DialogManager.Open("Shell/Splash");
			} else {
				The.DialogManager.Open("Shell/MainMenu");
			}
		}

		public WindowWidget World { get; private set; }
		public bool TimeAccelerationMode { get; set; }

		public delegate void CalculatingWorldUpdatingParametersDelegate(ref float delta, ref int iterationsCount, ref bool isTimeQuantized);
		public CalculatingWorldUpdatingParametersDelegate CalculatingWorldUpdatingParameters;

		public delegate void CustomWorldUpdatingDelegate(float delta, int iterationsCount, bool isTimeQuantized, Action<float, bool> updateFrameAction);
		public CustomWorldUpdatingDelegate CustomWorldUpdating;

		private bool requiredToWaitForWindowRendering;

		private AssetBundle CreateAssetBundle()
		{
#if ANDROID
			return new PackedAssetBundle("Assets.Android.Data.Android", "Assets.Android");
#elif iOS
			return new PackedAssetBundle("Data.iOS");
#elif WIN
			return new PackedAssetBundle("Data.Win");
#elif MAC
			return new PackedAssetBundle("Data.Mac");
#endif
			throw new System.InvalidOperationException("Invalid Platform.");
		}

		private void LoadDictionary()
		{
			var fileName = "Dictionary.txt";
#if WIN
			if (File.Exists(fileName)) {
				Localization.Dictionary.Clear();
				using (var stream = new FileStream(fileName, FileMode.Open)) {
					Localization.Dictionary.ReadFromStream(new LocalizationDictionaryTextSerializer(), stream);
				}

				return;
			}
#endif

			if (!AssetBundle.Current.FileExists(fileName)) {
				return;
			}

			Localization.Dictionary.Clear();
			using (var stream = AssetBundle.Current.OpenFile(fileName)) {
				Localization.Dictionary.ReadFromStream(new LocalizationDictionaryTextSerializer(), stream);
			}
		}

		private WindowWidget CreateWorld()
		{
			var options = new WindowOptions { Title = ApplicationName, UseTimer = false, AsyncRendering = false };
			var window = new Window(options);
			window.Updating += OnUpdateFrame;
			window.Rendering += OnRenderFrame;
			window.Resized += OnResize;
			var world = new WindowWidget(window) { Layer = RenderChain.LayerCount - 1 };
			return world;
		}

		private static void SetWindowSize()
		{
#if WIN
			The.Window.ClientSize = DisplayInfo.GetResolution();
#endif
			DisplayInfo.HandleOrientationOrResolutionChange();
		}

		private void OnUpdateFrame(float delta)
		{
			var speedMultiplier = 1.0f;
			if (TimeAccelerationMode || Cheats.IsKeyPressed(Key.Shift) || Cheats.IsTripleTouch()) {
				speedMultiplier = 10.0f;
			}
			if (Cheats.IsKeyPressed(Key.Tilde)) {
				speedMultiplier = 0.1f;
			}

			delta *= speedMultiplier;
			UpdateWorld(delta * speedMultiplier);
			The.World.PrepareToRender();
		}

		private void UpdateWorld(float delta)
		{
			var isTimeQuantized = false;
			var iterationsCount = 1;

			CalculatingWorldUpdatingParameters?.Invoke(ref delta, ref iterationsCount, ref isTimeQuantized);
			if (CustomWorldUpdating != null) {
				CustomWorldUpdating.Invoke(delta, iterationsCount, isTimeQuantized, UpdateFrame);
			} else {
				if (iterationsCount == 1) {
					var validDelta = Mathf.Clamp(delta, 0, Lime.Application.MaxDelta);
					iterationsCount = (int)(delta / validDelta);
					var remainDelta = delta - validDelta * iterationsCount;
					for (var i = 0; i < iterationsCount; i++) {
						UpdateFrame(validDelta, requiredInputSimulation: i + 1 < iterationsCount || remainDelta > 0);
						if (requiredToWaitForWindowRendering) {
							break;
						}
					}
					if (remainDelta > 0 && !isTimeQuantized) {
						UpdateFrame(remainDelta);
					}
				} else {
					for (var i = 0; i < iterationsCount; i++) {
						UpdateFrame(delta, requiredInputSimulation: i + 1 < iterationsCount);
						if (requiredToWaitForWindowRendering) {
							break;
						}
					}
				}
			}
			requiredToWaitForWindowRendering = false;
		}

		private void UpdateFrame(float delta, bool requiredInputSimulation = false)
		{
			Cheats.ProcessCheatKeys();
			The.World.Update(delta);
			if (requiredInputSimulation && !requiredToWaitForWindowRendering) {
				Lime.Application.Input.Simulator.OnBetweenFrames(delta);
			}
		}

		public void WaitForRenderingOnNextFrame()
		{
			requiredToWaitForWindowRendering = true;
		}

		private void OnResize(bool isDeviceRotated)
		{
			DisplayInfo.HandleOrientationOrResolutionChange();
		}

		private void OnRenderFrame()
		{
			Renderer.BeginFrame();
			SetupViewportAndProjectionMatrix();
			World.RenderAll();
			Cheats.RenderDebugInfo();
			Renderer.EndFrame();
		}

		private static void SetupViewportAndProjectionMatrix()
		{
			Renderer.SetOrthogonalProjection(0, 0, The.World.Width, The.World.Height);
			var windowSize = The.Window.ClientSize;
			The.Window.Input.MousePositionTransform = Matrix32.Scaling(The.World.Width / windowSize.X,
				The.World.Height / windowSize.Y);
		}

		public string GetVersion() => "1.0";
	}
}
