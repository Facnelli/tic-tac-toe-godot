using System;
using System.Collections.Generic;
using System.Linq;
using TicTacToeRoguelike.Application.Actions;
using TicTacToeRoguelike.Application.Encounters;
using TicTacToeRoguelike.Domain.Combat;
using TicTacToeRoguelike.Domain.Effects.Capabilities;
using TicTacToeRoguelike.Domain.Runes;
using TicTacToeRoguelike.Domain.Scoring;
using TicTacToeRoguelike.Infrastructure.Random;

namespace TicTacToeRoguelike.Application.Effects
{
    // One instance per encounter. Typed pipelines calculate; owner services mutate.
    public sealed class EncounterEffects
    {
        private readonly CapabilityPipeline<TurnEffectContext, TurnPlanModifier> _turns;
        private readonly CapabilityPipeline<DamageEffectContext, DamageContribution> _damage;
        private readonly CapabilityPipeline<EncounterEffectContext, DirectDamageCommand> _direct;
        private readonly CapabilityPipeline<RuneEffectContext, RuneCommand> _runes;
        private readonly EffectDamageService _damageService = new EffectDamageService();
        private readonly List<DirectDamageReport> _damageReports = new List<DirectDamageReport>();
        private readonly List<RuneCommandReport> _runeReports = new List<RuneCommandReport>();
        private readonly List<SourcedEffect<DamageContribution>> _damageContributions = new List<SourcedEffect<DamageContribution>>();
        private readonly List<SourcedEffect<TurnPlanModifier>> _turnModifiers = new List<SourcedEffect<TurnPlanModifier>>();
        private readonly HashSet<string> _opportunitiesUsed = new HashSet<string>(StringComparer.Ordinal);
        private long _nextEvent;
        private bool _bound;
        public EffectExecutionJournal Journal { get; }
        public EffectRandom Random { get; }
        public RuneActionUsage ActionUsage { get; }
        public IReadOnlyList<DirectDamageReport> DamageReports => _damageReports.AsReadOnly();
        public IReadOnlyList<RuneCommandReport> RuneReports => _runeReports.AsReadOnly();
        public IReadOnlyList<SourcedEffect<DamageContribution>> DamageContributions => _damageContributions.AsReadOnly();
        public IReadOnlyList<SourcedEffect<TurnPlanModifier>> TurnModifiers => _turnModifiers.AsReadOnly();
        public EncounterEffects(ulong seed = 1,
            IEnumerable<ICapabilityHandler<TurnEffectContext, TurnPlanModifier>> turnHandlers = null,
            IEnumerable<ICapabilityHandler<DamageEffectContext, DamageContribution>> damageHandlers = null,
            IEnumerable<ICapabilityHandler<EncounterEffectContext, DirectDamageCommand>> directHandlers = null,
            IEnumerable<ICapabilityHandler<RuneEffectContext, RuneCommand>> runeHandlers = null,
            RuneActionUsage actionUsage = null, int maximumChainDepth = 16)
        {
            Journal = new EffectExecutionJournal(maximumChainDepth);
            Random = new EffectRandom(new SeededRandomSource(seed));
            ActionUsage = actionUsage ?? new RuneActionUsage();
            _turns = new CapabilityPipeline<TurnEffectContext, TurnPlanModifier>(turnHandlers, Journal, Random);
            _damage = new CapabilityPipeline<DamageEffectContext, DamageContribution>(damageHandlers, Journal, Random);
            _direct = new CapabilityPipeline<EncounterEffectContext, DirectDamageCommand>(directHandlers, Journal, Random);
            _runes = new CapabilityPipeline<RuneEffectContext, RuneCommand>(runeHandlers, Journal, Random);
        }
        internal void Bind()
        { if (_bound) throw new InvalidOperationException("Effects session already belongs to an encounter."); _bound = true; }
        internal void BeginRound() { ActionUsage.BeginRound(); _opportunitiesUsed.Clear(); }
        private EffectEvent Next(EffectTrigger trigger) => new EffectEvent($"event:{checked(++_nextEvent):D10}", trigger);
        private static EncounterEffectContext Snapshot(EncounterState state, EffectEvent evt, ScoreActor actor, long turnId = 0) =>
            new EncounterEffectContext(evt, state.RoundNumber, turnId, actor, state.Board, state.PlayerState, state.EnemyState, state.PlayerRunes, state.EnemyRunes);
        public int OpeningBudget(EncounterState state, ScoreActor actor, int budget)
        {
            var snapshot = Snapshot(state, Next(EffectTrigger.TurnOpening), actor);
            var effects = _turns.Resolve(new TurnEffectContext(snapshot, actor, budget), snapshot);
            foreach (var effect in effects)
            {
                if (effect.Output.Kind != TurnModifierKind.AdditionalActions) throw new InvalidOperationException("TurnOpening accepts action budgets only.");
                if (effect.Output.Actor != actor) continue;
                budget = checked(budget + effect.Output.Amount); _turnModifiers.Add(effect);
            }
            if (budget > 16) throw new InvalidOperationException("Action budget exceeds the encounter safety limit of 16.");
            return budget;
        }
        public EncounterTurnPlan NextTurn(EncounterState state, ScoreActor completedActor, long completedTurn,
            EncounterTurnPlan proposed, out bool skipOpponent)
        {
            skipOpponent = false;
            var snapshot = Snapshot(state, Next(EffectTrigger.TurnCompleted), completedActor, completedTurn);
            var effects = _turns.Resolve(new TurnEffectContext(snapshot, proposed.Actor, proposed.ActionBudget), snapshot);
            foreach (var effect in effects)
            {
                if (effect.Output.Kind == TurnModifierKind.AdditionalActions) throw new InvalidOperationException("Action budgets belong to TurnOpening.");
                if (effect.Output.Actor != completedActor || effect.Source.Owner != completedActor) continue;
                // At most one scheduling override per transition and per source/round.
                if (!_opportunitiesUsed.Add(effect.Source.Key)) continue;
                skipOpponent = effect.Output.Kind == TurnModifierKind.SkipOpponent;
                _turnModifiers.Add(effect);
                return new EncounterTurnPlan(completedActor, proposed.ActionBudget);
            }
            return proposed;
        }
        public (int Increase, int Prevention) DamageModifiers(EncounterState state, ScoreActor source, ScoreActor target, int raw, EffectEvent evt = null)
        {
            var snapshot = Snapshot(state, evt ?? Next(EffectTrigger.DamageRequested), source);
            int increase = 0, prevention = 0;
            foreach (var effect in _damage.Resolve(new DamageEffectContext(snapshot, source, target, raw), snapshot))
            {
                // Attacker augments its attack; defender protects itself.
                if (effect.Output.Kind == DamageContributionKind.Increase && effect.Source.Owner == source)
                    increase = checked(increase + effect.Output.Amount);
                else if (effect.Output.Kind == DamageContributionKind.Prevent && effect.Source.Owner == target)
                    prevention = checked(prevention + effect.Output.Amount);
                else throw new InvalidOperationException("Damage contribution has an invalid owner for this operation.");
                _damageContributions.Add(effect);
            }
            return (increase, prevention);
        }
        public void Lifecycle(EncounterState state, EffectTrigger trigger, ScoreActor actor, long turnId = 0)
        {
            EffectEvent evt = Next(trigger);
            var snapshot = Snapshot(state, evt, actor, turnId);
            // Direct damage is supported at explicit gameplay boundaries, never after encounter end.
            if (trigger != EffectTrigger.EncounterEnded)
            {
                int index = 0;
                foreach (var effect in _direct.Resolve(snapshot, snapshot))
                {
                    CombatantState target = effect.Output.Target == ScoreActor.Player ? state.PlayerState : state.EnemyState;
                    if (target.IsDefeated) continue;
                    var modifiers = DamageModifiers(state, effect.Source.Owner, target.Actor, effect.Output.Amount,
                        evt.Child(EffectTrigger.DamageRequested, index++));
                    _damageReports.Add(_damageService.Apply(effect, target, modifiers.Increase, modifiers.Prevention));
                    if (state.PlayerState.IsDefeated || state.EnemyState.IsDefeated) break;
                }
            }
            ApplyRuneChain(state, evt, actor, turnId);
        }
        private void ApplyRuneChain(EncounterState state, EffectEvent root, ScoreActor actor, long turnId)
        {
            var queue = new Queue<(EffectEvent Event, RuneCommandReport Change)>();
            queue.Enqueue((root, null));
            int child = 0;
            while (queue.Count > 0)
            {
                var pending = queue.Dequeue();
                var snapshot = Snapshot(state, pending.Event, actor, turnId);
                var context = new RuneEffectContext(snapshot, pending.Change);
                foreach (var effect in _runes.Resolve(context, snapshot))
                {
                    var inventory = effect.Output.Owner == ScoreActor.Player ? state.PlayerRunes : state.EnemyRunes;
                    var report = inventory.Apply(effect);
                    _runeReports.Add(report);
                    if (report.Status == RuneCommandStatus.Applied)
                        queue.Enqueue((pending.Event.Child(EffectTrigger.RuneChanged, child++), report));
                }
            }
        }
    }
}
