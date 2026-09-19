using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using TicTacToeRoguelike.Domain.Actions;
using TicTacToeRoguelike.Domain.Boards;
using TicTacToeRoguelike.Domain.Moves;
using TicTacToeRoguelike.Domain.Scoring;

namespace TicTacToeRoguelike.Application.Actions
{
    public sealed class NormalMoveActionProvider : IGameActionProvider
    {
        private readonly MoveValidator _moveValidator;

        public NormalMoveActionProvider(MoveValidator moveValidator)
        {
            _moveValidator = moveValidator ?? throw new ArgumentNullException(nameof(moveValidator));
        }

        public IReadOnlyList<GameAction> GetAvailableActions(GameActionProviderContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));

            CellMark mark = context.TurnContext.Actor == ScoreActor.Player
                ? CellMark.X
                : context.TurnContext.Actor == ScoreActor.Enemy
                    ? CellMark.O
                    : throw new InvalidOperationException("Somente Player e Enemy possuem jogadas normais.");

            long version = context.Board.Version;
            IReadOnlyList<BoardCoordinate> coordinates =
                _moveValidator.GetLegalNormalMoves(context.Board, mark);

            if (context.Board.Version != version)
                throw new InvalidOperationException("MoveValidator alterou o tabuleiro durante uma consulta.");

            List<GameAction> actions = new List<GameAction>(coordinates.Count);
            for (int i = 0; i < coordinates.Count; i++)
            {
                actions.Add(new PlaceMarkAction(
                    context.TurnContext.Actor,
                    context.Origin,
                    context.TurnContext.TurnId,
                    version,
                    coordinates[i],
                    mark));
            }

            return new ReadOnlyCollection<GameAction>(actions);
        }
    }
}
