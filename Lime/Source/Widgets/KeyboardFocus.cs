using System;
using System.Collections.Generic;
using System.Linq;

namespace Lime
{
	/// <summary>
	/// Controls whether a widget could be traversed with Tab or Shift+Tab.
	/// </summary>
	public class TabTraversable
	{
		public int Order { get; set; }
	}

	/// <summary>
	/// Switches keyboard focus with Tab/Shift+Tab and makes the widget focused by mouse click.
	/// </summary>
	public class KeyboardFocusScope
	{
		public readonly List<Key> FocusNext = new List<Key> { Key.MapShortcut(Key.Tab) };
		public readonly List<Key> FocusPrevious = new List<Key> {
			Key.MapShortcut(new Shortcut(Modifiers.Shift, Key.Tab))
		};
		private Widget lastFocused;

		public readonly Widget Widget;
		public bool FocusOnMousePress { get; set; }
		public Direction LastDirection { get; private set; }

		public KeyboardFocusScope(Widget widget)
		{
			FocusOnMousePress = true;
			Widget = widget;
			widget.HitTestTarget = true;
			widget.Input.AcceptMouseThroughDescendants = true;
			widget.Tasks.AddLoop(HandleSetFocusOnMousePress);
			widget.LateTasks.AddLoop(HandleFocusSwitchWithKeyboard);
		}

		public static KeyboardFocusScope GetEnclosingScope(Widget widget)
		{
			return widget.Ancestors
				.OfType<Widget>()
				.Select(i => i.FocusScope)
				.FirstOrDefault(i => i != null);
		}

		private void HandleSetFocusOnMousePress()
		{
			var focused = Widget.Focused;
			if (FocusOnMousePress && Widget.Input.WasAnyMouseButtonPressed()) {
				if (
					focused == null ||
					!focused.SameOrDescendantOf(Widget) ||
					!focused.LocalHitTest(Widget.Input.MousePosition) ||
					!focused.GloballyVisible
				) {
					Widget.SetFocus();
				}
			}
		}

		private void HandleFocusSwitchWithKeyboard()
		{
			var focused = Widget.Focused;
			if (focused != null && focused.DescendantOf(Widget)) {
				lastFocused = focused;
			}
			foreach (var key in FocusNext) {
				if (Widget.Input.ConsumeKeyRepeat(key)) {
					AdvanceFocus(Direction.Forward);
					return;
				}
			}
			foreach (var key in FocusPrevious) {
				if (Widget.Input.ConsumeKeyRepeat(key)) {
					AdvanceFocus(Direction.Backward);
					return;
				}
			}
		}

		public void AdvanceFocus(Direction direction)
		{
			LastDirection = direction;
			if (!CanRegainFocus()) {
				var focused = GetFirstFocusable() ?? Widget;
				Widget.SetFocus(focused);
				lastFocused = focused;
			} else if (Widget.Focused == Widget) {
				lastFocused.SetFocus();
			} else {
				var traversables = GetTabTraversables(Widget).ToList();
				if (traversables.Count > 0) {
					var i = traversables.IndexOf(lastFocused);
					i = (i < 0) ? 0 : Mathf.Wrap(i + (int)direction, 0, traversables.Count - 1);
					traversables[i].SetFocus();
					lastFocused = Widget.Focused;
				}
			}
		}

		public Widget GetFirstFocusable() => GetTabTraversables(Widget).FirstOrDefault();

		private bool CanRegainFocus()
		{
			return lastFocused != null && lastFocused.GloballyVisible && lastFocused.DescendantOf(Widget);
		}

		private static IEnumerable<Widget> GetTabTraversables(Widget root)
		{
			return root.Descendants
				.OfType<Widget>()
				.Where(i => i.TabTravesable != null && i.GloballyVisible)
				.OrderBy(i => i.TabTravesable.Order);
		}

		public void SetDefaultFocus()
		{
			var firstFocusable = GetTabTraversables(Widget).FirstOrDefault();
			if (firstFocusable != null) {
				firstFocusable.SetFocus();
			} else {
				Widget.SetFocus();
			}
		}

		public enum Direction : int
		{
			Forward = 1,
			Backward = -1
		}
	}
}
