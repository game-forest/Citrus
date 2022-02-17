using System;
using System.Collections.Generic;
using Lime;
using Tangerine.Core;
using Tangerine.Core.Operations;

namespace Tangerine.UI.SceneView
{
	public class CreateSplinePoint3DProcessor : ITaskProvider
	{
		private SceneView SceneView => SceneView.Instance;
		private DocumentHistory History => Document.Current.History;
		private WidgetInput Input => SceneView.Instance.Input;
		private ICommand command;

		public IEnumerator<object> Task()
		{
			while (true) {
				if (CreateNodeRequestComponent.Consume<SplinePoint3D>(SceneView.Instance.Components, out command)) {
					yield return CreateSplinePoint3DTask();
				}
				yield return null;
			}
		}

		private IEnumerator<object> CreateSplinePoint3DTask()
		{
			command.Checked = true;
			while (true) {
				if (SceneView.InputArea.IsMouseOver()) {
					Utils.ChangeCursorIfDefault(MouseCursor.Hand);
				}
				CreateNodeRequestComponent.Consume<Node>(SceneView.Components);
				if (Input.ConsumeKeyPress(Key.Mouse0)) {
					using (History.BeginTransaction()) {
						SplinePoint3D point;
						try {
							point = (SplinePoint3D)CreateNode.Perform(typeof(SplinePoint3D), aboveSelected: false);
						} catch (InvalidOperationException e) {
							AlertDialog.Show(e.Message);
							yield break;
						}
						var spline = (Spline3D)Document.Current.Container;
						var vp = spline.Viewport;
						var ray = vp.ScreenPointToRay(SceneView.MousePosition);
						var xyPlane = new Plane(new Vector3(0, 0, 1), 0).Transform(spline.GlobalTransform);
						var d = ray.Intersects(xyPlane);
						if (d.HasValue) {
							var pos = (ray.Position + ray.Direction * d.Value) * spline.GlobalTransform.CalcInverted();
							SetProperty.Perform(point, nameof(SplinePoint3D.Position), pos);
							using (History.BeginTransaction()) {
								while (Input.IsMousePressed()) {
									History.RollbackTransaction();
									ray = vp.ScreenPointToRay(SceneView.MousePosition);
									d = ray.Intersects(xyPlane);
									if (d.HasValue) {
										var tangent = (ray.Position + ray.Direction * d.Value) *
											spline.GlobalTransform.CalcInverted() - point.Position;
										SetProperty.Perform(point, nameof(SplinePoint3D.TangentA), tangent);
										SetProperty.Perform(point, nameof(SplinePoint3D.TangentB), -tangent);
									}
									History.CommitTransaction();
									yield return null;
								}

								if (point.TangentA.Length < 0.01f) {
									SetProperty.Perform(point, nameof(SplinePoint3D.TangentA), new Vector3(1, 0, 0));
									SetProperty.Perform(point, nameof(SplinePoint3D.TangentB), new Vector3(-1, 0, 0));
								}
							}
						}
						History.CommitTransaction();
					}
				}
				if (Input.WasMousePressed(1) || Input.WasKeyPressed(Key.Escape)) {
					break;
				}
				yield return null;
			}

			command.Checked = false;
			Utils.ChangeCursorIfDefault(MouseCursor.Default);
		}
	}
}
