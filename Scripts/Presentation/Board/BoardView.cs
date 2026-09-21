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
        private const float GridDrawDuration = 2.16f;
        private const float SequenceDrawDuration = 0.18f;
        private const float RoundEraseDuration = 1.25f;
        private const double ReactionFlashDuration = 0.30d;

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

        private Tween _reactionTween;
        private float _eraseProgress;
        private bool _eraseAnimating;
        private Action _eraseCompleted;

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
                    _gridProgress +
                    (float)(delta / GridDrawDuration));

                if (_gridProgress >= 1f)
                    _gridAnimating = false;
                else
                    needsProcess = true;
            }

            if (_sequenceAnimating)
            {
                _sequenceProgress = MathF.Min(
                    1f,
                    _sequenceProgress +
                    (float)(delta / SequenceDrawDuration));

                if (_sequenceProgress >= 1f)
                    _sequenceAnimating = false;
                else
                    needsProcess = true;
            }

            if (_eraseAnimating)
            {
                _eraseProgress = MathF.Min(
                    1f,
                    _eraseProgress +
                    (float)(delta / RoundEraseDuration));

                UpdateCellEraseAmounts();
                needsProcess = _eraseProgress < 1f;

                if (_eraseProgress >= 1f)
                {
                    _eraseAnimating = false;

                    Action completed =
                        _eraseCompleted;
                    _eraseCompleted = null;

                    completed?.Invoke();
                }
            }

            QueueRedraw();

            if (!needsProcess &&
                !_gridAnimating &&
                !_sequenceAnimating &&
                !_eraseAnimating)
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

            if (_eraseProgress > 0f)
                DrawBoardEraseMask();
        }

        public void RenderBoard(
            BoardState board,
            bool inputEnabled)
        {
            if (board == null)
                throw new ArgumentNullException(nameof(board));

            if (!ReferenceEquals(
                    _definition,
                    board.Definition))
            {
                Rebuild(board.Definition);
            }

            foreach (BoardCoordinate coordinate
                     in board.Definition.Cells)
            {
                BoardCellView view = _cells[coordinate];
                view.Render(
                    board.GetCell(coordinate),
                    inputEnabled);
            }
        }

        public void ShowScoringSequence(
            SequenceMatch sequence)
        {
            _scoringSequence = sequence ??
                throw new ArgumentNullException(
                    nameof(sequence));

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

        public void CancelTransientAnimations()
        {
            _reactionTween?.Kill();
            _reactionTween = null;
            _eraseAnimating = false;
            _eraseCompleted = null;
            _sequenceAnimating = false;
            _scoringSequence = null;
            SetProcess(false);
        }

        public void PlayRoundErase(
            Action completed)
        {
            _reactionTween?.Kill();
            _reactionTween = null;

            HideScoringSequence();
            ClearReactionHighlights();

            _eraseProgress = 0f;
            _eraseAnimating = true;
            _eraseCompleted = completed;

            foreach (BoardCellView cell
                     in _cells.Values)
            {
                cell.Disabled = true;
                cell.ResetErase();
            }

            UpdateCellEraseAmounts();
            SetProcess(true);
            QueueRedraw();
        }

        /// <summary>
        /// Mostra rapidamente todas as sequências atuais e depois deixa acesas
        /// somente as sequências que representam a vantagem líquida da reação.
        /// Assim, sequências equivalentes dos dois lados visualmente se cancelam.
        /// </summary>
        public void ShowReactionTransition(
            IReadOnlyList<SequenceMatch> playerSequences,
            IReadOnlyList<SequenceMatch> enemySequences,
            bool animateTransition)
        {
            if (playerSequences == null)
            {
                throw new ArgumentNullException(
                    nameof(playerSequences));
            }

            if (enemySequences == null)
            {
                throw new ArgumentNullException(
                    nameof(enemySequences));
            }

            _reactionTween?.Kill();
            _reactionTween = null;

            List<SequenceMatch> playerCopy =
                new List<SequenceMatch>(playerSequences);
            List<SequenceMatch> enemyCopy =
                new List<SequenceMatch>(enemySequences);

            BuildSurplusSequences(
                playerCopy,
                enemyCopy,
                out List<SequenceMatch> playerSurplus,
                out List<SequenceMatch> enemySurplus);

            if (!animateTransition)
            {
                ApplyVictoryGlow(
                    playerSurplus,
                    enemySurplus);
                return;
            }

            // Primeiro acende tudo que existe depois da nova jogada.
            ApplyVictoryGlow(
                playerCopy,
                enemyCopy);

            // Em seguida, as sequências equivalentes se anulam e sobra apenas
            // a vantagem que ainda exige reação do outro lado.
            _reactionTween = CreateTween();
            _reactionTween.TweenInterval(
                ReactionFlashDuration);
            _reactionTween.TweenCallback(
                Callable.From(
                    () => ApplyVictoryGlow(
                        playerSurplus,
                        enemySurplus)));
        }

        public void ClearReactionHighlights()
        {
            _reactionTween?.Kill();
            _reactionTween = null;

            foreach (BoardCellView cell
                     in _cells.Values)
            {
                cell.SetVictoryGlow(false, false);
            }
        }

        private static void BuildSurplusSequences(
            IReadOnlyList<SequenceMatch> playerSequences,
            IReadOnlyList<SequenceMatch> enemySequences,
            out List<SequenceMatch> playerSurplus,
            out List<SequenceMatch> enemySurplus)
        {
            playerSurplus = new List<SequenceMatch>();
            enemySurplus = new List<SequenceMatch>();

            int cancelledCount = Math.Min(
                playerSequences.Count,
                enemySequences.Count);

            if (playerSequences.Count >
                enemySequences.Count)
            {
                for (int i = cancelledCount;
                     i < playerSequences.Count;
                     i++)
                {
                    playerSurplus.Add(
                        playerSequences[i]);
                }

                return;
            }

            if (enemySequences.Count >
                playerSequences.Count)
            {
                for (int i = cancelledCount;
                     i < enemySequences.Count;
                     i++)
                {
                    enemySurplus.Add(
                        enemySequences[i]);
                }
            }
        }

        private void ApplyVictoryGlow(
            IReadOnlyList<SequenceMatch> playerSequences,
            IReadOnlyList<SequenceMatch> enemySequences)
        {
            HashSet<BoardCoordinate> playerCells =
                CollectSequenceCells(playerSequences);

            HashSet<BoardCoordinate> enemyCells =
                CollectSequenceCells(enemySequences);

            foreach (KeyValuePair<BoardCoordinate, BoardCellView> pair
                     in _cells)
            {
                pair.Value.SetVictoryGlow(
                    playerCells.Contains(pair.Key),
                    enemyCells.Contains(pair.Key));
            }
        }

        private static HashSet<BoardCoordinate>
            CollectSequenceCells(
                IReadOnlyList<SequenceMatch> sequences)
        {
            HashSet<BoardCoordinate> cells =
                new HashSet<BoardCoordinate>();

            for (int i = 0;
                 i < sequences.Count;
                 i++)
            {
                SequenceMatch sequence = sequences[i];

                for (int j = 0;
                     j < sequence.Cells.Count;
                     j++)
                {
                    cells.Add(sequence.Cells[j]);
                }
            }

            return cells;
        }

        private void UpdateCellEraseAmounts()
        {
            if (_definition == null)
                return;

            float boardWidth =
                _definition.Width * _cellSize;

            float wipeX =
                _eraseProgress *
                (boardWidth + _cellSize * 0.32f);

            foreach (KeyValuePair<BoardCoordinate, BoardCellView> pair
                     in _cells)
            {
                BoardCoordinate coordinate =
                    pair.Key;

                float rowPhase =
                    (coordinate.Y * 2.41f) +
                    (coordinate.X * 0.37f);

                float jitter =
                    MathF.Sin(rowPhase) *
                    _cellSize * 0.16f;

                float local =
                    (wipeX +
                     jitter -
                     coordinate.X * _cellSize) /
                    _cellSize;

                pair.Value.SetEraseAmount(
                    Mathf.Clamp(local, 0f, 1f));
            }
        }

        private void DrawBoardEraseMask()
        {
            if (_definition == null)
                return;

            float boardWidth =
                _definition.Width * _cellSize;
            float boardHeight =
                _definition.Height * _cellSize;

            const int bands = 14;
            float bandHeight =
                boardHeight / bands;

            float baseEdge =
                _eraseProgress *
                (boardWidth + _cellSize * 0.32f);

            Color erased =
                new Color(
                    0.016f,
                    0.019f,
                    0.022f,
                    0.985f);

            for (int band = 0;
                 band < bands;
                 band++)
            {
                float phase =
                    band * 1.87f;

                float edge =
                    baseEdge +
                    MathF.Sin(phase) *
                    _cellSize * 0.14f +
                    MathF.Sin(phase * 0.43f) *
                    _cellSize * 0.06f;

                edge =
                    Mathf.Clamp(
                        edge,
                        0f,
                        boardWidth);

                float y =
                    band * bandHeight;

                DrawRect(
                    new Rect2(
                        new Vector2(0f, y),
                        new Vector2(
                            edge,
                            bandHeight + 2f)),
                    erased,
                    true);

                if (edge > 0f &&
                    edge < boardWidth)
                {
                    float residueWidth =
                        MathF.Max(
                            4f,
                            _cellSize * 0.055f);

                    DrawLine(
                        new Vector2(
                            edge - residueWidth,
                            y),
                        new Vector2(
                            edge,
                            y + bandHeight),
                        new Color(
                            0.72f,
                            0.69f,
                            0.61f,
                            0.12f),
                        residueWidth,
                        true);
                }
            }

            if (_eraseProgress > 0.02f &&
                _eraseProgress < 0.98f)
            {
                float eraserX =
                    Mathf.Clamp(
                        baseEdge,
                        18f,
                        boardWidth - 18f);

                float eraserWidth =
                    MathF.Max(
                        18f,
                        _cellSize * 0.16f);

                DrawRect(
                    new Rect2(
                        new Vector2(
                            eraserX - eraserWidth * 0.72f,
                            boardHeight * 0.08f),
                        new Vector2(
                            eraserWidth,
                            boardHeight * 0.84f)),
                    new Color(
                        0.24f,
                        0.22f,
                        0.18f,
                        0.22f),
                    true);
            }
        }

        private void DrawBoardGrid()
        {
            int verticalLines =
                Math.Max(0, _definition.Width - 1);
            int horizontalLines =
                Math.Max(0, _definition.Height - 1);
            int totalLines =
                verticalLines + horizontalLines;

            if (totalLines == 0)
                return;

            float boardWidth =
                _definition.Width * _cellSize;
            float boardHeight =
                _definition.Height * _cellSize;
            float globalProgress =
                _gridProgress * totalLines;
            int lineIndex = 0;

            for (int x = 1;
                 x < _definition.Width;
                 x++)
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

            for (int y = 1;
                 y < _definition.Height;
                 y++)
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

            Vector2 from =
                GetCellCenter(_scoringSequence.Start);
            Vector2 to =
                GetCellCenter(_scoringSequence.End);

            Color color =
                _scoringSequence.Mark == CellMark.X
                    ? PlayerSequence
                    : EnemySequence;

            float width =
                MathF.Max(
                    4f,
                    _cellSize * 0.055f);

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
                    GetCellCenter(
                        _scoringSequence.Cells[i]);

                DrawArc(
                    center,
                    MathF.Max(
                        8f,
                        _cellSize * 0.105f),
                    0f,
                    MathF.Tau * reveal,
                    28,
                    new Color(
                        color.R,
                        color.G,
                        color.B,
                        0.68f),
                    MathF.Max(
                        1.5f,
                        width * 0.34f),
                    true);
            }
        }

        private Vector2 GetCellCenter(
            BoardCoordinate coordinate)
        {
            return new Vector2(
                (coordinate.X + 0.5f) * _cellSize,
                (coordinate.Y + 0.5f) * _cellSize);
        }

        private void Rebuild(
            BoardDefinition definition)
        {
            _reactionTween?.Kill();
            _reactionTween = null;

            foreach (Node child in GetChildren())
                child.QueueFree();

            _cells.Clear();

            _definition = definition ??
                throw new ArgumentNullException(
                    nameof(definition));

            _scoringSequence = null;
            _eraseAnimating = false;
            _eraseProgress = 0f;
            _eraseCompleted = null;

            Columns = definition.Width;
            _cellSize = CalculateCellSize(definition);
            CustomMinimumSize = new Vector2(
                definition.Width * _cellSize,
                definition.Height * _cellSize);

            for (int y = 0;
                 y < definition.Height;
                 y++)
            {
                for (int x = 0;
                     x < definition.Width;
                     x++)
                {
                    BoardCoordinate coordinate =
                        new BoardCoordinate(x, y);

                    if (!definition.ContainsCell(
                            coordinate))
                    {
                        Control gap = new Control
                        {
                            CustomMinimumSize =
                                new Vector2(
                                    _cellSize,
                                    _cellSize),
                            MouseFilter =
                                MouseFilterEnum.Ignore
                        };

                        AddChild(gap);
                        continue;
                    }

                    BoardCellView cell =
                        new BoardCellView();

                    cell.Configure(
                        coordinate,
                        _cellSize);

                    cell.Activated +=
                        OnCellActivated;

                    _cells.Add(
                        coordinate,
                        cell);

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
            float size =
                MathF.Min(byWidth, byHeight);

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

            Vector2 end =
                from.Lerp(to, progress);

            Vector2 direction = end - from;
            float length = direction.Length();

            if (length <= 0.01f)
                return;

            Vector2 normalized = direction / length;
            Vector2 normal =
                new Vector2(-normalized.Y, normalized.X);

            Color dust = new Color(
                color.R,
                color.G,
                color.B,
                MathF.Min(color.A, 0.18f));

            // Base fosca e irregular, como pó de giz depositado na pedra.
            DrawLine(
                from,
                end,
                dust,
                width + 4.5f,
                true);

            DrawLine(
                from + normal * 0.65f,
                end + normal * 0.65f,
                new Color(
                    color.R,
                    color.G,
                    color.B,
                    MathF.Min(color.A, 0.82f)),
                width,
                true);

            DrawLine(
                from - normal * 0.95f,
                end - normal * 0.95f,
                new Color(
                    color.R,
                    color.G,
                    color.B,
                    MathF.Min(color.A, 0.28f)),
                MathF.Max(0.8f, width * 0.32f),
                true);

            DrawChalkGrain(
                from,
                normalized,
                normal,
                length,
                color,
                width);
        }

        private void DrawChalkGrain(
            Vector2 from,
            Vector2 direction,
            Vector2 normal,
            float length,
            Color color,
            float width)
        {
            int grainCount =
                Math.Max(6, (int)(length / 10f));

            for (int i = 0;
                 i < grainCount;
                 i++)
            {
                float t =
                    (i + 0.35f) / grainCount;

                float phase =
                    (i * 12.9898f) +
                    (from.X * 0.071f) +
                    (from.Y * 0.113f);

                float jitter =
                    MathF.Sin(phase) *
                    MathF.Max(0.7f, width * 0.72f);

                float along =
                    MathF.Sin(phase * 0.47f) * 1.8f;

                Vector2 point =
                    from +
                    direction * (length * t + along) +
                    normal * jitter;

                float radius =
                    0.35f +
                    MathF.Abs(MathF.Sin(phase * 1.73f)) *
                    MathF.Max(0.35f, width * 0.18f);

                float alpha =
                    0.10f +
                    MathF.Abs(MathF.Sin(phase * 0.83f)) *
                    0.22f;

                DrawCircle(
                    point,
                    radius,
                    new Color(
                        color.R,
                        color.G,
                        color.B,
                        MathF.Min(color.A, alpha)));
            }
        }

        private void OnCellActivated(
            BoardCoordinate coordinate)
        {
            CellActivated?.Invoke(coordinate);
        }
    }
}
