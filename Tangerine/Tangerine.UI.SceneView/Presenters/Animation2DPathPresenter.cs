using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Lime;
using Tangerine.Core;

namespace Tangerine.UI.SceneView
{
	public class Animation2DPathPresenter
	{
		private List<Vector2> points = new List<Vector2>();
		private List<Vector2> approximation = new List<Vector2>();

		private readonly VisualHint animationPathHint =
			VisualHintsRegistry.Instance.Register(
				"/All/Animation Path", hideRule: VisualHintsRegistry.HideRules.VisibleIfProjectOpened
			);

		private readonly SceneView sv;

		public Animation2DPathPresenter(SceneView sceneView)
		{
			sv = sceneView;
			sceneView.Frame.CompoundPostPresenter.Add(new SyncDelegatePresenter<Widget>(Render));
		}

		public void Render(Widget canvas)
		{
			if (Document.Current.PreviewScene || !animationPathHint.Enabled) {
				return;
			}
			canvas.PrepareRendererState();
			var nodes = Document.Current.SelectedNodes().Editable();
			foreach (var node in nodes) {
				if (node is IAnimationHost) {
					var animable = node as IAnimationHost;
					foreach (var animator in animable.Animators) {
						if (animator.AnimationId != Document.Current.AnimationId) {
							continue;
						}
						if (
							animator is Vector2Animator &&
							animator.TargetPropertyPath == nameof(Widget.Position)) {
							var keys = animator.ReadonlyKeys.ToList();
							if (keys.Count < 2) {
								continue;
							}
							points.Clear();
							var transform = node.Parent.AsWidget.LocalToWorldTransform
								* sv.CalcTransitionFromSceneSpace(canvas);
							if (node is Widget) {
								for (int i = 0; i < keys.Count; ++i) {
									points.Add((Vector2)keys[i].Value * transform);
								}
							} else {
								continue;
							}
							for (int i = 0; i < points.Count - 1; ++i) {
								Approximate(i, i + 1, keys[i].Function, 10);
								for (int j = 0; j < approximation.Count - 1; ++j) {
									Renderer.DrawDashedLine(
										approximation[j],
										approximation[j + 1],
										ColorTheme.Current.SceneView.PointObject,
										new Vector2(4, 1));
								}
								Renderer.DrawRound(
									center: points[i],
									radius: 3,
									numSegments: 10,
									color: ColorTheme.Current.SceneView.PointObject.Darken(0.3f)
								);
							}
							Renderer.DrawRound(
								center: points[points.Count - 1],
								radius: 3,
								numSegments: 10,
								color: ColorTheme.Current.SceneView.PointObject.Darken(0.3f)
							);
						}
					}
				}
			}
		}

		private void Approximate(int index1, int index2, KeyFunction keyFunction, int numberOfPoints)
		{
			int index0;
			int index3;
			approximation.Clear();
			if (keyFunction == KeyFunction.Spline) {
				index0 = index1 < 1 ? 0 : index1 - 1;
				index3 = index2 >= points.Count - 1 ? points.Count - 1 : index2 + 1;
			} else if (keyFunction == KeyFunction.ClosedSpline) {
				index0 = index1 < 1 ? points.Count - 1 : index1 - 1;
				index3 = index2 >= points.Count - 1 ? 0 : index2 + 1;
			} else {
				approximation.Add(points[index1]);
				approximation.Add(points[index2]);
				return;
			}
			approximation.Add(points[index1]);
			for (int i = 1; i < numberOfPoints - 1; ++i) {
				approximation.Add(
					Mathf.CatmullRomSpline(
						(float)i / numberOfPoints,
						points[index0],
						points[index1],
						points[index2],
						points[index3]));
			}
			approximation.Add(points[index2]);
		}
	}
}
