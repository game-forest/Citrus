using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Lime;
using Tangerine.Core;
using Tangerine.Core.Operations;

namespace Tangerine.UI.SceneView
{
	public class CreateBoneProcessor : ITaskProvider
	{
		private SceneView SceneView => SceneView.Instance;
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

		private IEnumerator<object> CreateBoneTask()
		{
			command.Checked = true;
			while (true) {
				Bone bone = null;
				if (!SceneTreeUtils.TryGetSceneItemLinkLocation(
					out var containerSceneItem, out _, typeof(Bone), aboveFocused: true)) {
					throw new InvalidOperationException();
				}
				var container = (Widget)SceneTreeUtils.GetOwnerNodeSceneItem(containerSceneItem).GetNode();
				var transform = container.LocalToWorldTransform;
				if (SceneView.InputArea.IsMouseOver()) {
					Utils.ChangeCursorIfDefault(MouseCursor.Hand);
				}
				var items = container.BoneArray.items;
				var baseBoneIndex = 0;
				if (items != null) {
					for (var i = 1; i < items.Length; i++) {
						if (SceneView.HitTestControlPoint(transform * items[i].Tip)) {
							baseBoneIndex = i;
							break;
						}
					}
					SceneView.Instance.Components.GetOrAdd<CreateBoneHelper>().HitTip =
						baseBoneIndex != 0 ? (Vector2?)(transform * items[baseBoneIndex].Tip) : null;
				}

				Window.Current.Invalidate();
				CreateNodeRequestComponent.Consume<Node>(SceneView.Components);
				if (SceneView.Input.ConsumeKeyPress(Key.Mouse0)) {
					var worldToLocal = container.LocalToWorldTransform.CalcInversed();
					var initialPosition = SceneView.MousePosition * worldToLocal;
					var pos = Vector2.Zero;
					if (
						baseBoneIndex == 0 &&
						container.Width.Abs() > Mathf.ZeroTolerance &&
						container.Height.Abs() > Mathf.ZeroTolerance) {
						pos = initialPosition;
					}
					using (Document.Current.History.BeginTransaction()) {
						try {
							if (baseBoneIndex != 0) {
								var baseBone = container.Nodes.First(
									n => n is Bone b && b.Index == baseBoneIndex);
								var baseBoneItem = Document.Current.GetSceneItemForObject(baseBone);
								bone = (Bone)CreateNode.Perform(baseBoneItem, new SceneTreeIndex(0), typeof(Bone));
							} else {
								if (
									!SceneTreeUtils.TryGetSceneItemLinkLocation(
										parent: out var parent,
										index: out var index,
										insertingType: typeof(Bone),
										aboveFocused: true,
										raiseThroughHierarchyPredicate: i => i.GetNode() is Bone
									)
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
							while (SceneView.Input.IsMousePressed()) {
								Document.Current.History.RollbackTransaction();

								var direction = (
									SceneView.MousePosition * worldToLocal - initialPosition
								).Snap(Vector2.Zero);
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
				if (SceneView.Input.WasMousePressed(1) || SceneView.Input.WasKeyPressed(Key.Escape)) {
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
