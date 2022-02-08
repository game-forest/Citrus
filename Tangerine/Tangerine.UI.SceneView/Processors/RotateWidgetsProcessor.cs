using System;
using System.Collections.Generic;
using System.Linq;
using Lime;
using Tangerine.Core;
using Tangerine.Core.Operations;
using Tangerine.UI.AnimeshEditor;
using Tangerine.UI.SceneView.WidgetTransforms;

namespace Tangerine.UI.SceneView
{
	public class RotateWidgetsProcessor : ITaskProvider
	{
		private SceneView SceneView => SceneView.Instance;

		public IEnumerator<object> Task()
		{
			while (true) {
				if (!SceneView.Instance.InputArea.IsMouseOverThisOrDescendant()) {
					yield return null;
					continue;
				}
				var widgets = Document.Current.TopLevelSelectedNodes().Editable().OfType<Widget>()
					.Where(w => !w.IsPropertyReadOnly(nameof(Widget.Rotation)));
				if (AnimeshTools.State != AnimeshTools.ModificationState.Transformation) {
					widgets = widgets.Where(w => !(w is Animesh));
				}
				if (Utils.CalcHullAndPivot(widgets, out var hull, out var pivot)) {
					for (int i = 0; i < 4; i++) {
						if (SceneView.HitTestControlPoint(hull[i])) {
							Utils.ChangeCursorIfDefault(Cursors.Rotate);
							if (SceneView.Input.ConsumeKeyPress(Key.Mouse0)) {
								yield return Rotate(pivot);
							}
						}
					}
				}
				yield return null;
			}
		}

		private IEnumerator<object> Rotate(Vector2 pivot)
		{
			using (Document.Current.History.BeginTransaction()) {
				var widgets = Document.Current.TopLevelSelectedNodes().Editable().OfType<Widget>().ToList();
				var mouseStartPos = SceneView.MousePosition;

				List<(Widget, AccumulativeRotationHelper)> accumulateRotationHelpers =
					widgets.Select(widget =>
						(widget, new AccumulativeRotationHelper(widget.Rotation, 0)))
					.ToList();

				while (SceneView.Input.IsMousePressed()) {
					Utils.ChangeCursorIfDefault(Cursors.Rotate);
					var isRoundingMode = SceneView.Input.IsKeyPressed(Key.C);
					Document.Current.History.RollbackTransaction();
					RotateWidgets(
						pivotPoint: pivot,
						widgets: widgets,
						curMousePos: SceneView.MousePosition,
						prevMousePos: mouseStartPos,
						snapped: SceneView.Input.IsKeyPressed(Key.Shift),
						accumulativeRotationHelpers: accumulateRotationHelpers,
						isRoundingMode: isRoundingMode);
					yield return null;
				}
				Document.Current.History.CommitTransaction();
			}
			SceneView.Input.ConsumeKey(Key.Mouse0);
		}

		private static void RotateWidgets(
			Vector2 pivotPoint,
			List<Widget> widgets,
			Vector2 curMousePos,
			Vector2 prevMousePos,
			bool snapped,
			List<(Widget, AccumulativeRotationHelper)> accumulativeRotationHelpers,
			bool isRoundingMode) {
			WidgetTransformsHelper.ApplyTransformationToWidgetsGroupObb(
				widgetsInParentSpace: widgets,
				overridePivotInSceneSpace: widgets.Count <= 1
					? (Vector2?)null
					: pivotPoint,
				obbInFirstWidgetSpace: widgets.Count <= 1,
				currentMousePosInSceneSpace: curMousePos,
				previousMousePosSceneSpace: prevMousePos,
				convertScaleToSize: false,
				isRoundingMode: isRoundingMode,
				onCalculateTransformation: (originalVectorInObbSpace, deformedVectorInObbSpace) => {
					double rotation = 0;
					if (originalVectorInObbSpace.Length > Mathf.ZeroTolerance &&
						deformedVectorInObbSpace.Length > Mathf.ZeroTolerance) {
						rotation = Mathd.Wrap180(deformedVectorInObbSpace.Atan2Deg - originalVectorInObbSpace.Atan2Deg);
					}
					if (snapped) {
						rotation = WidgetTransformsHelper.RoundTo(rotation, 15);
					}
					foreach (var (widget, newRotation) in accumulativeRotationHelpers) {
						newRotation.Rotate((float)rotation);
					}
					return new Transform2d(Vector2d.Zero, Vector2d.One, rotation);
				});

			foreach ((Widget, AccumulativeRotationHelper) tuple in accumulativeRotationHelpers) {
				var newRotation = tuple.Item2.Rotation;
				if (isRoundingMode) {
					newRotation = MathF.Floor(newRotation);
				}
				SetAnimableProperty.Perform(
					@object: tuple.Item1,
					propertyPath: nameof(Widget.Rotation),
					value: newRotation,
					createAnimatorIfNeeded: CoreUserPreferences.Instance.AutoKeyframes);
			}
		}
	}
}
