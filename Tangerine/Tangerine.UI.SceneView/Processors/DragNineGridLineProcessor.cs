using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Lime;
using Tangerine.Core;

namespace Tangerine.UI.SceneView
{
	internal class DragNineGridLineProcessor : ITaskProvider
	{
		private static SceneView SceneView => SceneView.Instance;

		public IEnumerator<object> Task()
		{
			while (true) {
				if (!SceneView.Instance.InputArea.IsMouseOverThisOrDescendant()) {
					yield return null;
					continue;
				}
				var grids = Document.Current.SelectedNodes().Editable().OfType<NineGrid>();
				var mousePosition = SceneView.MousePosition;
				foreach (var grid in grids) {
					foreach (var line in NineGridLine.GetForNineGrid(grid)) {
						if (line.HitTest(mousePosition, SceneView.Scene)) {
							Utils.ChangeCursorIfDefault(MouseCursor.Hand);
							if (SceneView.Input.ConsumeKeyPress(Key.Mouse0)) {
								yield return Drag(line);
							}
							goto Next;
						}
					}
				}
			Next:
				yield return null;
			}
		}

		private IEnumerator<object> Drag(NineGridLine nineGridLine)
		{
			var transform = Document.Current.Container.AsWidget.LocalToWorldTransform.CalcInversed();
			var initMousePos = SceneView.MousePosition * transform;
			var dir = nineGridLine.GetDirection();
			float value = nineGridLine.Value;
			var nineGrid = nineGridLine.Owner;
			var propertyName = nineGridLine.PropertyName;
			var maxValue = nineGridLine.MaxValue;
			var size = nineGridLine.TextureSize * nineGridLine.Scale;
			float clipTolerance = 15 / size;
			float[] clipPositions = { 0, maxValue, maxValue / 3, maxValue / 3 * 2 };
			using (Document.Current.History.BeginTransaction()) {
				while (SceneView.Input.IsMousePressed()) {
					Document.Current.History.RollbackTransaction();

					Utils.ChangeCursorIfDefault(MouseCursor.Hand);
					var curMousePos = SceneView.MousePosition * transform;
					var diff = Vector2.DotProduct(curMousePos - initMousePos, dir) / size;
					if (Mathf.Abs(diff) > Mathf.ZeroTolerance) {
						float newValue = value + diff;
						newValue = Mathf.Clamp(newValue, Mathf.ZeroTolerance, maxValue);
						if (SceneView.Input.IsKeyPressed(Key.Shift)) {
							foreach (var origin in clipPositions) {
								newValue = newValue.Snap(origin, clipTolerance);
							}
						}
						Core.Operations.SetAnimableProperty.Perform(
							nineGrid,
							propertyName,
							newValue,
							CoreUserPreferences.Instance.AutoKeyframes
						);
					}
					yield return null;
				}
				SceneView.Input.ConsumeKey(Key.Mouse0);
				Document.Current.History.CommitTransaction();
			}
		}
	}
}
