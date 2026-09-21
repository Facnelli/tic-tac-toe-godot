using System;
using TicTacToeRoguelike.Domain.Actions;
using TicTacToeRoguelike.Domain.Boards;
using TicTacToeRoguelike.Domain.Moves;
using TicTacToeRoguelike.Domain.Scoring;
using TicTacToeRoguelike.Domain.Sequences;

namespace TicTacToeRoguelike.Application.AI
{
    // Bounded adapter for the pilot active capability. All candidates came from the common catalog.
    public sealed class RuneAwareAgentPolicy : IAgentPolicy
    {
        private readonly IAgentPolicy _normal;
        public RuneAwareAgentPolicy(IAgentPolicy normal) { _normal = normal ?? throw new ArgumentNullException(nameof(normal)); }
        public bool TryChooseAction(AgentDecisionContext context, out GameAction action)
        {
            _normal.TryChooseAction(context, out action);
            int baseline = action == null ? int.MinValue : Evaluate(context, action);
            foreach (var candidate in context.LegalActions)
            {
                if (!(candidate is ClearCellAction)) continue;
                int score = Evaluate(context, candidate);
                // Save the limited charge unless it improves the immediate sequence balance.
                if (score > baseline) { baseline = score; action = candidate; }
            }
            return action != null;
        }
        private static int Evaluate(AgentDecisionContext context, GameAction action)
        {
            var board = context.BoardSnapshot.Clone();
            if (action is PlaceMarkAction place) new MoveService().TryApplyNormalMove(board, place.Target, place.Mark);
            else if (action is ClearCellAction clear)
                new BoardMutationService().ApplyAuthorized(board, new[] { BoardMutationCommand.ClearMarks(clear.Target) });
            var evaluator = new SequenceEvaluator();
            var own = action.Actor == ScoreActor.Player ? CellMark.X : CellMark.O;
            var other = own == CellMark.X ? CellMark.O : CellMark.X;
            return evaluator.EvaluateWinningSequences(board, own, context.RequiredSequenceLength).Count -
                evaluator.EvaluateWinningSequences(board, other, context.RequiredSequenceLength).Count;
        }
    }
}
