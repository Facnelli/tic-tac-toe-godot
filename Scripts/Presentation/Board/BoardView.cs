using System;
using System.Collections.Generic;
using Godot;
using TicTacToeRoguelike.Domain.Boards;

namespace TicTacToeRoguelike.Presentation.Board
{
    /// <summary>
    /// Grade visual adaptativa. O tamanho das casas é calculado a partir das
    /// dimensões lógicas do tabuleiro, sem assumir 3x3.
    /// </summary>
    public sealed partial class BoardView : GridContainer
    {
        private static readonly Color ChalkGrid =
            new Color(0.77f, 0.74f, 0.66f, 0.70f);

        private const float MaximumBoardSpan = 444f;
        private const float MinimumCellSize = 22f;
        private const float MaximumCellSize = 148f;
        private const float GridDrawDuration = 0.72f;

        public event Action<BoardCoordinate> CellActivated;

        private readonly Dictionary<BoardCoordinate, BoardCellView> _cells =
            new Dictionary<BoardCoordinate, BoardCellView>();

        private BoardDefinition _definition;
        private float _cellSize = MaximumCellSize;
        private float _gridProgress = 1f;
        private bool _gridAnimating;

        public BoardView()
        {
            AddThemeConstantOverride("h_separation", 0);
            AddThemeConstantOverride("v_separation", 0);
            MouseFilter = MouseFilterEnum.Pass;
        }

        public override void _Ready()
        {
            SetProcess(false);
            Resized += QueueRedraw;
        }

        public override void _Process(double delta)
        {
            if (!_gridAnimating)
            {
                SetProcess(false);
                return;
            }

            _gridProgress = MathF.Min(
                1f,
                _gridProgress + (float)(delta / GridDrawDuration));

            QueueRedraw();

            if (_gridProgress >= 1f)
            {
                _gridAnimating = false;
                SetProcess(false);
            }
        }

        public override void _Draw()
        {
            if (_definition == null)
                return;

            int verticalLines = Math.Max(0, _definition.Width - 1);
            int horizontalLines = Math.Max(0, _definition.Height - 1);
            int totalLines = verticalLines + horizontalLines;

            if (totalLines == 0)
                return;

            float boardWidth = _definition.Width * _cellSize;
            float boardHeight = _definition.Height * _cellSize;
            float globalProgress = _gridProgress * totalLines;
            int lineIndex = 0;

            for (int x = 1; x < _definition.Width; x++)
            {
                float local = Mathf.Clamp(globalProgress - lineIndex, 0f, 1f);
                float px = x * _cellSize;

                Vector2 from = x % 2 == 0
                    ? new Vector2(px, boardHeight)
                    : new Vector2(px, 0f);
                Vector2 to = x % 2 == 0
                    ? new Vector2(px, 0f)
                    : new Vector2(px, boardHeight);

                DrawProgressiveChalkLine(from, to, local);
                lineIndex++;
            }

            for (int y = 1; y < _definition.Height; y++)
            {
                float local = Mathf.Clamp(globalProgress - lineIndex, 0f, 1f);
                float py = y * _cellSize;

                Vector2 from = y % 2 == 0
                    ? new Vector2(boardWidth, py)
                    : new Vector2(0f, py);
                Vector2 to = y % 2 == 0
                    ? new Vector2(0f, py)
                    : new Vector2(boardWidth, py);

                DrawProgressiveChalkLine(from, to, local);
                lineIndex++;
            }
        }

        public void RenderBoard(BoardState board, bool inputEnabled)
        {
            if (board == null)
                throw new ArgumentNullException(nameof(board));

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
            _definition = definition ??
                throw new ArgumentNullException(nameof(definition));

            Columns = definition.Width;
            _cellSize = CalculateCellSize(definition);
            CustomMinimumSize = new Vector2(
                definition.Width * _cellSize,
                definition.Height * _cellSize);

            for (int y = 0; y < definition.Height; y++)
            {
                for (int x = 0; x < definition.Width; x++)
                {
                    BoardCoordinate coordinate = new BoardCoordinate(x, y);

                    if (!definition.ContainsCell(coordinate))
                    {
                        Control gap = new Control
                        {
                            CustomMinimumSize = new Vector2(_cellSize, _cellSize),
                            MouseFilter = MouseFilterEnum.Ignore
                        };
                        AddChild(gap);
                        continue;
                    }

                    BoardCellView cell = new BoardCellView();
                    cell.Configure(coordinate, _cellSize);
                    cell.Activated += OnCellActivated;
                    _cells.Add(coordinate, cell);
                    AddChild(cell);
                }
            }

            _gridProgress = 0f;
            _gridAnimating = true;
            SetProcess(true);
            QueueRedraw();
        }

        private static float CalculateCellSize(BoardDefinition definition)
        {
            float byWidth = MaximumBoardSpan / definition.Width;
            float byHeight = MaximumBoardSpan / definition.Height;
            float size = MathF.Min(byWidth, byHeight);

            return Mathf.Clamp(
                size,
                MinimumCellSize,
                MaximumCellSize);
        }

        private void DrawProgressiveChalkLine(
            Vector2 from,
            Vector2 to,
            float progress)
        {
            if (progress <= 0f)
                return;

            Vector2 end = from.Lerp(to, progress);
            Color dust = new Color(
                ChalkGrid.R,
                ChalkGrid.G,
                ChalkGrid.B,
                0.18f);

            DrawLine(from, end, dust, 5f, true);
            DrawLine(
                from + new Vector2(0.7f, -0.5f),
                end + new Vector2(0.7f, -0.5f),
                ChalkGrid,
                2.1f,
                true);
            DrawLine(
                from + new Vector2(-0.8f, 0.6f),
                end + new Vector2(-0.8f, 0.6f),
                new Color(
                    ChalkGrid.R,
                    ChalkGrid.G,
                    ChalkGrid.B,
                    0.28f),
                0.9f,
                true);
        }

        private void OnCellActivated(BoardCoordinate coordinate)
        {
            CellActivated?.Invoke(coordinate);
        }
    }
}
