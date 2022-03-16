using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Lime;
using Tangerine.Core;

namespace Tangerine.UI.SceneView
{
	internal class PointObjectsPresenter
	{
		private readonly SceneView sv;
		public static readonly float CornerOffset = 15f;

		public PointObjectsPresenter(SceneView sceneView)
		{
			sv = sceneView;
			sceneView.Frame.CompoundPostPresenter.Add(new SyncDelegatePresenter<Widget>(Render));
		}

		private void Render(Widget canvas)
		{
			if (Document.Current.PreviewScene || !Document.Current.Container.Nodes.Any(i => i is PointObject)) {
				return;
			}
			canvas.PrepareRendererState();
			var t = Document.Current.Container.AsWidget.LocalToWorldTransform * sv.CalcTransitionFromSceneSpace(canvas);

			var selectedPointObjects = Document.Current.SelectedNodes().Editable().OfType<PointObject>().ToList();
			var pointObjects = Document.Current.Container.Nodes.OfType<PointObject>()
				.Except(selectedPointObjects)
				.ToList();
			foreach (var po in pointObjects) {
				DrawPointObject(po.TransformedPosition * t, ColorTheme.Current.SceneView.PointObject);
			}
			foreach (var po in selectedPointObjects) {
				DrawPointObject(po.TransformedPosition * t, ColorTheme.Current.SceneView.Selection);
			}
			if (selectedPointObjects.Count == 0) {
				return;
			}

			var bounds = CalcExpandedHullInSpaceOf(selectedPointObjects, sv);
			RendererNvg.DrawQuadrangleOutline(bounds, ColorTheme.Current.SceneView.Selection);
			var hullSize = bounds[0] - bounds[2];
			if (selectedPointObjects.Count() > 1) {
				for (var i = 0; i < 4; i++) {
					var a = bounds[i];
					var b = bounds[(i + 1) % 4];
					if (Mathf.Abs(hullSize.X) > Mathf.ZeroTolerance && Mathf.Abs(hullSize.Y) > Mathf.ZeroTolerance) {
						DrawStretchMark(a);
					}
					if (Mathf.Abs(hullSize.X) < Mathf.ZeroTolerance && i % 2 == 1 ||
						Mathf.Abs(hullSize.Y) < Mathf.ZeroTolerance && i % 2 == 0) {
						continue;
					}
					DrawStretchMark((a + b) / 2);
				}

				DrawStretchMark((bounds.V1 + bounds.V3) / 2);
			}
		}

		public static Quadrangle CalcExpandedHullInSpaceOf(IEnumerable<PointObject> points, SceneView sceneView)
		{
			Utils.CalcHullAndPivot(points, out var hull, out _);
			var aabb = hull.Transform(
				Document.Current.Container.AsWidget.LocalToWorldTransform.CalcInversed()
			).ToAABB();
			return ExpandAndTranslateToSpaceOf(aabb.ToQuadrangle(), Document.Current.Container.AsWidget, sceneView);
		}

		public static Quadrangle ExpandAndTranslateToSpaceOf(Quadrangle hull, Widget sourceWidget, SceneView sceneView)
		{
			var t = sourceWidget.LocalToWorldTransform * sceneView.CalcTransitionFromSceneSpace(sceneView.Frame);
			var size = sourceWidget.Size;
			var corners = new Quadrangle();
			for (var i = 0; i < 4; i++) {
				corners[i] = Corners[i] * size * t;
				hull[i] *= t;
			}
			var bounds = new Quadrangle();
			for (var i = 0; i < 4; i++) {
				var next = (i + 1) % 4;
				var prev = (i + 3) % 4;
				var dir1 = hull[i] - hull[next];
				var dir2 = hull[i] - hull[prev];
				if (dir1 + dir2 == Vector2.Zero) {
					dir1 = corners[i] - corners[next];
					dir2 = corners[i] - corners[prev];
				}
				bounds[i] = hull[i] + (dir1.Normalized + dir2.Normalized) * CornerOffset;
			}

			return bounds;
		}

		public static readonly List<Vector2> Corners = new List<Vector2> {
			Vector2.Zero,
			Vector2.Right,
			Vector2.One,
			Vector2.Down,
		};

		private void DrawPointObject(Vector2 position, Color4 color)
		{
			RendererNvg.DrawRound(position, 6, ColorTheme.Current.SceneView.SplineOutline);
			RendererNvg.DrawRound(position, 4, color);
		}

		private void DrawStretchMark(Vector2 position)
		{
			Renderer.DrawRect(
				position - Vector2.One * 3, position + Vector2.One * 3, ColorTheme.Current.SceneView.Selection
			);
		}
	}
}
