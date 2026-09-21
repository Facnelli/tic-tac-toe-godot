using System.Collections.Generic;
using System.Linq;
using Godot;
using TicTacToeRoguelike.Domain.Actions;
using TicTacToeRoguelike.Domain.Boards;

namespace TicTacToeRoguelike.Presentation.Runes
{
    // Selects a category of already legal board actions; contains no effect rules.
    public sealed partial class BoardActionSelector : HBoxContainer
    {
        private GameActionType _selected = GameActionType.PlaceMark;
        private readonly Button _place = new Button { Text = "Símbolo", ToggleMode = true };
        private readonly Button _clear = new Button { Text = "Limpar casa", ToggleMode = true,
            TooltipText = "Usa uma ação. Uma utilização por runa a cada rodada. Conserva a casa dourada." };
        public BoardActionSelector()
        {
            AddChild(_place); AddChild(_clear);
            _place.Pressed += () => Select(GameActionType.PlaceMark);
            _clear.Pressed += () => Select(GameActionType.ClearCell);
            Select(GameActionType.PlaceMark);
        }
        private void Select(GameActionType type)
        { _selected = type; _place.ButtonPressed = type == GameActionType.PlaceMark; _clear.ButtonPressed = type == GameActionType.ClearCell; }
        public void Refresh(IReadOnlyList<GameAction> actions)
        {
            _place.Disabled = !actions.Any(a => a.ActionType == GameActionType.PlaceMark);
            _clear.Disabled = !actions.Any(a => a.ActionType == GameActionType.ClearCell);
            if (!actions.Any(a => a.ActionType == _selected)) Select(_place.Disabled ? GameActionType.ClearCell : GameActionType.PlaceMark);
        }
        public GameAction SelectTarget(IReadOnlyList<GameAction> actions, BoardCoordinate target) =>
            actions.FirstOrDefault(a => a.ActionType == _selected && a is GameAction<BoardCoordinate> cell && cell.Target == target);
        public void ResetSelection() => Select(GameActionType.PlaceMark);
    }
}
