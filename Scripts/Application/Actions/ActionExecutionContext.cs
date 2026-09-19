using System;
using TicTacToeRoguelike.Domain.Boards;
using TicTacToeRoguelike.Domain.Turns;

namespace TicTacToeRoguelike.Application.Actions
{
    public sealed class ActionExecutionContext
    {
        public BoardState Board { get; }
        public TurnContext TurnContext { get; }
        public bool EncounterAcceptsActions { get; }

        public ActionExecutionContext(
            BoardState board,
            TurnContext turnContext,
            bool encounterAcceptsActions)
        {
            Board = board ?? throw new ArgumentNullException(nameof(board));
            TurnContext = turnContext;
            EncounterAcceptsActions = encounterAcceptsActions;
        }
    }
}
