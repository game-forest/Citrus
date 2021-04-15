using System.Linq;
using Lime;
using Tangerine.Core;
using Tangerine.UI;
using Tangerine.UI.SceneView;

namespace Tangerine
{
	public class RestoreOriginalSize : DocumentCommandHandler
	{
		public override void ExecuteTransaction()
		{
			foreach (var node in Core.Document.Current.SelectedNodes().Editable()) {
				if (node is Frame) {
					var frame = node as Frame;
					if (!string.IsNullOrEmpty(frame.ContentsPath)) {
						var extNode = Node.Load(frame.ContentsPath);
						if (extNode is Widget) {
							Core.Operations.SetAnimableProperty.Perform(node, nameof(Widget.Size), ((Widget)extNode).Size);
						}
						continue;
					}
				}
				if (node is Widget) {
					var widget = node as Widget;
					var originalSize = widget.Texture == null ? Widget.DefaultWidgetSize : (Vector2)widget.Texture.ImageSize;
					Core.Operations.SetAnimableProperty.Perform(node, nameof(Widget.Size), originalSize);
				} else if (node is ParticleModifier) {
					var particleModifier = node as ParticleModifier;
					var originalSize = particleModifier.Texture == null ? Widget.DefaultWidgetSize : (Vector2)particleModifier.Texture.ImageSize;
					Core.Operations.SetAnimableProperty.Perform(node, nameof(ParticleModifier.Size), originalSize);
				}
			}
		}
	}

	public class ResetScale : DocumentCommandHandler
	{
		public override void ExecuteTransaction()
		{
			foreach (var widget in Core.Document.Current.SelectedNodes().Editable().OfType<Widget>()) {
				Core.Operations.SetAnimableProperty.Perform(widget, nameof(Widget.Scale), Vector2.One);
			}
		}
	}

	public class ResetRotation : DocumentCommandHandler
	{
		public override void ExecuteTransaction()
		{
			foreach (var widget in Core.Document.Current.SelectedNodes().Editable().OfType<Widget>()) {
				Core.Operations.SetAnimableProperty.Perform(widget, nameof(Widget.Rotation), 0.0f);
			}
		}
	}

	public class FlipX : DocumentCommandHandler
	{
		public override void ExecuteTransaction()
		{
			Core.Operations.Flip.Perform(
				Core.Document.Current.SelectedNodes().Editable(),
				Core.Document.Current.Container.AsWidget, flipX: true, flipY: false);
		}
	}

	public class FlipY : DocumentCommandHandler
	{
		public override void ExecuteTransaction()
		{
			Core.Operations.Flip.Perform(
				Core.Document.Current.SelectedNodes().Editable(),
				Core.Document.Current.Container.AsWidget, flipX: false, flipY: true);
		}
	}

	public class FitToContainer : DocumentCommandHandler
	{
		public override void ExecuteTransaction()
		{
			var container = (Widget)Core.Document.Current.Container;
			foreach (var widget in Core.Document.Current.SelectedNodes().Editable().OfType<Widget>()) {
				Core.Operations.SetAnimableProperty.Perform(widget, nameof(Widget.Size), container.Size);
				Core.Operations.SetAnimableProperty.Perform(widget, nameof(Widget.Rotation), 0.0f);
				Core.Operations.SetAnimableProperty.Perform(widget, nameof(Widget.Position), widget.Pivot * container.Size);
				Core.Operations.SetAnimableProperty.Perform(widget, nameof(Widget.Scale), Vector2.One);
				Core.Operations.SetAnimableProperty.Perform(widget, nameof(Widget.Anchors), Anchors.LeftRightTopBottom);
			}
		}
	}

	public class FitToContent : DocumentCommandHandler
	{
		public override void ExecuteTransaction()
		{
			var container = (Widget)Core.Document.Current.Container;
			var nodes = Core.Document.Current.SelectedNodes().Editable();
			foreach (var widget in nodes.OfType<Widget>()) {
				if (Utils.CalcHullAndPivot(widget.Nodes, out var hull, out _)) {
					hull = hull.Transform(widget.LocalToWorldTransform.CalcInversed());
					var aabb = hull.ToAABB();
					foreach (var n in widget.Nodes) {
						// either Position of PointObject or of Widget
						foreach (var animator in n.Animators.Where(a => a.TargetPropertyPath == "Position")) {
							foreach (var keyframe in animator.ReadonlyKeys.ToList()) {
								var k = keyframe.Clone();
								k.Value = (Vector2)k.Value - aabb.A;
								Core.Operations.SetKeyframe.Perform(animator, Document.Current.Animation, k);
							}
						}
						if (n is Widget w) {
							Core.Operations.SetProperty.Perform(w, nameof(Widget.Position), w.Position - aabb.A);
						}
						if (n is PointObject po) {
							Core.Operations.SetProperty.Perform(po, nameof(PointObject.Position), po.Position - aabb.A);
						}
					}
					var p0 = widget.CalcTransitionToSpaceOf(container) * aabb.A;
					Core.Operations.SetAnimableProperty.Perform(widget, nameof(Widget.Size), aabb.Size);
					var p1 = widget.CalcTransitionToSpaceOf(container) * Vector2.Zero;
					Core.Operations.SetAnimableProperty.Perform(widget, nameof(Widget.Position), widget.Position + p0 - p1);
				}
			}
		}
	}

	public class CenterView : DocumentCommandHandler
	{
		public override void ExecuteTransaction()
		{
			var sv = SceneView.Instance;
			var scene = sv.Scene;
			var frame = sv.Frame;
			if (Utils.CalcHullAndPivot(Core.Document.Current.SelectedNodes().Editable(), out var hull, out _)) {
				var aabb = hull.ToAABB();
				var sceneViewBottomOffset = SceneView.ShowNodeDecorationsPanelButton.Size.Y;
				// The Vector2(9) is extra offset to make rect markers visible.
				var sceneViewInnerSize = scene.Size -
					new Vector2(RulersWidget.RulerHeight) -
					new Vector2(9) - new Vector2(0, sceneViewBottomOffset);
				var targetScale = sceneViewInnerSize / aabb.Size;
				targetScale = new Vector2(Mathf.Clamp(
					value: Mathf.Min(targetScale.X, targetScale.Y),
					min: ZoomWidget.zoomTable.First(),
					max: ZoomWidget.zoomTable.Last()));
				var offset = new Vector2(
					RulersWidget.RulerHeight / 2,
					(RulersWidget.RulerHeight - sceneViewBottomOffset) / 2);
				var targetPosition = offset + GetPosition(aabb, targetScale, frame);
				bool positionNotChanged = Vector2.Distance(targetPosition, scene.Position) < 0.1f / scene.Scale.X;
				bool scaleNotChanged = Vector2.Distance(targetScale, scene.Scale) < 0.001f * scene.Scale.X;
				if (positionNotChanged && scaleNotChanged) {
					// Add indent 10%
					targetScale *= 0.8f;
					targetPosition = offset + GetPosition(aabb, targetScale, frame);
				}
				scene.Position = targetPosition;
				scene.Scale = targetScale;
			}
		}

		private static Vector2 GetPosition(Rectangle aabb, Vector2 targetScale, Widget frame) =>
			-aabb.Center * targetScale + new Vector2(frame.Width / 2, frame.Height / 2);
	}
}
