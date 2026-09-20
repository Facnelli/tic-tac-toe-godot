using System.Collections.Generic;
using NUnit.Framework;
using TicTacToeRoguelike.Application.Encounters;
using TicTacToeRoguelike.Domain.Boards;
using TicTacToeRoguelike.Domain.Combat;
using TicTacToeRoguelike.Domain.Effects;
using TicTacToeRoguelike.Domain.Runes;
using TicTacToeRoguelike.Domain.Scoring;

namespace TicTacToeRoguelike.Tests.Effects
{
    public sealed class EncounterEffectIntegrationTests
    {
        [Test]
        public void RoundScoring_ConsumesEffectEngineContributionsAndReports()
        {
            RuneInventoryState playerRunes =
                new RuneInventoryState(
                    ScoreActor.Player);

            RuneInventoryState enemyRunes =
                new RuneInventoryState(
                    ScoreActor.Enemy);

            EffectEngine effects =
                new EffectEngine(
                    new IGameEffectHandler[]
                    {
                        new FlatBonusHandler()
                    });

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
                    effects);

            BoardState fullBoard =
                CreateFullDrawBoard();

            engine.StartEncounter(
                fullBoard,
                EncounterSide.Player);

            RoundResolution resolution =
                engine.State.LastRoundResolution;

            Assert.That(resolution, Is.Not.Null);

            Assert.That(
                resolution.PlayerEffects
                    .ScoreContributions.Count,
                Is.EqualTo(1));

            Assert.That(
                resolution.EnemyEffects
                    .ScoreContributions.Count,
                Is.EqualTo(1));

            Assert.That(
                resolution.PlayerEffects
                    .ScoreContributions[0]
                    .SourceId,
                Is.EqualTo(
                    "test:flat-bonus:Player"));

            Assert.That(
                resolution.EnemyEffects
                    .ScoreContributions[0]
                    .SourceId,
                Is.EqualTo(
                    "test:flat-bonus:Enemy"));

            Assert.That(
                resolution.PlayerScore
                    .Breakdown.FlatPoints,
                Is.EqualTo(3m));

            Assert.That(
                resolution.EnemyScore
                    .Breakdown.FlatPoints,
                Is.EqualTo(3m));

            Assert.That(
                resolution.PlayerEffects.BoardVersion,
                Is.EqualTo(fullBoard.Version));

            Assert.That(
                resolution.EnemyEffects.BoardVersion,
                Is.EqualTo(fullBoard.Version));
        }

        private static BoardState CreateFullDrawBoard()
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

        private sealed class FlatBonusHandler :
            IGameEffectHandler
        {
            public string HandlerId =>
                "test.flat-bonus";

            public EffectEventKind EventKind =>
                EffectEventKind.ScoreRequested;

            public int Priority => 0;

            public bool CanHandle(
                EffectContext context)
            {
                return true;
            }

            public EffectOutput Resolve(
                EffectContext context)
            {
                return new EffectOutput(
                    new[]
                    {
                        ScoreContribution.CreateFlatPoints(
                            $"test:flat-bonus:{context.Participant}",
                            "Bônus de teste",
                            context.Participant,
                            context.Participant,
                            3m)
                    });
            }
        }
    }
}
