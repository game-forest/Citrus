using System;
using Tangerine.Core;
using Lime;
using System.Collections.Generic;

namespace Tangerine.UI.SceneView
{
	public class PaddingLinePresenter
	{
		private readonly SceneView sv;

		private readonly VisualHint paddingVisualHint =
			VisualHintsRegistry.Instance.Register("/All/Padding", hideRule: VisualHintsRegistry.HideRules.VisibleIfProjectOpened);

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
						line.Render(widget.LocalToWorldTransform * transition);
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
			Top
		}

		public Widget Owner { get; }
		public float Value => propertyGetters[index](Owner);
		public ThicknessProperty PropertyName => propertyNames[index];
		public Vector2 A { get; set; }
		public Vector2 B { get; set; }
		public Vector2 Center { get; set; }

		private readonly int index;

		private static readonly Vector2[] directions = {
			new Vector2(1, 0),
			new Vector2(0, -1),
			new Vector2(-1, 0),
			new Vector2(0, 1)
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
			g => g.Padding.Top
		};

		public PaddingLine(int index, Tuple<Vector2, Vector2> AB, Widget widget)
		{
			this.index = index;
			Owner = widget;
			A = AB.Item1;
			B = AB.Item2;
		}

		public void Render(Matrix32 matrix)
		{
			var a = matrix.TransformVector(A);
			var b = matrix.TransformVector(B);
			Renderer.DrawLine(a, b, Color4.Yellow, 2);

			var label = propertyNames[index].ToString()[0].ToString();
			var fontHeight = 20;
			if (index % 2 == 0) {
				Center = new Vector2(a.X, (a.Y + b.Y) / 2);
			} else {
				Center = new Vector2((a.X + b.X) / 2, a.Y);
			}
			Center -= new Vector2(fontHeight / 4, fontHeight / 2);
			var lt = new Vector2(Center.X - 2, Center.Y);
			var rb = new Vector2(Center.X + fontHeight / 2 + 1, Center.Y + fontHeight);
			Renderer.DrawRect(lt, rb, Color4.Yellow);
			Renderer.DrawTextLine(
				Center,
				label,
				fontHeight,
				Color4.Blue,
				0
			);
		}

		public Vector2 GetDirection()
		{
			var matrix = Owner.CalcLocalToParentTransform();
			return (matrix * directions[index] - matrix * Vector2.Zero).Normalized;
		}

		public static IEnumerable<PaddingLine> GetLines(Widget widget)
		{
			var aabb = new Rectangle(Vector2.Zero, widget.Size);
			var paddings = new float[] {
				widget.Padding.Left,
				widget.Padding.Bottom,
				widget.Padding.Right,
				widget.Padding.Top
			};
			var lines = new[] {
				Tuple.Create(
					new Vector2(aabb.AX + paddings[0], aabb.AY + paddings[3]),
					new Vector2(aabb.AX + paddings[0], aabb.BY - paddings[1])
				),
				Tuple.Create(
					new Vector2(aabb.AX + paddings[0], aabb.BY - paddings[1]),
					new Vector2(aabb.BX - paddings[2], aabb.BY - paddings[1])
				),
				Tuple.Create(
					new Vector2(aabb.BX - paddings[2], aabb.AY + paddings[3]),
					new Vector2(aabb.BX - paddings[2], aabb.BY - paddings[1])
				),
				Tuple.Create(
					new Vector2(aabb.AX + paddings[0], aabb.AY + paddings[3]),
					new Vector2(aabb.BX - paddings[2], aabb.AY + paddings[3])
				)
			};
			for (var i = 0; i < 4; ++i) {
				yield return new PaddingLine(i, lines[i], widget);
			}
		}
	}
}
