using System.Collections.Generic;
using System.Linq;
using Tangerine.Core;

namespace Tangerine.UI.SceneView.PolygonMesh
{
	class PolygonMeshContextualPanelProcessor : ITaskProvider
	{
		private readonly SceneView sv;
		private readonly PolygonMeshContextualPanel panel;

		public PolygonMeshContextualPanelProcessor(SceneView sceneView, PolygonMeshContextualPanel panel)
		{
			sv = sceneView;
			this.panel = panel;
		}

		public IEnumerator<object> Task()
		{
			while (true) {
				panel.RootNode.Visible = Document.Current.SelectedNodes()
					.Any(node => node is Lime.Widgets.PolygonMesh.PolygonMesh);
				yield return null;
			}
		}
	}
}
