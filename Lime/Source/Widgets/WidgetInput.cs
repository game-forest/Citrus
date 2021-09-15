using System;
using System.Collections;
using System.Collections.Generic;

namespace Lime
{
	/// <summary>
	/// WidgetInput class allows a widget to capture an input device (mouse, keyboard).
	/// After capturing the device, the widget and all its children receive
	/// an actual buttons and device axes state (e.g. mouse position).
	/// Other widgets receive released buttons state and frozen axes values.
	/// </summary>
	public class WidgetInput : IDisposable
	{
		private readonly Widget widget;
		private WindowInput WindowInput => CommonWindow.Current.Input;
		private WidgetContext Context => WidgetContext.Current;

		private static readonly WidgetStack InputScopeStack = new WidgetStack();

		public delegate bool FilterFunc(Widget widget, Key key);
		public static FilterFunc Filter;

		public static bool AcceptMouseBeyondWidgetByDefault = true;

		/// <summary>
		/// Indicates whether mouse events should be accepted even the widget is not under the mouse cursor.
		/// </summary>
		public bool AcceptMouseBeyondWidget = AcceptMouseBeyondWidgetByDefault;

		/// <summary>
		/// Indicates whether mouse events should be accepted even the mouse is over one of widget's descendant.
		/// </summary>
		public bool AcceptMouseThroughDescendants;

		public WidgetInput(Widget widget)
		{
			this.widget = widget;
		}

		public string TextInput => widget.IsFocused() ? WindowInput.TextInput : string.Empty;

		public Vector2 MousePosition => WindowInput.MousePosition;

		[Obsolete("Use Widget.LocalMousePosition()")]
		public Vector2 LocalMousePosition => WindowInput.MousePosition * widget.LocalToWorldTransform.CalcInversed();

		public Vector2 GetTouchPosition(int index) => CommonWindow.Current.Input.GetTouchPosition(index);

		public int GetNumTouches() => IsAcceptingKey(Key.Touch0) ? WindowInput.GetNumTouches() : 0;

		public bool IsAcceptingMouse() => IsAcceptingKey(Key.Mouse0);

		/// <summary>
		/// Enumerates input scope stack from top to bottom.
		/// Top being the highest priority input scope widget.
		/// </summary>
		/// <returns></returns>
		public IEnumerable<Widget> EnumerateScopes()
		{
			for (int i = InputScopeStack.Count - 1; i >= 0; i--) {
				yield return InputScopeStack[i];
			}
		}

		/// <summary>
		/// Returns input scope widget with highest priority.
		/// </summary>
		/// <returns></returns>
		public Widget CurrentScope => InputScopeStack.Top;

		/// <summary>
		/// Removes widgets which are not <see cref="Widget.GloballyVisible"/> or
		/// not attached to <see cref="Lime.NodeManager"/> from InputScope.
		/// Should only be called once per frame because input scope stack is static.
		/// </summary>
		internal static void CleanScopeStack()
		{
			foreach (var w in InputScopeStack) {
				if (w.Manager == null || !w.GloballyVisible) {
					toRemoveFromScopeStack.Add(w);
				}
			}
			foreach (var w in toRemoveFromScopeStack) {
				InputScopeStack.Remove(w);
			}
		}
		private static List<Widget> toRemoveFromScopeStack = new List<Widget>();

		public bool IsAcceptingKey(Key key)
		{
			if (Filter != null && !Filter(widget, key)) {
				return false;
			}
			if (InputScopeStack.Top != null && !widget.SameOrDescendantOf(InputScopeStack.Top)) {
				return false;
			}
			if (key.IsMouseKey()) {
				if (AcceptMouseBeyondWidget) {
					return true;
				}
				var node = Context.NodeCapturedByMouse ?? Context.NodeUnderMouse;
				return AcceptMouseThroughDescendants ? (node?.SameOrDescendantOf(widget) ?? false) : node == widget;
			}
			if (key.IsModifier()) {
				return true;
			}
			var focused = Widget.Focused;
			return focused != null && focused.SameOrDescendantOf(widget);
		}

		public bool IsMousePressed(int button = 0) => IsKeyPressed(Input.GetMouseButtonByIndex(button));

		public bool WasMousePressed(int button = 0) => WasKeyPressed(Input.GetMouseButtonByIndex(button));

		public bool WasMouseReleased(int button = 0) => WasKeyReleased(Input.GetMouseButtonByIndex(button));

		public bool WasAnyMouseButtonPressed()
		{
			return WasKeyPressed(Key.Mouse0) ||
				WasKeyPressed(Key.Mouse1) ||
				WasKeyPressed(Key.Mouse2) ||
				WasKeyPressed(Key.MouseBack) ||
				WasKeyPressed(Key.MouseForward);
		}

		public float WheelScrollAmount => IsAcceptingKey(Key.MouseWheelUp) ? WindowInput.WheelScrollAmount : 0;

		public bool IsKeyPressed(Key key) => WindowInput.IsKeyPressed(key) && IsAcceptingKey(key);

		public bool WasKeyPressed(Key key) => WindowInput.WasKeyPressed(key) && IsAcceptingKey(key);

		public bool ConsumeKeyPress(Key key)
		{
			if (WasKeyPressed(key)) {
				ConsumeKey(key);
				return true;
			}
			return false;
		}

		public bool WasKeyReleased(Key key) => WindowInput.WasKeyReleased(key) && IsAcceptingKey(key);

		public bool ConsumeKeyRelease(Key key)
		{
			if (WasKeyReleased(key)) {
				ConsumeKey(key);
				return true;
			}
			return false;
		}

		public bool WasKeyRepeated(Key key) => WindowInput.WasKeyRepeated(key) && IsAcceptingKey(key);

		public bool ConsumeKeyRepeat(Key key)
		{
			if (WasKeyRepeated(key)) {
				ConsumeKey(key);
				return true;
			}
			return false;
		}

		public void ConsumeKey(Key key)
		{
			if (IsAcceptingKey(key)) {
				WindowInput.ConsumeKey(key);
			}
		}

		public void ConsumeKeys(List<Key> keys)
		{
			foreach (var key in keys) {
				ConsumeKey(key);
			}
		}

		public void ConsumeKeys(IEnumerable<Key> keys)
		{
			foreach (var key in keys) {
				ConsumeKey(key);
			}
		}

		/// <summary>
		/// Restricts input scope with the current widget and its descendants.
		/// </summary>
		public void RestrictScope()
		{
			InputScopeStack.Add(widget);
		}

		/// <summary>
		/// Derestricts input scope from the current widget and its descendants.
		/// </summary>
		public void DerestrictScope() => InputScopeStack.Remove(widget);

		[Obsolete("Use RestrictScope() instead")]
		public void CaptureAll() => RestrictScope();

		[Obsolete("Use DerestrictScope() instead")]
		public void ReleaseAll() => DerestrictScope();

		private class WidgetStack : IReadOnlyList<Widget>
		{
			private readonly List<Widget> stack = new List<Widget>();

			public Widget Top { get; private set; }

			public void Add(Widget widget)
			{
				var thisLayer = widget.GetEffectiveLayer();
				var t = stack.FindLastIndex(i => i.GetEffectiveLayer() <= thisLayer);
				stack.Insert(t + 1, widget);
				RefreshTop();
			}

			public void Remove(Widget widget)
			{
				var i = stack.IndexOf(widget);
				if (i >= 0) {
					stack.RemoveAt(i);
				}
				RefreshTop();
			}

			public void RemoveAll(Predicate<Widget> match)
			{
				stack.RemoveAll(match);
				RefreshTop();
			}

			private void RefreshTop()
			{
				int i = stack.Count;
				Top = i > 0 ? stack[i - 1] : null;
			}

			public IEnumerator<Widget> GetEnumerator() => stack.GetEnumerator();

			IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable)stack).GetEnumerator();

			public int Count => stack.Count;

			public Widget this[int index] => stack[index];
		}

		public void Dispose() => InputScopeStack.RemoveAll(i => i == widget);
	}
}
