using System;
using System.Collections.Generic;
using Lime;
using Tangerine.Core;

namespace Tangerine.UI.SceneView
{
	public class PaddingLinePresenter
	{
		private readonly SceneView sv;

		private readonly VisualHint paddingVisualHint =
			VisualHintsRegistry.Instance.Register(
				"/All/Padding", hideRule: VisualHintsRegistry.HideRules.VisibleIfProjectOpened
			);

		public PaddingLinePresenter(SceneView sceneView)
		{
			this.sv = sceneView;
			sceneView.Frame.CompoundPostPresenter.Add(new SyncDelegatePresenter<Widget>(Render));
		}

		private void Render(Widget canvas)
		{
			if (Document.Current.ExpositionMode ||
				Document.Current.PreviewAnimation ||
				Document.Current.PreviewScene ||
				!paddingVisualHint.Enabled) {
				return;
			}
			canvas.PrepareRendererState();
			var transition = sv.CalcTransitionFromSceneSpace(canvas);
			foreach (var node in Document.Current.SelectedNodes()) {
				if (node is Widget widget) {
					foreach (var line in PaddingLine.GetLines(widget)) {
						line.Render(canvas, widget.LocalToWorldTransform * transition);
					}
				}
			}
		}
	}

	public class PaddingLine
	{
		public enum ThicknessProperty
		{
			Left,
			Bottom,
			Right,
			Top,
		}

		public Widget Owner { get; }
		public float Value => propertyGetters[index](Owner);
		public ThicknessProperty PropertyName => propertyNames[index];
		public Vector2 A { get; set; }
		public Vector2 B { get; set; }
		public Vector2 Center { get; set; }
		public int FontHeight { get; private set; } = 20;

		private readonly int index;

		private static readonly Vector2 rectScale = new Vector2(2, 0);
		private static readonly Rectangle rect = new Rectangle(-1, -1, 1, 1);

		private static readonly Vector2[] directions = {
			new Vector2(1, 0),
			new Vector2(0, -1),
			new Vector2(-1, 0),
			new Vector2(0, 1),
		};

		private static readonly ThicknessProperty[] propertyNames = {
			ThicknessProperty.Left,
			ThicknessProperty.Bottom,
			ThicknessProperty.Right,
			ThicknessProperty.Top,
		};

		private static readonly Func<Widget, float>[] propertyGetters = {
			g => g.Padding.Left,
			g => g.Padding.Bottom,
			g => g.Padding.Right,
			g => g.Padding.Top,
		};

		public PaddingLine(int index, Tuple<Vector2, Vector2> aB, Widget widget)
		{
			this.index = index;
			Owner = widget;
			A = aB.Item1;
			B = aB.Item2;
		}

		public void Render(Widget canvas, Matrix32 matrix)
		{
			var a = matrix.TransformVector(A);
			var b = matrix.TransformVector(B);
			Renderer.DrawLine(a, b, ColorTheme.Current.SceneView.PaddingEditorBorder, 2);

			Center = matrix.TransformVector((A + B) / 2);
			var label = propertyNames[index].ToString()[0].ToString();
			var angle = Owner.LocalToWorldTransform.U.Atan2Rad;
			var scale = new Vector2(FontHeight * 0.25f, FontHeight * 0.5f);
			var rectMatrix = Matrix32.Transformation(directions[index], scale + rectScale, angle, Center);
			var textMatrix = Matrix32.Transformation(Vector2.Zero, Vector2.One, angle, Center);
			Renderer.PushState(RenderState.Transform1);
			Renderer.Transform1 = rectMatrix * canvas.LocalToWorldTransform;
			Renderer.DrawRect(rect.A, rect.B, ColorTheme.Current.SceneView.PaddingEditorBorder);
			Renderer.PushState(RenderState.Transform1);
			Renderer.Transform1 = textMatrix * canvas.LocalToWorldTransform;
			Renderer.DrawTextLine(
				(directions[(index + 2) % 4] - Vector2.One) * scale,
				label,
				FontHeight,
				ColorTheme.Current.SceneView.PaddingEditorText,
				0);
			Renderer.PopState();
			Renderer.PopState();
		}

		public Vector2 GetDirection()
		{
			return directions[index];
		}

		public static IEnumerable<PaddingLine> GetLines(Widget widget)
		{
			var aabb = new Rectangle(Vector2.Zero, widget.Size);
			var paddings = new float[] {
				widget.Padding.Left,
				widget.Padding.Bottom,
				widget.Padding.Right,
				widget.Padding.Top,
			};
			var lines = new[] {
				Tuple.Create(
					new Vector2(aabb.AX + paddings[0], aabb.AY + paddings[3]),
					new Vector2(aabb.AX + paddings[0], aabb.BY - paddings[1])),
				Tuple.Create(
					new Vector2(aabb.AX + paddings[0], aabb.BY - paddings[1]),
					new Vector2(aabb.BX - paddings[2], aabb.BY - paddings[1])),
				Tuple.Create(
					new Vector2(aabb.BX - paddings[2], aabb.AY + paddings[3]),
					new Vector2(aabb.BX - paddings[2], aabb.BY - paddings[1])),
				Tuple.Create(
					new Vector2(aabb.AX + paddings[0], aabb.AY + paddings[3]),
					new Vector2(aabb.BX - paddings[2], aabb.AY + paddings[3])),
			};
			for (var i = 0; i < 4; ++i) {
				yield return new PaddingLine(i, lines[i], widget);
			}
		}
	}
}
