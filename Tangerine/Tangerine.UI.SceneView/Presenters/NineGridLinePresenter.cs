using System;
using System.Collections.Generic;
using System.Linq;
using Lime;
using Tangerine.Core;

namespace Tangerine.UI.SceneView
{
	public class NineGridLinePresenter
	{
		public NineGridLinePresenter(SceneView sceneView)
		{
			sceneView.Frame.CompoundPostPresenter.Add(new SyncDelegatePresenter<Widget>(Render));
		}

		private static void Render(Widget canvas)
		{
			if (Document.Current.PreviewScene) {
				return;
			}
			var grids = Document.Current.SelectedNodes().Editable().OfType<NineGrid>();
			foreach (var grid in grids) {
				foreach (var line in NineGridLine.GetForNineGrid(grid)) {
					line.Render(canvas);
				}
			}
		}
	}

	public class NineGridLine
	{
		public NineGrid Owner { get; }
		public string PropertyName => propertyNames[index];
		public float Value => propertyGetters[index](Owner);
		public float TextureSize => textureSizeGetters[index % 2](Owner);
		public float GridSize => nineGridSizeGetters[index % 2](Owner);
		public float MaxValue => GridSize / TextureSize;
		public float Scale => nineGridScaleGetters[index % 2](Owner);

		private readonly int index;
		private int IndexA => indexes[index].Item1;
		private int IndexB => indexes[index].Item2;

		private static readonly Tuple<int, int>[] indexes = {
			Tuple.Create(5, 2),
			Tuple.Create(7, 1),
			Tuple.Create(1, 6),
			Tuple.Create(2, 8),
		};
		private static readonly string[] propertyNames = {
			nameof(NineGrid.LeftOffset),
			nameof(NineGrid.TopOffset),
			nameof(NineGrid.RightOffset),
			nameof(NineGrid.BottomOffset),
		};
		private static readonly Func<NineGrid, float>[] propertyGetters = {
			g => g.LeftOffset,
			g => g.TopOffset,
			g => g.RightOffset,
			g => g.BottomOffset,
		};
		private static readonly Func<NineGrid, float>[] textureSizeGetters = {
			g => g.Texture.ImageSize.Width,
			g => g.Texture.ImageSize.Height,
		};
		private static readonly Func<NineGrid, float>[] nineGridSizeGetters = {
			g => g.Size.X,
			g => g.Size.Y,
		};
		private static readonly Func<NineGrid, float>[] nineGridScaleGetters = {
			g => g.Scale.X,
			g => g.Scale.Y,
		};
		private static readonly Vector2[] directions = {
			new Vector2(1, 0), new Vector2(0, 1),
			new Vector2(-1, 0), new Vector2(0, -1),
		};

		public NineGridLine(int index, NineGrid nineGrid)
		{
			this.index = index;
			Owner = nineGrid;
		}

		private NineGrid.Part[] parts = new NineGrid.Part[9];

		private void CalcGeometry(Matrix32 matrix, out Vector2 a, out Vector2 b)
		{
			NineGrid.BuildLayout(
				layout: parts,
				textureSize: (Vector2)Owner.Texture.ImageSize,
				leftOffset: Owner.LeftOffset,
				rightOffset: Owner.RightOffset,
				topOffset: Owner.TopOffset,
				bottomOffset: Owner.BottomOffset,
				size: Owner.Size
			);
			a = matrix.TransformVector(parts[IndexA].Rect.A);
			b = matrix.TransformVector(parts[IndexB].Rect.B);
		}

		public void Render(Widget canvas)
		{
			var sv = SceneView.Instance;
			var matrix = Owner.LocalToWorldTransform * sv.CalcTransitionFromSceneSpace(canvas);
			CalcGeometry(matrix, out var a, out var b);
			RendererNvg.DrawLine(a, b, Color4.Red, 2);
		}

		public bool HitTest(Vector2 point, Widget canvas, float radius = 20)
		{
			var sv = SceneView.Instance;
			var matrix = Owner.LocalToWorldTransform * sv.CalcTransitionFromSceneSpace(canvas);
			CalcGeometry(matrix, out var a, out var b);
			return Utils.LineHitTest(point, a, b, radius);
		}

		public Vector2 GetDirection()
		{
			var matrix = Owner.CalcLocalToParentTransform();
			return (matrix * directions[index] - matrix * Vector2.Zero).Normalized;
		}

		public static IEnumerable<NineGridLine> GetForNineGrid(NineGrid nineGrid)
		{
			for (var i = 0; i < 4; ++i) {
				yield return new NineGridLine(i, nineGrid);
			}
		}
	}
}
