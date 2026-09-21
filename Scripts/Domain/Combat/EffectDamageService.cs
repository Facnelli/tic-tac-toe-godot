using System;
using TicTacToeRoguelike.Domain.Effects.Capabilities;
using TicTacToeRoguelike.Domain.Scoring;

namespace TicTacToeRoguelike.Domain.Combat
{
    public enum DamageContributionKind { Increase, Prevent }
    public sealed class DamageContribution
    {
        public DamageContributionKind Kind { get; }
        public int Amount { get; }
        public DamageContribution(DamageContributionKind kind, int amount)
        { if (!Enum.IsDefined(typeof(DamageContributionKind), kind) || amount < 0) throw new ArgumentOutOfRangeException(nameof(amount)); Kind = kind; Amount = amount; }
    }
    public sealed class DirectDamageCommand
    {
        public ScoreActor Target { get; }
        public int Amount { get; }
        public DirectDamageCommand(ScoreActor target, int amount)
        {
            if (target != ScoreActor.Player && target != ScoreActor.Enemy) throw new ArgumentOutOfRangeException(nameof(target));
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount)); Target = target; Amount = amount;
        }
    }
    public sealed class DirectDamageReport
    {
        public EffectEvent Event { get; }
        public EffectSource Source { get; }
        public CombatantSnapshot Before { get; }
        public CombatantSnapshot After { get; }
        public int RawDamage { get; }
        public int IncreasedDamage { get; }
        public int PreventedDamage { get; }
        public int ShieldConsumed => Before.Shield - After.Shield;
        public int HealthDamage => Before.CurrentHealth - After.CurrentHealth;
        public int Overkill { get; }
        internal DirectDamageReport(SourcedEffect<DirectDamageCommand> effect, CombatantSnapshot before, CombatantSnapshot after, int increase, int prevention, int overkill)
        { Event = effect.Event; Source = effect.Source; Before = before; After = after; RawDamage = effect.Output.Amount; IncreasedDamage = increase; PreventedDamage = prevention; Overkill = overkill; }
    }
    public sealed class EffectDamageService
    {
        public DirectDamageReport Apply(SourcedEffect<DirectDamageCommand> effect, CombatantState target, int increase = 0, int prevention = 0)
        {
            if (effect == null || target == null) throw new ArgumentNullException();
            if (effect.Output.Target != target.Actor) throw new ArgumentException("Damage target mismatch.");
            if (increase < 0 || prevention < 0) throw new ArgumentOutOfRangeException();
            var before = CombatantSnapshot.Capture(target);
            int total = checked(effect.Output.Amount + increase);
            int prevented = Math.Min(total, prevention);
            int damage = total - prevented;
            int shield = Math.Min(target.Shield, damage);
            int health = damage - shield;
            if (!target.IsDefeated) target.ApplyResolvedDamage(shield, health);
            return new DirectDamageReport(effect, before, CombatantSnapshot.Capture(target), increase, prevented,
                Math.Max(0, health - before.CurrentHealth));
        }
    }
}
