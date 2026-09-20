using System.Collections.Generic;
using NUnit.Framework;
using TicTacToeRoguelike.Application.Effects;
using TicTacToeRoguelike.Application.Encounters;
using TicTacToeRoguelike.Domain.Boards;
using TicTacToeRoguelike.Domain.Combat;
using TicTacToeRoguelike.Domain.Runes;
using TicTacToeRoguelike.Domain.Scoring;

namespace TicTacToeRoguelike.Tests.Effects
{
    public sealed class PilotRuneEncounterIntegrationTests
    {
        [Test]
        public void Encounter_PilotRuneFlowsThroughEffectsIntoScoreSteps()
        {
            RuneInventoryState playerRunes =
                new RuneInventoryState(
                    ScoreActor.Player);

            RuneInventoryState enemyRunes =
                new RuneInventoryState(
                    ScoreActor.Enemy);

            RuneInstance pilot =
                new RuneInstance(
                    new RuneInstanceId(
                        "pilot-player-001"),
                    new RuneDefinition(
                        RuneDefinitionIds
                            .PilotIndependentMultiplier,
                        "Runa do Eco",
                        "Passiva: MULT ×1,20.",
                        RuneRarity.Rare,
                        "ᛞ"));

            Assert.That(
                playerRunes.TryAdd(pilot),
                Is.EqualTo(
                    RuneInventoryAddResult.Added));

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
                        requiredSequenceLength: 2,
                        minimumScoringSequenceLength: 2,
                        maximumScoringSequenceLength: 2,
                        victoryMultiplier: 1.5m),
                    playerRunes,
                    enemyRunes,
                    DefaultEffectEngineFactory.Create());

            BoardState fullBoard =
                CreateFullBoard();

            engine.StartEncounter(
                fullBoard,
                EncounterSide.Player);

            RoundResolution resolution =
                engine.State.LastRoundResolution;

            Assert.That(
                resolution,
                Is.Not.Null);

            Assert.That(
                resolution.PlayerEffects
                    .ScoreContributions.Count,
                Is.EqualTo(1));

            Assert.That(
                resolution.EnemyEffects
                    .ScoreContributions,
                Is.Empty);

            Assert.That(
                resolution.PlayerScore
                    .Breakdown
                    .IndependentMultiplierProduct,
                Is.EqualTo(1.20m));

            Assert.That(
                resolution.EnemyScore
                    .Breakdown
                    .IndependentMultiplierProduct,
                Is.EqualTo(1m));

            ScoreStep runeStep = null;

            for (int i = 0;
                 i < resolution.PlayerScore
                    .Breakdown.Steps.Count;
                 i++)
            {
                ScoreStep step =
                    resolution.PlayerScore
                        .Breakdown.Steps[i];

                if (step.Contribution.Phase ==
                    ScorePhase.IndependentMultiplier)
                {
                    runeStep = step;
                    break;
                }
            }

            Assert.That(
                runeStep,
                Is.Not.Null);

            Assert.That(
                runeStep.Contribution.DisplayText,
                Is.EqualTo("Runa do Eco"));

            Assert.That(
                runeStep.Contribution.Amount,
                Is.EqualTo(1.20m));

            Assert.That(
                runeStep.EffectiveFactor,
                Is.EqualTo(1.20m));

            Assert.That(
                runeStep.Contribution.SourceId,
                Does.Contain("pilot-player-001"));
        }

        private static BoardState CreateFullBoard()
        {
            BoardDefinition definition =
                BoardDefinition.CreateRectangular(
                    2,
                    2);

            List<CellState> cells =
                new List<CellState>
                {
                    new CellState(
                        new BoardCoordinate(0, 0),
                        CellMark.X),
                    new CellState(
                        new BoardCoordinate(1, 0),
                        CellMark.X),
                    new CellState(
                        new BoardCoordinate(0, 1),
                        CellMark.O),
                    new CellState(
                        new BoardCoordinate(1, 1),
                        CellMark.O)
                };

            return new BoardState(
                definition,
                cells);
        }
    }
}
