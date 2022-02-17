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
	public class ResizeWidgetsProcessor : ITaskProvider
	{
		private static SceneView SceneView => SceneView.Instance;

		private static readonly int[] lookupPivotIndex = {
			4, 5, 6, 7, 0, 1, 2, 3,
		};

		private static readonly bool[][] lookupInvolvedAxes = {
			new[] { true, true },
			new[] { false, true },
			new[] { true, true },
			new[] { true, false },
			new[] { true, true },
			new[] { false, true },
			new[] { true, true },
			new[] { true, false },
		};

		public IEnumerator<object> Task()
		{
			while (true) {
				if (!SceneView.Instance.InputArea.IsMouseOverThisOrDescendant()) {
					yield return null;
					continue;
				}
				var widgets = Document.Current.TopLevelSelectedNodes().Editable().OfType<Widget>();
				if (AnimeshTools.State != AnimeshTools.ModificationState.Transformation) {
					widgets = widgets.Where(w => !(w is Animesh));
				}
				if (Utils.CalcHullAndPivot(widgets, out var hull, out var pivot)) {
					for (var i = 0; i < 4; i++) {
						var a = hull[i];
						if (SceneView.HitTestResizeControlPoint(a)) {
							var cursor = i % 2 == 0 ? MouseCursor.SizeNWSE : MouseCursor.SizeNESW;
							Utils.ChangeCursorIfDefault(cursor);
							if (SceneView.Input.ConsumeKeyPress(Key.Mouse0)) {
								yield return Resize(hull, i * 2, pivot);
							}
						}
						var b = hull[(i + 1) % 4];
						if (SceneView.HitTestResizeControlPoint((a + b) / 2)) {
							var cursor = (b.X - a.X).Abs() > (b.Y - a.Y).Abs()
								? MouseCursor.SizeNS
								: MouseCursor.SizeWE;
							Utils.ChangeCursorIfDefault(cursor);
							if (SceneView.Input.ConsumeKeyPress(Key.Mouse0)) {
								yield return Resize(hull, i * 2 + 1, pivot);
							}
						}
					}
				}
				yield return null;
			}
		}

		private static IEnumerator<object> Resize(Quadrangle hull, int controlPointIndex, Vector2 pivot)
		{
			var cursor = WidgetContext.Current.MouseCursor;
			using (Document.Current.History.BeginTransaction()) {
				var widgets = Document.Current.TopLevelSelectedNodes().Editable().OfType<Widget>();
				var mouseStartPos = SceneView.MousePosition;
				var startPositionForPivotRestore = widgets.FirstOrDefault(
					w => !w.IsPropertyReadOnly(nameof(Widget.Size)))?.Position;
				while (SceneView.Input.IsMousePressed()) {
					Document.Current.History.RollbackTransaction();
					Matrix32 transform = Matrix32.Identity;
					Utils.ChangeCursorIfDefault(cursor);
					var proportional = SceneView.Input.IsKeyPressed(Key.Shift);
					var isChangingScale = SceneView.Input.IsKeyPressed(Key.Control);
					var toBeTransformed = widgets.Where(w => isChangingScale
						? !w.IsPropertyReadOnly(nameof(Widget.Scale))
						: !w.IsPropertyReadOnly(nameof(Widget.Size))).ToList();
					var isFreezeAllowed = !isChangingScale && toBeTransformed.Count == 1;
					var areChildrenFrozen = SceneView.Input.IsKeyPressed(Key.Z) && isFreezeAllowed;
					var isPivotFrozen = SceneView.Input.IsKeyPressed(Key.X) && isFreezeAllowed;
					var isRoundingMode = SceneView.Input.IsKeyPressed(Key.C);
					if (areChildrenFrozen) {
						transform = toBeTransformed[0].CalcTransitionToSpaceOf(Document.Current.Container.AsWidget);
					}
					var pivotPoint =
						isChangingScale ?
						(toBeTransformed.Count <= 1 ? (Vector2?)null : pivot) :
						hull[lookupPivotIndex[controlPointIndex] / 2];
					RescaleWidgets(
						hullInFirstWidgetSpace: toBeTransformed.Count <= 1,
						pivotPoint: pivotPoint,
						widgets: toBeTransformed,
						controlPointIndex: controlPointIndex,
						curMousePos: SceneView.MousePosition,
						prevMousePos: mouseStartPos,
						proportional: proportional,
						convertScaleToSize: !isChangingScale,
						isRoundingMode: isRoundingMode);
					if (areChildrenFrozen) {
						transform *= Document.Current.Container.AsWidget.CalcTransitionToSpaceOf(toBeTransformed[0]);
						RestoreChildrenPositions(toBeTransformed[0], transform, isRoundingMode);
					}
					if (isPivotFrozen) {
						RestorePivot(toBeTransformed[0], pivot, startPositionForPivotRestore.Value, isRoundingMode);
					}
					yield return null;
				}
				SceneView.Input.ConsumeKey(Key.Mouse0);
				Document.Current.History.CommitTransaction();
			}
		}

		private static void RestorePivot(Widget widget, Vector2 globalPivot, Vector2 startPosition, bool isRoundingMode)
		{
			var newPivot = widget.LocalToWorldTransform.CalcInversed().TransformVector(globalPivot) / widget.Size;
			SetProperty.Perform(widget, nameof(Widget.Pivot), newPivot);
			if (widget.Animators.TryFind(nameof(Widget.Pivot), out var pivotAnimator)) {
				var newKey = pivotAnimator.ReadonlyKeys.GetByFrame(Document.Current.AnimationFrame);
				if (newKey != null) {
					newKey = newKey.Clone();
					newKey.Value = newPivot;
					SetKeyframe.Perform(pivotAnimator, Document.Current.Animation, newKey);
				}
			}
			if (isRoundingMode) {
				startPosition = Vector2.Floor(startPosition);
			}
			SetProperty.Perform(widget, nameof(Widget.Position), startPosition);
			if (widget.Animators.TryFind(nameof(Widget.Position), out var positionAnimator)) {
				var newKey = positionAnimator.ReadonlyKeys.GetByFrame(Document.Current.AnimationFrame);
				if (newKey != null) {
					newKey = newKey.Clone();
					newKey.Value = startPosition;
					SetKeyframe.Perform(positionAnimator, Document.Current.Animation, newKey);
				}
			}
		}

		private static void RestoreChildrenPositions(Widget widget, Matrix32 transform, bool isRoundingMode)
		{
			foreach (var child in widget.Nodes.OfType<Widget>()) {
				var newPosition = transform.TransformVector(child.Position);
				if (isRoundingMode) {
					newPosition = Vector2.Floor(newPosition);
				}
				SetProperty.Perform(child, nameof(Widget.Position), newPosition);
				if (child.Animators.TryFind(nameof(Widget.Position), out var animator)) {
					foreach (var key in animator.ReadonlyKeys.ToList()) {
						var newKey = key.Clone();
						newPosition = transform.TransformVector((Vector2)key.Value);
						if (isRoundingMode) {
							newPosition = Vector2.Floor(newPosition);
						}
						newKey.Value = newPosition;
						SetKeyframe.Perform(animator, Document.Current.Animation, newKey);
					}
				}
			}
		}

		private static void RescaleWidgets(
			bool hullInFirstWidgetSpace,
			Vector2? pivotPoint,
			List<Widget> widgets,
			int controlPointIndex,
			Vector2 curMousePos,
			Vector2 prevMousePos,
			bool proportional,
			bool convertScaleToSize,
			bool isRoundingMode) {
			WidgetTransformsHelper.ApplyTransformationToWidgetsGroupObb(
				widgetsInParentSpace: widgets,
				overridePivotInSceneSpace: pivotPoint,
				obbInFirstWidgetSpace: hullInFirstWidgetSpace,
				currentMousePosInSceneSpace: curMousePos,
				previousMousePosSceneSpace: prevMousePos,
				convertScaleToSize: convertScaleToSize,
				isRoundingMode: isRoundingMode,
				onCalculateTransformation: (originalVectorInObbSpace, deformedVectorInObbSpace) => {
					var deformationScaleInObbSpace = new Vector2d(
						x: Math.Abs(originalVectorInObbSpace.X) < Mathf.ZeroTolerance
							? 1
							: deformedVectorInObbSpace.X / originalVectorInObbSpace.X,
						y: Math.Abs(originalVectorInObbSpace.Y) < Mathf.ZeroTolerance
							? 1
							: deformedVectorInObbSpace.Y / originalVectorInObbSpace.Y);
					if (!lookupInvolvedAxes[controlPointIndex][0]) {
						deformationScaleInObbSpace.X = proportional
							? deformationScaleInObbSpace.Y
							: 1;
					} else if (!lookupInvolvedAxes[controlPointIndex][1]) {
						deformationScaleInObbSpace.Y = proportional
							? deformationScaleInObbSpace.X
							: 1;
					} else if (proportional) {
						deformationScaleInObbSpace.X =
							(deformationScaleInObbSpace.X + deformationScaleInObbSpace.Y) / 2;
						deformationScaleInObbSpace.Y = deformationScaleInObbSpace.X;
					}
					return new Transform2d(Vector2d.Zero, deformationScaleInObbSpace, 0);
				});
		}
	}
}
