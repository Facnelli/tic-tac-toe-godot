using TicTacToeRoguelike.Domain.Effects;
using TicTacToeRoguelike.Domain.Effects.Runes;

namespace TicTacToeRoguelike.Application.Effects
{
    /// <summary>
    /// Ponto de composição dos efeitos disponíveis no jogo.
    ///
    /// O fluxo central conhece somente IGameEffectHandler/EffectEngine. Runas
    /// concretas entram aqui como plugins, sem branches por ID no encontro.
    /// </summary>
    public static class DefaultEffectEngineFactory
    {
        public static EffectEngine Create(
            decimal pilotIndependentMultiplier)
        {
            return new EffectEngine(
                new IGameEffectHandler[]
                {
                    new PilotIndependentMultiplierRuneHandler(
                        pilotIndependentMultiplier)
                });
        }
    }
}
