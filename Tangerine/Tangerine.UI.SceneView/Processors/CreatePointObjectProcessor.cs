using System;
using System.Collections.Generic;
using Lime;
using Tangerine.Core;

namespace Tangerine.UI.SceneView
{
	public class CreatePointObjectProcessor : ITaskProvider
	{
		private SceneView SceneView => SceneView.Instance;
		private ICommand command;

		public IEnumerator<object> Task()
		{
			while (true) {
				Type nodeType;
				if (CreateNodeRequestComponent.Consume<PointObject>(SceneView.Components, out nodeType, out command)) {
					yield return CreatePointObjectTask(nodeType);
				}
				yield return null;
			}
		}

		private IEnumerator<object> CreatePointObjectTask(Type nodeType)
		{
			command.Checked = true;
			while (true) {
				if (SceneView.InputArea.IsMouseOver()) {
					Utils.ChangeCursorIfDefault(MouseCursor.Hand);
				}
				CreateNodeRequestComponent.Consume<Node>(SceneView.Components);
				if (SceneView.Input.WasMousePressed()) {
					try {
						Document.Current.History.DoTransaction(() => {
							var currentPoint = (PointObject)Core.Operations.CreateNode.Perform(
								nodeType, aboveSelected: nodeType != typeof(SplinePoint)
							);
							var container = (Widget)Document.Current.Container;
							var t = container.LocalToWorldTransform.CalcInversed();
							var pos = Vector2.Zero;
							if (
								container.Width.Abs() > Mathf.ZeroTolerance
								&& container.Height.Abs() > Mathf.ZeroTolerance
							) {
								pos = SceneView.MousePosition * t / container.Size;
							}
							if (container is ParticleEmitter && currentPoint is EmitterShapePoint) {
								Core.Operations.SetProperty.Perform(
									container,
									nameof(ParticleEmitter.Shape),
									EmitterShape.Custom);
							}
							Core.Operations.SetProperty.Perform(currentPoint, nameof(PointObject.Position), pos);
						});
					} catch (InvalidOperationException e) {
						AlertDialog.Show(e.Message);
						break;
					}
				}
				if (SceneView.Input.WasMousePressed(1) || SceneView.Input.WasKeyPressed(Key.Escape)) {
					break;
				}
				yield return null;
			}
			this.command.Checked = false;
		}
	}
}
