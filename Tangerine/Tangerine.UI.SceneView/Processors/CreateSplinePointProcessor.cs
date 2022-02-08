using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Lime;
using Tangerine.Core;

namespace Tangerine.UI.SceneView
{
	internal class CreateSplinePointProcessor : ITaskProvider
	{
		private SceneView SceneView => SceneView.Instance;
		private ICommand command;

		public IEnumerator<object> Task()
		{
			while (true) {
				if (CreateNodeRequestComponent.Consume<SplinePoint>(SceneView.Instance.Components, out command)) {
					yield return CreateSplinePointTask();
				}
				yield return null;
			}
		}

		private IEnumerator<object> CreateSplinePointTask()
		{
			command.Checked = true;
			while (true) {
				if (SceneView.InputArea.IsMouseOver()) {
					Utils.ChangeCursorIfDefault(MouseCursor.Hand);
				}
				CreateNodeRequestComponent.Consume<Node>(SceneView.Components);
				if (SceneView.Input.ConsumeKeyPress(Key.Mouse0)) {
					using (Document.Current.History.BeginTransaction()) {
						PointObject currentPoint;
						try {
							currentPoint = (PointObject)Core.Operations.CreateNode.Perform(
								typeof(SplinePoint), aboveSelected: false
							);
						} catch (InvalidOperationException e) {
							AlertDialog.Show(e.Message);
							yield break;
						}
						var container = (Widget)Document.Current.Container;
						var t = container.LocalToWorldTransform.CalcInversed();
						var pos = Vector2.Zero;
						if (
							container.Width.Abs() > Mathf.ZeroTolerance
							&& container.Height.Abs() > Mathf.ZeroTolerance
						) {
							pos = SceneView.MousePosition * t / container.Size;
						}
						Core.Operations.SetProperty.Perform(currentPoint, nameof(PointObject.Position), pos);
						using (Document.Current.History.BeginTransaction()) {
							while (SceneView.Input.IsMousePressed()) {
								Document.Current.History.RollbackTransaction();

								var dir = (SceneView.MousePosition * t - currentPoint.TransformedPosition)
									/ SplinePointPresenter.TangentWeightRatio;
								Core.Operations.SetProperty.Perform(
									currentPoint, nameof(SplinePoint.TangentAngle), dir.Atan2Deg
								);
								Core.Operations.SetProperty.Perform(
									currentPoint, nameof(SplinePoint.TangentWeight), dir.Length
								);
								yield return null;
							}
							Document.Current.History.CommitTransaction();
						}
						Document.Current.History.CommitTransaction();
					}
				}
				if (SceneView.Input.WasMousePressed(1) || SceneView.Input.WasKeyPressed(Key.Escape)) {
					break;
				}
				yield return null;
			}
			command.Checked = false;
		}
	}
}
