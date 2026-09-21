using System;
using TicTacToeRoguelike.Domain.Scoring;

namespace TicTacToeRoguelike.Domain.Effects.Capabilities
{
    public enum TurnModifierKind { AdditionalActions, ExtraTurn, SkipOpponent }
    public sealed class TurnPlanModifier
    {
        public TurnModifierKind Kind { get; }
        public ScoreActor Actor { get; }
        public int Amount { get; }
        public TurnPlanModifier(TurnModifierKind kind, ScoreActor actor, int amount = 1)
        {
            if (!Enum.IsDefined(typeof(TurnModifierKind), kind) || amount < 1 || amount > 8) throw new ArgumentOutOfRangeException(nameof(amount));
            if (actor != ScoreActor.Player && actor != ScoreActor.Enemy) throw new ArgumentOutOfRangeException(nameof(actor));
            if (kind != TurnModifierKind.AdditionalActions && amount != 1) throw new ArgumentException("Only one extra opportunity per modifier.");
            Kind = kind; Actor = actor; Amount = amount;
        }
    }
}
