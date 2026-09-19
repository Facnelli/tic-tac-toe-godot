using TicTacToeRoguelike.Domain.Actions;

namespace TicTacToeRoguelike.Application.Actions
{
    public interface IGameActionHandler
    {
        GameActionType ActionType { get; }
        ActionResult Execute(GameAction action, ActionExecutionContext context);
    }
}
