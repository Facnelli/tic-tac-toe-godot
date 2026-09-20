using System;
using System.Collections.Generic;
using NUnit.Framework;
using TicTacToeRoguelike.Application.Effects;
using TicTacToeRoguelike.Domain.Boards;
using TicTacToeRoguelike.Domain.Effects;
using TicTacToeRoguelike.Domain.Effects.Runes;
using TicTacToeRoguelike.Domain.Runes;
using TicTacToeRoguelike.Domain.Scoring;

namespace TicTacToeRoguelike.Tests.Effects
{
    public sealed class PilotIndependentMultiplierRuneHandlerTests
    {
        [TestCase(ScoreActor.Player)]
        [TestCase(ScoreActor.Enemy)]
        public void Resolve_WithPilotRune_ProducesIndependentMultiplier(
            ScoreActor actor)
        {
            RuneInstance rune =
                CreatePilotRune("instance-001");

            EffectContext context =
                CreateContext(
                    actor,
                    new[] { rune });

            PilotIndependentMultiplierRuneHandler handler =
                new PilotIndependentMultiplierRuneHandler();

            Assert.That(
                handler.CanHandle(context),
                Is.True);

            EffectOutput output =
                handler.Resolve(context);

            Assert.That(
                output.ScoreContributions.Count,
                Is.EqualTo(1));

            ScoreContribution contribution =
                output.ScoreContributions[0];

            Assert.That(
                contribution.Phase,
                Is.EqualTo(
                    ScorePhase.IndependentMultiplier));

            Assert.That(
                contribution.Amount,
                Is.EqualTo(1.20m));

            Assert.That(
                contribution.SourceOwner,
                Is.EqualTo(actor));

            Assert.That(
                contribution.Target,
                Is.EqualTo(actor));

            Assert.That(
                contribution.DisplayText,
                Is.EqualTo("Runa do Eco"));

            Assert.That(
                contribution.SourceId,
                Does.Contain(
                    RuneDefinitionIds
                        .PilotIndependentMultiplier
                        .Value));

            Assert.That(
                contribution.SourceId,
                Does.Contain("instance-001"));
        }

        [Test]
        public void Resolve_WithoutPilotRune_IsNotApplicable()
        {
            RuneDefinition otherDefinition =
                new RuneDefinition(
                    new RuneDefinitionId(
                        "rune.other"),
                    "Outra Runa",
                    "Sem efeito piloto.",
                    RuneRarity.Common);

            EffectContext context =
                CreateContext(
                    ScoreActor.Player,
                    new[]
                    {
                        new RuneInstance(
                            new RuneInstanceId(
                                "other-001"),
                            otherDefinition)
                    });

            PilotIndependentMultiplierRuneHandler handler =
                new PilotIndependentMultiplierRuneHandler();

            Assert.That(
                handler.CanHandle(context),
                Is.False);
        }

        [Test]
        public void TwoCopies_StackAsIndependentMultipliers()
        {
            RuneInstance second =
                CreatePilotRune("instance-002");

            RuneInstance first =
                CreatePilotRune("instance-001");

            EffectEngine engine =
                new EffectEngine(
                    new IGameEffectHandler[]
                    {
                        new PilotIndependentMultiplierRuneHandler()
                    });

            EffectExecutionReport report =
                engine.Resolve(
                    CreateContext(
                        ScoreActor.Player,
                        new[] { second, first }));

            Assert.That(
                report.ScoreContributions.Count,
                Is.EqualTo(2));

            Assert.That(
                report.ScoreContributions[0].SourceId,
                Does.Contain("instance-001"));

            Assert.That(
                report.ScoreContributions[1].SourceId,
                Does.Contain("instance-002"));

            List<ScoreContribution> contributions =
                new List<ScoreContribution>
                {
                    ScoreContribution.CreateBasePoints(
                        "test:base",
                        "Base",
                        ScoreActor.Player,
                        ScoreActor.Player,
                        10m)
                };

            contributions.AddRange(
                report.ScoreContributions);

            ScoreBreakdown breakdown =
                ScoreBreakdown.Resolve(
                    ScoreActor.Player,
                    contributions);

            Assert.That(
                breakdown.IndependentMultiplierProduct,
                Is.EqualTo(1.44m));

            Assert.That(
                breakdown.UnclampedScore,
                Is.EqualTo(14.40m));

            Assert.That(
                breakdown.FinalScore,
                Is.EqualTo(14));
        }

        [Test]
        public void DefaultFactory_RegistersPilotRuneHandler()
        {
            EffectEngine engine =
                DefaultEffectEngineFactory.Create();

            Assert.That(
                engine.Handlers.Count,
                Is.EqualTo(1));

            Assert.That(
                engine.Handlers[0],
                Is.TypeOf<
                    PilotIndependentMultiplierRuneHandler>());
        }

        private static EffectContext CreateContext(
            ScoreActor actor,
            IEnumerable<RuneInstance> ownRunes)
        {
            BoardState board =
                new BoardState(
                    BoardDefinition.CreateRectangular(
                        3,
                        3));

            return new EffectContext(
                EffectEventKind.ScoreRequested,
                actor,
                board,
                ownRunes,
                Array.Empty<RuneInstance>(),
                roundNumber: 1,
                isWinner: false);
        }

        private static RuneInstance CreatePilotRune(
            string instanceId)
        {
            RuneDefinition definition =
                new RuneDefinition(
                    RuneDefinitionIds
                        .PilotIndependentMultiplier,
                    "Runa do Eco",
                    "Passiva: MULT ×1,20.",
                    RuneRarity.Rare,
                    "ᛞ");

            return new RuneInstance(
                new RuneInstanceId(instanceId),
                definition);
        }
    }
}
