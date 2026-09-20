using System;
using System.Collections.Generic;
using NUnit.Framework;
using TicTacToeRoguelike.Domain.Boards;
using TicTacToeRoguelike.Domain.Effects;
using TicTacToeRoguelike.Domain.Runes;
using TicTacToeRoguelike.Domain.Scoring;

namespace TicTacToeRoguelike.Tests.Effects
{
    public sealed class EffectEngineTests
    {
        [Test]
        public void Resolve_OrdersHandlersByPriorityThenStableId()
        {
            BoardState board = CreateBoard();

            IGameEffectHandler[] unordered =
            {
                new TestHandler("z-last", 10),
                new TestHandler("b-second", 0),
                new TestHandler("a-first", 0)
            };

            EffectEngine engine =
                new EffectEngine(unordered);

            EffectExecutionReport report =
                engine.Resolve(
                    CreateContext(board));

            Assert.That(report.Steps.Count, Is.EqualTo(3));
            Assert.That(
                report.Steps[0].HandlerId,
                Is.EqualTo("a-first"));
            Assert.That(
                report.Steps[1].HandlerId,
                Is.EqualTo("b-second"));
            Assert.That(
                report.Steps[2].HandlerId,
                Is.EqualTo("z-last"));

            Assert.That(
                report.ScoreContributions[0].SourceId,
                Is.EqualTo("effect:a-first"));
            Assert.That(
                report.ScoreContributions[1].SourceId,
                Is.EqualTo("effect:b-second"));
            Assert.That(
                report.ScoreContributions[2].SourceId,
                Is.EqualTo("effect:z-last"));
        }

        [Test]
        public void Resolve_IsIndependentFromRegistrationOrder()
        {
            BoardState board = CreateBoard();

            EffectEngine first =
                new EffectEngine(
                    new IGameEffectHandler[]
                    {
                        new TestHandler("beta", 0),
                        new TestHandler("alpha", 0)
                    });

            EffectEngine second =
                new EffectEngine(
                    new IGameEffectHandler[]
                    {
                        new TestHandler("alpha", 0),
                        new TestHandler("beta", 0)
                    });

            EffectExecutionReport firstReport =
                first.Resolve(CreateContext(board));

            EffectExecutionReport secondReport =
                second.Resolve(CreateContext(board));

            Assert.That(
                firstReport.ScoreContributions.Count,
                Is.EqualTo(
                    secondReport.ScoreContributions.Count));

            for (int i = 0;
                 i < firstReport.ScoreContributions.Count;
                 i++)
            {
                Assert.That(
                    firstReport.ScoreContributions[i].SourceId,
                    Is.EqualTo(
                        secondReport.ScoreContributions[i].SourceId));
            }
        }

        [Test]
        public void Resolve_OnlyRunsHandlersForRequestedEvent()
        {
            BoardState board = CreateBoard();

            EffectEngine engine =
                new EffectEngine(
                    new IGameEffectHandler[]
                    {
                        new TestHandler(
                            "turn",
                            0,
                            EffectEventKind.TurnStarted)
                    });

            EffectExecutionReport report =
                engine.Resolve(CreateContext(board));

            Assert.That(report.Steps, Is.Empty);
            Assert.That(
                report.ScoreContributions,
                Is.Empty);
        }

        [Test]
        public void Constructor_RejectsDuplicateHandlerIds()
        {
            Assert.Throws<ArgumentException>(
                () => new EffectEngine(
                    new IGameEffectHandler[]
                    {
                        new TestHandler("duplicate", 0),
                        new TestHandler("duplicate", 5)
                    }));
        }

        [Test]
        public void Resolve_RejectsContributionForOtherParticipant()
        {
            BoardState board = CreateBoard();

            EffectEngine engine =
                new EffectEngine(
                    new[]
                    {
                        new WrongTargetHandler()
                    });

            Assert.Throws<InvalidOperationException>(
                () => engine.Resolve(
                    CreateContext(board)));
        }

        [Test]
        public void HandlerReceivesIndependentBoardSnapshot()
        {
            BoardState authoritative = CreateBoard();

            TestHandler handler =
                new TestHandler("snapshot", 0);

            EffectEngine engine =
                new EffectEngine(
                    new[] { handler });

            EffectExecutionReport report =
                engine.Resolve(
                    CreateContext(authoritative));

            Assert.That(report.HadApplicableEffects, Is.True);
            Assert.That(
                handler.LastBoardSeen,
                Is.Not.Null);
            Assert.That(
                handler.LastBoardSeen,
                Is.Not.SameAs(authoritative));
            Assert.That(
                handler.LastBoardSeen.Version,
                Is.EqualTo(authoritative.Version));
        }

        [Test]
        public void NonApplicableHandler_IsReportedWithoutOutput()
        {
            EffectEngine engine =
                new EffectEngine(
                    new[]
                    {
                        new TestHandler(
                            "conditional",
                            0,
                            EffectEventKind.ScoreRequested,
                            canHandle: false)
                    });

            EffectExecutionReport report =
                engine.Resolve(
                    CreateContext(CreateBoard()));

            Assert.That(report.Steps.Count, Is.EqualTo(1));
            Assert.That(
                report.Steps[0].WasApplicable,
                Is.False);
            Assert.That(
                report.ScoreContributions,
                Is.Empty);
        }

        private static EffectContext CreateContext(
            BoardState board)
        {
            return new EffectContext(
                EffectEventKind.ScoreRequested,
                ScoreActor.Player,
                board,
                Array.Empty<RuneInstance>(),
                Array.Empty<RuneInstance>(),
                roundNumber: 1,
                isWinner: false);
        }

        private static BoardState CreateBoard()
        {
            return new BoardState(
                BoardDefinition.CreateRectangular(
                    3,
                    3));
        }

        private sealed class TestHandler :
            IGameEffectHandler
        {
            private readonly bool _canHandle;

            public string HandlerId { get; }
            public EffectEventKind EventKind { get; }
            public int Priority { get; }
            public BoardState LastBoardSeen { get; private set; }

            public TestHandler(
                string handlerId,
                int priority,
                EffectEventKind eventKind =
                    EffectEventKind.ScoreRequested,
                bool canHandle = true)
            {
                HandlerId = handlerId;
                Priority = priority;
                EventKind = eventKind;
                _canHandle = canHandle;
            }

            public bool CanHandle(
                EffectContext context)
            {
                LastBoardSeen =
                    context.BoardSnapshot;

                return _canHandle;
            }

            public EffectOutput Resolve(
                EffectContext context)
            {
                LastBoardSeen =
                    context.BoardSnapshot;

                return new EffectOutput(
                    new[]
                    {
                        ScoreContribution.CreateFlatPoints(
                            $"effect:{HandlerId}",
                            HandlerId,
                            context.Participant,
                            context.Participant,
                            1m)
                    });
            }
        }

        private sealed class WrongTargetHandler :
            IGameEffectHandler
        {
            public string HandlerId => "wrong-target";
            public EffectEventKind EventKind =>
                EffectEventKind.ScoreRequested;
            public int Priority => 0;

            public bool CanHandle(
                EffectContext context) => true;

            public EffectOutput Resolve(
                EffectContext context)
            {
                ScoreActor other =
                    context.Participant ==
                    ScoreActor.Player
                        ? ScoreActor.Enemy
                        : ScoreActor.Player;

                return new EffectOutput(
                    new[]
                    {
                        ScoreContribution.CreateFlatPoints(
                            "effect:wrong-target",
                            "Wrong target",
                            context.Participant,
                            other,
                            5m)
                    });
            }
        }
    }
}
