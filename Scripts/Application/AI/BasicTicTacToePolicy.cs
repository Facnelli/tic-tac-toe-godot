using System;
using System.Collections.Generic;
using TicTacToeRoguelike.Domain.Actions;
using TicTacToeRoguelike.Domain.Boards;
using TicTacToeRoguelike.Domain.Moves;
using TicTacToeRoguelike.Domain.Reactions;
using TicTacToeRoguelike.Domain.Scoring;
using TicTacToeRoguelike.Domain.Turns;

namespace TicTacToeRoguelike.Application.AI
{
    /// <summary>
    /// IA determinística do jogo da velha.
    ///
    /// Diferente da versão Unity original, esta política implementa IAgentPolicy
    /// e escolhe exatamente uma instância recebida em AgentDecisionContext.
    /// A busca respeita a regra de reação: criar uma sequência apenas altera o
    /// placar; uma vitória só existe quando ReactionRule produz uma decisão
    /// terminal.
    /// </summary>
    public sealed class BasicTicTacToePolicy : IAgentPolicy
    {
        private const int VictoryScore = 1_000_000_000;
        private const int SequenceWeight = 100_000;
        private const int MobilityWeight = 100;

        private readonly MoveValidator _moveValidator;
        private readonly MoveService _moveService;
        private readonly ReactionStateFactory _reactionStateFactory;
        private readonly ReactionRule _reactionRule;

        public int MaximumSearchDepth { get; }
        public int MaximumVisitedNodes { get; }

        public int LastCompletedDepth { get; private set; }
        public int LastVisitedNodeCount { get; private set; }
        public bool LastDecisionWasProvenOptimal { get; private set; }
        public int LastUnsupportedActionCount { get; private set; }

        public BasicTicTacToePolicy(
            int maximumSearchDepth = 10,
            int maximumVisitedNodes = 100_000)
        {
            if (maximumSearchDepth <= 0)
                throw new ArgumentOutOfRangeException(nameof(maximumSearchDepth));
            if (maximumVisitedNodes <= 0)
                throw new ArgumentOutOfRangeException(nameof(maximumVisitedNodes));

            MaximumSearchDepth = maximumSearchDepth;
            MaximumVisitedNodes = maximumVisitedNodes;

            _moveValidator = new MoveValidator();
            _moveService = new MoveService(_moveValidator);
            _reactionStateFactory = new ReactionStateFactory();
            _reactionRule = new ReactionRule();
        }

        public bool TryChooseAction(
            AgentDecisionContext context,
            out GameAction action)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            ResetDiagnostics();

            List<PlaceMarkAction> supported = new List<PlaceMarkAction>();

            for (int i = 0; i < context.LegalActions.Count; i++)
            {
                GameAction candidate = context.LegalActions[i];

                if (candidate is PlaceMarkAction place)
                    supported.Add(place);
                else
                    LastUnsupportedActionCount++;
            }

            if (supported.Count == 0)
            {
                action = null;
                return false;
            }

            supported.Sort(CompareRootActions);

            GameAction bestAction = supported[0];
            int deepestCompleted = 0;
            bool exhaustedEntireTree = false;

            SearchBudget budget = new SearchBudget(MaximumVisitedNodes);

            for (int depth = 1; depth <= MaximumSearchDepth; depth++)
            {
                RootResult iteration = SearchRoot(context, supported, depth, budget);

                if (!iteration.Completed)
                    break;

                bestAction = iteration.Action;
                deepestCompleted = depth;

                if (iteration.TreeExhausted)
                {
                    exhaustedEntireTree = true;
                    break;
                }
            }

            LastCompletedDepth = deepestCompleted;
            LastVisitedNodeCount = budget.Visited;
            LastDecisionWasProvenOptimal = exhaustedEntireTree;

            action = bestAction;
            return true;
        }

        /// <summary>
        /// Adaptador temporário para consumidores antigos do Marco 3/4.
        /// O fluxo novo deve usar IAgentPolicy.TryChooseAction.
        /// </summary>
        public bool TryChooseMove(
            BoardState board,
            CellMark controlledMark,
            CellMark opponentMark,
            int requiredSequenceLength,
            out BoardCoordinate move)
        {
            if (board == null) throw new ArgumentNullException(nameof(board));
            if (controlledMark == opponentMark)
                throw new ArgumentException("As marcas dos participantes precisam ser diferentes.");

            ScoreActor actor = controlledMark == CellMark.X
                ? ScoreActor.Player
                : controlledMark == CellMark.O
                    ? ScoreActor.Enemy
                    : throw new ArgumentOutOfRangeException(nameof(controlledMark));

            if ((actor == ScoreActor.Player && opponentMark != CellMark.O) ||
                (actor == ScoreActor.Enemy && opponentMark != CellMark.X))
                throw new ArgumentOutOfRangeException(nameof(opponentMark));

            TurnContext turn = new TurnContext(actor, 1, board.Version, 1);
            ReactionState reaction = _reactionStateFactory.Create(
                board, CellMark.X, CellMark.O, requiredSequenceLength);

            IReadOnlyList<BoardCoordinate> coordinates =
                _moveValidator.GetLegalNormalMoves(board, controlledMark);

            List<GameAction> actions = new List<GameAction>(coordinates.Count);
            for (int i = 0; i < coordinates.Count; i++)
            {
                actions.Add(new PlaceMarkAction(
                    actor,
                    GameActionOrigin.AgentPolicy,
                    turn.TurnId,
                    board.Version,
                    coordinates[i],
                    controlledMark));
            }

            AgentDecisionContext context = new AgentDecisionContext(
                board,
                turn,
                reaction,
                requiredSequenceLength,
                actions,
                encounterGeneration: 0);

            if (!TryChooseAction(context, out GameAction selected))
            {
                move = default(BoardCoordinate);
                return false;
            }

            move = ((PlaceMarkAction)selected).Target;
            return true;
        }

        private RootResult SearchRoot(
            AgentDecisionContext context,
            IReadOnlyList<PlaceMarkAction> rootActions,
            int depth,
            SearchBudget budget)
        {
            int bestScore = int.MinValue;
            PlaceMarkAction best = rootActions[0];
            bool allBranchesExhausted = true;

            for (int i = 0; i < rootActions.Count; i++)
            {
                if (!budget.TryVisit())
                    return RootResult.Incomplete();

                PlaceMarkAction rootAction = rootActions[i];
                BoardState board = context.BoardSnapshot.Clone();

                MoveValidationResult applied =
                    _moveService.TryApplyNormalMove(
                        board,
                        rootAction.Target,
                        rootAction.Mark);

                if (!applied.IsValid)
                    throw new InvalidOperationException(
                        "AgentDecisionContext continha uma PlaceMarkAction que não é legal no snapshot.");

                SearchTransition transition = ResolveAfterAction(
                    board,
                    context.ReactionState,
                    context.Actor,
                    context.RemainingActions - 1,
                    context.RequiredSequenceLength);

                int score;
                bool branchExhausted;

                if (transition.IsTerminal)
                {
                    score = ScoreTerminal(
                        transition.Winner,
                        context.Actor,
                        ply: 1);
                    branchExhausted = true;
                }
                else if (depth <= 1)
                {
                    score = Evaluate(
                        board,
                        transition.ReactionState,
                        context.Actor,
                        context.RequiredSequenceLength);
                    branchExhausted = false;
                }
                else
                {
                    NodeResult child = Search(
                        board,
                        transition.ReactionState,
                        transition.ActorToMove,
                        transition.ActionsRemainingInTurn,
                        context.Actor,
                        context.RequiredSequenceLength,
                        depth - 1,
                        ply: 1,
                        alpha: int.MinValue + 1,
                        beta: int.MaxValue - 1,
                        budget);

                    if (!child.Completed)
                        return RootResult.Incomplete();

                    score = child.Score;
                    branchExhausted = child.TreeExhausted;
                }

                if (score > bestScore)
                {
                    bestScore = score;
                    best = rootAction;
                }

                allBranchesExhausted &= branchExhausted;
            }

            return RootResult.Success(best, bestScore, allBranchesExhausted);
        }

        private NodeResult Search(
            BoardState board,
            ReactionState reactionState,
            ScoreActor actorToMove,
            int actionsRemainingInTurn,
            ScoreActor rootActor,
            int requiredSequenceLength,
            int remainingDepth,
            int ply,
            int alpha,
            int beta,
            SearchBudget budget)
        {
            if (remainingDepth <= 0)
            {
                return NodeResult.Success(
                    Evaluate(board, reactionState, rootActor, requiredSequenceLength),
                    treeExhausted: false);
            }

            CellMark mark = GetMark(actorToMove);
            IReadOnlyList<BoardCoordinate> moves =
                _moveValidator.GetLegalNormalMoves(board, mark);

            if (moves.Count == 0)
            {
                // Esta situação normalmente já teria sido encerrada pela
                // ReactionRule ao final do Turno anterior.
                return NodeResult.Success(
                    Evaluate(board, reactionState, rootActor, requiredSequenceLength),
                    treeExhausted: true);
            }

            List<BoardCoordinate> ordered = new List<BoardCoordinate>(moves);
            ordered.Sort(CompareCoordinates);

            bool maximizing = actorToMove == rootActor;
            int bestScore = maximizing ? int.MinValue : int.MaxValue;
            bool allBranchesExhausted = true;

            for (int i = 0; i < ordered.Count; i++)
            {
                if (!budget.TryVisit())
                    return NodeResult.Incomplete();

                BoardState simulation = board.Clone();
                MoveValidationResult applied =
                    _moveService.TryApplyNormalMove(
                        simulation,
                        ordered[i],
                        mark);

                if (!applied.IsValid)
                    throw new InvalidOperationException("Uma jogada enumerada como legal falhou na simulação.");

                SearchTransition transition = ResolveAfterAction(
                    simulation,
                    reactionState,
                    actorToMove,
                    actionsRemainingInTurn - 1,
                    requiredSequenceLength);

                int score;
                bool branchExhausted;

                if (transition.IsTerminal)
                {
                    score = ScoreTerminal(
                        transition.Winner,
                        rootActor,
                        ply + 1);
                    branchExhausted = true;
                }
                else
                {
                    NodeResult child = Search(
                        simulation,
                        transition.ReactionState,
                        transition.ActorToMove,
                        transition.ActionsRemainingInTurn,
                        rootActor,
                        requiredSequenceLength,
                        remainingDepth - 1,
                        ply + 1,
                        alpha,
                        beta,
                        budget);

                    if (!child.Completed)
                        return NodeResult.Incomplete();

                    score = child.Score;
                    branchExhausted = child.TreeExhausted;
                }

                allBranchesExhausted &= branchExhausted;

                if (maximizing)
                {
                    if (score > bestScore) bestScore = score;
                    if (bestScore > alpha) alpha = bestScore;
                }
                else
                {
                    if (score < bestScore) bestScore = score;
                    if (bestScore < beta) beta = bestScore;
                }

                if (alpha >= beta)
                    break;
            }

            return NodeResult.Success(bestScore, allBranchesExhausted);
        }

        private SearchTransition ResolveAfterAction(
            BoardState board,
            ReactionState previousReaction,
            ScoreActor completedActionActor,
            int actionsRemainingInTurn,
            int requiredSequenceLength)
        {
            if (actionsRemainingInTurn > 0)
            {
                return SearchTransition.Continue(
                    previousReaction,
                    completedActionActor,
                    actionsRemainingInTurn);
            }

            ReactionState currentReaction = _reactionStateFactory.Create(
                board,
                CellMark.X,
                CellMark.O,
                requiredSequenceLength);

            ScoreActor nextActor = Opposite(completedActionActor);
            CellMark nextMark = GetMark(nextActor);

            bool nextActorHasActions =
                _moveValidator.GetLegalNormalMoves(board, nextMark).Count > 0;

            ReactionDecision decision = _reactionRule.Evaluate(
                previousReaction,
                currentReaction,
                completedActionActor,
                nextActorHasActions);

            if (decision.EndsBoardMatch)
                return SearchTransition.Terminal(currentReaction, decision.Winner);

            return SearchTransition.Continue(
                currentReaction,
                nextActor,
                actionsRemainingInTurn: 1);
        }

        private int Evaluate(
            BoardState board,
            ReactionState reaction,
            ScoreActor rootActor,
            int requiredSequenceLength)
        {
            int own = reaction.GetSequenceCount(rootActor);
            int opponent = reaction.GetSequenceCount(Opposite(rootActor));
            int sequenceScore = (own - opponent) * SequenceWeight;

            int ownMobility = _moveValidator
                .GetLegalNormalMoves(board, GetMark(rootActor)).Count;
            int opponentMobility = _moveValidator
                .GetLegalNormalMoves(board, GetMark(Opposite(rootActor))).Count;

            int mobilityScore = (ownMobility - opponentMobility) * MobilityWeight;

            int reactionBias = 0;
            if (reaction.IsReactionActive)
            {
                if (reaction.Leader == rootActor)
                    reactionBias = SequenceWeight / 4;
                else
                    reactionBias = -SequenceWeight / 4;
            }

            return sequenceScore + mobilityScore + reactionBias;
        }

        private static int ScoreTerminal(ScoreActor winner, ScoreActor rootActor, int ply)
        {
            if (winner == ScoreActor.None) return 0;
            return winner == rootActor
                ? VictoryScore - ply
                : -VictoryScore + ply;
        }

        private static CellMark GetMark(ScoreActor actor)
        {
            if (actor == ScoreActor.Player) return CellMark.X;
            if (actor == ScoreActor.Enemy) return CellMark.O;
            throw new ArgumentOutOfRangeException(nameof(actor));
        }

        private static ScoreActor Opposite(ScoreActor actor)
        {
            if (actor == ScoreActor.Player) return ScoreActor.Enemy;
            if (actor == ScoreActor.Enemy) return ScoreActor.Player;
            throw new ArgumentOutOfRangeException(nameof(actor));
        }

        private static int CompareRootActions(PlaceMarkAction left, PlaceMarkAction right)
        {
            return CompareCoordinates(left.Target, right.Target);
        }

        private static int CompareCoordinates(BoardCoordinate left, BoardCoordinate right)
        {
            int y = left.Y.CompareTo(right.Y);
            return y != 0 ? y : left.X.CompareTo(right.X);
        }

        private void ResetDiagnostics()
        {
            LastCompletedDepth = 0;
            LastVisitedNodeCount = 0;
            LastDecisionWasProvenOptimal = false;
            LastUnsupportedActionCount = 0;
        }

        private sealed class SearchBudget
        {
            private readonly int _maximum;
            public int Visited { get; private set; }

            public SearchBudget(int maximum)
            {
                _maximum = maximum;
            }

            public bool TryVisit()
            {
                if (Visited >= _maximum) return false;
                Visited++;
                return true;
            }
        }

        private readonly struct SearchTransition
        {
            public ReactionState ReactionState { get; }
            public ScoreActor ActorToMove { get; }
            public int ActionsRemainingInTurn { get; }
            public bool IsTerminal { get; }
            public ScoreActor Winner { get; }

            private SearchTransition(
                ReactionState reactionState,
                ScoreActor actorToMove,
                int actionsRemainingInTurn,
                bool isTerminal,
                ScoreActor winner)
            {
                ReactionState = reactionState;
                ActorToMove = actorToMove;
                ActionsRemainingInTurn = actionsRemainingInTurn;
                IsTerminal = isTerminal;
                Winner = winner;
            }

            public static SearchTransition Continue(
                ReactionState reactionState,
                ScoreActor actorToMove,
                int actionsRemainingInTurn)
            {
                return new SearchTransition(
                    reactionState,
                    actorToMove,
                    actionsRemainingInTurn,
                    false,
                    ScoreActor.None);
            }

            public static SearchTransition Terminal(
                ReactionState reactionState,
                ScoreActor winner)
            {
                return new SearchTransition(
                    reactionState,
                    ScoreActor.None,
                    0,
                    true,
                    winner);
            }
        }

        private readonly struct NodeResult
        {
            public bool Completed { get; }
            public int Score { get; }
            public bool TreeExhausted { get; }

            private NodeResult(bool completed, int score, bool treeExhausted)
            {
                Completed = completed;
                Score = score;
                TreeExhausted = treeExhausted;
            }

            public static NodeResult Success(int score, bool treeExhausted)
                => new NodeResult(true, score, treeExhausted);

            public static NodeResult Incomplete()
                => new NodeResult(false, 0, false);
        }

        private readonly struct RootResult
        {
            public bool Completed { get; }
            public GameAction Action { get; }
            public int Score { get; }
            public bool TreeExhausted { get; }

            private RootResult(bool completed, GameAction action, int score, bool treeExhausted)
            {
                Completed = completed;
                Action = action;
                Score = score;
                TreeExhausted = treeExhausted;
            }

            public static RootResult Success(GameAction action, int score, bool treeExhausted)
                => new RootResult(true, action, score, treeExhausted);

            public static RootResult Incomplete()
                => new RootResult(false, null, 0, false);
        }
    }
}
