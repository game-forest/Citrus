using Lime;
using System.Linq;
using Tangerine.Core;
using Tangerine.UI.SceneView.PolygonMesh;

namespace Tangerine.UI.SceneView
{
	public class PolygonMeshPresenter
	{
		private static SceneView sv => SceneView.Instance;

		public PolygonMeshPresenter(SceneView sceneView)
		{
			sceneView.Frame.CompoundPostPresenter.Add(new SyncDelegatePresenter<Widget>(Render));
		}

		private static void Render(Widget canvas)
		{
			if (Document.Current.ExpositionMode || Document.Current.PreviewAnimation) {
				return;
			}
			var meshes = Document.Current.SelectedNodes().Editable().OfType<Lime.Widgets.PolygonMesh.PolygonMesh>().ToList();
			if (meshes.Count != 0) {
				canvas.PrepareRendererState();
				foreach (var mesh in meshes) {
					mesh.Controller().Render(sv.Frame, sv.Scene, sv.MousePosition, sv.Scene.Scale.X);
				}
			}
		}
	}
}
