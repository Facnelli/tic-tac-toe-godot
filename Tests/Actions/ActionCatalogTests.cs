using System;
using NUnit.Framework;
using TicTacToeRoguelike.Application.Actions;
using TicTacToeRoguelike.Domain.Actions;
using TicTacToeRoguelike.Domain.Boards;
using TicTacToeRoguelike.Domain.Moves;
using TicTacToeRoguelike.Domain.Scoring;
using TicTacToeRoguelike.Domain.Turns;

namespace TicTacToeRoguelike.Tests.Actions
{
    public sealed class ActionCatalogTests
    {
        [Test]
        public void DefaultCatalog_EnumeratesConcreteActionsForCurrentTurn()
        {
            BoardState board = new BoardState(BoardDefinition.CreateRectangular(3, 3));
            TurnContext turn = new TurnContext(ScoreActor.Player, 7, board.Version, 1);
            ActionCatalog catalog = ActionCatalog.CreateDefault();

            var actions = catalog.GetAvailableActions(
                board,
                turn,
                GameActionOrigin.PlayerInput);

            Assert.That(actions.Count, Is.EqualTo(9));

            foreach (GameAction action in actions)
            {
                Assert.That(action, Is.TypeOf<PlaceMarkAction>());
                Assert.That(action.Actor, Is.EqualTo(ScoreActor.Player));
                Assert.That(action.ExpectedTurnId, Is.EqualTo(7));
                Assert.That(action.ExpectedBoardVersion, Is.EqualTo(board.Version));
                Assert.That(action.Origin, Is.EqualTo(GameActionOrigin.PlayerInput));
            }
        }

        [Test]
        public void ExecutorWithCatalog_AppliesThroughRegisteredHandler()
        {
            MoveValidator validator = new MoveValidator();
            MoveService moveService = new MoveService(validator);
            ActionCatalog catalog = ActionCatalog.CreateDefault(validator, moveService);
            ActionExecutor executor = ActionExecutor.CreateWithCatalog(catalog);

            BoardState board = new BoardState(BoardDefinition.CreateRectangular(3, 3));
            TurnContext turn = new TurnContext(ScoreActor.Player, 1, board.Version, 1);

            GameAction action = catalog.GetAvailableActions(
                board,
                turn,
                GameActionOrigin.PlayerInput)[0];

            ActionResult result = executor.Execute(
                action,
                board,
                turn,
                encounterAcceptsActions: true);

            Assert.That(result.WasApplied, Is.True);
            Assert.That(turn.Status, Is.EqualTo(TurnStatus.Completed));
            Assert.That(board.Version, Is.EqualTo(1));
        }

        [Test]
        public void Constructor_WithDuplicateHandlerType_RejectsComposition()
        {
            MoveService service = new MoveService();

            Assert.Throws<ArgumentException>(() =>
                new ActionCatalog(
                    Array.Empty<IGameActionProvider>(),
                    new IGameActionHandler[]
                    {
                        new PlaceMarkActionHandler(service),
                        new PlaceMarkActionHandler(service)
                    }));
        }
    }
}
