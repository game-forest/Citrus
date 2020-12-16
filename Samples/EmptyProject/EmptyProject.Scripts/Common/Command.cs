using System;
using System.Diagnostics;
using System.Linq;
using EmptyProject.Dialogs;
using EmptyProject.Debug;
using Lime;

namespace EmptyProject.Scripts.Common
{
	public static class Command
	{
		public static async Coroutine RenderThisFrame()
		{
			WaitForRenderingOnNextFrame();
			await WaitNextFrame();
		}

		public static Coroutine WaitNextFrame() => new WaitNextFrameCoroutine();

		public static Coroutine Wait(float seconds) => seconds > 0 ? new DelayCoroutine(seconds) : WaitNextFrame();

		public static Coroutine WaitWhile(Func<bool> predicate) => new WaitPredicateCoroutine(Task.WaitWhile(predicate));

		public static Coroutine WaitWhile(Func<float, bool> timePredicate) => new WaitPredicateCoroutine(Task.WaitWhile(timePredicate));

		public static Coroutine WaitForAnimation(Animation animation) => new WaitPredicateCoroutine(Task.WaitForAnimation(animation));

		public static Coroutine<Node> WaitNode(string path, Node root = null, TaskParameters parameters = null) => WaitNode<Node>(path, root, parameters);

		public static async Coroutine<T> WaitNode<T>(string path, Node root = null, TaskParameters parameters = null) where T : Node
		{
			root = root ?? The.World;
			parameters = parameters ?? WaitNodeTaskParameters.Default;

			Logger.Instance.Script($@"{nameof(WaitNode)}(""{path}"") processing...");
			var time = 0f;
			Stopwatch stopwatch = null;
			if (parameters.UseUnscaledTime) {
				stopwatch = new Stopwatch();
				stopwatch.Start();
			}
			while (time <= parameters.Duration) {
				if (root.TryFind<T>(path, out var node)) {
					var done = !(parameters is WaitNodeTaskParameters waitNodeTaskParameters) || waitNodeTaskParameters.IsConditionMet(node);
					if (done) {
						Logger.Instance.Script($@"{nameof(WaitNode)}(""{path}"") succeed.");
						return node;
					}
				}
				await Wait(parameters.Period);
				time += stopwatch?.ElapsedMilliseconds * 0.001f ?? Mathf.Max(Task.Current.Delta, parameters.Period);
				stopwatch?.Restart();
			}
			stopwatch?.Stop();
			Logger.Instance.Script($@"{nameof(WaitNode)}(""{path}"") failed.");

			if (!parameters.IsStrictly) {
				return null;
			}
			var userAnswer = await MessageBox.Show($"Failed to found node: {path}.");
			return userAnswer == MessageBoxResult.Retry ? await WaitNode<T>(path, root, parameters) : null;
		}

		public static async Coroutine<T> WaitDialog<T>(TaskParameters parameters = null) where T : Dialog
		{
			var dialog = await WaitDialogs(parameters, typeof(T));
			return dialog as T;
		}

		public static Coroutine<Dialog> WaitDialogs<T1, T2>(TaskParameters parameters = null) where T1 : Dialog where T2 : Dialog
		{
			return WaitDialogs(parameters, typeof(T1), typeof(T2));
		}

		public static Coroutine<Dialog> WaitDialogs<T1, T2, T3>(TaskParameters parameters = null) where T1 : Dialog where T2 : Dialog where T3 : Dialog
		{
			return WaitDialogs(parameters, typeof(T1), typeof(T2), typeof(T3));
		}

		public static Coroutine<Dialog> WaitDialogs<T1, T2, T3, T4>(TaskParameters parameters = null) where T1 : Dialog where T2 : Dialog where T3 : Dialog where T4 : Dialog
		{
			return WaitDialogs(parameters, typeof(T1), typeof(T2), typeof(T3), typeof(T4));
		}

		public static Coroutine<Dialog> WaitDialogs<T1, T2, T3, T4, T5>(TaskParameters parameters = null) where T1 : Dialog where T2 : Dialog where T3 : Dialog where T4 : Dialog where T5 : Dialog
		{
			return WaitDialogs(parameters, typeof(T1), typeof(T2), typeof(T3), typeof(T4), typeof(T5));
		}

		public static Coroutine<Dialog> WaitDialogs<T1, T2, T3, T4, T5, T6>(TaskParameters parameters = null) where T1 : Dialog where T2 : Dialog where T3 : Dialog where T4 : Dialog where T5 : Dialog where T6 : Dialog
		{
			return WaitDialogs(parameters, typeof(T1), typeof(T2), typeof(T3), typeof(T4), typeof(T5), typeof(T6));
		}

		public static Coroutine<Dialog> WaitDialogs<T1, T2, T3, T4, T5, T6, T7>(TaskParameters parameters = null) where T1 : Dialog where T2 : Dialog where T3 : Dialog where T4 : Dialog where T5 : Dialog where T6 : Dialog where T7 : Dialog
		{
			return WaitDialogs(parameters, typeof(T1), typeof(T2), typeof(T3), typeof(T4), typeof(T5), typeof(T6), typeof(T7));
		}

		public static Coroutine<Dialog> WaitDialogs<T1, T2, T3, T4, T5, T6, T7, T8>(TaskParameters parameters = null) where T1 : Dialog where T2 : Dialog where T3 : Dialog where T4 : Dialog where T5 : Dialog where T6 : Dialog where T7 : Dialog where T8 : Dialog
		{
			return WaitDialogs(parameters, typeof(T1), typeof(T2), typeof(T3), typeof(T4), typeof(T5), typeof(T6), typeof(T7), typeof(T8));
		}

		public static async Coroutine<Dialog> WaitDialogs(TaskParameters parameters, params Type[] dialogsTypes)
		{
			var searchingDialogsInfo = dialogsTypes.Aggregate(string.Empty, (s, dialogType) => (s.Length == 0 ? string.Empty : $"{s}, ") + $"{dialogType.Name}");
			parameters = parameters ?? WaitDialogTaskParameters.Default;

			Logger.Instance.Script($@"{nameof(WaitDialogs)} {searchingDialogsInfo} processing...");
			var time = 0f;
			Stopwatch stopwatch = null;
			if (parameters.UseUnscaledTime) {
				stopwatch = new Stopwatch();
				stopwatch.Start();
			}
			while (time <= parameters.Duration) {
				var dialog = DialogManager.Instance.ActiveDialogs.LastOrDefault(d => {
					var dialogType = d.GetType();
					return dialogsTypes.Any(i => i.IsAssignableFrom(dialogType));
				});
				if (dialog != null) {
					var done =
						parameters is WaitDialogTaskParameters waitDialogTaskParameters ?
						waitDialogTaskParameters.IsConditionMet(dialog) :
						dialog.State == DialogState.Shown;
					if (done) {
						Logger.Instance.Script($@"{nameof(WaitDialogs)} {dialog.GetType().Name} succeed. {GetVisibleDialogsInfo()}");
						return dialog;
					}
				}
				await Wait(parameters.Period);
				time += stopwatch?.ElapsedMilliseconds * 0.001f ?? Mathf.Max(Task.Current.Delta, parameters.Period);
				stopwatch?.Restart();
			}
			stopwatch?.Stop();
			Logger.Instance.Script($@"{nameof(WaitDialogs)} {searchingDialogsInfo} failed. {GetVisibleDialogsInfo()}");

			if (!parameters.IsStrictly) {
				return null;
			}
			var userAnswer = await MessageBox.Show($"Failed to found dialog {searchingDialogsInfo}.");
			return userAnswer == MessageBoxResult.Retry ? await WaitDialogs(parameters, dialogsTypes) : null;
		}

		private static string GetVisibleDialogsInfo()
		{
			var visibleDialogsInfo = DialogManager.Instance.ActiveDialogs.Aggregate(
				string.Empty,
				(s, dialog) => (s.Length == 0 ? string.Empty : $"{s}, ") + $"{dialog.GetType().Name} (State: {dialog.State})"
			);
			return visibleDialogsInfo.Length > 0 ? $"Now on screen: {visibleDialogsInfo}." : string.Empty;
		}

		public static Coroutine<bool> WaitWhileDialogOnScreen(Dialog dialog, TaskParameters parameters = null)
		{
			return WaitWhile($"{dialog.GetType().Name} is on screen", () => dialog.Root.Parent != null, parameters);
		}

		public static async Coroutine WaitForMarker(Node node, string markerId, string animationId = null, TaskParameters parameters = null)
		{
			Animation animation;
			if (animationId == null) {
				animation = node.DefaultAnimation;
			} else {
				node.Animations.TryFind(animationId, out animation);
				if (animation == null) {
					var msg = $"Animation with id {animationId} not found";
					Logger.Instance.Error(msg);
					await MessageBox.Show(msg);
					return;
				}
			}

			if (animation.Markers.All(marker => marker.Id != markerId)) {
				var msg = $"Marker with id {markerId} not found";
				Logger.Instance.Error(msg);
				await MessageBox.Show(msg);
				return;
			}

			await WaitWhile($"{markerId} marker in node {node}", () => animation.RunningMarkerId == markerId && animation.IsRunning, parameters);
		}

		public static async Coroutine<bool> WaitWhile(string name, Func<bool> condition, TaskParameters parameters = null)
		{
			parameters = parameters ?? TaskParameters.Default;

			Logger.Instance.Script($@"{nameof(WaitWhile)}({name}) processing...");
			var time = 0f;
			Stopwatch stopwatch = null;
			if (parameters.UseUnscaledTime) {
				stopwatch = new Stopwatch();
				stopwatch.Start();
			}
			while (time <= parameters.Duration) {
				if (!condition()) {
					Logger.Instance.Script($@"{nameof(WaitWhile)}({name}) succeed.");
					return true;
				}
				await Wait(parameters.Period);
				time += stopwatch?.ElapsedMilliseconds * 0.001f ?? Mathf.Max(Task.Current.Delta, parameters.Period);
				stopwatch?.Restart();
			}
			stopwatch?.Stop();
			Logger.Instance.Script($@"{nameof(WaitWhile)}({name}) failed.");

			if (!parameters.IsStrictly) {
				return false;
			}
			var userAnswer = await MessageBox.Show($"Failed to {nameof(WaitWhile)}({name}).");
			return userAnswer == MessageBoxResult.Retry && await WaitWhile(name, condition, parameters);
		}

		public static async Coroutine WaitUnscaledTime(float duration)
		{
			Logger.Instance.Script($@"{nameof(WaitUnscaledTime)}({duration:F3} s) processing...");

			var stopwatch = Stopwatch.StartNew();
			while (stopwatch.ElapsedMilliseconds * 0.001f < duration) {
				await WaitNextFrame();
			}
			stopwatch.Stop();

			Logger.Instance.Script($@"{nameof(WaitUnscaledTime)}({duration:F3} s) done.");
		}

		public static async Coroutine<bool> ScrollToListViewItem(ListView listView, string widgetPath, TaskParameters parameters = null)
		{
			var widget = await WaitNode<Widget>(widgetPath, listView.Content, parameters);
			if (widget == null) {
				Logger.Instance.Warn($@"{nameof(ScrollToListViewItem)}(""{widgetPath}"") failed.");
				return false;
			}

			await ScrollToListViewItem(listView, widget);
			Logger.Instance.Script($@"{nameof(ScrollToListViewItem)}(""{widgetPath}"") succeed.");
			return true;
		}

		public static async Coroutine ScrollToListViewItem(ListView listView, Widget widget)
		{
			var scrollPosition = listView.ProjectToScrollAxis(widget.CalcPositionInSpaceOf(listView.Content));
			listView.ScrollTo(scrollPosition - listView.Frame.Height * 0.5f, instantly: true);
			await RenderThisFrame();
		}

		public static async Coroutine<bool> ClickOnListViewItem(ListView listView, string widgetPath, TaskParameters parameters = null)
		{
			var widget = await WaitNode<Widget>(widgetPath, listView.Content, parameters);
			var result = widget != null && await ClickOnListViewItem(listView, widget);
			if (result) {
				Logger.Instance.Script($@"{nameof(ClickOnListViewItem)}(""{widgetPath}"") succeed.");
			} else {
				Logger.Instance.Warn($@"{nameof(ClickOnListViewItem)}(""{widgetPath}"") failed.");
			}
			return result;
		}

		public static async Coroutine<bool> ClickOnListViewItem(ListView listView, Widget widget, ClickWidgetTaskParameters clickWidgetParameters = null)
		{
			await ScrollToListViewItem(listView, widget);
			return await Click(widget, clickWidgetParameters);
		}

		public static async Coroutine<bool> Click(string widgetPath, Node root = null, TaskParameters waitWidgetParameters = null, ClickWidgetTaskParameters clickWidgetParameters = null)
		{
			var widget = await WaitNode<Widget>(widgetPath, root, waitWidgetParameters ?? WaitNodeTaskParameters.Immediately);
			var result = widget != null && await Click(widget, clickWidgetParameters);
			if (result) {
				Logger.Instance.Script($@"{nameof(Click)}(""{widgetPath}"") clicked.");
			} else {
				Logger.Instance.Warn($@"{nameof(Click)}(""{widgetPath}"") failed.");
			}
			return result;
		}

		public static async Coroutine<bool> Click(Widget widget, ClickWidgetTaskParameters parameters = null)
		{
			parameters = parameters ?? ClickWidgetTaskParameters.Default;

			var time = 0f;
			Stopwatch stopwatch = null;
			if (parameters.UseUnscaledTime) {
				stopwatch = new Stopwatch();
				stopwatch.Start();
			}
			var isInsideWindow = false;
			var visible = false;
			var enable = false;
			while (time <= parameters.Duration) {
				isInsideWindow = new Rectangle(Vector2.Zero, The.World.Size).Contains(widget.GlobalCenter);
				visible = widget.GloballyVisible;
				enable = widget.GloballyEnabled;
				if (parameters.IsConditionMet(widget, isInsideWindow, visible, enable)) {
					await Click(widget.GlobalCenter);
					Logger.Instance.Script($@"{nameof(Click)}(""{widget}"") clicked.");
					return true;
				}
				await Wait(parameters.Period);
				time += stopwatch?.ElapsedMilliseconds * 0.001f ?? Mathf.Max(Task.Current.Delta, parameters.Period);
				stopwatch?.Restart();
			}

			if (!parameters.IsStrictly) {
				return false;
			}
			var userAnswer = await MessageBox.Show($"Failed to click widget: {widget}.\nInside screen: {isInsideWindow}; Visible: {visible}; Enable: {enable}.");
			return userAnswer == MessageBoxResult.Retry && await Click(widget, parameters);
		}

		public static Coroutine Click(Vector2 position) => PressMouseAndRelease(position, position, 3);

		public static Coroutine Drag(Vector2 pressPosition, Vector2 releasePosition) => PressMouseAndRelease(pressPosition, releasePosition, 10);

		private static async Coroutine PressMouseAndRelease(Vector2 pressPosition, Vector2 releasePosition, int mouseMovingFrames)
		{
			var cursor = RemoteScripting.Frame?.Find<Frame>("Cursor");
			var moving = cursor?.Animations.Find("Moving");
			var pressing = cursor?.Animations.Find("Pressing");
			void OnMouseAction(Vector2 p, bool? isPressed = null)
			{
				if (cursor != null) {
					cursor.Position = p;
				}
				moving?.Run("Moved");
				if (isPressed.HasValue) {
					pressing?.Run(isPressed.Value ? "Pressed" : "Released");
				}
			}

			try {
				await RenderThisFrame();
				InputSimulator.Begin();
				InputSimulator.MouseMove(pressPosition);
				OnMouseAction(pressPosition, isPressed: false);
				await RenderThisFrame();
				InputSimulator.MouseMove(pressPosition);
				InputSimulator.PressMouse0();
				OnMouseAction(pressPosition, isPressed: true);
				await RenderThisFrame();

				for (var i = 0; i < mouseMovingFrames; i++) {
					var progress = Mathf.Clamp((float)i / mouseMovingFrames, 0, 1);
					var position = Vector2.Lerp(progress, pressPosition, releasePosition);
					InputSimulator.MouseMove(position);
					OnMouseAction(position);
					await RenderThisFrame();
				}

				InputSimulator.MouseMove(releasePosition);
				InputSimulator.ReleaseMouse0();
				OnMouseAction(releasePosition, isPressed: false);
				await RenderThisFrame();
			} finally {
				InputSimulator.MouseMove(The.World.Size * 1.1f);
				InputSimulator.End();
			}
		}

		public static async Coroutine<bool> MoveMouseAndPressKeys(Widget widget, ClickWidgetTaskParameters parameters = null, params Key[] keys)
		{
			parameters = parameters ?? ClickWidgetTaskParameters.Default;

			var time = 0f;
			Stopwatch stopwatch = null;
			if (parameters.UseUnscaledTime) {
				stopwatch = new Stopwatch();
				stopwatch.Start();
			}
			var isInsideWindow = false;
			var visible = false;
			var enable = false;
			while (time <= parameters.Duration) {
				isInsideWindow = new Rectangle(Vector2.Zero, The.World.Size).Contains(widget.GlobalCenter);
				visible = widget.GloballyVisible;
				enable = widget.GloballyEnabled;
				if (parameters.IsConditionMet(widget, isInsideWindow, visible, enable)) {
					await MoveMouseAndPressKeys(widget.GlobalCenter, keys);
					Logger.Instance.Script($@"{nameof(MoveMouseAndPressKeys)}(""{widget}"") finished.");
					return true;
				}
				await Wait(parameters.Period);
				time += stopwatch?.ElapsedMilliseconds * 0.001f ?? Mathf.Max(Task.Current.Delta, parameters.Period);
				stopwatch?.Restart();
			}

			if (!parameters.IsStrictly) {
				return false;
			}
			var userAnswer = await MessageBox.Show($"Failed to move mouse to widget: {widget}.\nInside screen: {isInsideWindow}; Visible: {visible}; Enable: {enable}.");
			return userAnswer == MessageBoxResult.Retry && await MoveMouseAndPressKeys(widget, parameters, keys);
		}

		private static async Coroutine MoveMouseAndPressKeys(Vector2 position, params Key[] keys)
		{
			var cursor = RemoteScripting.Frame?.Find<Frame>("Cursor");
			var moving = cursor?.Animations.Find("Moving");
			var pressing = cursor?.Animations.Find("Pressing");
			void OnMouseAction(Vector2 p, bool? isPressed = null)
			{
				if (cursor != null) {
					cursor.Position = p;
				}
				moving?.Run("Moved");
				if (isPressed.HasValue) {
					pressing?.Run(isPressed.Value ? "Pressed" : "Released");
				}
			}

			try {
				await RenderThisFrame();
				InputSimulator.Begin();
				InputSimulator.MouseMove(position);
				OnMouseAction(position, isPressed: false);
				await RenderThisFrame();
				InputSimulator.MouseMove(position);
				foreach (var key in keys) {
					InputSimulator.PressKey(key);
				}
				OnMouseAction(position, isPressed: true);
				await RenderThisFrame();

				InputSimulator.MouseMove(position);
				foreach (var key in keys) {
					InputSimulator.ReleaseKey(key);
				}
				OnMouseAction(position, isPressed: false);
				await RenderThisFrame();
			} finally {
				InputSimulator.MouseMove(The.World.Size * 1.1f);
				InputSimulator.End();
			}
		}

		public static async Coroutine<bool> Cheat(string cheatPath, TaskParameters parameters = null)
		{
			parameters = parameters ?? TaskParameters.Default;

			Logger.Instance.Script($@"{nameof(Cheat)} ""{cheatPath}"" processing...");
			var menu = Cheats.ShowMenu();
			var existCheat = RainbowDash.Menu.Cheat(cheatPath);
			if (menu.IsShown) {
				menu.Hide();
			}

			if (!existCheat && parameters.IsStrictly) {
				Logger.Instance.Warn($@"{nameof(Cheat)} ""{cheatPath}"" failed.");
				var userAnswer = await MessageBox.Show($"Failed to found cheat: \"{cheatPath}\".");
				if (userAnswer != MessageBoxResult.Retry) {
					return false;
				}
				await WaitNextFrame();
				return await Cheat(cheatPath, parameters);
			}
			var keyStatus = RemoteScripting.Frame?.Find<Frame>("KeyStatus");
			if (keyStatus != null) {
				keyStatus["SimpleText"].Text = cheatPath;
				keyStatus.RunAnimation("ShowAndHide", "Presentation");
			}
			await RenderThisFrame();
			Logger.Instance.Script($@"{nameof(Cheat)} ""{cheatPath}"" succeed.");
			return true;
		}

		public static async Coroutine<byte[]> RequestRemoteFile(string path, TaskParameters parameters = null)
		{
			parameters = parameters ?? TaskParameters.Default;

			Logger.Instance.Script($@"{nameof(RequestRemoteFile)}(""{path}"") processing...");

			var requestResult = new TaskResult<byte[]>();
			var time = 0f;
			Stopwatch stopwatch = null;
			if (parameters.UseUnscaledTime) {
				stopwatch = new Stopwatch();
				stopwatch.Start();
			}
			var task = ScriptingToolbox.PreEarlyTasks.Add(RemoteScripting.RequestRemoteFileTask(path, requestResult));
			while (time <= parameters.Duration) {
				if (task.Completed) {
					if (requestResult.Value == null) {
						break;
					}
					Logger.Instance.Script($@"{nameof(RequestRemoteFile)}(""{path}"") succeed.");
					return requestResult.Value;
				}
				await Wait(parameters.Period);
				time += stopwatch?.ElapsedMilliseconds * 0.001f ?? Mathf.Max(Task.Current.Delta, parameters.Period);
				stopwatch?.Restart();
			}
			stopwatch?.Stop();
			Logger.Instance.Warn($@"{nameof(RequestRemoteFile)}(""{path}"") failed.");
			ScriptingToolbox.PreEarlyTasks.Stop(t => t == task);

			if (!parameters.IsStrictly) {
				return null;
			}
			var userAnswer = await MessageBox.Show($@"{nameof(RequestRemoteFile)}(""{path}"") failed.");
			return userAnswer == MessageBoxResult.Retry ? await RequestRemoteFile(path, parameters) : null;
		}

		private static readonly System.Reflection.FieldInfo worldRenderChainField =
			typeof(WindowWidget).GetField("renderChain", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
		private static UpdatingParameters? worldUpdatingParameters;

		private static void SetTimeBoost(bool speedMode, bool vSync, UpdatingParameters? updatingParameters = null)
		{
			var application = Application.Application.Instance;
			application.TimeAccelerationMode = speedMode;
			Window.Current.VSync = vSync;
			application.CalculatingWorldUpdatingParameters -= CalculatingWorldUpdatingParameters;
			application.CustomWorldUpdating -= ScriptingToolbox.CustomWorldUpdating;
			The.World.RenderChainBuilder = The.World;
			ScriptingToolbox.WorldRenderChain = null;
			if (updatingParameters.HasValue) {
				worldUpdatingParameters = updatingParameters.Value;
				application.CalculatingWorldUpdatingParameters += CalculatingWorldUpdatingParameters;
				application.CustomWorldUpdating += ScriptingToolbox.CustomWorldUpdating;
				The.World.RenderChainBuilder = null;
				ScriptingToolbox.WorldRenderChain = (RenderChain)worldRenderChainField.GetValue(The.World);
			} else {
				worldUpdatingParameters = null;
			}
			WaitForRenderingOnNextFrame();
		}

		private static void CalculatingWorldUpdatingParameters(ref float delta, ref int iterationsCount, ref bool isTimeQuantized)
		{
			if (!worldUpdatingParameters.HasValue) {
				throw new InvalidOperationException();
			}

			iterationsCount = worldUpdatingParameters.Value.IterationsCount;
			delta = worldUpdatingParameters.Value.Delta;
			isTimeQuantized = true;
		}

		public static void WaitForRenderingOnNextFrame() => The.App.WaitForRenderingOnNextFrame();

		public class FrozenTimeFlow : TimeFlow
		{
			public FrozenTimeFlow(bool applyImmediately = true) : base(false, true, new UpdatingParameters { Delta = 0, IterationsCount = 1 }, applyImmediately) { }
		}

		public class SlowMotionTimeFlow : TimeFlow
		{
			public SlowMotionTimeFlow(bool applyImmediately = true) : base(false, true, new UpdatingParameters { Delta = 0.0017f, IterationsCount = 1 }, applyImmediately) { }
		}

		public class NormalTimeFlow : TimeFlow
		{
			public NormalTimeFlow(bool applyImmediately = true) : base(applyImmediately: applyImmediately) { }
		}

		public class AcceleratedTimeFlow : TimeFlow
		{
			public AcceleratedTimeFlow(bool applyImmediately = true)
				: base(false, true, new UpdatingParameters { Delta = Lime.Application.MaxDelta, IterationsCount = 1 }, applyImmediately) { }
		}

		public class DisabledVSyncTimeFlow : TimeFlow
		{
			public DisabledVSyncTimeFlow(bool applyImmediately = true)
				: base(false, false, new UpdatingParameters { Delta = Lime.Application.MaxDelta, IterationsCount = 1 }, applyImmediately) { }
		}

		public class FramesSkippingTimeFlow : TimeFlow
		{
			public FramesSkippingTimeFlow(bool applyImmediately = true)
				: base(false, false, new UpdatingParameters { Delta = Lime.Application.MaxDelta, IterationsCount = (30f / Lime.Application.MaxDelta).Ceiling() }, applyImmediately) { }
		}

		public abstract class TimeFlow : IDisposable
		{
			private readonly bool speedMode;
			private readonly bool vSync;
			private readonly UpdatingParameters? updatingParameters;

			private bool savedSpeedMode;
			private bool savedVSync;
			private UpdatingParameters? savedUpdatingParameters;
			private bool wasApplied;
			private bool isDisposed;

			protected TimeFlow(bool speedMode = false, bool vSync = true, UpdatingParameters? updatingParameters = null, bool applyImmediately = true)
			{
				this.speedMode = speedMode;
				this.vSync = vSync;
				this.updatingParameters = updatingParameters;
				if (applyImmediately) {
					Apply();
				}
			}

			public void Apply()
			{
				if (wasApplied) {
					return;
				}
				var application = Application.Application.Instance;
				savedSpeedMode = application.TimeAccelerationMode;
				savedVSync = Window.Current.VSync;
				savedUpdatingParameters = worldUpdatingParameters;

				SetTimeBoost(speedMode, vSync, updatingParameters);
				wasApplied = true;
			}

			public void Dispose()
			{
				if (wasApplied && !isDisposed) {
					isDisposed = true;
					SetTimeBoost(savedSpeedMode, savedVSync, savedUpdatingParameters);
				}
			}
		}

		public struct UpdatingParameters
		{
			public static readonly UpdatingParameters Default = new UpdatingParameters { IterationsCount = 1, Delta = Lime.Application.MaxDelta };

			public int IterationsCount;
			public float Delta;
		}
	}
}
