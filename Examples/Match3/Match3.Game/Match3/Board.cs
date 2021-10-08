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
		public static bool WaitForAnimateDropDownLand { get; set; } = true;
		public static bool WaitForAnimateDropDownFall { get; set; } = true;
		internal static float OneCellFallTime { get; set; } = 0.1f;
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
				Fall();
				HandleInput();
				ProcessMatches();
				yield return null;
			}
		}

		private void Spawn()
		{
			for (int x = 0; x < BoardConfig.ColumnCount; x++) {
				var gridPosition = new IntVector2(x, 0);
				if (grid[gridPosition] == null) {
					var piece = CreatePiece(gridPosition);
					var a = piece.AnimateShow();
					piece.RunTask(Task.Repeat(() => {
						return a.IsRunning;
					}));
				}
			}
		}

		private void Fall()
		{
			foreach (var piece in pieces) {
				if (piece.Task == null && CanFall(piece)) {
					piece.RunTask(FallTask(piece));
				}
			}
		}

		private bool CanFall(Piece piece)
		{
			var belowPosition = piece.GridPosition + IntVector2.Down;
			return grid[belowPosition] == null
				&& belowPosition.Y < BoardConfig.RowCount;
		}

		private IEnumerator<object> FallTask(Piece piece)
		{
			var a = piece.AnimateDropDownFall();
			if (Match3Config.WaitForAnimateDropDownFall) {
				yield return a;
			}
			while (CanFall(piece)) {
				var belowPosition = piece.GridPosition + IntVector2.Down;
				yield return piece.MoveTo(belowPosition, Match3Config.OneCellFallTime);
			}
			a = piece.AnimateDropDownLand();
			if (Match3Config.WaitForAnimateDropDownLand) {
				yield return a;
			}
		}

		private void HandleInput()
		{
			if (Window.Current.Input.WasMousePressed()) {
				var piece = WidgetContext.Current.NodeUnderMouse?.Components.Get<Piece>();
				if (piece != null && piece.Task == null) {
					piece.RunTask(BlowTask(piece));
				}
			}
		}

		private void ProcessMatches()
		{
			var matches = FindMatches();
			foreach (var match in matches) {
				foreach (var piece in match) {
					piece.RunTask(BlowTask(piece));
				}
			}
		}

		private List<List<Piece>> FindMatches()
		{
			var matches = new List<List<Piece>>();
			Grid<List<Piece>> matchMap = new Grid<List<Piece>>();
			Pass(0);
			Pass(1);
			return matches;

			void Pass(int passIndex)
			{
				int[] boardSize = { BoardConfig.ColumnCount, BoardConfig.RowCount };
				for (int i = 0; i <= boardSize[passIndex]; i++) {
					Piece a = null;
					var matchLength = 1;
					List<Piece> match = new List<Piece>();
					for (int j = -1; j <= boardSize[(passIndex + 1) % 2]; j++) {
						var (x, y) = passIndex == 0 ? (i, j) : (j, i);
						var p = new IntVector2(x, y);
						var b = grid[p];
						if (a?.CanMatch(b) ?? false) {
							matchLength++;
						} else {
							if (matchLength >= 3) {
								var newMatch = match.ToList();
								var intersectedMatches = match
									.Select(i => matchMap[i.GridPosition])
									.Where(i => i != null)
									.Distinct()
									.ToList();
								foreach (var m in intersectedMatches) {
									matches.Remove(m);
								}
								newMatch = newMatch
									.Union(intersectedMatches.SelectMany(i => i))
									.ToList();
								foreach (var piece in newMatch) {
									matchMap[piece.GridPosition] = newMatch;
								}
								matches.Add(newMatch);
							}
							matchLength = 1;
							match.Clear();
						}
						match.Add(b);
						a = b;
					}
				}
			}
		}

		private IEnumerator<object> BlowTask(Piece piece)
		{
			Animation animation = piece.AnimateMatch();
			yield return animation;
			KillPiece(piece);
		}

		private void KillPiece(Piece piece)
		{
			grid[piece.GridPosition] = null;
			pieces.Remove(piece);
			piece.Owner.UnlinkAndDispose();

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

