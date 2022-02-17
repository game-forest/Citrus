using Lime;
using Tangerine.Core;

namespace Tangerine.UI.SceneView
{
	public class DistortionMeshPresenter : SyncCustomPresenter<DistortionMesh>
	{
		private readonly SceneView sv;
		private readonly VisualHint meshHint =
			VisualHintsRegistry.Instance.Register(
				"/All/Distortion Mesh Grid",
				hideRule: VisualHintsRegistry.HideRules.VisibleIfProjectOpened
			);

		public DistortionMeshPresenter(SceneView sceneView)
		{
			this.sv = sceneView;
			sceneView.Frame.CompoundPostPresenter.Add(new SyncDelegatePresenter<Widget>(Render));
		}

		private void Render(Widget canvas)
		{
			if (meshHint.Enabled && !Document.Current.PreviewScene && Document.Current.Container is DistortionMesh) {
				var mesh = Document.Current.Container as DistortionMesh;
				canvas.PrepareRendererState();
				var transform = mesh.LocalToWorldTransform * sv.CalcTransitionFromSceneSpace(sv.Frame);
				for (int i = 0; i <= mesh.NumRows; i++) {
					for (int j = 0; j <= mesh.NumCols; j++) {
						var p = mesh.GetPoint(i, j).TransformedPosition * transform;
						if (i + 1 <= mesh.NumRows) {
							Renderer.DrawLine(
								a: p,
								b: mesh.GetPoint(i + 1, j).TransformedPosition * transform,
								color: ColorTheme.Current.SceneView.DistortionMeshOutline
							);
						}
						if (j + 1 <= mesh.NumCols) {
							Renderer.DrawLine(
								a: p,
								b: mesh.GetPoint(i, j + 1).TransformedPosition * transform,
								color: ColorTheme.Current.SceneView.DistortionMeshOutline
							);
						}
					}
				}
			}
		}
	}
}
