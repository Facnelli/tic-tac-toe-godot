using System;
using Godot;
using TicTacToeRoguelike.Domain.Boards;

namespace TicTacToeRoguelike.Presentation.Board
{
    public sealed partial class BoardCellView : Button
    {
        private static readonly Color Chalk = new Color(0.78f, 0.78f, 0.72f, 0.68f);
        private static readonly Color ChalkHover = new Color(0.90f, 0.86f, 0.72f, 0.86f);
        private static readonly Color Cyan = new Color(0.10f, 0.83f, 0.86f, 1f);
        private static readonly Color Rose = new Color(1.00f, 0.30f, 0.40f, 1f);
        private static readonly Color Gold = new Color(0.94f, 0.72f, 0.24f, 1f);

        public event Action<BoardCoordinate> Activated;

        public BoardCoordinate Coordinate { get; private set; }

        public BoardCellView()
        {
            CustomMinimumSize = new Vector2(148, 148);
            FocusMode = FocusModeEnum.None;
            Flat = true;
            Pressed += OnPressed;

            AddThemeFontSizeOverride("font_size", 68);
            AddThemeColorOverride("font_hover_color", ChalkHover);
            AddThemeColorOverride("font_pressed_color", ChalkHover);
            AddThemeStyleboxOverride("normal", CreateCellStyle(new Color(0.025f, 0.028f, 0.031f, 0.42f), Chalk));
            AddThemeStyleboxOverride("hover", CreateCellStyle(new Color(0.08f, 0.075f, 0.065f, 0.58f), ChalkHover));
            AddThemeStyleboxOverride("pressed", CreateCellStyle(new Color(0.11f, 0.09f, 0.07f, 0.70f), ChalkHover));
            AddThemeStyleboxOverride("disabled", CreateCellStyle(new Color(0.02f, 0.023f, 0.026f, 0.42f), Chalk));
            AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
        }

        public void Configure(BoardCoordinate coordinate)
        {
            Coordinate = coordinate;
            TooltipText = $"Casa {coordinate.X + 1}, {coordinate.Y + 1}";
        }

        public void Render(CellState cell, bool inputEnabled)
        {
            if (cell == null) throw new ArgumentNullException(nameof(cell));

            bool hasX = cell.HasMark(CellMark.X);
            bool hasO = cell.HasMark(CellMark.O);

            Text = hasX && hasO
                ? "X\nO"
                : hasX
                    ? "X"
                    : hasO
                        ? "O"
                        : "";

            Disabled = !inputEnabled;

            Color markColor = hasX && hasO
                ? Gold
                : hasX
                    ? Cyan
                    : hasO
                        ? Rose
                        : Chalk;

            AddThemeColorOverride("font_color", markColor);
            AddThemeColorOverride("font_disabled_color", markColor);

            if (cell.HasModifier(CellModifierId.Golden))
            {
                TooltipText = $"Casa dourada {Coordinate.X + 1}, {Coordinate.Y + 1} — permite sobreposição";
                AddThemeStyleboxOverride(
                    "normal",
                    CreateCellStyle(new Color(0.17f, 0.12f, 0.035f, 0.36f), new Color(0.66f, 0.45f, 0.18f, 0.72f)));
                AddThemeStyleboxOverride(
                    "disabled",
                    CreateCellStyle(new Color(0.14f, 0.10f, 0.03f, 0.32f), new Color(0.60f, 0.41f, 0.16f, 0.62f)));
            }
            else
            {
                AddThemeStyleboxOverride("normal", CreateCellStyle(new Color(0.025f, 0.028f, 0.031f, 0.42f), Chalk));
                AddThemeStyleboxOverride("disabled", CreateCellStyle(new Color(0.02f, 0.023f, 0.026f, 0.42f), Chalk));
            }
        }

        private static StyleBoxFlat CreateCellStyle(Color background, Color border)
        {
            StyleBoxFlat style = new StyleBoxFlat
            {
                BgColor = background,
                BorderColor = border,
                BorderWidthLeft = 1,
                BorderWidthTop = 1,
                BorderWidthRight = 1,
                BorderWidthBottom = 1
            };

            return style;
        }

        private void OnPressed()
        {
            Activated?.Invoke(Coordinate);
        }
    }
}
