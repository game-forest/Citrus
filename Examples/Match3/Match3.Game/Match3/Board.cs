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

	}

	public static class BoardConfig
	{

	}

	public class Board
	{
		private readonly Widget boardContainer;
		private readonly Frame pieceContainer;
		private readonly Widget pieceTemplate;

		public Board(Widget boardContainer)
		{
			this.boardContainer = boardContainer;
			pieceTemplate = Node.Load<Widget>("Game/Match3/MultiMarble");
			pieceContainer = new Frame {

			};
			this.boardContainer.Nodes.Insert(0, pieceContainer);
			this.boardContainer.Tasks.Add(this.Update);
		}

		private IEnumerator<object> Update()
		{
			yield break;
		}

		private Piece CreatePiece()
		{
			var pieceWidget = pieceTemplate.Clone<Widget>();
			pieceContainer.AddNode(pieceWidget);
			var piece = new Piece(pieceWidget);
			return piece;
		}
	}
}

