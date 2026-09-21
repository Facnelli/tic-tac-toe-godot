using System;
using System.Collections.Generic;
using System.Linq;
using TicTacToeRoguelike.Domain.Boards;
using TicTacToeRoguelike.Domain.Combat;
using TicTacToeRoguelike.Domain.Runes;
using TicTacToeRoguelike.Domain.Scoring;

namespace TicTacToeRoguelike.Domain.Effects.Capabilities
{
    public sealed class EncounterEffectContext
    {
        private readonly BoardState _board;
        public EffectEvent Event { get; }
        public int RoundNumber { get; }
        public long TurnId { get; }
        public ScoreActor Actor { get; }
        public BoardState Board => _board.Clone();
        public CombatantSnapshot Player { get; }
        public CombatantSnapshot Enemy { get; }
        public IReadOnlyList<RuneInstance> PlayerRunes { get; }
        public IReadOnlyList<RuneInstance> EnemyRunes { get; }
        public EncounterEffectContext(EffectEvent evt, int roundNumber, long turnId, ScoreActor actor,
            BoardState board, CombatantState player, CombatantState enemy, RuneInventoryState playerRunes, RuneInventoryState enemyRunes)
        {
            Event = evt ?? throw new ArgumentNullException(nameof(evt));
            _board = board.Clone(); RoundNumber = roundNumber; TurnId = turnId; Actor = actor;
            Player = CombatantSnapshot.Capture(player); Enemy = CombatantSnapshot.Capture(enemy);
            PlayerRunes = Array.AsReadOnly(playerRunes.Runes.ToArray()); EnemyRunes = Array.AsReadOnly(enemyRunes.Runes.ToArray());
        }
    }
    public sealed class TurnEffectContext
    {
        public EncounterEffectContext Encounter { get; }
        public ScoreActor ProposedActor { get; }
        public int ProposedBudget { get; }
        public TurnEffectContext(EncounterEffectContext encounter, ScoreActor actor, int budget)
        { Encounter = encounter; ProposedActor = actor; ProposedBudget = budget; }
    }
    public sealed class DamageEffectContext
    {
        public EncounterEffectContext Encounter { get; }
        public ScoreActor Source { get; }
        public ScoreActor Target { get; }
        public int RawDamage { get; }
        public DamageEffectContext(EncounterEffectContext encounter, ScoreActor source, ScoreActor target, int rawDamage)
        { Encounter = encounter; Source = source; Target = target; RawDamage = rawDamage; }
    }
    public sealed class RuneEffectContext
    {
        public EncounterEffectContext Encounter { get; }
        public RuneCommandReport Change { get; }
        public RuneEffectContext(EncounterEffectContext encounter, RuneCommandReport change = null)
        { Encounter = encounter; Change = change; }
    }
    public interface ICapabilityHandler<TContext, TOutput>
    {
        string HandlerId { get; }
        string DefinitionId { get; }
        int Priority { get; }
        EffectTrigger Trigger { get; }
        IReadOnlyList<TOutput> Resolve(TContext context, EffectSource source, EffectRandom random);
    }
    public sealed class SourcedEffect<T>
    {
        public EffectEvent Event { get; }
        public EffectSource Source { get; }
        public T Output { get; }
        public SourcedEffect(EffectEvent evt, EffectSource source, T output) { Event = evt; Source = source; Output = output; }
    }
    public sealed class CapabilityPipeline<TContext, TOutput>
    {
        private readonly ICapabilityHandler<TContext, TOutput>[] _handlers;
        private readonly EffectExecutionJournal _journal;
        private readonly EffectRandom _random;
        public CapabilityPipeline(IEnumerable<ICapabilityHandler<TContext, TOutput>> handlers, EffectExecutionJournal journal, EffectRandom random)
        {
            _handlers = (handlers ?? Array.Empty<ICapabilityHandler<TContext, TOutput>>()).ToArray();
            if (_handlers.Any(h => h == null || string.IsNullOrWhiteSpace(h.HandlerId) || string.IsNullOrWhiteSpace(h.DefinitionId)) ||
                _handlers.GroupBy(h => h.HandlerId, StringComparer.Ordinal).Any(g => g.Count() > 1))
                throw new ArgumentException("Handlers must have unique stable IDs and definitions.");
            _journal = journal ?? throw new ArgumentNullException(nameof(journal)); _random = random ?? throw new ArgumentNullException(nameof(random));
        }
        public IReadOnlyList<SourcedEffect<TOutput>> Resolve(TContext context, EncounterEffectContext snapshot)
        {
            var candidates = new List<(ICapabilityHandler<TContext, TOutput> Handler, EffectSource Source)>();
            foreach (var handler in _handlers.Where(h => h.Trigger == snapshot.Event.Trigger))
                foreach (ScoreActor owner in new[] { ScoreActor.Player, ScoreActor.Enemy })
                    foreach (var rune in owner == ScoreActor.Player ? snapshot.PlayerRunes : snapshot.EnemyRunes)
                        if (rune.Definition.Id.Value == handler.DefinitionId)
                            candidates.Add((handler, new EffectSource(owner, handler.DefinitionId, rune.InstanceId.Value, handler.HandlerId)));
            var result = new List<SourcedEffect<TOutput>>();
            foreach (var item in candidates.OrderBy(c => c.Handler.Priority)
                .ThenBy(c => c.Source.DefinitionId, StringComparer.Ordinal).ThenBy(c => c.Source.InstanceId, StringComparer.Ordinal)
                .ThenBy(c => c.Source.Owner).ThenBy(c => c.Handler.HandlerId, StringComparer.Ordinal))
            {
                string capability = typeof(TOutput).FullName;
                var status = _journal.Enter(snapshot.Event, item.Source, capability);
                int count = 0;
                if (status == EffectExecutionStatus.Applied)
                {
                    var outputs = item.Handler.Resolve(context, item.Source, _random) ?? throw new InvalidOperationException("Null effect output.");
                    foreach (var output in outputs)
                    {
                        if (ReferenceEquals(output, null)) throw new InvalidOperationException("Null command.");
                        result.Add(new SourcedEffect<TOutput>(snapshot.Event, item.Source, output)); count++;
                    }
                }
                _journal.Record(new CapabilityExecutionStep(snapshot.Event, item.Source, capability, status, count));
            }
            return result.AsReadOnly();
        }
    }
}
