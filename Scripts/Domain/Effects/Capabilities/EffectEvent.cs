using System;
using System.Collections.Generic;
using TicTacToeRoguelike.Domain.Scoring;

namespace TicTacToeRoguelike.Domain.Effects.Capabilities
{
    public enum EffectTrigger { TurnOpening, TurnCompleted, AfterAction, DamageRequested, RoundResolved, EncounterEnded, RuneChanged }

    // An event ID belongs to one encounter session. Descendants retain their root.
    public sealed class EffectEvent
    {
        public string Id { get; }
        public string RootId { get; }
        public EffectTrigger Trigger { get; }
        public int Depth { get; }
        public EffectEvent(string id, EffectTrigger trigger, string rootId = null, int depth = 0)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Event ID required.", nameof(id));
            if (!Enum.IsDefined(typeof(EffectTrigger), trigger) || depth < 0) throw new ArgumentOutOfRangeException(nameof(trigger));
            Id = id; RootId = rootId ?? id; Trigger = trigger; Depth = depth;
        }
        public EffectEvent Child(EffectTrigger trigger, int index) =>
            new EffectEvent($"{Id}/{trigger}/{index}", trigger, RootId, checked(Depth + 1));
    }

    public sealed class EffectSource
    {
        public ScoreActor Owner { get; }
        public string DefinitionId { get; }
        public string InstanceId { get; }
        public string HandlerId { get; }
        public EffectSource(ScoreActor owner, string definitionId, string instanceId, string handlerId)
        {
            if (owner != ScoreActor.Player && owner != ScoreActor.Enemy) throw new ArgumentOutOfRangeException(nameof(owner));
            if (string.IsNullOrWhiteSpace(definitionId) || string.IsNullOrWhiteSpace(instanceId) || string.IsNullOrWhiteSpace(handlerId))
                throw new ArgumentException("Effect provenance cannot be empty.");
            Owner = owner; DefinitionId = definitionId; InstanceId = instanceId; HandlerId = handlerId;
        }
        internal string Key => $"{Owner}:{DefinitionId.Length}:{DefinitionId}:{InstanceId.Length}:{InstanceId}:{HandlerId}";
    }

    public enum EffectExecutionStatus { Applied, Duplicate, ChainLimit, CycleSuppressed }
    public sealed class CapabilityExecutionStep
    {
        public EffectEvent Event { get; }
        public EffectSource Source { get; }
        public string Capability { get; }
        public EffectExecutionStatus Status { get; }
        public int OutputCount { get; }
        public CapabilityExecutionStep(EffectEvent evt, EffectSource source, string capability, EffectExecutionStatus status, int outputs)
        { Event = evt; Source = source; Capability = capability; Status = status; OutputCount = outputs; }
    }

    // Session-owned journal shared by all typed pipelines; never static/global.
    public sealed class EffectExecutionJournal
    {
        private readonly HashSet<string> _events = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> _roots = new HashSet<string>(StringComparer.Ordinal);
        private readonly List<CapabilityExecutionStep> _steps = new List<CapabilityExecutionStep>();
        public IReadOnlyList<CapabilityExecutionStep> Steps => _steps.AsReadOnly();
        public int MaximumDepth { get; }
        public EffectExecutionJournal(int maximumDepth = 16)
        { if (maximumDepth < 1) throw new ArgumentOutOfRangeException(nameof(maximumDepth)); MaximumDepth = maximumDepth; }
        internal EffectExecutionStatus Enter(EffectEvent evt, EffectSource source, string capability)
        {
            string key = $"{evt.Id.Length}:{evt.Id}:{capability}:{source.Key}";
            if (evt.Depth >= MaximumDepth) return EffectExecutionStatus.ChainLimit;
            if (!_events.Add(key)) return EffectExecutionStatus.Duplicate;
            string rootKey = $"{evt.RootId.Length}:{evt.RootId}:{capability}:{evt.Trigger}:{source.Key}";
            if (!_roots.Add(rootKey)) return EffectExecutionStatus.CycleSuppressed;
            return EffectExecutionStatus.Applied;
        }
        internal void Record(CapabilityExecutionStep step) => _steps.Add(step);
    }
}
