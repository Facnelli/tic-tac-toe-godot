using System;
using TicTacToeRoguelike.Domain.Effects.Capabilities;
using TicTacToeRoguelike.Domain.Scoring;

namespace TicTacToeRoguelike.Domain.Runes
{
    public abstract class RuneCommand
    {
        public ScoreActor Owner { get; }
        public RuneInstanceId Target { get; }
        protected RuneCommand(ScoreActor owner, RuneInstanceId target)
        {
            if (owner != ScoreActor.Player && owner != ScoreActor.Enemy) throw new ArgumentOutOfRangeException(nameof(owner));
            if (string.IsNullOrWhiteSpace(target.Value)) throw new ArgumentException("Rune target required.");
            Owner = owner; Target = target;
        }
    }
    public sealed class ChangeRuneAttributeCommand : RuneCommand
    {
        public RuneAttributeId Attribute { get; }
        public bool Add { get; }
        public ChangeRuneAttributeCommand(ScoreActor owner, RuneInstanceId target, RuneAttributeId attribute, bool add) : base(owner, target)
        {
            if (!Enum.IsDefined(typeof(RuneAttributeId), attribute)) throw new ArgumentOutOfRangeException(nameof(attribute));
            Attribute = attribute; Add = add;
        }
    }
    public sealed class RemoveRuneCommand : RuneCommand
    {
        public RuneRemovalReason Reason { get; }
        public RemoveRuneCommand(ScoreActor owner, RuneInstanceId target, RuneRemovalReason reason) : base(owner, target)
        {
            if (!Enum.IsDefined(typeof(RuneRemovalReason), reason) || reason == RuneRemovalReason.Unknown) throw new ArgumentOutOfRangeException(nameof(reason));
            Reason = reason;
        }
    }
    public enum RuneCommandStatus { Applied, NoChange, MissingTarget, CapacityReached }
    public sealed class RuneCommandReport
    {
        public EffectEvent Event { get; }
        public EffectSource Source { get; }
        public RuneCommand Command { get; }
        public RuneCommandStatus Status { get; }
        public RuneInstance Before { get; }
        public RuneInstance After { get; }
        public RuneRemovalResult Removal { get; }
        public RuneCommandReport(SourcedEffect<RuneCommand> effect, RuneCommandStatus status, RuneInstance before, RuneInstance after, RuneRemovalResult removal = null)
        { Event = effect.Event; Source = effect.Source; Command = effect.Output; Status = status; Before = before; After = after; Removal = removal; }
    }
}
