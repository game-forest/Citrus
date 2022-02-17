using System;
using System.Linq;
using System.Collections.Generic;
using Lime;
using Tangerine.Core;
using Tangerine.UI.AnimeshEditor;

namespace Tangerine.UI.SceneView
{
	public class MouseSelectionProcessor : ITaskProvider
	{
		public static readonly List<IProber> Probers = new List<IProber>();

		static MouseSelectionProcessor()
		{
			Probers.Add(new BoneProber());
			Probers.Add(new WidgetProber());
			Probers.Add(new PointObjectProber());
			Probers.Add(new SplinePoint3DProber());
			Probers.Add(new AnimeshProber());
		}

		public IEnumerator<object> Task()
		{
			var sceneView = SceneView.Instance;
			var input = sceneView.Input;
			while (true) {
				if (input.WasMousePressed() && !input.IsKeyPressed(Key.Shift)) {
					var rect = new Rectangle(sceneView.MousePosition, sceneView.MousePosition);
					var presenter = new SyncDelegatePresenter<Widget>(w => {
						w.PrepareRendererState();
						var t = sceneView.Scene.CalcTransitionToSpaceOf(sceneView.Frame);
						Renderer.DrawRectOutline(rect.A * t, rect.B * t, ColorTheme.Current.SceneView.MouseSelection);
					});
					sceneView.Frame.CompoundPostPresenter.Add(presenter);
					using (Document.Current.History.BeginTransaction()) {
						var clicked = true;
						var selectedNodes = Document.Current.SelectedNodes().Editable().ToList();
						while (input.IsMousePressed()) {
							rect.B = sceneView.MousePosition;
							clicked &= (rect.B - rect.A).Length <= 5;
							if (!clicked) {
								RefreshSelectedNodes(rect, selectedNodes);
							}

							CommonWindow.Current.Invalidate();
							yield return null;
						}
						// Evgenii Polikutin: this covers for the edge case, when no nodes
						// were selected on click, therefore Inspector.Rebuild() wasn't called.
						Document.Current.InspectRootNode = false;

						if (clicked) {
							var controlPressed = SceneView.Instance.Input.IsKeyPressed(Key.Control);
							if (!controlPressed) {
								Core.Operations.ClearSceneItemSelection.Perform();
							}

							Node selectedNode = null;
							foreach (var widget in WidgetsPivotMarkPresenter.WidgetsWithDisplayedPivot()) {
								var pos = widget.GlobalPivotPosition;
								if (SceneView.Instance.HitTestControlPoint(pos)) {
									selectedNode = widget;
									break;
								}
							}
							if (selectedNode == null) {
								foreach (var node in Document.Current.Container.Nodes.Editable()) {
									if (Probe(node, rect.A)) {
										selectedNode = node;
										break;
									}
								}
							}
							if (selectedNode != null) {
								Core.Operations.SelectNode.Perform(
									selectedNode,
									!controlPressed || !Document.Current.SelectedNodes().Contains(selectedNode)
								);
							}
						}
						sceneView.Frame.CompoundPostPresenter.Remove(presenter);
						CommonWindow.Current.Invalidate();
						input.ConsumeKey(Key.Mouse0);
						Document.Current.History.CommitTransaction();
					}
				}
				yield return null;
			}
		}

		private void RefreshSelectedNodes(Rectangle rect, IEnumerable<Node> originalSelection)
		{
			var ctrlPressed = SceneView.Instance.Input.IsKeyPressed(Key.Control);
			var currentSelection = Document.Current.SelectedNodes();
			var newSelection = Document.Current.ContainerChildNodes().Editable().Where(n =>
				ctrlPressed ? Probe(n, rect) ^ originalSelection.Contains(n) : Probe(n, rect));
			if (!newSelection.SequenceEqual(currentSelection)) {
				Core.Operations.ClearSceneItemSelection.Perform();
				foreach (var node in newSelection) {
					Core.Operations.SelectNode.Perform(node);
				}
			}
		}

		private bool Probe(Node node, Vector2 point) => Probers.Any(i => i.Probe(node, point));
		private bool Probe(Node node, Rectangle rectangle) => Probers.Any(i => i.Probe(node, rectangle));

		public interface IProber
		{
			bool Probe(Node node, Vector2 point);
			bool Probe(Node node, Rectangle rectangle);
		}

		public abstract class Prober<T>
			: IProber
			where T : Node
		{
			public bool Probe(Node node, Vector2 point) => (node is T) && ProbeInternal((T)node, point);
			public bool Probe(Node node, Rectangle rectangle) => (node is T) && ProbeInternal((T)node, rectangle);

			protected abstract bool ProbeInternal(T node, Vector2 point);
			protected abstract bool ProbeInternal(T node, Rectangle rectangle);
		}

		public class WidgetProber : Prober<Widget>
		{
			protected override bool ProbeInternal(Widget widget, Vector2 point)
			{
				if (!widget.GloballyVisible) {
					return false;
				}
				var hull = widget.CalcHull();
				return hull.Contains(point);
			}

			protected override bool ProbeInternal(Widget widget, Rectangle rectangle)
			{
				if (!widget.GloballyVisible) {
					return false;
				}
				var hull = widget.CalcHull();
				for (int i = 0; i < 4; i++) {
					if (rectangle.Contains(hull[i])) {
						return true;
					}
				}
				var pivot = widget.GlobalPivotPosition;
				return rectangle.Contains(pivot);
			}
		}

		public class BoneProber : Prober<Bone>
		{
			protected override bool ProbeInternal(Bone bone, Vector2 point)
			{
				var t = Document.Current.Container.AsWidget.LocalToWorldTransform;
				var hull = BonePresenter.CalcHull(bone) * t;
				return hull.Contains(point);
			}

			protected override bool ProbeInternal(Bone bone, Rectangle rectangle)
			{
				var t = Document.Current.Container.AsWidget.LocalToWorldTransform;
				var hull = BonePresenter.CalcHull(bone);
				for (int i = 0; i < 4; i++) {
					if (rectangle.Contains(hull[i] * t)) {
						return true;
					}
				}
				var center = (hull.V1 * t + hull.V3 * t) / 2;
				return rectangle.Contains(center);
			}
		}

		public class PointObjectProber : Prober<PointObject>
		{
			protected override bool ProbeInternal(PointObject pobject, Vector2 point)
			{
				var pos = pobject.TransformedPosition * pobject.Parent.AsWidget.LocalToWorldTransform;
				return SceneView.Instance.HitTestControlPoint(pos, 5);
			}

			protected override bool ProbeInternal(PointObject pobject, Rectangle rectangle)
			{
				var p = pobject.TransformedPosition;
				var t = pobject.Parent.AsWidget.LocalToWorldTransform;
				return rectangle.Contains(t * p);
			}
		}

		public class SplinePoint3DProber : Prober<SplinePoint3D>
		{
			protected override bool ProbeInternal(SplinePoint3D splinePoint, Vector2 point)
			{
				return SceneView.Instance.HitTestControlPoint(CalcPositionInSceneViewSpace(splinePoint));
			}

			protected override bool ProbeInternal(SplinePoint3D splinePoint, Rectangle rectangle)
			{
				return rectangle.Contains(CalcPositionInSceneViewSpace(splinePoint));
			}

			private Vector2 CalcPositionInSceneViewSpace(SplinePoint3D splinePoint)
			{
				var spline = (Spline3D)splinePoint.Parent;
				var viewport = spline.Viewport;
				var viewportToScene = viewport.LocalToWorldTransform;
				return (Vector2)viewport.WorldToViewportPoint(
					splinePoint.Position * spline.GlobalTransform
				) * viewportToScene;
			}
		}

		public class AnimeshProber : Prober<Lime.Animesh>
		{
			protected override bool ProbeInternal(Lime.Animesh mesh, Vector2 point)
			{
				if (!mesh.GloballyVisible) {
					return false;
				}
				return mesh.Controller(SceneView.Instance)
					.HitTest(point, SceneView.Instance.Scene.Scale.X, ignoreState: true);
			}

			protected override bool ProbeInternal(Lime.Animesh mesh, Rectangle rectangle)
			{
				if (!mesh.GloballyVisible) {
					return false;
				}
				var points = new[] {
					rectangle.A, new Vector2(rectangle.BX, rectangle.AY),
					rectangle.B, new Vector2(rectangle.AX, rectangle.BY),
				};
				for (var i = 0; i < points.Length; ++i) {
					foreach (var face in mesh.Faces.ToArray()) {
						for (var j = 0; j < 3; ++j) {
							var v0 = mesh.Controller(SceneView.Instance).Vertices[face[j]].Pos
								* mesh.LocalToWorldTransform;
							var v1 = mesh.Controller(SceneView.Instance).Vertices[face[(j + 1) % 3]].Pos
								* mesh.LocalToWorldTransform;
							if (
								rectangle.Contains(v0)
								|| rectangle.Contains(v1)
								|| (points[(i + 1) % points.Length] - points[i]).Length >= 1f
								&& AnimeshUtils.LineLineIntersection(
									points[i], points[(i + 1) % points.Length], v0, v1, out var p
								)
							) {
								return true;
							}
						}
					}
				}
				return false;
			}
		}
	}
}
