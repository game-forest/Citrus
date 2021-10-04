using Lime;

namespace Match3.Scripts.Common
{
	public static class InputSimulator
	{
		private static Vector2? mousePosition;

		public static void Begin()
		{
			Lime.Application.Input.Simulator.ActiveRunners++;
			Lime.Application.Input.Simulator.ProcessingPendingInputEvents += ProcessingPendingInputEvents;
			mousePosition = null;
		}

		public static void End()
		{
			Lime.Application.Input.Simulator.ActiveRunners--;
			Lime.Application.Input.Simulator.ProcessingPendingInputEvents -= ProcessingPendingInputEvents;
			mousePosition = null;
		}

		public static void MouseMove(Vector2 position)
		{
			mousePosition = position;
			Lime.Application.Input.Simulator.SetDesktopMousePosition(The.Window.Input.MouseLocalToDesktop(position));
			Lime.Application.Input.Simulator.SetWindowUnderMouse(The.Window);
		}

		public static void PressMouse0()
		{
			PressKey(Key.Mouse0);
			PressKey(Key.Touch0);
		}

		public static void ReleaseMouse0()
		{
			ReleaseKey(Key.Mouse0);
			ReleaseKey(Key.Touch0);
		}

		public static void PressKey(Key key) => Lime.Application.Input.Simulator.SetKeyState(key, true);

		public static void ReleaseKey(Key key) => Lime.Application.Input.Simulator.SetKeyState(key, false);

		private static void ProcessingPendingInputEvents()
		{
			if (mousePosition.HasValue) {
				MouseMove(mousePosition.Value);
			}
		}
	}
}
