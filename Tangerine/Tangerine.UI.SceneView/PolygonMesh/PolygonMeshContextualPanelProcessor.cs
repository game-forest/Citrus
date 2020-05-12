using System.Collections.Generic;
using System.Linq;
using Tangerine.Core;

namespace Tangerine.UI.SceneView.PolygonMesh
{
	class PolygonMeshContextualPanelProcessor : ITaskProvider
	{
		private SceneView sv => SceneView.Instance;

		public IEnumerator<object> Task()
		{
			while (true) {
				if (Document.Current.SelectedNodes().Any(node => node is Lime.Widgets.PolygonMesh.PolygonMesh)) {
					if (!sv.Panel.Nodes.Contains(PolygonMeshContextualPanel.Instance.RootNode)) {
						sv.Panel.Nodes.Insert(0, PolygonMeshContextualPanel.Instance.RootNode);
					}
				} else {
					sv.Panel.Nodes.Remove(PolygonMeshContextualPanel.Instance.RootNode);
				}
				yield return null;
			}
		}
	}
}
