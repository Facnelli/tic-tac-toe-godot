using System;
using Godot;
using TicTacToeRoguelike.Domain.Boards;

namespace TicTacToeRoguelike.Presentation.Board
{
    /// <summary>
    /// Casa visual do tabuleiro. Os símbolos são desenhados como traços de giz
    /// para permitir sobreposição real de X e O e animação progressiva.
    /// </summary>
    public sealed partial class BoardCellView : Button
    {
        private static readonly Color Chalk = new Color(0.78f, 0.78f, 0.72f, 0.68f);
        private static readonly Color ChalkHover = new Color(0.90f, 0.86f, 0.72f, 0.86f);
        private static readonly Color Cyan = new Color(0.10f, 0.83f, 0.86f, 1f);
        private static readonly Color Rose = new Color(1.00f, 0.30f, 0.40f, 1f);

        private const float MarkDrawDuration = 0.30f;

        private CellMark _marks;
        private float _xProgress;
        private float _oProgress;
        private bool _animateX;
        private bool _animateO;
        private float _cellSize = 148f;

        public event Action<BoardCoordinate> Activated;

        public BoardCoordinate Coordinate { get; private set; }

        public BoardCellView()
        {
            FocusMode = FocusModeEnum.None;
            Flat = true;
            Text = "";
            Pressed += OnPressed;

            AddThemeColorOverride("font_color", Colors.Transparent);
            AddThemeColorOverride("font_disabled_color", Colors.Transparent);
            AddThemeStyleboxOverride(
                "normal",
                CreateCellStyle(new Color(0.025f, 0.028f, 0.031f, 0.20f), Colors.Transparent));
            AddThemeStyleboxOverride(
                "hover",
                CreateCellStyle(new Color(0.09f, 0.075f, 0.055f, 0.45f), ChalkHover));
            AddThemeStyleboxOverride(
                "pressed",
                CreateCellStyle(new Color(0.12f, 0.09f, 0.055f, 0.56f), ChalkHover));
            AddThemeStyleboxOverride(
                "disabled",
                CreateCellStyle(new Color(0.018f, 0.021f, 0.024f, 0.18f), Colors.Transparent));
            AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
        }

        public override void _Ready()
        {
            SetProcess(false);
        }

        public override void _Process(double delta)
        {
            bool changed = false;
            float amount = (float)(delta / MarkDrawDuration);

            if (_animateX)
            {
                _xProgress = MathF.Min(1f, _xProgress + amount);
                _animateX = _xProgress < 1f;
                changed = true;
            }

            if (_animateO)
            {
                _oProgress = MathF.Min(1f, _oProgress + amount);
                _animateO = _oProgress < 1f;
                changed = true;
            }

            if (changed)
                QueueRedraw();

            if (!_animateX && !_animateO)
                SetProcess(false);
        }

        public override void _Draw()
        {
            bool hasX = (_marks & CellMark.X) == CellMark.X;
            bool hasO = (_marks & CellMark.O) == CellMark.O;

            if (!hasX && !hasO)
                return;

            Vector2 center = Size * 0.5f;
            float radius = MathF.Min(Size.X, Size.Y) * 0.285f;
            float half = radius * 0.78f;
            float width = MathF.Max(3f, _cellSize * 0.045f);

            if (hasX)
                DrawChalkX(center, half, width, Cyan, _xProgress);

            if (hasO)
                DrawChalkO(center, radius, width, Rose, _oProgress);
        }

        public void Configure(BoardCoordinate coordinate, float cellSize)
        {
            Coordinate = coordinate;
            _cellSize = cellSize;
            CustomMinimumSize = new Vector2(cellSize, cellSize);
            TooltipText = $"Casa {coordinate.X + 1}, {coordinate.Y + 1}";
        }

        public void Render(CellState cell, bool inputEnabled)
        {
            if (cell == null)
                throw new ArgumentNullException(nameof(cell));

            CellMark previous = _marks;
            _marks = cell.Marks;

            bool hadX = (previous & CellMark.X) == CellMark.X;
            bool hadO = (previous & CellMark.O) == CellMark.O;
            bool hasX = (_marks & CellMark.X) == CellMark.X;
            bool hasO = (_marks & CellMark.O) == CellMark.O;

            if (hasX && !hadX)
            {
                _xProgress = 0f;
                _animateX = true;
            }
            else if (!hasX)
            {
                _xProgress = 0f;
                _animateX = false;
            }
            else if (!_animateX)
            {
                _xProgress = 1f;
            }

            if (hasO && !hadO)
            {
                _oProgress = 0f;
                _animateO = true;
            }
            else if (!hasO)
            {
                _oProgress = 0f;
                _animateO = false;
            }
            else if (!_animateO)
            {
                _oProgress = 1f;
            }

            Disabled = !inputEnabled;

            if (cell.HasModifier(CellModifierId.Golden))
            {
                TooltipText =
                    $"Casa dourada {Coordinate.X + 1}, {Coordinate.Y + 1} — aceita X e O sobrepostos";

                AddThemeStyleboxOverride(
                    "normal",
                    CreateCellStyle(
                        new Color(0.19f, 0.13f, 0.035f, 0.34f),
                        new Color(0.70f, 0.48f, 0.18f, 0.62f)));

                AddThemeStyleboxOverride(
                    "disabled",
                    CreateCellStyle(
                        new Color(0.16f, 0.11f, 0.03f, 0.29f),
                        new Color(0.62f, 0.42f, 0.15f, 0.52f)));
            }
            else
            {
                AddThemeStyleboxOverride(
                    "normal",
                    CreateCellStyle(
                        new Color(0.025f, 0.028f, 0.031f, 0.20f),
                        Colors.Transparent));

                AddThemeStyleboxOverride(
                    "disabled",
                    CreateCellStyle(
                        new Color(0.018f, 0.021f, 0.024f, 0.18f),
                        Colors.Transparent));
            }

            if (_animateX || _animateO)
                SetProcess(true);

            QueueRedraw();
        }

        private static StyleBoxFlat CreateCellStyle(Color background, Color border)
        {
            return new StyleBoxFlat
            {
                BgColor = background,
                BorderColor = border,
                BorderWidthLeft = border.A > 0f ? 1 : 0,
                BorderWidthTop = border.A > 0f ? 1 : 0,
                BorderWidthRight = border.A > 0f ? 1 : 0,
                BorderWidthBottom = border.A > 0f ? 1 : 0,
                CornerRadiusTopLeft = 2,
                CornerRadiusTopRight = 2,
                CornerRadiusBottomLeft = 2,
                CornerRadiusBottomRight = 2
            };
        }

        private void DrawChalkX(
            Vector2 center,
            float half,
            float width,
            Color color,
            float progress)
        {
            float first = Mathf.Clamp(progress * 2f, 0f, 1f);
            float second = Mathf.Clamp(progress * 2f - 1f, 0f, 1f);

            Vector2 a = center + new Vector2(-half, -half);
            Vector2 b = center + new Vector2(half, half);
            Vector2 c = center + new Vector2(half, -half);
            Vector2 d = center + new Vector2(-half, half);

            if (first > 0f)
                DrawChalkSegment(a, a.Lerp(b, first), width, color);

            if (second > 0f)
                DrawChalkSegment(c, c.Lerp(d, second), width, color);
        }

        private void DrawChalkO(
            Vector2 center,
            float radius,
            float width,
            Color color,
            float progress)
        {
            if (progress <= 0f)
                return;

            float start = -MathF.PI * 0.5f;
            float end = start + MathF.Tau * Mathf.Clamp(progress, 0f, 1f);
            Color dust = WithAlpha(color, 0.23f);

            DrawArc(center, radius, start, end, 72, dust, width + 3f, true);
            DrawArc(center + new Vector2(0.7f, -0.5f), radius, start, end, 72, color, width, true);
            DrawArc(
                center + new Vector2(-0.9f, 0.8f),
                radius * 0.985f,
                start,
                end,
                72,
                WithAlpha(color, 0.34f),
                MathF.Max(1f, width * 0.32f),
                true);
        }

        private void DrawChalkSegment(
            Vector2 from,
            Vector2 to,
            float width,
            Color color)
        {
            DrawLine(from, to, WithAlpha(color, 0.22f), width + 3f, true);
            DrawLine(
                from + new Vector2(0.8f, -0.6f),
                to + new Vector2(0.8f, -0.6f),
                color,
                width,
                true);
            DrawLine(
                from + new Vector2(-0.9f, 0.7f),
                to + new Vector2(-0.9f, 0.7f),
                WithAlpha(color, 0.33f),
                MathF.Max(1f, width * 0.32f),
                true);
        }

        private static Color WithAlpha(Color color, float alpha)
        {
            return new Color(color.R, color.G, color.B, alpha);
        }

        private void OnPressed()
        {
            Activated?.Invoke(Coordinate);
        }
    }
}
