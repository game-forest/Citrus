using System;
using System.Linq;
using Lime;
using Tangerine.Core;

namespace Tangerine.UI.SceneView
{
	public class SplinePresenter : SyncCustomPresenter<Spline>
	{
		protected override void InternalRender(Spline spline)
		{
			if (
				Document.Current.PreviewScene ||
				(
					!(Document.Current.Container == spline) &&
					!Document.Current.SelectedNodes().Contains(spline) &&
					!VisualHintsRegistry.Instance.DisplayCondition(spline))) {
				return;
			}
			if (
				Document.Current.Container == spline.Parent
				|| Document.Current.Container == spline
				|| CoreUserPreferences.Instance.ShowSplinesGlobally
			) {
				DrawSpline(spline);
			}
		}

		private void DrawSpline(Spline spline)
		{
			Renderer.PushState(RenderState.Transform2);
			try {
				SceneView.Instance.Frame.PrepareRendererState();
				Renderer.Transform2 = Matrix32.Identity;
				var transform =
					spline.AsWidget.LocalToWorldTransform * SceneView.Instance.CalcTransitionFromSceneSpace(SceneView.Instance.Frame);
				var nvg = Lime.NanoVG.Context.Instance;
				for (int i = 0; i < 2; i++) {
					nvg.StrokeWidth(i == 1 ? 1 : 2);
					nvg.StrokeColor(i == 0 ? Color4.White : Color4.Black);
					var j = 0;
					var step = 7.0f / SceneView.Instance.Scene.Scale.X;
					foreach (var v in spline.CalcPoints(step)) {
						if (j % 1000 == 0) { // protect from indices overflow
							if (j > 0) {
								nvg.Stroke();
							}
							nvg.BeginPath();
							nvg.MoveTo(v * transform);
						} else {
							nvg.LineTo(v * transform);
						}
						j += 1;
					}
					if (j > 0) {
						nvg.Stroke();
					}
				}
			} finally {
				Renderer.PopState();
			}
		}
	}
}
