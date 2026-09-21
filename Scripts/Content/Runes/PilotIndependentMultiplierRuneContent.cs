using System;
using Godot;
using TicTacToeRoguelike.Domain.Runes;

namespace TicTacToeRoguelike.Content.Runes
{
    public sealed class PilotIndependentMultiplierRuneContent
    {
        public const string ResourcePath =
            "res://Content/Runes/Definitions/PilotIndependentMultiplier.tres";

        public RuneDefinition Definition { get; }
        public decimal IndependentMultiplier { get; }

        private PilotIndependentMultiplierRuneContent(
            RuneDefinition definition,
            decimal independentMultiplier)
        {
            Definition = definition;
            IndependentMultiplier = independentMultiplier;
        }
        public static PilotIndependentMultiplierRuneContent LoadValidated()
        {
            var resource = ResourceLoader.Load<IndependentMultiplierRuneDefinitionResource>(
                ResourcePath);

            if (resource == null)
                throw new InvalidOperationException(
                    $"Conteúdo inválido: não foi possível carregar {ResourcePath}.");

            RuneDefinition definition;
            try
            {
                definition = resource.ToDomain();
            }
            catch (Exception exception)
            {
                throw new InvalidOperationException(
                    $"Conteúdo inválido em {ResourcePath}: definição de runa rejeitada.",
                    exception);
            }

            if (definition.Id != RuneDefinitionIds.PilotIndependentMultiplier)
                throw new InvalidOperationException(
                    $"Conteúdo inválido em {ResourcePath}: DefinitionId deve ser " +
                    $"'{RuneDefinitionIds.PilotIndependentMultiplier.Value}'.");
            double rawMultiplier = resource.IndependentMultiplier;
            if (double.IsNaN(rawMultiplier) ||
                double.IsInfinity(rawMultiplier) ||
                rawMultiplier <= 0d ||
                rawMultiplier > 10d)
            {
                throw new InvalidOperationException(
                    $"Conteúdo inválido em {ResourcePath}: IndependentMultiplier " +
                    "deve estar no intervalo (0, 10].");
            }

            decimal multiplier;
            try
            {
                multiplier = Convert.ToDecimal(rawMultiplier);
            }
            catch (Exception exception)
            {
                throw new InvalidOperationException(
                    $"Conteúdo inválido em {ResourcePath}: multiplicador não conversível.",
                    exception);
            }

            return new PilotIndependentMultiplierRuneContent(
                definition,
                multiplier);
        }
    }
}
