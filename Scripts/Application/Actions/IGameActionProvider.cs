using System;
using System.Collections.Generic;
using TicTacToeRoguelike.Domain.Actions;
using TicTacToeRoguelike.Domain.Boards;
using TicTacToeRoguelike.Domain.Turns;

namespace TicTacToeRoguelike.Application.Actions
{
    public sealed class GameActionProviderContext
    {
        public BoardState Board { get; }
        public TurnContext TurnContext { get; }
        public GameActionOrigin Origin { get; }

        public GameActionProviderContext(
            BoardState board,
            TurnContext turnContext,
            GameActionOrigin origin)
        {
            Board = board ?? throw new ArgumentNullException(nameof(board));
            TurnContext = turnContext ?? throw new ArgumentNullException(nameof(turnContext));

            if (!TurnContext.IsOpen)
                throw new ArgumentException("O catálogo exige um TurnContext aberto.", nameof(turnContext));

            if (TurnContext.CurrentBoardVersion != Board.Version)
                throw new ArgumentException("TurnContext e BoardState precisam estar sincronizados.");

            if (origin == GameActionOrigin.None)
                throw new ArgumentOutOfRangeException(nameof(origin));

            Origin = origin;
        }
    }

    public interface IGameActionProvider
    {
        IReadOnlyList<GameAction> GetAvailableActions(GameActionProviderContext context);
    }
}
