using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TicTacToeRoguelike.Application.Actions;
using TicTacToeRoguelike.Application.Effects;
using TicTacToeRoguelike.Application.Encounters;
using TicTacToeRoguelike.Domain.Actions;
using TicTacToeRoguelike.Domain.Boards;
using TicTacToeRoguelike.Domain.Combat;
using TicTacToeRoguelike.Domain.Effects;
using TicTacToeRoguelike.Domain.Effects.Capabilities;
using TicTacToeRoguelike.Domain.Moves;
using TicTacToeRoguelike.Domain.Reactions;
using TicTacToeRoguelike.Domain.Runes;
using TicTacToeRoguelike.Domain.Scoring;
using TicTacToeRoguelike.Infrastructure.Random;

namespace TicTacToeRoguelike.Tests.Effects
{
    public sealed class Marco11CapabilityIntegrationTests
    {
        [TestCase(ScoreActor.Player)]
        [TestCase(ScoreActor.Enemy)]
        public void ClearCell_OnFullBoard_UsesCommonCatalogForBothSides(ScoreActor actor)
        {
            var playerRunes = new RuneInventoryState(ScoreActor.Player);
            var enemyRunes = new RuneInventoryState(ScoreActor.Enemy);
            RuneInventoryState owner = actor == ScoreActor.Player ? playerRunes : enemyRunes;
            RuneInstance source = AddRune(owner, ClearCellRuneActions.PilotDefinitionId, $"clear:{actor}");

            var effects = new EncounterEffects(seed: 11);
            Fixture fixture = CreateFixture(playerRunes, enemyRunes, effects);
            var target = new BoardCoordinate(0, 0);
            var board = new BoardState(
                BoardDefinition.CreateRectangular(1, 1),
                new[] { new CellState(target, CellMark.X) });

            fixture.Engine.StartEncounter(
                board,
                actor == ScoreActor.Player ? EncounterSide.Player : EncounterSide.Enemy);

            Assert.That(fixture.Engine.State.Phase, Is.EqualTo(EncounterPhase.WaitingForAction));

            IReadOnlyList<GameAction> legal = fixture.Catalog.GetAvailableActions(
                board,
                fixture.Engine.State.CurrentTurn,
                actor == ScoreActor.Player ? GameActionOrigin.PlayerInput : GameActionOrigin.AgentPolicy);

            ClearCellAction clear = legal
                .OfType<ClearCellAction>()
                .Single(action => action.Target == target && action.SourceInstanceId == source.InstanceId);

            ActionResult result = fixture.Engine.ExecuteAction(clear);

            Assert.That(result.WasApplied, Is.True);
            Assert.That(board.GetCell(target).Marks, Is.EqualTo(CellMark.None));
            Assert.That(effects.ActionUsage.Used(actor, source.InstanceId), Is.EqualTo(1));
            Assert.That(fixture.Engine.State.Phase, Is.EqualTo(EncounterPhase.WaitingForAction));
            Assert.That(fixture.Engine.State.CurrentActor, Is.Not.EqualTo(actor));

            ExecuteCurrentMove(fixture.Engine, target);

            Assert.That(fixture.Engine.State.Phase, Is.EqualTo(EncounterPhase.RoundResolved));
            Assert.That(
                fixture.Engine.State.LastReactionDecision.Kind,
                Is.EqualTo(ReactionDecisionKind.DrawNoActions));
            Assert.That(effects.ActionUsage.Used(actor, source.InstanceId), Is.EqualTo(1));
        }

        [Test]
        public void ClearCell_ReevaluatesReactionAfterRemovingASequence()
        {
            var playerRunes = new RuneInventoryState(ScoreActor.Player);
            var enemyRunes = new RuneInventoryState(ScoreActor.Enemy);
            RuneInstance source = AddRune(
                enemyRunes,
                ClearCellRuneActions.PilotDefinitionId,
                "clear:enemy:reaction");

            var effects = new EncounterEffects(seed: 111);
            Fixture fixture = CreateFixture(playerRunes, enemyRunes, effects);
            BoardState board = CreateBoard(
                3,
                3,
                Cell(0, 0, CellMark.X),
                Cell(1, 0, CellMark.X),
                Cell(2, 0, CellMark.X));

            fixture.Engine.StartEncounter(board, EncounterSide.Enemy);

            ClearCellAction clear = fixture.Catalog.GetAvailableActions(
                    board,
                    fixture.Engine.State.CurrentTurn,
                    GameActionOrigin.AgentPolicy)
                .OfType<ClearCellAction>()
                .Single(action =>
                    action.SourceInstanceId == source.InstanceId &&
                    action.Target == new BoardCoordinate(1, 0));

            fixture.Engine.ExecuteAction(clear);

            Assert.That(
                fixture.Engine.State.LastReactionDecision.Kind,
                Is.EqualTo(ReactionDecisionKind.ReactionResolvedByTie));
            Assert.That(fixture.Engine.State.ReactionState.IsTied, Is.True);
            Assert.That(fixture.Engine.State.CurrentActor, Is.EqualTo(ScoreActor.Player));
        }

        [Test]
        public void AdditionalActions_DefersReactionUntilTheWholeTurnEnds()
        {
            var playerRunes = new RuneInventoryState(ScoreActor.Player);
            var enemyRunes = new RuneInventoryState(ScoreActor.Enemy);
            AddRune(playerRunes, "test.two-actions", "two-actions:player");

            var handler = new TestCapabilityHandler<TurnEffectContext, TurnPlanModifier>(
                "handler.two-actions",
                "test.two-actions",
                EffectTrigger.TurnOpening,
                (context, source, random) =>
                    context.ProposedActor == source.Owner
                        ? new[] { new TurnPlanModifier(TurnModifierKind.AdditionalActions, source.Owner, 1) }
                        : Array.Empty<TurnPlanModifier>());

            var effects = new EncounterEffects(seed: 12, turnHandlers: new[] { handler });
            Fixture fixture = CreateFixture(playerRunes, enemyRunes, effects);
            BoardState board = CreateBoard(
                3,
                3,
                Cell(0, 0, CellMark.X),
                Cell(1, 0, CellMark.X));

            fixture.Engine.StartEncounter(board, EncounterSide.Player);

            Assert.That(fixture.Engine.State.CurrentTurn.ActionBudget, Is.EqualTo(2));

            ExecuteCurrentMove(fixture.Engine, new BoardCoordinate(2, 0));

            Assert.That(fixture.Engine.State.LastReactionDecision, Is.Null);
            Assert.That(fixture.Engine.State.CurrentActor, Is.EqualTo(ScoreActor.Player));
            Assert.That(fixture.Engine.State.CurrentTurn.ActionsConsumed, Is.EqualTo(1));

            ExecuteCurrentMove(fixture.Engine, new BoardCoordinate(0, 1));

            Assert.That(
                fixture.Engine.State.LastReactionDecision.Kind,
                Is.EqualTo(ReactionDecisionKind.ReactionStarted));
            Assert.That(fixture.Engine.State.CurrentActor, Is.EqualTo(ScoreActor.Enemy));
        }

        [Test]
        public void ExtraTurn_DoesNotRescueResponderWhoAlreadyFailedReaction()
        {
            var playerRunes = new RuneInventoryState(ScoreActor.Player);
            var enemyRunes = new RuneInventoryState(ScoreActor.Enemy);
            AddRune(enemyRunes, "test.extra-turn", "extra-turn:enemy");

            var handler = new TestCapabilityHandler<TurnEffectContext, TurnPlanModifier>(
                "handler.extra-turn",
                "test.extra-turn",
                EffectTrigger.TurnCompleted,
                (context, source, random) =>
                    context.Encounter.Actor == source.Owner
                        ? new[] { new TurnPlanModifier(TurnModifierKind.ExtraTurn, source.Owner) }
                        : Array.Empty<TurnPlanModifier>());

            var effects = new EncounterEffects(seed: 13, turnHandlers: new[] { handler });
            Fixture fixture = CreateFixture(playerRunes, enemyRunes, effects);
            BoardState board = CreateBoard(
                3,
                3,
                Cell(0, 0, CellMark.X),
                Cell(1, 0, CellMark.X),
                Cell(2, 0, CellMark.X));

            fixture.Engine.StartEncounter(board, EncounterSide.Enemy);
            ExecuteCurrentMove(fixture.Engine, new BoardCoordinate(0, 2));

            Assert.That(fixture.Engine.State.Phase, Is.EqualTo(EncounterPhase.RoundResolved));
            Assert.That(
                fixture.Engine.State.LastReactionDecision.EndReason,
                Is.EqualTo(ReactionEndReason.ResponderRemainedBehind));
            Assert.That(
                fixture.Engine.State.LastRoundResult.Outcome,
                Is.EqualTo(RoundOutcome.PlayerVictory));
        }

        [Test]
        public void SkipOpponent_DoesNotConsumeTheRespondersEffectiveDefenseChance()
        {
            var playerRunes = new RuneInventoryState(ScoreActor.Player);
            var enemyRunes = new RuneInventoryState(ScoreActor.Enemy);
            AddRune(playerRunes, "test.skip-opponent", "skip:player");

            var handler = new TestCapabilityHandler<TurnEffectContext, TurnPlanModifier>(
                "handler.skip-opponent",
                "test.skip-opponent",
                EffectTrigger.TurnCompleted,
                (context, source, random) =>
                    context.Encounter.Actor == source.Owner
                        ? new[] { new TurnPlanModifier(TurnModifierKind.SkipOpponent, source.Owner) }
                        : Array.Empty<TurnPlanModifier>());

            var effects = new EncounterEffects(seed: 14, turnHandlers: new[] { handler });
            Fixture fixture = CreateFixture(playerRunes, enemyRunes, effects);
            BoardState board = CreateBoard(
                3,
                3,
                Cell(0, 0, CellMark.X),
                Cell(1, 0, CellMark.X));

            fixture.Engine.StartEncounter(board, EncounterSide.Player);
            ExecuteCurrentMove(fixture.Engine, new BoardCoordinate(2, 0));

            Assert.That(
                fixture.Engine.State.LastReactionDecision.Kind,
                Is.EqualTo(ReactionDecisionKind.ReactionStarted));
            Assert.That(fixture.Engine.LastSkippedTurn, Is.Not.Null);
            Assert.That(fixture.Engine.LastSkippedTurn.Actor, Is.EqualTo(ScoreActor.Enemy));
            Assert.That(fixture.Engine.LastSkippedTurn.CanEvaluateReaction, Is.False);
            Assert.That(fixture.Engine.State.CurrentActor, Is.EqualTo(ScoreActor.Player));

            ExecuteCurrentMove(fixture.Engine, new BoardCoordinate(0, 1));

            Assert.That(
                fixture.Engine.State.LastReactionDecision.Kind,
                Is.EqualTo(ReactionDecisionKind.ReactionMaintained));
            Assert.That(
                fixture.Engine.State.ReactionState.PendingResponder,
                Is.EqualTo(ScoreActor.Enemy));
            Assert.That(fixture.Engine.State.CurrentActor, Is.EqualTo(ScoreActor.Enemy));
        }

        [Test]
        public void PassiveDamage_UsesTypedIncreaseAndPreventionAndOwnerCombatService()
        {
            var playerRunes = new RuneInventoryState(ScoreActor.Player);
            var enemyRunes = new RuneInventoryState(ScoreActor.Enemy);
            AddRune(playerRunes, "test.passive-damage", "passive:player");
            AddRune(playerRunes, "test.attack-up", "attack-up:player");
            AddRune(enemyRunes, "test.guard", "guard:enemy");

            var direct = new TestCapabilityHandler<EncounterEffectContext, DirectDamageCommand>(
                "handler.passive-damage",
                "test.passive-damage",
                EffectTrigger.AfterAction,
                (context, source, random) =>
                    context.Actor == source.Owner
                        ? new[]
                        {
                            new DirectDamageCommand(
                                source.Owner == ScoreActor.Player ? ScoreActor.Enemy : ScoreActor.Player,
                                5)
                        }
                        : Array.Empty<DirectDamageCommand>());

            var attack = new TestCapabilityHandler<DamageEffectContext, DamageContribution>(
                "handler.attack-up",
                "test.attack-up",
                EffectTrigger.DamageRequested,
                (context, source, random) =>
                    context.Source == source.Owner
                        ? new[] { new DamageContribution(DamageContributionKind.Increase, 2) }
                        : Array.Empty<DamageContribution>());

            var guard = new TestCapabilityHandler<DamageEffectContext, DamageContribution>(
                "handler.guard",
                "test.guard",
                EffectTrigger.DamageRequested,
                (context, source, random) =>
                    context.Target == source.Owner
                        ? new[] { new DamageContribution(DamageContributionKind.Prevent, 3) }
                        : Array.Empty<DamageContribution>());

            var effects = new EncounterEffects(
                seed: 15,
                damageHandlers: new[] { attack, guard },
                directHandlers: new[] { direct });

            Fixture fixture = CreateFixture(playerRunes, enemyRunes, effects);
            fixture.Engine.StartEncounter(new BoardState(BoardDefinition.CreateRectangular(3, 3)), EncounterSide.Player);

            ExecuteCurrentMove(fixture.Engine, new BoardCoordinate(0, 0));

            DirectDamageReport report = effects.DamageReports.Single();
            Assert.That(report.RawDamage, Is.EqualTo(5));
            Assert.That(report.IncreasedDamage, Is.EqualTo(2));
            Assert.That(report.PreventedDamage, Is.EqualTo(3));
            Assert.That(report.HealthDamage, Is.EqualTo(4));
            Assert.That(fixture.Engine.State.EnemyState.CurrentHealth, Is.EqualTo(96));
            Assert.That(report.Source.InstanceId, Is.EqualTo("passive:player"));
        }

        [Test]
        public void RuneMutation_PreservesProvenanceAndBrokenRemovalReason()
        {
            var playerRunes = new RuneInventoryState(ScoreActor.Player);
            var enemyRunes = new RuneInventoryState(ScoreActor.Enemy);
            RuneInstance sourceRune = AddRune(playerRunes, "test.mutator", "mutator:player");
            RuneInstance targetRune = AddRune(playerRunes, "test.target", "target:player");

            var addAttribute = new TestCapabilityHandler<RuneEffectContext, RuneCommand>(
                "handler.add-blessed",
                sourceRune.Definition.Id.Value,
                EffectTrigger.AfterAction,
                (context, source, random) =>
                    new RuneCommand[]
                    {
                        new ChangeRuneAttributeCommand(
                            ScoreActor.Player,
                            targetRune.InstanceId,
                            RuneAttributeId.Blessed,
                            add: true)
                    });

            var removeOnChange = new TestCapabilityHandler<RuneEffectContext, RuneCommand>(
                "handler.break-target",
                sourceRune.Definition.Id.Value,
                EffectTrigger.RuneChanged,
                (context, source, random) =>
                    context.Change != null &&
                    context.Change.Command.Target == targetRune.InstanceId
                        ? new RuneCommand[]
                        {
                            new RemoveRuneCommand(
                                ScoreActor.Player,
                                targetRune.InstanceId,
                                RuneRemovalReason.Broken)
                        }
                        : Array.Empty<RuneCommand>());

            var effects = new EncounterEffects(
                seed: 16,
                runeHandlers: new[] { addAttribute, removeOnChange });

            Fixture fixture = CreateFixture(playerRunes, enemyRunes, effects);
            fixture.Engine.StartEncounter(new BoardState(BoardDefinition.CreateRectangular(3, 3)), EncounterSide.Player);

            ExecuteCurrentMove(fixture.Engine, new BoardCoordinate(0, 0));

            Assert.That(playerRunes.Contains(targetRune.InstanceId), Is.False);
            Assert.That(effects.RuneReports.Count, Is.EqualTo(2));
            Assert.That(effects.RuneReports[0].Source.InstanceId, Is.EqualTo(sourceRune.InstanceId.Value));
            Assert.That(effects.RuneReports[0].Status, Is.EqualTo(RuneCommandStatus.Applied));
            Assert.That(effects.RuneReports[1].Removal, Is.Not.Null);
            Assert.That(effects.RuneReports[1].Removal.Reason, Is.EqualTo(RuneRemovalReason.Broken));
        }

        [Test]
        public void CapabilityPipeline_RepeatedEventIsExecutedOnlyOnce()
        {
            var playerRunes = new RuneInventoryState(ScoreActor.Player);
            var enemyRunes = new RuneInventoryState(ScoreActor.Enemy);
            AddRune(playerRunes, "test.once", "once:player");

            var journal = new EffectExecutionJournal();
            var random = new EffectRandom(new SeededRandomSource(17));
            var handler = new TestCapabilityHandler<TurnEffectContext, TurnPlanModifier>(
                "handler.once",
                "test.once",
                EffectTrigger.TurnOpening,
                (context, source, rng) =>
                    new[] { new TurnPlanModifier(TurnModifierKind.AdditionalActions, source.Owner, 1) });
            var pipeline = new CapabilityPipeline<TurnEffectContext, TurnPlanModifier>(
                new[] { handler },
                journal,
                random);

            EffectEvent evt = new EffectEvent("event:repeat", EffectTrigger.TurnOpening);
            BoardState board = new BoardState(BoardDefinition.CreateRectangular(3, 3));
            var snapshot = new EncounterEffectContext(
                evt,
                1,
                1,
                ScoreActor.Player,
                board,
                new CombatantState("player", ScoreActor.Player, 100),
                new CombatantState("enemy", ScoreActor.Enemy, 100),
                playerRunes,
                enemyRunes);
            var context = new TurnEffectContext(snapshot, ScoreActor.Player, 1);

            Assert.That(pipeline.Resolve(context, snapshot).Count, Is.EqualTo(1));
            Assert.That(pipeline.Resolve(context, snapshot).Count, Is.Zero);
            Assert.That(journal.Steps.Select(step => step.Status),
                Is.EqualTo(new[] { EffectExecutionStatus.Applied, EffectExecutionStatus.Duplicate }));
        }

        [Test]
        public void RuneChangedCycle_IsSuppressedBeforeItCanLoop()
        {
            var playerRunes = new RuneInventoryState(ScoreActor.Player);
            var enemyRunes = new RuneInventoryState(ScoreActor.Enemy);
            RuneInstance sourceRune = AddRune(playerRunes, "test.cycle", "cycle:player");

            var start = new TestCapabilityHandler<RuneEffectContext, RuneCommand>(
                "handler.cycle-start",
                sourceRune.Definition.Id.Value,
                EffectTrigger.AfterAction,
                (context, source, random) =>
                    new RuneCommand[]
                    {
                        new ChangeRuneAttributeCommand(
                            ScoreActor.Player,
                            sourceRune.InstanceId,
                            RuneAttributeId.Blessed,
                            add: true)
                    });

            var toggle = new TestCapabilityHandler<RuneEffectContext, RuneCommand>(
                "handler.cycle-toggle",
                sourceRune.Definition.Id.Value,
                EffectTrigger.RuneChanged,
                (context, source, random) =>
                {
                    bool isBlessed = context.Encounter.PlayerRunes
                        .Single(rune => rune.InstanceId == sourceRune.InstanceId)
                        .Attributes.Contains(RuneAttributeId.Blessed);

                    return new RuneCommand[]
                    {
                        new ChangeRuneAttributeCommand(
                            ScoreActor.Player,
                            sourceRune.InstanceId,
                            RuneAttributeId.Blessed,
                            add: !isBlessed)
                    };
                });

            var effects = new EncounterEffects(
                seed: 18,
                runeHandlers: new[] { start, toggle },
                maximumChainDepth: 16);

            Fixture fixture = CreateFixture(playerRunes, enemyRunes, effects);
            fixture.Engine.StartEncounter(new BoardState(BoardDefinition.CreateRectangular(3, 3)), EncounterSide.Player);

            ExecuteCurrentMove(fixture.Engine, new BoardCoordinate(0, 0));

            Assert.That(
                effects.Journal.Steps.Any(step => step.Status == EffectExecutionStatus.CycleSuppressed),
                Is.True);
            Assert.That(effects.RuneReports.Count, Is.EqualTo(2));
        }

        [Test]
        public void CapabilityPipeline_StopsEventsAtConfiguredChainDepth()
        {
            var playerRunes = new RuneInventoryState(ScoreActor.Player);
            var enemyRunes = new RuneInventoryState(ScoreActor.Enemy);
            AddRune(playerRunes, "test.depth", "depth:player");

            var journal = new EffectExecutionJournal(maximumDepth: 1);
            var random = new EffectRandom(new SeededRandomSource(19));
            var handler = new TestCapabilityHandler<TurnEffectContext, TurnPlanModifier>(
                "handler.depth",
                "test.depth",
                EffectTrigger.TurnOpening,
                (context, source, rng) =>
                    new[] { new TurnPlanModifier(TurnModifierKind.AdditionalActions, source.Owner, 1) });
            var pipeline = new CapabilityPipeline<TurnEffectContext, TurnPlanModifier>(
                new[] { handler },
                journal,
                random);

            EffectEvent evt = new EffectEvent("event:deep", EffectTrigger.TurnOpening, "root", depth: 1);
            BoardState board = new BoardState(BoardDefinition.CreateRectangular(3, 3));
            var snapshot = new EncounterEffectContext(
                evt,
                1,
                1,
                ScoreActor.Player,
                board,
                new CombatantState("player", ScoreActor.Player, 100),
                new CombatantState("enemy", ScoreActor.Enemy, 100),
                playerRunes,
                enemyRunes);

            IReadOnlyList<SourcedEffect<TurnPlanModifier>> result =
                pipeline.Resolve(new TurnEffectContext(snapshot, ScoreActor.Player, 1), snapshot);

            Assert.That(result, Is.Empty);
            Assert.That(journal.Steps.Single().Status, Is.EqualTo(EffectExecutionStatus.ChainLimit));
        }

        [Test]
        public void SeededRandomAndRuneInstanceSequence_AreReproducibleAndLogged()
        {
            var evt = new EffectEvent("event:random", EffectTrigger.AfterAction);
            var source = new EffectSource(
                ScoreActor.Player,
                "test.random",
                "random:player",
                "handler.random");

            var first = new EffectRandom(new SeededRandomSource(123456));
            var second = new EffectRandom(new SeededRandomSource(123456));

            int firstRoll = first.Roll(evt, source, "chance", 0, 1000);
            int secondRoll = second.Roll(evt, source, "chance", 0, 1000);

            Assert.That(firstRoll, Is.EqualTo(secondRoll));
            Assert.That(first.Rolls.Single().Seed, Is.EqualTo(123456UL));
            Assert.That(first.Rolls.Single().Value, Is.EqualTo(firstRoll));
            Assert.That(first.Rolls.Single().Stream, Is.EqualTo(second.Rolls.Single().Stream));

            var idsA = new RuneInstanceIdSequence("run:123456");
            var idsB = new RuneInstanceIdSequence("run:123456");

            string[] sequenceA = { idsA.Next().Value, idsA.Next().Value, idsA.Next().Value };
            string[] sequenceB = { idsB.Next().Value, idsB.Next().Value, idsB.Next().Value };

            Assert.That(sequenceA, Is.EqualTo(sequenceB));
        }

        private static Fixture CreateFixture(
            RuneInventoryState playerRunes,
            RuneInventoryState enemyRunes,
            EncounterEffects effects)
        {
            var validator = new MoveValidator();
            var moveService = new MoveService(validator);
            var clear = new ClearCellRuneActions(playerRunes, enemyRunes, effects.ActionUsage);
            var catalog = new ActionCatalog(
                new IGameActionProvider[]
                {
                    new NormalMoveActionProvider(validator),
                    clear
                },
                new IGameActionHandler[]
                {
                    new PlaceMarkActionHandler(moveService),
                    clear
                });

            var engine = new EncounterEngine(
                new CombatantState("player", ScoreActor.Player, 100),
                new CombatantState("enemy", ScoreActor.Enemy, 100),
                CreateRules(),
                playerRunes,
                enemyRunes,
                new EffectEngine(),
                ActionExecutor.CreateWithCatalog(catalog),
                new ActionAvailabilityService(catalog),
                new ReactionStateFactory(),
                new ReactionRule(),
                new ScorePipeline(),
                new ClashResolver(),
                new DamageResolver(),
                new AlternatingEncounterTurnScheduler(),
                effects);

            return new Fixture(engine, catalog);
        }

        private static EncounterRules CreateRules()
        {
            return new EncounterRules(
                requiredSequenceLength: 3,
                minimumScoringSequenceLength: 2,
                maximumScoringSequenceLength: 3,
                victoryMultiplier: 2m);
        }

        private static RuneInstance AddRune(
            RuneInventoryState inventory,
            string definitionId,
            string instanceId)
        {
            var rune = new RuneInstance(
                new RuneInstanceId(instanceId),
                new RuneDefinition(
                    new RuneDefinitionId(definitionId),
                    $"Rune {definitionId}",
                    "Synthetic Marco 11 test rune",
                    RuneRarity.Common));

            Assert.That(inventory.TryAdd(rune), Is.EqualTo(RuneInventoryAddResult.Added));
            return rune;
        }

        private static ActionResult ExecuteCurrentMove(
            EncounterEngine engine,
            BoardCoordinate coordinate)
        {
            ScoreActor actor = engine.State.CurrentTurn.Actor;
            var action = new PlaceMarkAction(
                actor,
                actor == ScoreActor.Player ? GameActionOrigin.PlayerInput : GameActionOrigin.AgentPolicy,
                engine.State.CurrentTurn.TurnId,
                engine.State.Board.Version,
                coordinate,
                actor == ScoreActor.Player ? CellMark.X : CellMark.O);

            return engine.ExecuteAction(action);
        }

        private static BoardState CreateBoard(
            int width,
            int height,
            params CellState[] cells)
        {
            return new BoardState(
                BoardDefinition.CreateRectangular(width, height),
                cells);
        }

        private static CellState Cell(int x, int y, CellMark marks)
        {
            return new CellState(new BoardCoordinate(x, y), marks);
        }

        private sealed class Fixture
        {
            public EncounterEngine Engine { get; }
            public ActionCatalog Catalog { get; }

            public Fixture(EncounterEngine engine, ActionCatalog catalog)
            {
                Engine = engine;
                Catalog = catalog;
            }
        }

        private sealed class TestCapabilityHandler<TContext, TOutput> :
            ICapabilityHandler<TContext, TOutput>
        {
            private readonly Func<TContext, EffectSource, EffectRandom, IReadOnlyList<TOutput>> _resolve;

            public string HandlerId { get; }
            public string DefinitionId { get; }
            public int Priority { get; }
            public EffectTrigger Trigger { get; }

            public TestCapabilityHandler(
                string handlerId,
                string definitionId,
                EffectTrigger trigger,
                Func<TContext, EffectSource, EffectRandom, IReadOnlyList<TOutput>> resolve,
                int priority = 0)
            {
                HandlerId = handlerId;
                DefinitionId = definitionId;
                Trigger = trigger;
                Priority = priority;
                _resolve = resolve ?? throw new ArgumentNullException(nameof(resolve));
            }

            public IReadOnlyList<TOutput> Resolve(
                TContext context,
                EffectSource source,
                EffectRandom random)
            {
                return _resolve(context, source, random);
            }
        }
    }
}
