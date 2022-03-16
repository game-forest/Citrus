using System;
using System.Linq;
using Lime;
using Tangerine.Core;

namespace Tangerine.UI.SceneView
{
	public class BonePresenter : SyncCustomPresenter<Frame>
	{
		public static float TipWidth => SceneUserPreferences.Instance.DefaultBoneWidth;

		private readonly SceneView sv;

		public BonePresenter(SceneView sceneView)
		{
			sv = sceneView;
			sceneView.Frame.CompoundPostPresenter.Add(new SyncDelegatePresenter<Widget>(Render));
		}

		private void Render(Widget canvas)
		{
			if (Document.Current.PreviewScene) {
				return;
			}
			canvas.PrepareRendererState();
			if (VisualHintsRegistry.Instance.FindHint(typeof(Bone)).Enabled) {
				var nodesContainingBones = Document.Current.VisibleSceneItems
					.Select(i => i.GetNode())
					.OfType<Bone>()
					.Select(i => i.Parent).Distinct();
				foreach (var node in nodesContainingBones) {
					foreach (var bone in node.Nodes.OfType<Bone>().Where(IsVisibleNode)) {
						DrawBone(bone, canvas, selected: false);
					}
				}
			}
			foreach (var bone in Document.Current.SelectedNodes().Where(IsVisibleNode).OfType<Bone>()) {
				DrawBone(bone, canvas, selected: true);
			}
		}

		public static Quadrangle CalcHull(Bone bone)
		{
			var entry = bone.Parent.AsWidget.BoneArray[bone.Index];
			var start = entry.Joint;
			var end = entry.Tip;
			var dir = end - start;
			var tail = 2 * dir.Normalized * TipWidth;
			var cross = start + (dir.Length * 0.1f < tail.Length ? dir * 0.1f : tail);
			var n = new Vector2(-dir.Y, dir.X).Normalized;
			var scaleFactor = Math.Min(SceneView.Instance.Scene.Scale.X, 1f);
			var left = cross + n * TipWidth * scaleFactor;
			var right = cross - n * TipWidth * scaleFactor;
			return new Quadrangle {
				V1 = start,
				V2 = left,
				V3 = end,
				V4 = right,
			};
		}

		public static Quadrangle CalcRect(Bone bone)
		{
			var entry = bone.Parent.AsWidget.BoneArray[bone.Index];
			var start = entry.Joint;
			var end = entry.Tip;
			var dir = end - start;
			var n = new Vector2(-dir.Y, dir.X).Normalized;
			var scaleFactor = Math.Min(SceneView.Instance.Scene.Scale.X, 1f);
			var delta = n * TipWidth * scaleFactor;
			return new Quadrangle {
				V1 = start + delta,
				V2 = end + delta,
				V3 = end - delta,
				V4 = start - delta,
			};
		}

		private void DrawBone(Bone bone, Widget canvas, bool selected)
		{
			var t = bone.Parent.AsWidget.LocalToWorldTransform * sv.CalcTransitionFromSceneSpace(canvas);
			var color = selected ? ColorTheme.Current.SceneView.Selection : ColorTheme.Current.SceneView.BoneOutline;
			var hull = CalcHull(bone);
			// Draw bone outline
			Renderer.Flush();
			RendererNvg.DrawRound(hull.V1 * t, 3, color);
			RendererNvg.DrawRound(hull.V3 * t, 3, color);
			RendererNvg.DrawQuadrangleOutline(hull * t, color);
			RendererNvg.DrawQuadrangle(hull * t, ColorTheme.Current.SceneView.Bone);
			// Draw parent link
			if (bone.BaseIndex != 0) {
				var p = bone.Parent.AsWidget.BoneArray[bone.BaseIndex].Tip * t;
				Renderer.DrawDashedLine(p, hull.V1 * t, ColorTheme.Current.SceneView.BoneOutline, Vector2.One * 3);
			}
			if (selected) {
				var dir = hull.V3 - hull.V1;
				var n = new Vector2(-dir.Y, dir.X).Normalized;
				// Draw effective radius
				DrawCapsule(
					a: hull.V1,
					b: hull.V3,
					n: n * bone.EffectiveRadius,
					t: t,
					numSegments: 20,
					color: ColorTheme.Current.SceneView.BoneEffectiveRadius
				);
				// Draw Fadeout zone
				DrawCapsule(
					a: hull.V1,
					b: hull.V3,
					n: n * (bone.EffectiveRadius + bone.FadeoutZone),
					t: t,
					numSegments: 20,
					color: ColorTheme.Current.SceneView.BoneFadeoutZone
				);
			}
		}

		private static void DrawCapsule(
			Vector2 a, Vector2 b, Vector2 n, Matrix32 t, int numSegments, Color4 color, float thickness = 1
		) {
			RendererNvg.DrawLine((a + n) * t, (b + n) * t, color, thickness);
			RendererNvg.DrawLine((a - n) * t, (b - n) * t, color, thickness);
			var step = 180 / numSegments;
			var nvg = Lime.NanoVG.Context.Instance;
			nvg.StrokePaint(color);
			nvg.StrokeWidth(thickness);
			for (int j = 0; j < 2; j++) {
				nvg.BeginPath();
				for (var i = 0; i <= numSegments; i++) {
					var v = j == 0 ?
						(a + Vector2.RotateDeg(n, i * step)) * t :
						(b - Vector2.RotateDeg(n, i * step)) * t;
					if (i == 0) {
						nvg.MoveTo(v);
					} else {
						nvg.LineTo(v);
					}
				}
				nvg.Stroke();
			}
		}

		private static bool IsVisibleNode(Node node) => node.EditorState().Visibility != NodeVisibility.Hidden;
	}
}
