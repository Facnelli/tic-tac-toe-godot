using System;
using System.Collections.Generic;
using Godot;
using TicTacToeRoguelike.Domain.Boards;
using TicTacToeRoguelike.Domain.Sequences;

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
        private static readonly Color PlayerSequence =
            new Color(0.32f, 0.94f, 0.96f, 0.96f);
        private static readonly Color EnemySequence =
            new Color(1.00f, 0.43f, 0.49f, 0.96f);

        private const float MaximumBoardSpan = 444f;
        private const float MinimumCellSize = 22f;
        private const float MaximumCellSize = 148f;
        private const float GridDrawDuration = 0.72f;
        private const float SequenceDrawDuration = 0.18f;

        public event Action<BoardCoordinate> CellActivated;

        private readonly Dictionary<BoardCoordinate, BoardCellView> _cells =
            new Dictionary<BoardCoordinate, BoardCellView>();

        private BoardDefinition _definition;
        private float _cellSize = MaximumCellSize;
        private float _gridProgress = 1f;
        private bool _gridAnimating;

        private SequenceMatch _scoringSequence;
        private float _sequenceProgress = 1f;
        private bool _sequenceAnimating;

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
            bool needsProcess = false;

            if (_gridAnimating)
            {
                _gridProgress = MathF.Min(
                    1f,
                    _gridProgress + (float)(delta / GridDrawDuration));

                if (_gridProgress >= 1f)
                    _gridAnimating = false;
                else
                    needsProcess = true;
            }

            if (_sequenceAnimating)
            {
                _sequenceProgress = MathF.Min(
                    1f,
                    _sequenceProgress + (float)(delta / SequenceDrawDuration));

                if (_sequenceProgress >= 1f)
                    _sequenceAnimating = false;
                else
                    needsProcess = true;
            }

            QueueRedraw();

            if (!needsProcess &&
                !_gridAnimating &&
                !_sequenceAnimating)
            {
                SetProcess(false);
            }
        }

        public override void _Draw()
        {
            if (_definition == null)
                return;

            DrawBoardGrid();

            if (_scoringSequence != null)
                DrawScoringSequence();
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

        public void ShowScoringSequence(SequenceMatch sequence)
        {
            _scoringSequence = sequence ??
                throw new ArgumentNullException(nameof(sequence));

            _sequenceProgress = 0f;
            _sequenceAnimating = true;
            SetProcess(true);
            QueueRedraw();
        }

        public void HideScoringSequence()
        {
            _scoringSequence = null;
            _sequenceProgress = 1f;
            _sequenceAnimating = false;
            QueueRedraw();

            if (!_gridAnimating)
                SetProcess(false);
        }

        private void DrawBoardGrid()
        {
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
                float local = Mathf.Clamp(
                    globalProgress - lineIndex,
                    0f,
                    1f);
                float px = x * _cellSize;

                Vector2 from = x % 2 == 0
                    ? new Vector2(px, boardHeight)
                    : new Vector2(px, 0f);
                Vector2 to = x % 2 == 0
                    ? new Vector2(px, 0f)
                    : new Vector2(px, boardHeight);

                DrawProgressiveChalkLine(
                    from,
                    to,
                    local,
                    ChalkGrid,
                    2.1f);

                lineIndex++;
            }

            for (int y = 1; y < _definition.Height; y++)
            {
                float local = Mathf.Clamp(
                    globalProgress - lineIndex,
                    0f,
                    1f);
                float py = y * _cellSize;

                Vector2 from = y % 2 == 0
                    ? new Vector2(boardWidth, py)
                    : new Vector2(0f, py);
                Vector2 to = y % 2 == 0
                    ? new Vector2(0f, py)
                    : new Vector2(boardWidth, py);

                DrawProgressiveChalkLine(
                    from,
                    to,
                    local,
                    ChalkGrid,
                    2.1f);

                lineIndex++;
            }
        }

        private void DrawScoringSequence()
        {
            if (_scoringSequence.Cells.Count < 2)
                return;

            Vector2 from = GetCellCenter(_scoringSequence.Start);
            Vector2 to = GetCellCenter(_scoringSequence.End);
            Color color = _scoringSequence.Mark == CellMark.X
                ? PlayerSequence
                : EnemySequence;

            float width = MathF.Max(4f, _cellSize * 0.055f);

            DrawProgressiveChalkLine(
                from,
                to,
                _sequenceProgress,
                color,
                width);

            for (int i = 0;
                 i < _scoringSequence.Cells.Count;
                 i++)
            {
                float reveal = Mathf.Clamp(
                    (_sequenceProgress *
                     _scoringSequence.Cells.Count) - i,
                    0f,
                    1f);

                if (reveal <= 0f)
                    continue;

                Vector2 center =
                    GetCellCenter(_scoringSequence.Cells[i]);

                DrawArc(
                    center,
                    MathF.Max(8f, _cellSize * 0.105f),
                    0f,
                    MathF.Tau * reveal,
                    28,
                    new Color(
                        color.R,
                        color.G,
                        color.B,
                        0.68f),
                    MathF.Max(1.5f, width * 0.34f),
                    true);
            }
        }

        private Vector2 GetCellCenter(BoardCoordinate coordinate)
        {
            return new Vector2(
                (coordinate.X + 0.5f) * _cellSize,
                (coordinate.Y + 0.5f) * _cellSize);
        }

        private void Rebuild(BoardDefinition definition)
        {
            foreach (Node child in GetChildren())
                child.QueueFree();

            _cells.Clear();
            _definition = definition ??
                throw new ArgumentNullException(nameof(definition));

            _scoringSequence = null;

            Columns = definition.Width;
            _cellSize = CalculateCellSize(definition);
            CustomMinimumSize = new Vector2(
                definition.Width * _cellSize,
                definition.Height * _cellSize);

            for (int y = 0; y < definition.Height; y++)
            {
                for (int x = 0; x < definition.Width; x++)
                {
                    BoardCoordinate coordinate =
                        new BoardCoordinate(x, y);

                    if (!definition.ContainsCell(coordinate))
                    {
                        Control gap = new Control
                        {
                            CustomMinimumSize =
                                new Vector2(_cellSize, _cellSize),
                            MouseFilter =
                                MouseFilterEnum.Ignore
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

        private static float CalculateCellSize(
            BoardDefinition definition)
        {
            float byWidth =
                MaximumBoardSpan / definition.Width;
            float byHeight =
                MaximumBoardSpan / definition.Height;
            float size = MathF.Min(byWidth, byHeight);

            return Mathf.Clamp(
                size,
                MinimumCellSize,
                MaximumCellSize);
        }

        private void DrawProgressiveChalkLine(
            Vector2 from,
            Vector2 to,
            float progress,
            Color color,
            float width)
        {
            if (progress <= 0f)
                return;

            Vector2 end = from.Lerp(to, progress);
            Color dust = new Color(
                color.R,
                color.G,
                color.B,
                MathF.Min(color.A, 0.24f));

            DrawLine(
                from,
                end,
                dust,
                width + 3.2f,
                true);

            DrawLine(
                from + new Vector2(0.7f, -0.5f),
                end + new Vector2(0.7f, -0.5f),
                color,
                width,
                true);

            DrawLine(
                from + new Vector2(-0.8f, 0.6f),
                end + new Vector2(-0.8f, 0.6f),
                new Color(
                    color.R,
                    color.G,
                    color.B,
                    MathF.Min(color.A, 0.34f)),
                MathF.Max(0.9f, width * 0.38f),
                true);
        }

        private void OnCellActivated(
            BoardCoordinate coordinate)
        {
            CellActivated?.Invoke(coordinate);
        }
    }
}
