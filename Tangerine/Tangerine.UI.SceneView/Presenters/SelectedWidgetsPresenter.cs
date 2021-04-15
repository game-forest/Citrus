using System;
using System.Linq;
using System.Collections.Generic;
using System.Net;
using Lime;
using Tangerine.Core;
using Tangerine.UI.AnimeshEditor;

namespace Tangerine.UI.SceneView
{
	class SelectedWidgetsPresenter
	{
		private SceneView sceneView;

		private readonly VisualHint selectedWidgetPivotVisualHint =
			VisualHintsRegistry.Instance.Register("/All/Selected Widget Pivot", hideRule: VisualHintsRegistry.HideRules.VisibleIfProjectOpened);

		public SelectedWidgetsPresenter(SceneView sceneView)
		{
			this.sceneView = sceneView;
			sceneView.Frame.CompoundPostPresenter.Add(new SyncDelegatePresenter<Widget>(RenderSelection));
		}

		private void RenderSelection(Widget canvas)
		{
			if (Document.Current.PreviewScene) {
				return;
			}

			canvas.PrepareRendererState();
			var widgets = Document.Current.TopLevelSelectedNodes().Editable().OfType<Widget>().ToList();
			if (widgets.Count == 0) {
				return;
			}

			Quadrangle hull;
			Vector2 pivot;
			Utils.CalcHullAndPivot(widgets, out hull, out pivot);
			hull = hull.Transform(sceneView.CalcTransitionFromSceneSpace(canvas));
			pivot = pivot * sceneView.CalcTransitionFromSceneSpace(canvas);
			// Render rectangles.
			var locked = widgets.Any(w => w.GetTangerineFlag(TangerineFlags.Locked));
			var color = locked ? ColorTheme.Current.SceneView.LockedWidgetBorder : ColorTheme.Current.SceneView.Selection;
			if (
				widgets.Count == 1 && widgets[0] is Animesh &&
				AnimeshTools.State != AnimeshTools.ModificationState.Transformation
			) {
				return;
			}
			for (int i = 0; i < 4; i++) {
				var a = hull[i];
				var b = hull[(i + 1) % 4];
				Renderer.DrawLine(a, b, color);
				DrawStretchMark(a);

				if (i < 2) {
					var c = hull[(i + 2) % 4];
					var d = hull[(i + 3) % 4];
					var abCenter = (a + b) * 0.5f;
					var cdCenter = (c + d) * 0.5f;
					Renderer.DrawLine(abCenter, cdCenter, color);
					DrawStretchMark(abCenter);
					DrawStretchMark(cdCenter);
				}
			}
			// Render border and icon for widgets.
			var iconSize = new Vector2(16, 16);
			foreach (var widget in widgets) {
				if (widget is Animesh && AnimeshTools.State != AnimeshTools.ModificationState.Transformation) {
					continue;
				}
				var t = NodeIconPool.GetTexture(widget);
				var h = widget.CalcHull().Transform(sceneView.CalcTransitionFromSceneSpace(canvas));
				for (int i = 0; i < 4; i++) {
					var a = h[i];
					var b = h[(i + 1) % 4];
					Renderer.DrawLine(a, b, ColorTheme.Current.SceneView.SelectedWidget, 1);
				}
				var p = widget.GlobalPivotPosition * sceneView.CalcTransitionFromSceneSpace(canvas);
				Renderer.DrawSprite(t, ColorTheme.Current.SceneView.SelectedWidgetPivotOutline, p - iconSize / 2, iconSize, Vector2.Zero, Vector2.One);
				if (selectedWidgetPivotVisualHint.Enabled) {
					Renderer.DrawRectOutline(
						p - iconSize / 2 - 5 * Vector2.One,
						p + iconSize / 2 + 5 * Vector2.One,
						ColorTheme.Current.SceneView.SelectedWidgetPivotOutline,
						2.0f
					);
				}
			}
			// Render multi-pivot mark.
			if (widgets.Count > 1) {
				DrawMultiPivotMark(pivot);
			}
		}

		void DrawStretchMark(Vector2 position)
		{
			Renderer.DrawRect(position - Vector2.One * 3, position + Vector2.One * 3, ColorTheme.Current.SceneView.Selection);
		}

		void DrawMultiPivotMark(Vector2 position)
		{
			Renderer.DrawRect(position - Vector2.One * 5, position + Vector2.One * 5, ColorTheme.Current.SceneView.Selection);
		}
	}
}
