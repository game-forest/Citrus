using System;
using System.Collections.Generic;
using System.Linq;
using Lime;
using Tangerine.Core;

namespace Tangerine.UI.SceneView
{
	public class Spline3DPresenter : SyncCustomPresenter<Viewport3D>
	{
		private List<SplinePoint3D> emptySelection = new List<SplinePoint3D>();
		private List<Vector3> splineApproximation = new List<Vector3>();

		protected override void InternalRender(Viewport3D viewport)
		{
			if (Document.Current.PreviewScene) {
				return;
			}
			var selection = Document.Current.SelectedNodes().Editable().OfType<Spline3D>().ToList();
			foreach (var spline in viewport.Descendants.OfType<Spline3D>()) {
				if (
					!VisualHintsRegistry.Instance.DisplayCondition(spline) &&
					!selection.Contains(spline)) {
					continue;
				}
				Renderer.Flush();
				Renderer.PushState(RenderState.Transform2);
				try {
					SceneView.Instance.Frame.PrepareRendererState();
					Renderer.Transform2 = Matrix32.Identity;
					DrawSpline(spline, viewport);
					if (Document.Current.Container == spline) {
						var selectedPoints = GetSelectedPoints();
						foreach (var p in spline.Nodes) {
							DrawSplinePoint(
								(SplinePoint3D)p, viewport, spline.GlobalTransform, selectedPoints.Contains(p)
							);
						}
					}
					Renderer.Flush();
				} finally {
					Renderer.PopState();
				}
			}
		}

		private void DrawSplinePoint(
			SplinePoint3D point, Viewport3D viewport, Matrix44 splineWorldMatrix, bool selected
		) {
			var color = selected ? Color4.Green : Color4.Red;
			var sv = SceneView.Instance;
			var viewportToSceneFrame = viewport.LocalToWorldTransform * sv.CalcTransitionFromSceneSpace(sv.Frame);
			var a = (Vector2)viewport.WorldToViewportPoint(point.Position * splineWorldMatrix) * viewportToSceneFrame;
			RendererNvg.DrawRound(a, 6, color);
			RendererNvg.DrawRound(a, 4, Color4.White);
			for (int i = 0; i < 2; i++) {
				var t = i == 0 ? point.TangentA : point.TangentB;
				var b =
					(Vector2)viewport.WorldToViewportPoint((point.Position + t) * splineWorldMatrix)
					* viewportToSceneFrame;
				RendererNvg.DrawLine(a, b, color, 1);
				RendererNvg.DrawRound(b, 5, color);
				RendererNvg.DrawRound(b, 3, Color4.White);
			}
		}

		private List<SplinePoint3D> GetSelectedPoints()
		{
			if (Document.Current.Container is Spline3D) {
				return Document.Current.SelectedNodes().OfType<SplinePoint3D>().Editable().ToList();
			}
			return emptySelection;
		}

		private void DrawSpline(Spline3D spline, Viewport3D viewport)
		{
			var sv = SceneView.Instance;
			var viewportToSceneFrame = viewport.LocalToWorldTransform * sv.CalcTransitionFromSceneSpace(sv.Frame);
			for (var i = 0; i < spline.Nodes.Count - 1; i++) {
				var n1 = (SplinePoint3D)spline.Nodes[i];
				var n2 = (SplinePoint3D)spline.Nodes[i + 1];
				splineApproximation.Clear();
				splineApproximation.Add(n1.Position);
				Approximate(
					p1: n1.Position,
					p2: n1.Position + n1.TangentA,
					p3: n2.Position + n2.TangentB,
					p4: n2.Position,
					flatness: 0.01f,
					level: 0,
					points: splineApproximation
				);
				splineApproximation.Add(n2.Position);
				var nvg = Lime.NanoVG.Context.Instance;
				for (int t = 0; t < 2; t++) {
					nvg.StrokeWidth(t == 1 ? 1 : 2);
					nvg.StrokeColor(t == 0 ? Color4.White : Color4.Black);
					nvg.BeginPath();
					for (var j = 0; j < splineApproximation.Count; j++) {
						var v =
							(Vector2) viewport.WorldToViewportPoint(splineApproximation[j] * spline.GlobalTransform)
							* viewportToSceneFrame;
						if (j == 0) {
							nvg.MoveTo(v);
						} else {
							nvg.LineTo(v);
						}
					}
					nvg.Stroke();
				}
			}
		}

		private void Approximate(
			Vector3 p1, Vector3 p2, Vector3 p3, Vector3 p4, float flatness, int level, List<Vector3> points
		) {
			if (level == 10) {
				return;
			}

			var d12 = p2 - p1;
			var d13 = p3 - p1;
			var d14 = p4 - p1;
			var n1 = Vector3.CrossProduct(d12, d14);
			var n2 = Vector3.CrossProduct(d13, d14);
			var bn1 = Vector3.CrossProduct(d14, n1);
			var bn2 = Vector3.CrossProduct(d14, n2);
			var h1 = Mathf.Abs(Vector3.DotProduct(d12, bn1.Normalized));
			var h2 = Mathf.Abs(Vector3.DotProduct(d13, bn2.Normalized));
			if (h1 + h2 < flatness) {
				return;
			}

			var p12 = (p1 + p2) * 0.5f;
			var p23 = (p2 + p3) * 0.5f;
			var p34 = (p3 + p4) * 0.5f;
			var p123 = (p12 + p23) * 0.5f;
			var p234 = (p23 + p34) * 0.5f;
			var p1234 = (p123 + p234) * 0.5f;
			Approximate(p1, p12, p123, p1234, flatness, level + 1, points);
			points.Add(p1234);
			Approximate(p1234, p234, p34, p4, flatness, level + 1, points);
		}
	}
}
