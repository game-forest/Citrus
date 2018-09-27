using Lime;
using System;
using System.Collections.Generic;
using Tangerine.Core;

namespace Tangerine.UI.SceneView
{
	class DragPaddingLineProcessor : ITaskProvider
	{
		private readonly VisualHint paddingVisualHint =
			VisualHintsRegistry.Instance.Register("/All/Padding", hideRule: VisualHintsRegistry.HideRules.VisibleIfProjectOpened);

		private static SceneView sv => SceneView.Instance;

		public IEnumerator<object> Task()
		{
			while (true) {
				if (!SceneView.Instance.InputArea.IsMouseOverThisOrDescendant()) {
					yield return null;
					continue;
				}
				var nodes = Document.Current.SelectedNodes().Editable();
				var mousePosition = sv.MousePosition;
				foreach (var node in nodes) {
					if (node is Widget widget) {
						var matrix = widget.LocalToWorldTransform;
						foreach (var line in PaddingLine.GetLines(widget)) {
							var a = matrix.TransformVector(line.A);
							var b = matrix.TransformVector(line.B);
							if (Utils.LineHitTest(mousePosition, a, b) && paddingVisualHint.Enabled) {
								Utils.ChangeCursorIfDefault(MouseCursor.Hand);
								if (sv.Input.ConsumeKeyPress(Key.Mouse0)) {
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

		private IEnumerator<object> Drag(PaddingLine paddingLine)
		{
			var transform = sv.Scene.CalcTransitionToSpaceOf(Document.Current.Container.AsWidget);
			var initMousePos = sv.MousePosition * transform;
			var dir = paddingLine.GetDirection();
			var widget = paddingLine.Owner;
			var name = paddingLine.PropertyName;
			using (Document.Current.History.BeginTransaction()) {
				while (sv.Input.IsMousePressed()) {
					Document.Current.History.RollbackTransaction();
					Utils.ChangeCursorIfDefault(MouseCursor.Hand);
					var curMousePos = sv.MousePosition * transform;
					var diff = Vector2.DotProduct((curMousePos - initMousePos) / widget.Scale, dir);
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
						Core.Operations.SetAnimableProperty.Perform(widget, nameof(Widget.Padding),
							padding, CoreUserPreferences.Instance.AutoKeyframes);
					}
					yield return null;
				}
				sv.Input.ConsumeKey(Key.Mouse0);
				Document.Current.History.CommitTransaction();
			}
		}
	}
}
