using System;
using System.Linq;
using System.Collections.Generic;
using Lime;
using Tangerine.Core;

namespace Tangerine.UI.SceneView
{
	public class ShiftTimelineProcessor : ITaskProvider
	{
		private SceneView SceneView => SceneView.Instance;

		public IEnumerator<object> Task()
		{
			while (true) {
				if (SceneView.InputArea.IsMouseOverThisOrDescendant() && SceneView.Input.IsKeyPressed(Key.Alt)) {
					Utils.ChangeCursorIfDefault(MouseCursor.SizeWE);
					if (SceneView.Input.ConsumeKeyPress(Key.Mouse0)) {
						yield return Advance();
					}
				}
				yield return null;
			}
		}

		private enum DragDirection
		{
			Left = -1,
			None,
			Right,
		}

		private IEnumerator<object> Advance()
		{
			using (Document.Current.History.BeginTransaction()) {
				var step = 10;
				var distance = 0;
				var matrix = SceneView.Scene.LocalToWorldTransform;
				var prevMousPos = SceneView.MousePosition * matrix;
				var prevDirection = DragDirection.None;
				while (SceneView.Input.IsMousePressed()) {
					Document.Current.History.RollbackTransaction();
					Utils.ChangeCursorIfDefault(MouseCursor.SizeWE);
					var curMousePos = SceneView.MousePosition * matrix;
					var curDirection = (DragDirection)Math.Sign(curMousePos.X - prevMousPos.X);
					if (curDirection != DragDirection.None && curDirection != prevDirection) {
						distance = step - distance;
					}
					distance += (int)Math.Floor(Mathf.Abs(curMousePos.X - prevMousPos.X));
					var inc = distance / step;
					switch (curDirection) {
						case DragDirection.Left:
							Document.Current.AnimationFrame =
								Math.Max(0, Document.Current.AnimationFrame - inc);
							break;
						case DragDirection.Right:
							Document.Current.AnimationFrame += inc;
							break;
					}
					prevMousPos = curMousePos;
					prevDirection = curDirection != DragDirection.None ? curDirection : prevDirection;
					distance = distance % step;
					Document.Current?.ForceAnimationUpdate();
					yield return null;
				}
				SceneView.Input.ConsumeKey(Key.Mouse0);
				Document.Current.History.CommitTransaction();
			}
		}
	}
}
