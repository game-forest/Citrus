using System.Collections.Generic;
using System.Linq;

namespace Lime
{
	[YuzuDontGenerateDeserializer]
	public class AnchorLayout : ILayout
	{
		public static readonly ILayout Instance = new AnchorLayout();

		public List<Rectangle> DebugRectangles => throw new System.NotSupportedException();

		public Widget Owner
		{
			get => throw new System.NotSupportedException();
			set => throw new System.NotSupportedException();
		}

		public bool ConstraintsValid => throw new System.NotSupportedException();

		public bool ArrangementValid => throw new System.NotSupportedException();

		public void OnSizeChanged(Widget widget, Vector2 sizeDelta)
		{
			for (var child = widget.FirstChild; child != null; child = child.NextSibling) {
				if (child.AsWidget != null) {
					ApplyAnchors(child.AsWidget, sizeDelta);
				}
			}
		}

		private static void ApplyAnchors(Widget widget, Vector2 parentSizeDelta)
		{
			if (widget.Anchors == Anchors.None || widget.ParentWidget == null) {
				return;
			}
			Vector2 positionDelta;
			Vector2 sizeDelta;
			CalcXAndWidthDeltas(widget, parentSizeDelta.X, out positionDelta.X, out sizeDelta.X);
			CalcYAndHeightDeltas(widget, parentSizeDelta.Y, out positionDelta.Y, out sizeDelta.Y);
			ApplyPositionAndSizeDelta(widget, positionDelta, sizeDelta);
		}

		private static void CalcXAndWidthDeltas(
			Widget widget, float parentWidthDelta, out float xDelta, out float widthDelta
		) {
			xDelta = 0;
			widthDelta = 0;
			if ((widget.Anchors & Anchors.CenterH) != 0) {
				xDelta = parentWidthDelta * 0.5f;
			} else if ((widget.Anchors & Anchors.Left) != 0 && (widget.Anchors & Anchors.Right) != 0) {
				widthDelta = parentWidthDelta;
				xDelta = parentWidthDelta * widget.Pivot.X;
			} else if ((widget.Anchors & Anchors.Right) != 0) {
				xDelta = parentWidthDelta;
			}
		}

		private static void CalcYAndHeightDeltas(
			Widget widget, float parentHeightDelta, out float yDelta, out float heightDelta
		) {
			yDelta = 0;
			heightDelta = 0;
			if ((widget.Anchors & Anchors.CenterV) != 0) {
				yDelta = parentHeightDelta * 0.5f;
			} else if ((widget.Anchors & Anchors.Top) != 0 && (widget.Anchors & Anchors.Bottom) != 0) {
				heightDelta = parentHeightDelta;
				yDelta = parentHeightDelta * widget.Pivot.Y;
			} else if ((widget.Anchors & Anchors.Bottom) != 0) {
				yDelta = parentHeightDelta;
			}
		}

		private static void ApplyPositionAndSizeDelta(Widget widget, Vector2 positionDelta, Vector2 sizeDelta)
		{
			widget.Position += positionDelta;
			widget.Size += sizeDelta;
			if (widget.Animators.Count <= 0) {
				return;
			}
			foreach (var animator in widget.Animators.OfType<Animator<Vector2>>()) {
				if (animator.TargetPropertyPath == nameof(Widget.Position)) {
					foreach (var key in animator.Keys) {
						key.Value += positionDelta;
					}
					animator.ResetCache();
				}
				if (animator.TargetPropertyPath == nameof(Widget.Size)) {
					foreach (var key in animator.Keys) {
						key.Value += sizeDelta;
					}
					animator.ResetCache();
				}
			}
		}

		public void InvalidateConstraintsAndArrangement()
		{
		}

		public void InvalidateArrangement()
		{
			throw new System.NotSupportedException();
		}

		public void MeasureSizeConstraints()
		{
			throw new System.NotSupportedException();
		}

		public void ArrangeChildren()
		{
			throw new System.NotSupportedException();
		}
	}
}
