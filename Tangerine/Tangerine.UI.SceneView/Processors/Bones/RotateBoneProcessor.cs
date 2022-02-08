using System;
using System.Linq;
using System.Collections.Generic;
using Lime;
using Tangerine.Core;

namespace Tangerine.UI.SceneView
{
	public class RotateBoneProcessor : ITaskProvider
	{
		private SceneView SceneView => SceneView.Instance;

		public IEnumerator<object> Task()
		{
			while (true) {
				if (!SceneView.Instance.InputArea.IsMouseOverThisOrDescendant()) {
					yield return null;
					continue;
				}
				var bone = Document.Current.SelectedNodes().Editable().OfType<Bone>().FirstOrDefault();
				if (bone != null) {
					var entry = bone.Parent.AsWidget.BoneArray[bone.Index];
					var t = Document.Current.Container.AsWidget.LocalToWorldTransform;
					var hull = BonePresenter.CalcRect(bone) * t;
					if (hull.Contains(SceneView.MousePosition) && !SceneView.Input.IsKeyPressed(Key.Control)) {
						Utils.ChangeCursorIfDefault(Cursors.Rotate);
						if (SceneView.Input.ConsumeKeyPress(Key.Mouse0)) {
							yield return Rotate(bone, entry);
						}
					}
				}
				yield return null;
			}
		}

		private IEnumerator<object> Rotate(Bone bone, BoneArray.Entry entry)
		{
			using (Document.Current.History.BeginTransaction()) {
				float rotation = 0;
				var mousePos = SceneView.MousePosition;
				var initRotation = bone.Rotation;
				while (SceneView.Input.IsMousePressed()) {
					Document.Current.History.RollbackTransaction();

					var t = Document.Current.Container.AsWidget.LocalToWorldTransform.CalcInversed();
					Utils.ChangeCursorIfDefault(Cursors.Rotate);
					var a = mousePos * t - entry.Joint;
					var b = SceneView.MousePosition * t - entry.Joint;
					mousePos = SceneView.MousePosition;
					if (a.Length > Mathf.ZeroTolerance && b.Length > Mathf.ZeroTolerance) {
						rotation += Mathf.Wrap180(b.Atan2Deg - a.Atan2Deg);
					}
					Core.Operations.SetAnimableProperty.Perform(
						bone, nameof(Bone.Rotation), initRotation + rotation, CoreUserPreferences.Instance.AutoKeyframes
					);
					bone.Parent.Update(0);
					yield return null;
				}
				yield return null;
				SceneView.Input.ConsumeKey(Key.Mouse0);
				Document.Current.History.CommitTransaction();
			}
			yield return null;
		}
	}
}
