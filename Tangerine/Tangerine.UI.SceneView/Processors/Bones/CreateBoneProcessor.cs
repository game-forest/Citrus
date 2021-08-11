using Lime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Tangerine.Core;
using Tangerine.Core.Operations;

namespace Tangerine.UI.SceneView
{
	public class CreateBoneProcessor : ITaskProvider
	{
		SceneView sv => SceneView.Instance;
		private ICommand command;

		public IEnumerator<object> Task()
		{
			while (true) {
				if (CreateNodeRequestComponent.Consume<Bone>(SceneView.Instance.Components, out command)) {
					yield return CreateBoneTask();
				}
				yield return null;
			}
		}

		IEnumerator<object> CreateBoneTask()
		{
			command.Checked = true;
			while (true) {
				Bone bone = null;
				if (!SceneTreeUtils.GetSceneItemLinkLocation(
					out var containerSceneItem, out _, aboveFocused: true,
					raiseThroughHierarchyPredicate: (i, _) => !LinkSceneItem.CanLink(i, new Bone()))
				) {
					throw new InvalidOperationException();
				}
				var container = (Widget)SceneTreeUtils.GetOwnerNodeSceneItem(containerSceneItem).GetNode();
				var transform = container.LocalToWorldTransform;
				if (sv.InputArea.IsMouseOver()) {
					Utils.ChangeCursorIfDefault(MouseCursor.Hand);
				}
				var items = container.BoneArray.items;
				var baseBoneIndex = 0;
				if (items != null) {
					for (var i = 1; i < items.Length; i++) {
						if (sv.HitTestControlPoint(transform * items[i].Tip)) {
							baseBoneIndex = i;
							break;
						}
					}
					SceneView.Instance.Components.GetOrAdd<CreateBoneHelper>().HitTip =
						baseBoneIndex != 0 ? (Vector2?)(transform * items[baseBoneIndex].Tip) : null;
				}

				Window.Current.Invalidate();
				CreateNodeRequestComponent.Consume<Node>(sv.Components);
				if (sv.Input.ConsumeKeyPress(Key.Mouse0)) {
					var worldToLocal = container.LocalToWorldTransform.CalcInversed();
					var initialPosition = sv.MousePosition * worldToLocal;
					var pos = Vector2.Zero;
					if (
						baseBoneIndex == 0 &&
						container.Width.Abs() > Mathf.ZeroTolerance &&
						container.Height.Abs() > Mathf.ZeroTolerance
					) {
						pos = initialPosition;
					}
					using (Document.Current.History.BeginTransaction()) {
						try {
							if (baseBoneIndex != 0) {
								var baseBone = container.Nodes.First(
									n => n is Bone b && b.Index == baseBoneIndex);
								var baseBoneItem = Document.Current.GetSceneItemForObject(baseBone);
								bone = (Bone)CreateNode.Perform(baseBoneItem, 0, typeof(Bone));
							} else {
								if (!SceneTreeUtils.GetSceneItemLinkLocation(
									out var parent, out var index,
									raiseThroughHierarchyPredicate: (i, _) => i.GetNode() is Bone)
								) {
									throw new InvalidOperationException();
								}
								bone = (Bone)CreateNode.Perform(parent, index, typeof(Bone));
							}
						} catch (InvalidOperationException e) {
							AlertDialog.Show(e.Message);
							break;
						}
						SetProperty.Perform(bone, nameof(Bone.Position), pos);
						SelectNode.Perform(bone);
						using (Document.Current.History.BeginTransaction()) {
							while (sv.Input.IsMousePressed()) {
								Document.Current.History.RollbackTransaction();

								var direction = (sv.MousePosition * worldToLocal - initialPosition).Snap(Vector2.Zero);
								var angle = direction.Atan2Deg;
								if (baseBoneIndex != 0) {
									var prentDir = items[baseBoneIndex].Tip - items[baseBoneIndex].Joint;
									angle = Vector2.AngleDeg(prentDir, direction);
								}
								SetProperty.Perform(bone, nameof(Bone.Rotation), angle);
								SetProperty.Perform(bone, nameof(Bone.Length), direction.Length);
								yield return null;
							}
							Document.Current.History.CommitTransaction();
						}
						// do not create zero bone
						if (bone != null && bone.Length == 0) {
							Document.Current.History.RollbackTransaction();
							// must set length to zero to execute "break;" later
							bone.Length = 0;
						}
						Document.Current.History.CommitTransaction();
					}
					SceneView.Instance.Components.Remove<CreateBoneHelper>();
				}
				// turn off creation if was only click without drag (zero length bone)
				if (bone != null && bone.Length == 0) {
					break;
				}
				if (sv.Input.WasMousePressed(1) || sv.Input.WasKeyPressed(Key.Escape)) {
					break;
				}
				yield return null;
			}
			SceneView.Instance.Components.Remove<CreateBoneHelper>();
			command.Checked = false;
		}
	}

	internal class CreateBoneHelper : NodeComponent
	{
		public Vector2? HitTip { get; set; }
	}
}
