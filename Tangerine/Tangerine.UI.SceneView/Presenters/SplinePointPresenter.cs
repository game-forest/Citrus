using System;
using System.Collections.Generic;
using System.Linq;
using Lime;
using Tangerine.Core;

namespace Tangerine.UI.SceneView
{
	public class SplinePointPresenter
	{
		private static List<SplinePoint> emptySelection = new List<SplinePoint>();

		public SplinePointPresenter(SceneView sceneView)
		{
			sceneView.Frame.CompoundPostPresenter.Add(new SyncDelegatePresenter<Widget>(Render));
		}

		public const float TangentWeightRatio = 10f;

		private static void Render(Widget widget)
		{
			if (!Document.Current.PreviewScene && Document.Current.Container is Spline) {
				var spline = Document.Current.Container;
				foreach (SplinePoint point in spline.Nodes) {
					var color = GetSelectedPoints().Contains(point) ?
					ColorTheme.Current.SceneView.Selection :
					ColorTheme.Current.SceneView.PointObject;
					var sv = SceneView.Instance;
					sv.Frame.PrepareRendererState();
					var t = point.Parent.AsWidget.LocalToWorldTransform * sv.CalcTransitionFromSceneSpace(sv.Frame);
					var a = Vector2.CosSin(point.TangentAngle * Mathf.DegToRad)
						* TangentWeightRatio
						* point.TangentWeight;
					var p1 = t * (point.TransformedPosition + a);
					var p2 = t * (point.TransformedPosition - a);
					var norm = (p2 - p1).Normalized;
					norm = new Vector2(-norm.Y, norm.X);
					RendererNvg.DrawLine(p1 + norm, p2 + norm, ColorTheme.Current.SceneView.SplineOutline);
					RendererNvg.DrawLine(p1, p2, color);
					RendererNvg.DrawLine(p1 - norm, p2 - norm, ColorTheme.Current.SceneView.SplineOutline);
					RendererNvg.DrawRound(p1, 5, ColorTheme.Current.SceneView.SplineOutline);
					RendererNvg.DrawRound(p1, 3, color);
					RendererNvg.DrawRound(p2, 5, ColorTheme.Current.SceneView.SplineOutline);
					RendererNvg.DrawRound(p2, 3, color);
				}
			}
		}

		private static List<SplinePoint> GetSelectedPoints()
		{
			if (Document.Current.Container is Spline) {
				return Document.Current.SelectedNodes().OfType<SplinePoint>().Editable().ToList();
			}
			return emptySelection;
		}
	}
}
