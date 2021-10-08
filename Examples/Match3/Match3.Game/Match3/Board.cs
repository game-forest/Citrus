using Lime;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Debug = System.Diagnostics.Debug;

namespace Match3
{
	public static class Match3Config
	{
		public static float CellSize { get; set; } = 90.0f;
	}

	public static class BoardConfig
	{
		public static int ColumnCount { get; set; } = 6;
		public static int RowCount { get; set; } = 10;

		public static int[] AllowedPieceKinds = { 0, 1, 2, 3, 4 };
	}

	public class Board
	{
		private readonly Widget boardContainer;
		private readonly Frame pieceContainer;
		private readonly Widget pieceTemplate;
		private float boardScale;
		private Grid<Piece> grid = new Grid<Piece>();
		private List<Piece> pieces = new List<Piece>();

		public Board(Widget boardContainer)
		{
			this.boardContainer = boardContainer;
			pieceTemplate = Node.Load<Widget>("Game/Match3/MultiMarble");
			pieceContainer = new Frame {
				Width = BoardConfig.ColumnCount * Match3Config.CellSize,
				Height = BoardConfig.RowCount * Match3Config.CellSize,
			};
			this.boardContainer.Nodes.Insert(0, pieceContainer);
			this.boardContainer.Tasks.Add(this.Update);
			pieceContainer.CompoundPostPresenter.Add(new WidgetBoundsPresenter(Color4.Green, 2.0f));
			//FillBoard();
		}

		void UpdateBoardScale()
		{
			var widthAspect = boardContainer.Width / pieceContainer.Width;
			var heightAspect = boardContainer.Height / pieceContainer.Height;
			boardScale = Mathf.Min(widthAspect, heightAspect);
			pieceContainer.Scale = new Vector2(boardScale);
			pieceContainer.CenterOnParent();
		}

		private IEnumerator<object> Update()
		{
			while (true) {
				UpdateBoardScale();
				Spawn();
				yield return null;
			}
		}

		private void Spawn()
		{
			for (int x = 0; x < BoardConfig.ColumnCount; x++) {
				var gridPosition = new IntVector2(x, 0);
				if (grid[gridPosition] == null) {
					var piece = CreatePiece(gridPosition);
					piece.AnimateShow();
				}
			}
		}

		private void FillBoard()
		{
			for (int x = 0; x < BoardConfig.ColumnCount; x++) {
				for (int y = 0; y < BoardConfig.RowCount; y++) {
					CreatePiece(new IntVector2(x, y));
				}
			}
		}

		private Piece CreatePiece(IntVector2 gridPosition)
		{
			var pieceWidget = pieceTemplate.Clone<Widget>();
			pieceContainer.AddNode(pieceWidget);
			var piece = new Piece(
				pieceWidget,
				gridPosition,
				BoardConfig.AllowedPieceKinds.RandomItem(),
				Piece_SetGridPosition
			);
			pieces.Add(piece);
			return piece;

			void Piece_SetGridPosition(Piece piece, IntVector2 gridPosition)
			{
				grid[piece.GridPosition] = null;
				System.Diagnostics.Debug.Assert(grid[gridPosition] == null);
				grid[gridPosition] = piece;
			}
		}
	}
}

