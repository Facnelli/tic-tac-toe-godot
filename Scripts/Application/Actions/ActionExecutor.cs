using System;
using TicTacToeRoguelike.Domain.Actions;
using TicTacToeRoguelike.Domain.Boards;
using TicTacToeRoguelike.Domain.Moves;
using TicTacToeRoguelike.Domain.Turns;

namespace TicTacToeRoguelike.Application.Actions
{
    public sealed class ActionExecutor
    {
        private readonly ActionCatalog _catalog;

        public ActionExecutor()
            : this(ActionCatalog.CreateDefault())
        {
        }

        public ActionExecutor(MoveService moveService)
            : this(ActionCatalog.CreateDefault(new MoveValidator(), moveService))
        {
        }

        public ActionExecutor(ActionCatalog catalog)
        {
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        }

        public ActionResult Execute(
            GameAction action,
            BoardState board,
            TurnContext turnContext,
            bool encounterAcceptsActions)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            if (board == null) throw new ArgumentNullException(nameof(board));

            if (!encounterAcceptsActions)
                return ActionResult.Rejected(action, ActionRejectionReason.EncounterDoesNotAcceptActions, board.Version);

            if (turnContext == null || !turnContext.IsOpen || !turnContext.HasRemainingActions)
                return ActionResult.Rejected(action, ActionRejectionReason.TurnIsNotOpen, board.Version);

            if (action.ExpectedTurnId != turnContext.TurnId)
                return ActionResult.Rejected(action, ActionRejectionReason.TurnIdDoesNotMatch, board.Version);

            if (action.Actor != turnContext.Actor)
                return ActionResult.Rejected(action, ActionRejectionReason.ActorDoesNotOwnTurn, board.Version);

            if (action.ExpectedBoardVersion != board.Version)
                return ActionResult.Rejected(action, ActionRejectionReason.BoardVersionDoesNotMatch, board.Version);

            if (turnContext.CurrentBoardVersion != board.Version)
                throw new InvalidOperationException(
                    $"O Turno {turnContext.TurnId} conhece a versão {turnContext.CurrentBoardVersion}, " +
                    $"mas o BoardState está na versão {board.Version}.");

            if (!_catalog.TryGetHandler(action, out IGameActionHandler handler))
                return ActionResult.Rejected(action, ActionRejectionReason.ActionTypeNotSupported, board.Version);

            ActionExecutionContext context =
                new ActionExecutionContext(board, turnContext, encounterAcceptsActions);

            ActionResult result = handler.Execute(action, context)
                ?? throw new InvalidOperationException("O handler retornou null.");

            if (!ReferenceEquals(result.Action, action))
                throw new InvalidOperationException("O handler precisa devolver um resultado da mesma GameAction.");

            if (result.WasApplied)
                turnContext.RegisterAppliedAction(result.BoardVersionAfter);

            return result;
        }
    }
}
