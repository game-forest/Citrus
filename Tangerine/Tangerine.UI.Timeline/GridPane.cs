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

		public GridPane(Timeline timeline)
		{
			this.timeline = timeline;
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
				PostPresenter = new SyncDelegatePresenter<Widget>(w => OnPostRender(w))
			};
			RootWidget.Updated += _ => {
				RefreshItemWidths();
				ContentWidget.Position = -timeline.Offset;
			};
			RootWidget.AddNode(ContentWidget);
			RootWidget.AddChangeWatcher(() => RootWidget.Size,
				// Some document operation processors (e.g. ColumnCountUpdater) require up-to-date timeline dimensions.
				_ => Core.Operations.Dummy.Perform(Document.Current.History));
			OnPostRender += RenderSelection;
			OnPostRender += RenderCursor;
			DropFilesGesture = new DropFilesGesture();
			DropFilesGesture.Recognized += new GridPaneFilesDropHandler().Handle;
			RootWidget.Gestures.Add(DropFilesGesture);
			OnCreate?.Invoke(this);
			RootWidget.AddChangeLateWatcher(() => Document.Current.SceneTreeVersion, _ => Rebuild());
		}

		private void Rebuild()
		{
			var content = ContentWidget;
			content.Nodes.Clear();
			foreach (var item in Document.Current.Rows) {
				var gridItem = item.Components.Get<Components.RowView>().GridRowView;
				var widget = gridItem.GridWidget;
				if (!gridItem.GridWidgetAwakeBehavior.IsAwoken) {
					gridItem.GridWidgetAwakeBehavior.Update(0);
				}
				content.AddNode(widget);
			}
			RefreshItemWidths();
			// Layout widgets in order to have valid row positions and sizes, which are used for rows visibility determination.
			WidgetContext.Current.Root.LayoutManager.Layout();
		}

		private void RefreshItemWidths()
		{
			foreach (var item in Document.Current.Rows) {
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
				Renderer.DrawRect(Vector2.Zero, ContentWidget.Size, Theme.Colors.WhiteBackground);
				RenderSelectedRowsBackground();
			}
			RenderVerticalLines();
			RenderHorizontalLines();
			RenderMarkerRulers();
		}

		private void RenderSelectedRowsBackground()
		{
			foreach (var row in Document.Current.SelectedRows()) {
				var gridWidget = row.GridWidget();
				Renderer.DrawRect(
					0.0f, gridWidget.Top(), gridWidget.Right(), gridWidget.Bottom(),
					ColorTheme.Current.TimelineGrid.SelectedRowBackground);
			}
		}

		private void RenderVerticalLines()
		{
			var a = new Vector2(0, 1);
			var b = new Vector2(0, Document.Current.Rows.Count * (TimelineMetrics.DefaultRowHeight + 1) + 1);
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
			foreach (var row in Document.Current.Rows) {
				a.Y = b.Y = 0.5f + row.GridWidget().Bottom();
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
				x, 0, x, ContentWidget.Height - 1,
				Document.Current.PreviewScene ?
				ColorTheme.Current.TimelineRuler.RunningCursor :
				ColorTheme.Current.TimelineRuler.Cursor);
		}

		void RenderSelection(Widget widget)
		{
			RenderSelection(widget, IntVector2.Zero);
		}

		public void RenderSelection(Widget widget, IntVector2 offset)
		{
			widget.PrepareRendererState();
			var gridSpans = new List<Core.Components.GridSpanList>();
			foreach (var row in Document.Current.Rows) {
				gridSpans.Add(row.Components.GetOrAdd<Core.Components.GridSpanListComponent>().Spans.GetNonOverlappedSpans());
			}

			for (var row = 0; row < Document.Current.Rows.Count; row++) {
				var spans = gridSpans[row];
				int? lastColumn = null;
				var topSpans = row > 0 ? gridSpans[row - 1].GetEnumerator() : (IEnumerator<Core.Components.GridSpan>)null;
				var bottomSpans = row + 1 < Document.Current.Rows.Count ? gridSpans[row + 1].GetEnumerator() : (IEnumerator<Core.Components.GridSpan>)null;
				Core.Components.GridSpan? topSpan = null;
				Core.Components.GridSpan? bottomSpan = null;
				var offsetRow = row + offset.Y;
				var gridWidgetBottom = (0 <= offsetRow && offsetRow < Document.Current.Rows.Count) ? (float?)Document.Current.Rows[offsetRow].GridWidget().Bottom() : null;
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

						var a = CellToGridCoordinates(new IntVector2(column, row) + offset);
						var b = CellToGridCoordinates(new IntVector2(column + 1, row + 1) + offset);
						a = new Vector2(a.X + 1.5f, a.Y + 0.5f);
						b = new Vector2(b.X - 0.5f, (gridWidgetBottom ?? b.Y) - 0.5f);
						Renderer.DrawRect(a + Vector2.Up * 0.5f, b + new Vector2(1f + (isRightCellMissing ? 0 : 1), (isBottomCellMissing ? 0 : 1)), ColorTheme.Current.TimelineGrid.Selection);
						if (isLeftCellMissing) {
							Renderer.DrawLine(a.X, a.Y - (isTopCellMissing ? 0 : 1), a.X, b.Y + (isBottomCellMissing ? 0 : 1), ColorTheme.Current.TimelineGrid.SelectionBorder, cap: LineCap.Square);
						}
						if (isTopCellMissing) {
							Renderer.DrawLine(a.X - (isLeftCellMissing ? 0 : 1), a.Y, b.X + (isRightCellMissing ? 0 : 1), a.Y, ColorTheme.Current.TimelineGrid.SelectionBorder, cap: LineCap.Square);
						}
						if (isRightCellMissing) {
							Renderer.DrawLine(b.X, a.Y - (isTopCellMissing ? 0 : 1), b.X, b.Y + (isBottomCellMissing ? 0 : 1), ColorTheme.Current.TimelineGrid.SelectionBorder, cap: LineCap.Square);
						}
						if (isBottomCellMissing) {
							Renderer.DrawLine(a.X - (isLeftCellMissing ? 0 : 1), b.Y, b.X + (isRightCellMissing ? 0 : 1), b.Y, ColorTheme.Current.TimelineGrid.SelectionBorder, cap: LineCap.Square);
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
			var rows = doc.Rows;
			var y = row < rows.Count ? rows[Math.Max(row, 0)].GridWidget().Top() : rows[rows.Count - 1].GridWidget().Bottom();
			return new Vector2((col + (doc.Animation.IsCompound ? 0.5f : 0)) * TimelineMetrics.ColWidth, y);
		}

		public IntVector2 CellUnderMouse()
		{
			var mousePos = RootWidget.Input.MousePosition - ContentWidget.GlobalPosition;
			var offset = Document.Current.Animation.IsCompound ? -0.5f : 0;
			var x = (int)(mousePos.X / TimelineMetrics.ColWidth + offset);
			if (mousePos.Y <= 0) {
				return new IntVector2(x, Document.Current.Rows.Count > 0 ? 0 : -1);
			}
			foreach (var row in Document.Current.Rows) {
				var gridWidget = row.GridWidget();
				if (mousePos.Y >= gridWidget.Top() && mousePos.Y < gridWidget.Bottom() + TimelineMetrics.RowSpacing) {
					return new IntVector2(x, row.GetTimelineItemState().Index);
				}
			}
			return new IntVector2(x, Math.Max(0, Document.Current.Rows.Count - 1));
		}

		public bool IsMouseOverRow() => RootWidget.Input.MousePosition.Y - ContentWidget.GlobalPosition.Y < ContentSize.Y;
	}
}
