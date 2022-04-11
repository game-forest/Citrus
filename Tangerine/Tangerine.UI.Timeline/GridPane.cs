using System;
using System.Linq;
using System.Collections.Generic;
using Lime;
using Tangerine.Core;

namespace Tangerine.UI.Timeline
{
	public class GridPane
	{
		public static Action<GridPane> OnCreate;

		private readonly Timeline timeline;
		public readonly Widget RootWidget;
		public readonly Widget ContentWidget;
		public event Action<Widget> OnPostRender;
		public readonly DropFilesGesture DropFilesGesture;

		public Vector2 Size => RootWidget.Size;
		public Vector2 ContentSize => ContentWidget.Size;

		private readonly ItemsInAnimationScope itemsInAnimationScope;

		public GridPane(Timeline timeline)
		{
			this.timeline = timeline;
			itemsInAnimationScope = new ItemsInAnimationScope();
			RootWidget = new Frame {
				Id = nameof(GridPane),
				Layout = new StackLayout { HorizontallySizeable = true, VerticallySizeable = true },
				ClipChildren = ClipMethod.ScissorTest,
				HitTestTarget = true,
			};
			ContentWidget = new Widget {
				Id = nameof(GridPane) + "Content",
				Padding = new Thickness { Top = 1, Bottom = 1 },
				Layout = new VBoxLayout { Spacing = TimelineMetrics.RowSpacing },
				Presenter = new SyncDelegatePresenter<Node>(RenderBackground),
				PostPresenter = new SyncDelegatePresenter<Widget>(w => OnPostRender(w)),
			};
			RootWidget.Updated += _ => {
				RefreshItemWidths();
				ContentWidget.Position = -timeline.Offset;
			};
			RootWidget.AddNode(ContentWidget);
			RootWidget.AddChangeWatcher(
				() => RootWidget.Size,
				// Some document operation processors (e.g. ColumnCountUpdater) require up-to-date timeline dimensions.
				_ => Core.Operations.Dummy.Perform(Document.Current.History)
			);
			OnPostRender += RenderSelection;
			OnPostRender += RenderCursor;
			DropFilesGesture = new DropFilesGesture();
			DropFilesGesture.Recognized += new GridPaneFilesDropHandler().Handle;
			RootWidget.Gestures.Add(DropFilesGesture);
			OnCreate?.Invoke(this);
			RootWidget.AddLateChangeWatcher(() => Document.Current.SceneTreeVersion, _ => Rebuild());
		}

		private void Rebuild()
		{
			var content = ContentWidget;
			content.Nodes.Clear();
			foreach (var item in Document.Current.VisibleSceneItems) {
				var gridItem = item.Components.Get<Components.RowView>().GridRowView;
				var widget = gridItem.GridWidget;
				if (!gridItem.GridWidgetAwakeBehavior.IsAwoken) {
					gridItem.GridWidgetAwakeBehavior.Update(0);
				}
				content.AddNode(widget);
			}
			RefreshItemWidths();
			// Layout widgets in order to have valid row positions and sizes,
			// which are used for rows visibility determination.
			WidgetContext.Current.Root.LayoutManager.Layout();
			itemsInAnimationScope.CalculateAnimationScope();
		}

		private void RefreshItemWidths()
		{
			foreach (var item in Document.Current.VisibleSceneItems) {
				var gridItem = item.Components.Get<Components.RowView>().GridRowView;
				gridItem.GridWidget.MinWidth = Timeline.Instance.ColumnCount * TimelineMetrics.ColWidth;
			}
		}

		private void RenderBackground(Node node)
		{
			RootWidget.PrepareRendererState();
			Renderer.DrawRect(Vector2.Zero, RootWidget.Size, ColorTheme.Current.TimelineGrid.Lines);
			if (ContentWidget.Nodes.Count > 0) {
				ContentWidget.PrepareRendererState();
				Renderer.DrawRect(Vector2.Zero, ContentWidget.Size, ColorTheme.Current.TimelineGrid.Background);
				RenderSelectedItemsBackground();
			}
			RenderAnimationScope();
			RenderVerticalLines();
			RenderHorizontalLines();
			RenderMarkerRulers();
		}

		private void RenderSelectedItemsBackground()
		{
			foreach (var i in Document.Current.SelectedSceneItems()) {
				var gridWidget = i.GridWidget();
				Renderer.DrawRect(
					x0: 0.0f,
					y0: gridWidget.Top(),
					x1: gridWidget.Right(),
					y1: gridWidget.Bottom(),
					color: ColorTheme.Current.TimelineGrid.SelectedRowBackground
				);
			}
		}

		private void RenderAnimationScope()
		{
			if (Document.Current.Animation.IsCompound) {
				return;
			}
			int itemIndex = 0;
			foreach (var item in Document.Current.VisibleSceneItems) {
				if (!itemsInAnimationScope.IsItemInAnimationScope(itemIndex++)) {
					var w = item.GridWidget();
					var a = new Vector2(0.0f, w.Y);
					var b = new Vector2(ContentWidget.Width, w.Y + w.Height);
					Renderer.DrawRect(a, b, ColorTheme.Current.TimelineGrid.OutOfAnimationScope);
				}
			}
		}

		private void RenderVerticalLines()
		{
			var a = new Vector2(0, 1);
			var b = new Vector2(
				0,
				Document.Current.VisibleSceneItems.Count * (TimelineMetrics.DefaultRowHeight + 1) + 1
			);
			timeline.GetVisibleColumnRange(out var minColumn, out var maxColumn);
			var offset = Document.Current.Animation.IsCompound ? 0.5f : 0;
			for (int columnIndex = minColumn; columnIndex <= maxColumn; columnIndex++) {
				a.X = b.X = 0.5f + (columnIndex + offset) * TimelineMetrics.ColWidth;
				Renderer.DrawLine(a, b, ColorTheme.Current.TimelineGrid.LinesLight);
			}
		}

		private void RenderHorizontalLines()
		{
			var a = new Vector2(0.0f, 0.5f);
			var b = new Vector2(ContentWidget.Width, 0.5f);
			Renderer.DrawLine(a, b, ColorTheme.Current.TimelineGrid.Lines);
			foreach (var i in Document.Current.VisibleSceneItems) {
				a.Y = b.Y = 0.5f + i.GridWidget().Bottom();
				Renderer.DrawLine(a, b, ColorTheme.Current.TimelineGrid.Lines);
			}
		}

		private void RenderMarkerRulers()
		{
			var a = new Vector2(0.0f, 1.0f);
			var b = new Vector2(0.0f, ContentWidget.Height - 2.0f);
			var offset = Document.Current.Animation.IsCompound ? 0.5f : 0;
			foreach (var marker in Document.Current.Animation.Markers) {
				if (timeline.IsColumnVisible(marker.Frame)) {
					a.X = b.X = 0.5f + TimelineMetrics.ColWidth * (marker.Frame + offset);
					Renderer.DrawLine(a, b, ColorTheme.Current.TimelineGrid.Lines);
				}
			}
		}

		private void RenderCursor(Node node)
		{
			var x = TimelineMetrics.ColWidth * (timeline.CurrentColumnEased + 0.5f);
			ContentWidget.PrepareRendererState();
			Renderer.DrawLine(
				x0: x,
				y0: 0,
				x1: x,
				y1: ContentWidget.Height - 1,
				color: Document.Current.PreviewScene
					? ColorTheme.Current.TimelineRuler.RunningCursor
					: ColorTheme.Current.TimelineRuler.Cursor
			);
		}

		private void RenderSelection(Widget widget)
		{
			RenderSelection(widget, IntVector2.Zero);
		}

		public void RenderSelection(Widget widget, IntVector2 offset)
		{
			widget.PrepareRendererState();
			var gridSpans = new List<Core.Components.GridSpanList>();
			foreach (var item in Document.Current.VisibleSceneItems) {
				gridSpans.Add(
					item.Components.GetOrAdd<Core.Components.GridSpanListComponent>().Spans.GetNonOverlappedSpans()
				);
			}

			for (var itemIndex = 0; itemIndex < Document.Current.VisibleSceneItems.Count; itemIndex++) {
				var spans = gridSpans[itemIndex];
				int? lastColumn = null;
				var topSpans = itemIndex > 0
					? gridSpans[itemIndex - 1].GetEnumerator()
					: (IEnumerator<Core.Components.GridSpan>)null;
				var bottomSpans = itemIndex + 1 < Document.Current.VisibleSceneItems.Count
					? gridSpans[itemIndex + 1].GetEnumerator()
					: (IEnumerator<Core.Components.GridSpan>)null;
				Core.Components.GridSpan? topSpan = null;
				Core.Components.GridSpan? bottomSpan = null;
				var offsetItem = itemIndex + offset.Y;
				var gridWidgetBottom = (0 <= offsetItem && offsetItem < Document.Current.VisibleSceneItems.Count)
					? (float?)Document.Current.VisibleSceneItems[offsetItem].GridWidget().Bottom()
					: null;
				for (var i = 0; i < spans.Count; i++) {
					var span = spans[i];
					var isLastSpan = i + 1 == spans.Count;
					for (var column = span.A; column < span.B; column++) {
						var isLeftCellMissing = !lastColumn.HasValue || column - 1 > lastColumn.Value;
						if (topSpans != null && (!topSpan.HasValue || column >= topSpan.Value.B)) {
							do {
								if (!topSpans.MoveNext()) {
									topSpans = null;
									topSpan = null;
									break;
								}
								topSpan = topSpans.Current;
							} while (column >= topSpan.Value.B);
						}
						var isTopCellMissing = !topSpan.HasValue || column < topSpan.Value.A;
						var isRightCellMissing = column + 1 == span.B && (isLastSpan || column + 1 < spans[i + 1].A);
						if (bottomSpans != null && (!bottomSpan.HasValue || column >= bottomSpan.Value.B)) {
							do {
								if (!bottomSpans.MoveNext()) {
									bottomSpans = null;
									bottomSpan = null;
									break;
								}
								bottomSpan = bottomSpans.Current;
							} while (column >= bottomSpan.Value.B);
						}
						var isBottomCellMissing = !bottomSpan.HasValue || column < bottomSpan.Value.A;
						lastColumn = column;

						var a = CellToGridCoordinates(new IntVector2(column, itemIndex) + offset);
						var b = CellToGridCoordinates(new IntVector2(column + 1, itemIndex + 1) + offset);
						a = new Vector2(a.X + 1.5f, a.Y + 0.5f);
						b = new Vector2(b.X - 0.5f, (gridWidgetBottom ?? b.Y) - 0.5f);
						Renderer.DrawRect(
							a: a + Vector2.Up * 0.5f,
							b: b + new Vector2(
								x: 1f + (isRightCellMissing ? 0 : 1),
								y: isBottomCellMissing ? 0 : 1
							),
							color: ColorTheme.Current.TimelineGrid.Selection
						);
						if (isLeftCellMissing) {
							Renderer.DrawLine(
								x0: a.X,
								y0: a.Y - (isTopCellMissing ? 0 : 1),
								x1: a.X,
								y1: b.Y + (isBottomCellMissing ? 0 : 1),
								color: ColorTheme.Current.TimelineGrid.SelectionBorder,
								cap: LineCap.Square
							);
						}
						if (isTopCellMissing) {
							Renderer.DrawLine(
								x0: a.X - (isLeftCellMissing ? 0 : 1),
								y0: a.Y,
								x1: b.X + (isRightCellMissing ? 0 : 1),
								y1: a.Y,
								color: ColorTheme.Current.TimelineGrid.SelectionBorder,
								cap: LineCap.Square
							);
						}
						if (isRightCellMissing) {
							Renderer.DrawLine(
								x0: b.X,
								y0: a.Y - (isTopCellMissing ? 0 : 1),
								x1: b.X,
								y1: b.Y + (isBottomCellMissing ? 0 : 1),
								color: ColorTheme.Current.TimelineGrid.SelectionBorder,
								cap: LineCap.Square
							);
						}
						if (isBottomCellMissing) {
							Renderer.DrawLine(
								x0: a.X - (isLeftCellMissing ? 0 : 1),
								y0: b.Y,
								x1: b.X + (isRightCellMissing ? 0 : 1),
								y1: b.Y,
								color: ColorTheme.Current.TimelineGrid.SelectionBorder,
								cap: LineCap.Square
							);
						}
					}
				}
			}
		}

		public Vector2 CellToGridCoordinates(IntVector2 cell)
		{
			return CellToGridCoordinates(cell.Y, cell.X);
		}

		public Vector2 CellToGridCoordinates(int row, int col)
		{
			var doc = Document.Current;
			var items = doc.VisibleSceneItems;
			var y = row < items.Count
				? items[Math.Max(row, 0)].GridWidget().Top()
				: items[items.Count - 1].GridWidget().Bottom();
			return new Vector2((col + (doc.Animation.IsCompound ? 0.5f : 0)) * TimelineMetrics.ColWidth, y);
		}

		public IntVector2 CellUnderMouse()
		{
			var mousePos = RootWidget.Input.MousePosition - ContentWidget.GlobalPosition;
			var offset = Document.Current.Animation.IsCompound ? -0.5f : 0;
			var x = (int)(mousePos.X / TimelineMetrics.ColWidth + offset);
			if (mousePos.Y <= 0) {
				return new IntVector2(x, Document.Current.VisibleSceneItems.Count > 0 ? 0 : -1);
			}
			foreach (var item in Document.Current.VisibleSceneItems) {
				var gridWidget = item.GridWidget();
				if (mousePos.Y >= gridWidget.Top() && mousePos.Y < gridWidget.Bottom() + TimelineMetrics.RowSpacing) {
					return new IntVector2(x, item.GetTimelineSceneItemState().Index);
				}
			}
			return new IntVector2(x, Math.Max(0, Document.Current.VisibleSceneItems.Count - 1));
		}

		public bool IsMouseOverRow()
		{
			return RootWidget.Input.MousePosition.Y - ContentWidget.GlobalPosition.Y < ContentSize.Y;
		}

		private class ItemsInAnimationScope
		{
			private readonly List<Node> pathToContainer;
			private readonly HashSet<Node> nodesInAnimationScope;
			private readonly List<bool> itemsInAnimationScope;

			public ItemsInAnimationScope()
			{
				pathToContainer = new List<Node>();
				nodesInAnimationScope = new HashSet<Node>();
				itemsInAnimationScope = new List<bool>();
			}

			public bool IsItemInAnimationScope(int indexOfVisibleSceneItem)
			{
				return itemsInAnimationScope[indexOfVisibleSceneItem];
			}

			public void CalculateAnimationScope()
			{
				itemsInAnimationScope.Clear();
				var animation = Document.Current.Animation;
				if (animation.IsCompound) {
					return;
				}
				for (var node = Document.Current.Container; node != null; node = node.Parent) {
					pathToContainer.Add(node);
				}
				for (int i = pathToContainer.Count - 1; i >= 0; i--) {
					IsNodeInAnimationScope(pathToContainer[i]);
				}
				foreach (var item in Document.Current.VisibleSceneItems) {
					var node = item.TryGetAnimator(out _) ? item.Parent.GetNode() : item.GetNode();
					itemsInAnimationScope.Add(node != null && IsNodeInAnimationScope(node));
				}
				nodesInAnimationScope.Clear();
				pathToContainer.Clear();

				bool IsNodeInAnimationScope(Node node)
				{
					var parent = node.Parent;
					if (
						(nodesInAnimationScope.Contains(parent) || parent == animation.OwnerNode) &&
						parent.Animations.All(a => a == animation || a.Id != animation.Id)
					) {
						nodesInAnimationScope.Add(node);
						return true;
					}
					return false;
				}
			}
		}
	}
}
