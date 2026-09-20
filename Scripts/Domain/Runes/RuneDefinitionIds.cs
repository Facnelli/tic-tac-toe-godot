namespace TicTacToeRoguelike.Domain.Runes
{
    /// <summary>
    /// Identificadores estáveis das runas concretas já disponíveis.
    ///
    /// Eles ficam no domínio para que conteúdo, handlers e testes compartilhem a
    /// mesma identidade sem espalhar strings mágicas pelas camadas centrais.
    /// </summary>
    public static class RuneDefinitionIds
    {
        public static readonly RuneDefinitionId
            PilotIndependentMultiplier =
                new RuneDefinitionId(
                    "rune.pilot.independent-multiplier");
    }
}
