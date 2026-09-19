using System;
using Godot;
using TicTacToeRoguelike.Application.Encounters;

namespace TicTacToeRoguelike.Presentation.Combat
{
    /// <summary>
    /// Adaptador visual do relatório já resolvido pelo domínio.
    /// Nunca aplica dano: apenas conserva o último relatório para a apresentação.
    /// </summary>
    public sealed partial class CombatReportAnimator : Node
    {
        public RoundResolution LastPresentedResolution { get; private set; }

        public event Action<RoundResolution> ResolutionPresented;

        public void Present(RoundResolution resolution)
        {
            LastPresentedResolution = resolution
                ?? throw new ArgumentNullException(nameof(resolution));

            ResolutionPresented?.Invoke(resolution);
        }

        public void ResetPresentation()
        {
            LastPresentedResolution = null;
        }
    }
}
