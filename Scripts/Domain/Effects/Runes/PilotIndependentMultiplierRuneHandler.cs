using System;
using System.Collections.Generic;
using TicTacToeRoguelike.Domain.Runes;
using TicTacToeRoguelike.Domain.Scoring;

namespace TicTacToeRoguelike.Domain.Effects.Runes
{
    /// <summary>
    /// Primeira runa funcional do projeto.
    ///
    /// Cada instância equipada concede MULT x1,20 na fase de multiplicadores
    /// independentes. O handler é o único lugar que conhece esta runa concreta;
    /// EncounterEngine, ScorePipeline e Presentation continuam genéricos.
    /// </summary>
    public sealed class PilotIndependentMultiplierRuneHandler :
        IGameEffectHandler
    {
        public const decimal Multiplier = 1.20m;

        public string HandlerId =>
            "effect.rune.pilot.independent-multiplier";

        public EffectEventKind EventKind =>
            EffectEventKind.ScoreRequested;

        public int Priority => 0;

        public bool CanHandle(
            EffectContext context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            for (int i = 0;
                 i < context.OwnRunes.Count;
                 i++)
            {
                if (context.OwnRunes[i]
                        .Definition.Id ==
                    RuneDefinitionIds
                        .PilotIndependentMultiplier)
                {
                    return true;
                }
            }

            return false;
        }

        public EffectOutput Resolve(
            EffectContext context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            List<RuneInstance> matchingRunes =
                new List<RuneInstance>();

            for (int i = 0;
                 i < context.OwnRunes.Count;
                 i++)
            {
                RuneInstance rune =
                    context.OwnRunes[i];

                if (rune.Definition.Id ==
                    RuneDefinitionIds
                        .PilotIndependentMultiplier)
                {
                    matchingRunes.Add(rune);
                }
            }

            matchingRunes.Sort(
                CompareInstances);

            List<ScoreContribution> contributions =
                new List<ScoreContribution>(
                    matchingRunes.Count);

            for (int i = 0;
                 i < matchingRunes.Count;
                 i++)
            {
                RuneInstance rune =
                    matchingRunes[i];

                contributions.Add(
                    ScoreContribution
                        .CreateIndependentMultiplier(
                            BuildSourceId(rune),
                            rune.Definition.DisplayName,
                            context.Participant,
                            context.Participant,
                            Multiplier));
            }

            return new EffectOutput(contributions);
        }

        private static string BuildSourceId(
            RuneInstance rune)
        {
            return
                "rune:" +
                rune.Definition.Id.Value +
                ":" +
                rune.InstanceId.Value;
        }

        private static int CompareInstances(
            RuneInstance left,
            RuneInstance right)
        {
            return string.CompareOrdinal(
                left.InstanceId.Value,
                right.InstanceId.Value);
        }
    }
}
