using NUnit.Framework;
using TicTacToeRoguelike.Application.Encounters;
using TicTacToeRoguelike.Domain.Boards;
using TicTacToeRoguelike.Domain.Combat;
using TicTacToeRoguelike.Domain.Runes;
using TicTacToeRoguelike.Domain.Scoring;

namespace TicTacToeRoguelike.Tests.Runes
{
    public sealed class EncounterRunePersistenceTests
    {
        [Test]
        public void Encounter_UsesProvidedInventories_AndKeepsThemWhenRoundStarts()
        {
            RuneInventoryState playerRunes =
                new RuneInventoryState(
                    ScoreActor.Player);

            RuneInventoryState enemyRunes =
                new RuneInventoryState(
                    ScoreActor.Enemy);

            RuneInstance playerRune =
                RuneInstance.Create(
                    new RuneDefinition(
                        new RuneDefinitionId(
                            "test.player"),
                        "Player Rune",
                        "Test",
                        RuneRarity.Common));

            RuneInstance enemyRune =
                RuneInstance.Create(
                    new RuneDefinition(
                        new RuneDefinitionId(
                            "test.enemy"),
                        "Enemy Rune",
                        "Test",
                        RuneRarity.Rare));

            playerRunes.TryAdd(playerRune);
            enemyRunes.TryAdd(enemyRune);

            EncounterEngine engine =
                new EncounterEngine(
                    new CombatantState(
                        "player",
                        ScoreActor.Player,
                        100),
                    new CombatantState(
                        "enemy",
                        ScoreActor.Enemy,
                        100),
                    new EncounterRules(
                        requiredSequenceLength: 3,
                        minimumScoringSequenceLength: 2,
                        maximumScoringSequenceLength: 3,
                        victoryMultiplier: 1.5m),
                    playerRunes,
                    enemyRunes);

            Assert.That(
                engine.State.PlayerRunes,
                Is.SameAs(playerRunes));

            Assert.That(
                engine.State.EnemyRunes,
                Is.SameAs(enemyRunes));

            BoardState board =
                new BoardState(
                    BoardDefinition.CreateRectangular(
                        3,
                        3));

            engine.StartEncounter(
                board,
                EncounterSide.Player);

            Assert.That(
                engine.State.PlayerRunes,
                Is.SameAs(playerRunes));

            Assert.That(
                engine.State.PlayerRunes.Contains(
                    playerRune.InstanceId),
                Is.True);

            Assert.That(
                engine.State.EnemyRunes.Contains(
                    enemyRune.InstanceId),
                Is.True);
        }
    }
}
