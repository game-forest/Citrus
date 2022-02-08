using System;
using System.Collections.Generic;
using Lime;
using Tangerine.Core;

namespace Tangerine.UI.SceneView
{
	internal class DragPaddingLineProcessor : ITaskProvider
	{
		private readonly VisualHint paddingVisualHint =
			VisualHintsRegistry.Instance.Register(
				"/All/Padding", hideRule: VisualHintsRegistry.HideRules.VisibleIfProjectOpened
			);

		private static SceneView ScneView => SceneView.Instance;

		public IEnumerator<object> Task()
		{
			while (true) {
				if (!SceneView.Instance.InputArea.IsMouseOverThisOrDescendant()) {
					yield return null;
					continue;
				}
				var nodes = Document.Current.SelectedNodes().Editable();
				var mousePosition = ScneView.MousePosition;
				float zoom = ScneView.Scene.Scale.X;
				foreach (var node in nodes) {
					if (node is Widget widget) {
						var matrix = widget.LocalToWorldTransform;
						foreach (var line in PaddingLine.GetLines(widget)) {
							var a = matrix.TransformVector(line.A);
							var b = matrix.TransformVector(line.B);
							var c = matrix.TransformVector((line.A + line.B) / 2);
							var r = widget.LocalToWorldTransform.U.Atan2Rad;
							var s = new Vector2(line.FontHeight * 0.25f + 2, line.FontHeight * 0.5f) / zoom;
							var rectMatrix = Matrix32.Transformation(line.GetDirection(), s, r, c);
							var quad = new Rectangle(-1, -1, 1, 1).ToQuadrangle().Transform(rectMatrix);
							if (
								(Utils.LineHitTest(mousePosition, a, b, 10f / zoom) || quad.Contains(mousePosition)) &&
								paddingVisualHint.Enabled) {
								Utils.ChangeCursorIfDefault(MouseCursor.Hand);
								if (ScneView.Input.ConsumeKeyPress(Key.Mouse0)) {
									yield return Drag(line);
								}
								goto Next;
							}
						}
					}
				}
			Next:
				yield return null;
			}
		}

		private static IEnumerator<object> Drag(PaddingLine paddingLine)
		{
			var initMousePos = ScneView.MousePosition;
			var dir = paddingLine.GetDirection();
			var widget = paddingLine.Owner;
			var name = paddingLine.PropertyName;
			var rotation = Matrix32.Rotation(-widget.LocalToWorldTransform.U.Atan2Rad);
			using (Document.Current.History.BeginTransaction()) {
				while (ScneView.Input.IsMousePressed()) {
					Document.Current.History.RollbackTransaction();
					Utils.ChangeCursorIfDefault(MouseCursor.Hand);
					var curMousePos = ScneView.MousePosition;
					var diff = Vector2.DotProduct(rotation * (curMousePos - initMousePos) / widget.Scale, dir);
					if (Mathf.Abs(diff) > Mathf.ZeroTolerance) {
						var padding = widget.Padding;
						switch (name) {
							case PaddingLine.ThicknessProperty.Left:
								padding.Left += diff;
								break;
							case PaddingLine.ThicknessProperty.Bottom:
								padding.Bottom += diff;
								break;
							case PaddingLine.ThicknessProperty.Right:
								padding.Right += diff;
								break;
							case PaddingLine.ThicknessProperty.Top:
								padding.Top += diff;
								break;
							default:
								throw new InvalidOperationException();
						}
						Core.Operations.SetAnimableProperty.Perform(
							widget,
							nameof(Widget.Padding),
							padding,
							CoreUserPreferences.Instance.AutoKeyframes
						);
					}
					yield return null;
				}
				ScneView.Input.ConsumeKey(Key.Mouse0);
				Document.Current.History.CommitTransaction();
			}
		}
	}
}
