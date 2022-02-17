using System;
using System.Collections.Generic;
using System.Linq;
using Lime;
using Tangerine.Core;

namespace Tangerine.UI.SceneView
{
	public class Animation3DPathPresenter : SyncCustomPresenter<Viewport3D>
	{
		private List<Vector3> points = new List<Vector3>();
		private List<Vector3> approximation = new List<Vector3>();

		public readonly VisualHint AnimationPathHint =
			VisualHintsRegistry.Instance.Register(
				"/All/Animation Path", hideRule: VisualHintsRegistry.HideRules.VisibleIfProjectOpened
			);

		protected override void InternalRender(Viewport3D viewport)
		{
			if (Document.Current.PreviewScene || !AnimationPathHint.Enabled) {
				return;
			}
			foreach (var node in Document.Current.SelectedNodes().Editable().OfType<Node3D>()) {
				if (!(node is IAnimationHost)) {
					continue;
				}
				var animable = (IAnimationHost)node;
				foreach (var animator in animable.Animators) {
					if (animator.AnimationId != Document.Current.AnimationId) {
						continue;
					}
					if (
						animator is Vector3Animator &&
						animator.TargetPropertyPath == nameof(Node3D.Position)) {
						var keys = animator.ReadonlyKeys.ToList();
						if (keys.Count == 0) {
							continue;
						}
						points.Clear();
						for (int i = 0; i < keys.Count; ++i) {
							points.Add((Vector3)keys[i].Value);
						}
						if (points.Count < 2) {
							continue;
						}
						var worldTransform = Matrix44.Identity;
						if (node.Parent.AsNode3D != null) {
							worldTransform = node.Parent.AsNode3D.GlobalTransform;
						}
						var sv = SceneView.Instance;
						var viewportToSceneFrame = viewport.LocalToWorldTransform
							* sv.CalcTransitionFromSceneSpace(sv.Frame);
						sv.Frame.PrepareRendererState();
						for (int i = 0; i < points.Count - 1; ++i) {
							Approximate(i, i + 1, keys[i].Function, 10);
							var start = (Vector2)viewport.WorldToViewportPoint(approximation[0] * worldTransform)
								* viewportToSceneFrame;
							for (int j = 1; j < approximation.Count; ++j) {
								var end = (Vector2)viewport.WorldToViewportPoint(approximation[j] * worldTransform)
									* viewportToSceneFrame;
								Renderer.DrawDashedLine(
									start, end, ColorTheme.Current.SceneView.PointObject, new Vector2(4, 1));
								start = end;
							}
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
