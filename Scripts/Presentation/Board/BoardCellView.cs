using System;
using Godot;
using TicTacToeRoguelike.Domain.Boards;

namespace TicTacToeRoguelike.Presentation.Board
{
    public sealed partial class BoardCellView : Button
    {
        public event Action<BoardCoordinate> Activated;

        public BoardCoordinate Coordinate { get; private set; }

        public BoardCellView()
        {
            CustomMinimumSize = new Vector2(132, 132);
            FocusMode = FocusModeEnum.None;
            Pressed += OnPressed;
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

            if (cell.HasModifier(CellModifierId.Golden))
            {
                TooltipText = $"Casa dourada {Coordinate.X + 1}, {Coordinate.Y + 1} — permite sobreposição";
                AddThemeColorOverride("font_color", new Color(0.95f, 0.72f, 0.18f));
                AddThemeColorOverride("font_disabled_color", new Color(0.95f, 0.72f, 0.18f));
            }
            else
            {
                RemoveThemeColorOverride("font_color");
                RemoveThemeColorOverride("font_disabled_color");
            }

            AddThemeFontSizeOverride("font_size", 42);
        }

        private void OnPressed()
        {
            Activated?.Invoke(Coordinate);
        }
    }
}
