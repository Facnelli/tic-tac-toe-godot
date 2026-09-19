using System;
using System.Collections.Generic;
using Godot;
using TicTacToeRoguelike.Domain.Boards;

namespace TicTacToeRoguelike.Presentation.Board
{
    public sealed partial class BoardView : GridContainer
    {
        public event Action<BoardCoordinate> CellActivated;

        private readonly Dictionary<BoardCoordinate, BoardCellView> _cells =
            new Dictionary<BoardCoordinate, BoardCellView>();

        private BoardDefinition _definition;

        public void RenderBoard(BoardState board, bool inputEnabled)
        {
            if (board == null) throw new ArgumentNullException(nameof(board));

            if (!ReferenceEquals(_definition, board.Definition))
                Rebuild(board.Definition);

            foreach (BoardCoordinate coordinate in board.Definition.Cells)
            {
                BoardCellView view = _cells[coordinate];
                view.Render(board.GetCell(coordinate), inputEnabled);
            }
        }

        private void Rebuild(BoardDefinition definition)
        {
            foreach (Node child in GetChildren())
                child.QueueFree();

            _cells.Clear();
            _definition = definition;
            Columns = definition.Width;

            foreach (BoardCoordinate coordinate in definition.Cells)
            {
                BoardCellView cell = new BoardCellView();
                cell.Configure(coordinate);
                cell.Activated += OnCellActivated;
                _cells.Add(coordinate, cell);
                AddChild(cell);
            }
        }

        private void OnCellActivated(BoardCoordinate coordinate)
        {
            CellActivated?.Invoke(coordinate);
        }
    }
}
