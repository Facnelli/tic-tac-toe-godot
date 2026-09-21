using System.Collections.Generic;
using NUnit.Framework;
using TicTacToeRoguelike.Domain.Actions;
using TicTacToeRoguelike.Domain.Boards;
using TicTacToeRoguelike.Domain.Runes;
using TicTacToeRoguelike.Domain.Scoring;
using TicTacToeRoguelike.Presentation.Runes;

namespace TicTacToeRoguelike.Tests.Runes
{
    public sealed class RuneTargetingSelectionTests
    {
        private static readonly BoardCoordinate Target =
            new BoardCoordinate(1, 1);

        [Test]
        public void DefaultMode_AlwaysChoosesNormalPlacement()
        {
            RuneInstanceId source =
                new RuneInstanceId("clear:player");

            var place =
                new PlaceMarkAction(
                    ScoreActor.Player,
                    GameActionOrigin.PlayerInput,
                    1,
                    0,
                    Target,
                    CellMark.X);

            var clear =
                new ClearCellAction(
                    ScoreActor.Player,
                    GameActionOrigin.PlayerInput,
                    1,
                    0,
                    Target,
                    source);

            var selection =
                new RuneTargetingSelection();

            GameAction selected =
                selection.SelectTarget(
                    new GameAction[]
                    {
                        clear,
                        place
                    },
                    Target);

            Assert.That(
                selected,
                Is.SameAs(place));
            Assert.That(
                selection.HasSelection,
                Is.False);
        }

        [Test]
        public void SelectingRune_ChangesNextBoardClickToItsAction()
        {
            RuneInstanceId source =
                new RuneInstanceId("clear:player");

            var place =
                new PlaceMarkAction(
                    ScoreActor.Player,
                    GameActionOrigin.PlayerInput,
                    1,
                    0,
                    Target,
                    CellMark.X);

            var clear =
                new ClearCellAction(
                    ScoreActor.Player,
                    GameActionOrigin.PlayerInput,
                    1,
                    0,
                    Target,
                    source);

            IReadOnlyList<GameAction> actions =
                new GameAction[]
                {
                    place,
                    clear
                };

            var selection =
                new RuneTargetingSelection();

            Assert.That(
                selection.Toggle(
                    source,
                    actions),
                Is.True);

            Assert.That(
                selection.SelectedSource,
                Is.EqualTo(source));

            Assert.That(
                selection.SelectTarget(
                    actions,
                    Target),
                Is.SameAs(clear));
        }

        [Test]
        public void ClickingSelectedRuneAgain_CancelsSpecialMode()
        {
            RuneInstanceId source =
                new RuneInstanceId("clear:player");

            var place =
                new PlaceMarkAction(
                    ScoreActor.Player,
                    GameActionOrigin.PlayerInput,
                    1,
                    0,
                    Target,
                    CellMark.X);

            var clear =
                new ClearCellAction(
                    ScoreActor.Player,
                    GameActionOrigin.PlayerInput,
                    1,
                    0,
                    Target,
                    source);

            IReadOnlyList<GameAction> actions =
                new GameAction[]
                {
                    place,
                    clear
                };

            var selection =
                new RuneTargetingSelection();

            selection.Toggle(
                source,
                actions);

            selection.Toggle(
                source,
                actions);

            Assert.That(
                selection.HasSelection,
                Is.False);

            Assert.That(
                selection.SelectTarget(
                    actions,
                    Target),
                Is.SameAs(place));
        }

        [Test]
        public void InvalidTarget_KeepsRuneSelectedForAnotherClick()
        {
            RuneInstanceId source =
                new RuneInstanceId("clear:player");

            var clear =
                new ClearCellAction(
                    ScoreActor.Player,
                    GameActionOrigin.PlayerInput,
                    1,
                    0,
                    Target,
                    source);

            IReadOnlyList<GameAction> actions =
                new GameAction[]
                {
                    clear
                };

            var selection =
                new RuneTargetingSelection();

            selection.Toggle(
                source,
                actions);

            GameAction selected =
                selection.SelectTarget(
                    actions,
                    new BoardCoordinate(0, 0));

            Assert.That(selected, Is.Null);
            Assert.That(
                selection.SelectedSource,
                Is.EqualTo(source));
        }

        [Test]
        public void Refresh_WhenRuneStopsOfferingActions_ReturnsToNormalMode()
        {
            RuneInstanceId source =
                new RuneInstanceId("clear:player");

            var clear =
                new ClearCellAction(
                    ScoreActor.Player,
                    GameActionOrigin.PlayerInput,
                    1,
                    0,
                    Target,
                    source);

            var selection =
                new RuneTargetingSelection();

            selection.Toggle(
                source,
                new GameAction[]
                {
                    clear
                });

            selection.Refresh(
                System.Array.Empty<GameAction>());

            Assert.That(
                selection.HasSelection,
                Is.False);
        }

        [Test]
        public void OnlyRunesWithBoardTargetedActions_AreSelectable()
        {
            RuneInstanceId first =
                new RuneInstanceId("clear:first");

            RuneInstanceId second =
                new RuneInstanceId("clear:second");

            IReadOnlyList<GameAction> actions =
                new GameAction[]
                {
                    new ClearCellAction(
                        ScoreActor.Player,
                        GameActionOrigin.PlayerInput,
                        1,
                        0,
                        Target,
                        first),
                    new ClearCellAction(
                        ScoreActor.Player,
                        GameActionOrigin.PlayerInput,
                        1,
                        0,
                        new BoardCoordinate(2, 2),
                        first),
                    new ClearCellAction(
                        ScoreActor.Player,
                        GameActionOrigin.PlayerInput,
                        1,
                        0,
                        Target,
                        second)
                };

            var selection =
                new RuneTargetingSelection();

            IReadOnlyList<RuneInstanceId> selectable =
                selection.GetSelectableSources(
                    actions);

            Assert.That(
                selectable,
                Is.EqualTo(
                    new[]
                    {
                        first,
                        second
                    }));
        }
    }
}
