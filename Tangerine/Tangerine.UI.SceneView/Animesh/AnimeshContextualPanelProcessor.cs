using System.Collections.Generic;
using System.Linq;
using Tangerine.Core;

namespace Tangerine.UI.SceneView.Animesh
{
	class AnimeshContextualPanelProcessor : ITaskProvider
	{
		private readonly AnimeshContextualPanel panel;

		public AnimeshContextualPanelProcessor(AnimeshContextualPanel panel)
		{
			this.panel = panel;
		}

		public IEnumerator<object> Task()
		{
			while (true) {
				panel.RootNode.Visible = Document.Current.SelectedNodes()
					.Any(node => node is Lime.Widgets.Animesh.Animesh);
				yield return null;
			}
		}
	}
}
