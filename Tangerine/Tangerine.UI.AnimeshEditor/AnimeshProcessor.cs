using System.Collections.Generic;
using System.Linq;
using Lime;
using Tangerine.Core;

namespace Tangerine.UI.AnimeshEditor
{
	public class AnimeshProcessor : ITaskProvider
	{
		private ISceneView sv;

		public AnimeshProcessor(ISceneView sv)
		{
			this.sv = sv;
		}

		public IEnumerator<object> Task()
		{
			yield return null;
			while (true) {
				if (Document.Current.ExpositionMode || Document.Current.PreviewAnimation) {
					yield return null;
					continue;
				}
				if (!sv.InputArea.IsMouseOverThisOrDescendant()) {
					yield return null;
					continue;
				}
				var meshes = Document.Current.SelectedNodes().Editable().OfType<Lime.Animesh>().ToList();
				if (meshes.Count == 0) {
					yield return null;
					continue;
				}
				if (sv.Input.ConsumeKeyPress(Key.Mouse1)) {
					AnimeshTools.State = AnimeshTools.State.NextState();
				}
				var bones = Document.Current.SelectedNodes().Editable().OfType<Bone>().ToList();
				if (AnimeshTools.State == AnimeshTools.ModificationState.Animation) {
					foreach (var mesh in meshes) {
						if (sv.Input.IsKeyPressed(Key.Control)) {
							if (sv.Input.ConsumeKeyPress(Key.Mouse0)) {
								if (sv.Input.IsKeyPressed(Key.Shift)) {
									mesh.Controller(sv).UntieVertexFromBones(bones);
								} else {
									mesh.Controller(sv).TieVertexWithBones(bones);
								}
								mesh.Invalidate();
							}
						}
					}
				}
				foreach (var mesh in meshes) {
					if (mesh.Controller(sv).HitTest(sv.MousePosition, sv.Scene.Scale.X)) {
						UI.Utils.ChangeCursorIfDefault(MouseCursor.Hand);
						if (sv.Input.ConsumeKeyPress(Key.Mouse0) && !sv.Input.IsKeyPressed(Key.Control)) {
							switch (AnimeshTools.State) {
								case AnimeshTools.ModificationState.Animation:
									yield return mesh.Controller().AnimationTask();
									break;
								case AnimeshTools.ModificationState.Modification:
									yield return mesh.Controller().ModificationTask();
									break;
								case AnimeshTools.ModificationState.Creation:
									yield return mesh.Controller().CreationTask();
									break;
								case AnimeshTools.ModificationState.Removal:
									yield return mesh.Controller().RemovalTask();
									break;
							}
						}
					}
				}
				yield return null;
			}
		}
	}
}
