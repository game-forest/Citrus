using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Lime
{
	public class Input
	{
		public const int MaxTouches = 4;
		public float KeyRepeatDelay = 0.2f;
		public float KeyRepeatInterval = 0.03f;
		public bool Changed { get; private set; }

		private struct KeyEvent
		{
			public Key Key;
			public bool State;
		}

		public readonly InputSimulator Simulator;

		private readonly Vector2[] desktopTouchPositions = new Vector2[MaxTouches];
		private readonly List<KeyEvent> keyEventQueue = new List<KeyEvent>();

		private struct KeyState
		{
			public bool PreviousState;
			public bool CurrentState;
			public float RepeatDelay;
			public bool Repeated;
		}

		private readonly KeyState[] keys = new KeyState[Key.MaxCount];

		/// <summary>
		/// The current mouse position in desktop coordinates. (read only)
		/// </summary>
		public Vector2 DesktopMousePosition { get; internal set; }

		/// <summary>
		/// Indicates how much the mouse wheel was moved
		/// </summary>
		public float WheelScrollAmount { get; private set; }

		/// <summary>
		/// Gets the current acceleration in g-force units. This vector is based on the device in a "natural"
		/// orientation (portrait-up on iOS and device-specific on Android). The value is not changed when switching
		/// between orientations.
		/// </summary>
		public Vector3 NativeAcceleration { get; internal set; }

		/// <summary>
		/// Gets the current acceleration in g-force units. This vector is specified in the current coordinate space,
		/// which takes into account any interface rotations in effect for the device. Therefore,
		/// the value of this property may change when the device rotates between portrait and landscape orientations.
		/// </summary>
		public Vector3 Acceleration { get; internal set; }

		public Input()
		{
			Simulator = new InputSimulator(this);
		}

		/// <summary>
		/// Returns true while the user holds down the key identified by name. Think auto fire.
		/// </summary>
		public bool IsKeyPressed(Key key) => keys[key.Code].CurrentState;

		public Modifiers GetModifiers()
		{
			var result = Modifiers.None;
			if (IsKeyPressed(Key.Shift)) {
				result |= Modifiers.Shift;
			}
			if (IsKeyPressed(Key.Alt)) {
				result |= Modifiers.Alt;
			}
			if (IsKeyPressed(Key.Control)) {
				result |= Modifiers.Control;
			}
			if (IsKeyPressed(Key.Win)) {
				result |= Modifiers.Win;
			}
			return result;
		}

		/// <summary>
		/// Returns true during the frame the user releases the key identified by name.
		/// </summary>
		public bool WasKeyReleased(Key key) => !keys[key.Code].CurrentState && keys[key.Code].PreviousState;

		/// <summary>
		/// Returns true during the frame the user starts pressing down the key identified by name.
		/// </summary>
		public bool WasKeyPressed(Key key) => keys[key.Code].CurrentState && !keys[key.Code].PreviousState;

		public void ConsumeKey(Key key)
		{
			keys[key].PreviousState = keys[key].CurrentState;
			keys[key].Repeated = false;
		}

		/// <summary>
		/// Returns true during the frame the user starts pressing down
		/// the key identified by name or key event was repeated.
		/// </summary>
		public bool WasKeyRepeated(Key key) => keys[key.Code].Repeated;
		public bool WasMousePressed() => WasKeyPressed(GetMouseButtonByIndex(0));
		public bool WasMouseReleased() => WasKeyReleased(GetMouseButtonByIndex(0));
		public bool IsMousePressed() => IsKeyPressed(GetMouseButtonByIndex(0));
		public bool WasMousePressed(int button) => WasKeyPressed(GetMouseButtonByIndex(button));
		public bool WasMouseReleased(int button) => WasKeyReleased(GetMouseButtonByIndex(button));
		public bool IsMousePressed(int button) => IsKeyPressed(GetMouseButtonByIndex(button));

		public static Key GetMouseButtonByIndex(int button)
		{
			if (((uint)button) > 2) {
				throw new ArgumentException();
			}
			return Key.Mouse0 + button;
		}

		public bool WasDoubleClickPressed(int buttonIndex) => WasKeyPressed(GetDoubleClickByIndex(buttonIndex));

		public static Key GetDoubleClickByIndex(int button)
		{
			switch (button) {
				case 0: return Key.Mouse0DoubleClick;
				case 1: return Key.Mouse1DoubleClick;
				case 2: throw new NotImplementedException(
					$"DoubleClick Key not implemented for mouse button index {button}"
				);
				default: throw new NotImplementedException(
					$"DoubleClick Key not implemented for mouse button index {button}"
				);
			}
		}

		public bool WasTouchBegan(int index) => WasKeyPressed(GetTouchByIndex(index));
		public bool WasTouchEnded(int index) => WasKeyReleased(GetTouchByIndex(index));
		public bool IsTouching(int index) => IsKeyPressed(GetTouchByIndex(index));

		public static Key GetTouchByIndex(int index)
		{
			if (((uint)index) >= 4) {
				throw new ArgumentException();
			}
			return Key.Touch0 + index;
		}

		public Vector2 GetDesktopTouchPosition(int index) => desktopTouchPositions[index];
		internal void SetDesktopTouchPosition(int index, Vector2 position) => desktopTouchPositions[index] = position;

		public int GetNumTouches()
		{
			int j = 0;
			for (int i = 0; i < MaxTouches; i++) {
				if (IsTouching(i)) {
					j++;
				}
			}
			return j;
		}

		private string textInput;
		public string TextInput
		{
			get { return textInput; }
			internal set
			{
				Changed |= value != null;
				textInput = value;
			}
		}

		private Key currentShortcut = Key.Unknown;

		internal void SetKeyState(Key key, bool value) => keyEventQueue.Add(new KeyEvent { Key = key, State = value });

		internal void ProcessPendingInputEvents(float delta)
		{
			ProcessPendingKeyEvents(delta);
		}

		internal void ProcessPendingKeyEvents(float delta)
		{
			Changed = false;
			for (int i = 0; i < Key.Count; i++) {
				var key = keys[i];
				key.Repeated = false;
				if (key.CurrentState) {
					key.RepeatDelay -= delta;
					if (key.RepeatDelay <= 0) {
						key.RepeatDelay = KeyRepeatInterval;
						key.Repeated = true;
						Changed = true;
					}
				}
				keys[i] = key;
			}
			if (keyEventQueue.Count > 0) {
				Changed = true;
				var processedKeys = new BitArray(Key.MaxCount);
				for (int i = 0; i < keyEventQueue.Count; i++) {
					var evt = keyEventQueue[i];
					if (!processedKeys[evt.Key]) {
						ProcessKeyEvent(evt.Key, evt.State);
						processedKeys[evt.Key] = true;
						keyEventQueue[i] = new KeyEvent();
					}
				}
				keyEventQueue.RemoveAll(i => i.Key == Key.Unknown);
			}
		}

		private Key FindMainKey()
		{
			for (var i = Key.Unknown; i < Key.LastNormal; i++) {
				if (keys[i].CurrentState && Shortcut.ValidateMainKey(i)) {
					return i;
				}
			}
			return Key.Unknown;
		}

		private void ProcessKeyEvent(Key key, bool value)
		{
			// Dont let system key repeat events get to us, for our key repeat mechanism to work properly
			if (keys[key].CurrentState && value) {
				return;
			}
			var previousCurrentShortcut = currentShortcut;
			if (currentShortcut != Key.Unknown) {
				// If currentShortcut == key, we will restore state by the next call.
				SetKeyStateInternal(currentShortcut, false);
				currentShortcut = Key.Unknown;
			}
			SetKeyStateInternal(key, value);
			if (value && key > Key.LastNormal) {
				// Shortcut was simulated by menu item.
				currentShortcut = key;
				return;
			}
			// Give priority to the last pressed key, choose arbitrarily among others.
			Key mainKey = value && Shortcut.ValidateMainKey(key) ? key : FindMainKey();
			if (mainKey == Key.Unknown) {
				return;
			}
			if (Key.ShortcutMap.TryGetValue(new Shortcut(GetModifiers(), mainKey), out currentShortcut)) {
				SetKeyStateInternal(currentShortcut, true);
				if (previousCurrentShortcut == currentShortcut) {
					// Prevent setting Repeated = true before key repeat timeout for given key in case
					// we're here because another key has been released. e.g. releasing key "3" while
					// still holding "Enter" key in edit box (see CIT-783)
					keys[currentShortcut].Repeated = false;
				}
			}
		}

		private void SetKeyStateInternal(Key key, bool value)
		{
			keys[key].CurrentState = value;
			keys[key].RepeatDelay = KeyRepeatDelay;
			keys[key].Repeated = value;
			Changed = true;
		}

		internal void CopyKeysState()
		{
			for (int i = 0; i < Key.Count; i++) {
				keys[i].PreviousState = keys[i].CurrentState;
			}
		}

		internal void ClearKeyState(bool clearMouseButtons = true)
		{
			for (int k = 0; k < Key.Count; k++) {
				if (!clearMouseButtons && ((Key)k).IsMouseKey() && !((Key)k).IsDoubleClickKey()) {
					continue;
				}
				keys[k].CurrentState = false;
				keys[k].Repeated = false;
			}
			keyEventQueue.Clear();
		}

		internal void SetWheelScrollAmount(float delta)
		{
			if (delta == 0) {
				return;
			}
			var key = delta > 0 ? Key.MouseWheelUp : Key.MouseWheelDown;
			if (!keyEventQueue.Any(i => i.Key == key)) {
				SetKeyState(key, true);
				SetKeyState(key, false);
				WheelScrollAmount = delta;
			} else {
				WheelScrollAmount += delta;
			}
		}

		public class InputSimulator
		{
			private readonly Input input;
			private int activeRunners;

			public InputSimulator(Input input)
			{
				this.input = input;
			}

			public int ActiveRunners
			{
				get => activeRunners;
				set
				{
					activeRunners = value;
					Window.Current.Input.IsSimulationRunning = activeRunners > 0;
				}
			}

			public delegate void ProcessingPendingInputEventsDelegate();
			public event ProcessingPendingInputEventsDelegate ProcessingPendingInputEvents;

			public void SetDesktopMousePosition(Vector2 position) => input.DesktopMousePosition = position;
			public void SetKeyState(Key key, bool value) => input.SetKeyState(key, value);
			public void SetWindowUnderMouse(IWindow window) => Application.WindowUnderMouse = window;

			public void OnBetweenFrames(float delta)
			{
				input.CopyKeysState();
				input.ProcessPendingInputEvents(delta);
				OnProcessingPendingInputEvents();
				input.TextInput = null;
			}

			public void OnProcessingPendingInputEvents()
			{
				ProcessingPendingInputEvents?.Invoke();
			}
		}
	}
}
