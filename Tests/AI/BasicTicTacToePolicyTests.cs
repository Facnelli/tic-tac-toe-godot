using System.Collections.Generic;
using NUnit.Framework;
using TicTacToeRoguelike.Application.AI;
using TicTacToeRoguelike.Domain.Actions;
using TicTacToeRoguelike.Domain.Boards;
using TicTacToeRoguelike.Domain.Reactions;
using TicTacToeRoguelike.Domain.Scoring;
using TicTacToeRoguelike.Domain.Turns;

namespace TicTacToeRoguelike.Tests.AI
{
    public sealed class BasicTicTacToePolicyTests
    {
        [Test]
        public void TryChooseAction_ReturnsExactInstanceFromLegalActions()
        {
            BoardState board = new BoardState(BoardDefinition.CreateRectangular(3, 3));
            TurnContext turn = new TurnContext(ScoreActor.Enemy, 4, board.Version, 1);
            ReactionState reaction = new ReactionState(0, 0, board.Version);

            PlaceMarkAction first = new PlaceMarkAction(
                ScoreActor.Enemy,
                GameActionOrigin.AgentPolicy,
                turn.TurnId,
                board.Version,
                new BoardCoordinate(0, 0),
                CellMark.O);

            PlaceMarkAction second = new PlaceMarkAction(
                ScoreActor.Enemy,
                GameActionOrigin.AgentPolicy,
                turn.TurnId,
                board.Version,
                new BoardCoordinate(1, 1),
                CellMark.O);

            var legal = new List<GameAction> { first, second };
            AgentDecisionContext context = new AgentDecisionContext(
                board, turn, reaction, 3, legal, 1);

            BasicTicTacToePolicy policy =
                new BasicTicTacToePolicy(maximumSearchDepth: 3, maximumVisitedNodes: 5000);

            bool chose = policy.TryChooseAction(context, out GameAction selected);

            Assert.That(chose, Is.True);
            Assert.That(selected == first || selected == second, Is.True);
            Assert.That(context.ContainsLegalAction(selected), Is.True);
        }

        [Test]
        public void TryChooseAction_DoesNotMutateAuthoritativeBoard()
        {
            BoardState board = new BoardState(BoardDefinition.CreateRectangular(3, 3));
            TurnContext turn = new TurnContext(ScoreActor.Player, 1, board.Version, 1);
            ReactionState reaction = new ReactionState(0, 0, board.Version);

            var legal = new List<GameAction>
            {
                new PlaceMarkAction(
                    ScoreActor.Player,
                    GameActionOrigin.AgentPolicy,
                    turn.TurnId,
                    board.Version,
                    new BoardCoordinate(0, 0),
                    CellMark.X)
            };

            AgentDecisionContext context = new AgentDecisionContext(
                board, turn, reaction, 3, legal, 0);

            BasicTicTacToePolicy policy = new BasicTicTacToePolicy(2, 100);

            policy.TryChooseAction(context, out _);

            Assert.That(board.Version, Is.Zero);
            Assert.That(board.GetCell(0, 0).IsEmpty, Is.True);
            Assert.That(turn.ActionsConsumed, Is.Zero);
        }

        [Test]
        public void TryChooseMove_FirstSequenceIsNotAssumedToBeTerminal()
        {
            BoardState board = new BoardState(
                BoardDefinition.CreateRectangular(3, 3),
                new[]
                {
                    new CellState(new BoardCoordinate(0, 0), CellMark.O),
                    new CellState(new BoardCoordinate(1, 0), CellMark.O),
                    new CellState(new BoardCoordinate(0, 1), CellMark.X),
                    new CellState(new BoardCoordinate(1, 1), CellMark.X)
                });

            BasicTicTacToePolicy policy =
                new BasicTicTacToePolicy(maximumSearchDepth: 4, maximumVisitedNodes: 10000);

            bool chose = policy.TryChooseMove(
                board,
                CellMark.O,
                CellMark.X,
                requiredSequenceLength: 3,
                out BoardCoordinate move);

            Assert.That(chose, Is.True);
            Assert.That(board.Version, Is.Zero);
            Assert.That(board.GetCell(move).IsEmpty, Is.True);
            Assert.That(policy.LastVisitedNodeCount, Is.GreaterThan(0));
        }
    }
}
