using System;
using System.Collections.Generic;
using Lime;
using Tangerine.Core;
using Tangerine.Core.Operations;
using Yuzu;

namespace Tangerine.UI.SceneView
{
	public class CreateWidgetProcessor : ITaskProvider
	{
		private SceneView SceneView => SceneView.Instance;

		public IEnumerator<object> Task()
		{
			ICommand command = new Command();
			Type nodeTypeActive = null;
			while (true) {
				if (SceneView.Input.WasKeyPressed(Key.Escape) || SceneView.Input.WasMousePressed(1)) {
					nodeTypeActive = null;
				}
				if (
					CreateNodeRequestComponent.Consume<Widget>(
						SceneView.Components,
						out Type nodeTypeIncome,
						out ICommand newCommand)) {
					nodeTypeActive = nodeTypeIncome;
					command.Checked = false;
					command = newCommand;
					command.Checked = true;
				}

				if (nodeTypeActive == null) {
					command.Checked = false;
					yield return null;
					continue;
				}

				if (SceneView.InputArea.IsMouseOver()) {
					Utils.ChangeCursorIfDefault(MouseCursor.Hand);
				}
				if (
					!SceneTreeUtils.TryGetSceneItemLinkLocation(
						parent: out var containerSceneItem,
						index: out var linkLocation,
						insertingType: nodeTypeActive,
						aboveFocused: true)) {
					throw new InvalidOperationException();
				}
				var container = (Widget)SceneTreeUtils.GetOwnerNodeSceneItem(containerSceneItem).GetNode();
				CreateNodeRequestComponent.Consume<Node>(SceneView.Components);
				if (SceneView.Input.WasMousePressed() && container != null) {
					SceneView.Input.ConsumeKey(Key.Mouse0);
					var t = container.LocalToWorldTransform.CalcInversed();
					var rect = new Rectangle(SceneView.MousePosition * t, SceneView.MousePosition * t);
					var presenter = new SyncDelegatePresenter<Widget>(w => {
						w.PrepareRendererState();
						var t2 = container.LocalToWorldTransform
							* SceneView.CalcTransitionFromSceneSpace(SceneView.Frame);
						DrawCreateWidgetGizmo(rect.A, rect.B, t2);
					});
					SceneView.Frame.CompoundPostPresenter.Add(presenter);
					using (Document.Current.History.BeginTransaction()) {
						while (SceneView.Input.IsMousePressed()) {
							rect.B = SceneView.MousePosition * t;
							CommonWindow.Current.Invalidate();
							yield return null;
						}
						SceneView.Frame.CompoundPostPresenter.Remove(presenter);
						try {
							rect.Normalize();
							var widget = (Widget)CreateNode.Perform(containerSceneItem, linkLocation, nodeTypeActive);
							SetProperty.Perform(widget, nameof(Widget.Size), rect.B - rect.A);
							SetProperty.Perform(widget, nameof(Widget.Position), rect.A + widget.Size * 0.5f);
							SetProperty.Perform(widget, nameof(Widget.Pivot), Vector2.Half);
						} catch (InvalidOperationException e) {
							AlertDialog.Show(e.Message);
						}
						Document.Current.History.CommitTransaction();
					}

					nodeTypeActive = null;
					Utils.ChangeCursorIfDefault(MouseCursor.Default);
				}

				yield return null;
			}
		}

		private static void DrawCreateWidgetGizmo(Vector2 a, Vector2 b, Matrix32 t)
		{
			var c = ColorTheme.Current.SceneView.MouseSelection;
			Renderer.DrawLine(a * t, new Vector2(b.X, a.Y) * t, c, 1, LineCap.Square);
			Renderer.DrawLine(new Vector2(b.X, a.Y) * t, b * t, c, 1, LineCap.Square);
			Renderer.DrawLine(b * t, new Vector2(a.X, b.Y) * t, c, 1, LineCap.Square);
			Renderer.DrawLine(new Vector2(a.X, b.Y) * t, a * t, c, 1, LineCap.Square);
			var midX = (a.X + b.X) * 0.5f;
			var midY = (a.Y + b.Y) * 0.5f;
			Renderer.DrawLine(new Vector2(midX, a.Y) * t, new Vector2(midX, b.Y) * t, c, 1, LineCap.Square);
			Renderer.DrawLine(new Vector2(a.X, midY) * t, new Vector2(b.X, midY) * t, c, 1, LineCap.Square);
		}
	}
}
