using System;
using TicTacToeRoguelike.Domain.Actions;
using TicTacToeRoguelike.Domain.Boards;
using TicTacToeRoguelike.Domain.Moves;
using TicTacToeRoguelike.Domain.Scoring;

namespace TicTacToeRoguelike.Application.Actions
{
    public sealed class PlaceMarkActionHandler : IGameActionHandler
    {
        private readonly MoveService _moveService;

        public GameActionType ActionType => GameActionType.PlaceMark;

        public PlaceMarkActionHandler(MoveService moveService)
        {
            _moveService = moveService ?? throw new ArgumentNullException(nameof(moveService));
        }

        public ActionResult Execute(GameAction action, ActionExecutionContext context)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            if (context == null) throw new ArgumentNullException(nameof(context));

            PlaceMarkAction place = action as PlaceMarkAction;
            if (place == null)
                return ActionResult.Rejected(action, ActionRejectionReason.ActionTypeNotSupported, context.Board.Version);

            if (!DoesMarkBelongToActor(place.Actor, place.Mark))
                return ActionResult.Rejected(place, ActionRejectionReason.MarkDoesNotBelongToActor, context.Board.Version);

            long beforeVersion = context.Board.Version;
            CellState before = TrySnapshot(context.Board, place.Target);

            MoveValidationResult move =
                _moveService.TryApplyNormalMove(context.Board, place.Target, place.Mark);

            if (!move.IsValid)
            {
                if (context.Board.Version != beforeVersion)
                    throw new InvalidOperationException("Uma jogada rejeitada alterou o BoardState.");

                return ActionResult.Rejected(
                    place,
                    ConvertRejectionReason(move.RejectionReason),
                    beforeVersion);
            }

            if (before == null)
                throw new InvalidOperationException("Uma jogada aplicada não possuía casa anterior.");

            CellState current = context.Board.GetCell(place.Target);
            CellState after = new CellState(current.Coordinate, current.Marks, current.Modifiers);

            BoardChangeSet changes = BoardChangeSet.CreateSingle(new BoardChange(before, after));

            return ActionResult.Applied(
                place,
                beforeVersion,
                context.Board.Version,
                changes);
        }

        private static CellState TrySnapshot(BoardState board, BoardCoordinate coordinate)
        {
            if (!board.TryGetCell(coordinate, out CellState cell))
                return null;

            return new CellState(cell.Coordinate, cell.Marks, cell.Modifiers);
        }

        private static bool DoesMarkBelongToActor(ScoreActor actor, CellMark mark)
        {
            return (actor == ScoreActor.Player && mark == CellMark.X) ||
                   (actor == ScoreActor.Enemy && mark == CellMark.O);
        }

        private static ActionRejectionReason ConvertRejectionReason(MoveRejectionReason reason)
        {
            switch (reason)
            {
                case MoveRejectionReason.CellDoesNotExist:
                    return ActionRejectionReason.CellDoesNotExist;
                case MoveRejectionReason.MarkAlreadyPresent:
                    return ActionRejectionReason.MarkAlreadyPresent;
                case MoveRejectionReason.OccupiedCellDoesNotAllowOverlap:
                    return ActionRejectionReason.OccupiedCellDoesNotAllowOverlap;
                default:
                    throw new ArgumentOutOfRangeException(nameof(reason), reason, "Motivo de jogada não reconhecido.");
            }
        }
    }
}
