using System.Collections.Generic;
using System.Linq;
using Tangerine.Core;
using Lime.PolygonMesh;

namespace Tangerine.UI.SceneView
{
	public class PolygonMeshEditingProcessor : ITaskProvider
	{
		public IEnumerator<object> Task()
		{
			while (true) {
				if (Document.Current.ExpositionMode || Document.Current.PreviewAnimation) {
					yield return null;
					continue;
				}
				if (!SceneView.Instance.InputArea.IsMouseOverThisOrDescendant()) {
					yield return null;
					continue;
				}
				var meshes = Document.Current.SelectedNodes().Editable().OfType<PolygonMesh>().ToList();
				if (meshes.Count != 1) {
					yield return null;
					continue;
				}
				PolygonMeshManager.Instance.SetTargetMesh(meshes[0]);
				PolygonMeshManager.Instance.HitTestTarget();
				yield return PolygonMeshManager.Instance.ProcessModification();
				PolygonMeshManager.Instance.Invalidate();
				yield return null;
			}
		}
	}
}
