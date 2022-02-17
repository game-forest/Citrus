using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Lime;
using Tangerine.Core;

namespace Tangerine.UI.SceneView
{
	internal class ChangeBoneRadiusProcessor : ITaskProvider
	{
		private SceneView SceneView => SceneView.Instance;

		public IEnumerator<object> Task()
		{
			while (true) {
				if (!SceneView.Instance.InputArea.IsMouseOverThisOrDescendant()) {
					yield return null;
					continue;
				}
				var bones = Document.Current.SelectedNodes().Editable().OfType<Bone>().ToList();
				if (bones.Count == 1) {
					var t = Document.Current.Container.AsWidget.LocalToWorldTransform;
					var hull = BonePresenter.CalcHull(bones.First());
					for (int i = 0; i < 4; i++) {
						hull[i] = t * hull[i];
					}
					if (hull.Contains(SceneView.MousePosition) && SceneView.Input.IsKeyPressed(Key.Shift)) {
						Utils.ChangeCursorIfDefault(MouseCursor.SizeNS);
						if (SceneView.Input.ConsumeKeyPress(Key.Mouse0)) {
							yield return Resize(bones.First());
						}
					}
				}
				yield return null;
			}
		}

		private IEnumerator<object> Resize(Bone bone)
		{
			using (Document.Current.History.BeginTransaction()) {
				var iniMousePos = SceneView.MousePosition;
				var initEffectiveRadius = bone.EffectiveRadius;
				var initFadeoutZone = bone.FadeoutZone;
				while (SceneView.Input.IsMousePressed()) {
					Document.Current.History.RollbackTransaction();

					Utils.ChangeCursorIfDefault(MouseCursor.SizeNS);
					var dragDelta = SceneView.MousePosition - iniMousePos;
					Core.Operations.SetAnimableProperty.Perform(
						bone,
						nameof(Bone.EffectiveRadius),
						initEffectiveRadius + dragDelta.X,
						CoreUserPreferences.Instance.AutoKeyframes
					);
					Core.Operations.SetAnimableProperty.Perform(
						bone,
						nameof(Bone.FadeoutZone),
						initFadeoutZone + dragDelta.Y,
						CoreUserPreferences.Instance.AutoKeyframes
					);
					yield return null;
				}
				SceneView.Input.ConsumeKey(Key.Mouse0);
				Document.Current.History.CommitTransaction();
			}
		}
	}
}
