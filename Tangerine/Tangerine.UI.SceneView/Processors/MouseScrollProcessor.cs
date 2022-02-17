using System;
using System.Linq;
using System.Collections.Generic;
using Lime;

namespace Tangerine.UI.SceneView
{
	internal class MouseScrollProcessor : Core.ITaskProvider
	{
		private SceneView SceneView => SceneView.Instance;
		private static Vector2 mouseStartPos;

		public IEnumerator<object> Task()
		{
			var prevPos = SceneView.Input.MousePosition;
			while (true) {
				if (SceneView.Input.IsKeyPressed(Key.Space) && SceneView.InputArea.IsMouseOver()) {
					Utils.ChangeCursorIfDefault(Cursors.DragHandOpen);
				}

				if (
					SceneView.Input.WasMousePressed(0) && CommonWindow.Current.Input.IsKeyPressed(Key.Space)
					|| SceneView.Input.WasMousePressed(2)
				) {
					var initialMouse = SceneView.Input.MousePosition;
					var initialPosition = SceneView.Scene.Position;
					SceneView.Input.ConsumeKey(Key.Mouse0);
					SceneView.Input.ConsumeKey(Key.Mouse2);
					while (SceneView.Input.IsMousePressed(0) || SceneView.Input.IsMousePressed(2)) {
						Utils.ChangeCursorIfDefault(Cursors.DragHandClosed);
						SceneView.Scene.Position = SceneView.Input.MousePosition - initialMouse + initialPosition;
						yield return null;
					}
				}

				if (SceneView.Input.WasKeyPressed(Key.Alt) || SceneView.Input.WasMousePressed(1)) {
					mouseStartPos = SceneView.MousePosition;
				}

				if (SceneView.Input.WasKeyPressed(Key.MouseWheelDown)) {
					ZoomCanvas(-1);
				} else if (SceneView.Input.WasKeyPressed(Key.MouseWheelUp)) {
					ZoomCanvas(1);
				} else if (SceneView.Input.IsKeyPressed(Key.Alt) && SceneView.Input.IsKeyPressed(Key.Mouse1)) {
					var delta = (SceneView.Input.MousePosition - prevPos).X;
					var size = (float)Math.Sqrt(SceneView.Scene.Size.SqrLength);
					ZoomCanvas(delta / size);
				}
				prevPos = SceneView.Input.MousePosition;
				yield return null;
			}
		}

		private readonly List<float> zoomTable = new List<float> {
			0.001f, 0.0025f, 0.005f, 0.01f, 0.025f, 0.05f, 0.10f,
			0.15f, 0.25f, 0.5f, 0.75f, 1f, 1.5f, 2f, 3f,
			4f, 5f, 6f, 7f, 8f, 9f, 10f, 11f,
			12f, 13f, 14f, 15f, 16f,
		};

		private void ZoomCanvas(int advance)
		{
			var i = FindNearest(SceneView.Scene.Scale.X, 0, zoomTable.Count - 1);
			if (i < 0) {
				throw new InvalidOperationException();
			}
			var prevZoom = SceneView.Scene.Scale.X;
			var zoom = zoomTable[(i + advance).Clamp(0, zoomTable.Count - 1)];
			var p = SceneView.MousePosition;
			SceneView.Scene.Scale = zoom * Vector2.One;
			SceneView.Scene.Position -= p * (zoom - prevZoom);
		}

		private void ZoomCanvas(float delta)
		{
			var zoom = SceneView.Scene.Scale.X + delta;
			if (zoom < zoomTable.First() || zoom > zoomTable.Last()) {
				return;
			}
			SceneView.Scene.Scale = zoom * Vector2.One;
			SceneView.Scene.Position -= mouseStartPos * delta;
		}

		private int FindNearest(float x, int left, int right)
		{
			if (right - left == 1) {
				return left;
			}
			var idx = left + (right - left) / 2;
			return x < zoomTable[idx] ? FindNearest(x, left, idx) : FindNearest(x, idx, right);
		}
	}
}
