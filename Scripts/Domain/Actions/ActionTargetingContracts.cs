using TicTacToeRoguelike.Domain.Boards;
using TicTacToeRoguelike.Domain.Runes;

namespace TicTacToeRoguelike.Domain.Actions
{
    /// <summary>
    /// Marca uma GameAction como originada por uma instância concreta de runa.
    ///
    /// A interface existe para que apresentação, IA, logs e futuros sistemas de
    /// targeting consigam relacionar a ação à pedra que a habilitou sem conhecer
    /// classes concretas como ClearCellAction, InvertMarkAction ou BlockCellAction.
    /// </summary>
    public interface IRuneSourcedAction
    {
        RuneInstanceId SourceInstanceId { get; }
    }

    /// <summary>
    /// Marca ações cujo alvo é escolhido clicando em uma casa do tabuleiro.
    ///
    /// Combinar esta interface com IRuneSourcedAction permite o fluxo de UX:
    /// selecionar a runa primeiro e escolher o alvo no tabuleiro depois.
    /// </summary>
    public interface IBoardTargetedAction
    {
        BoardCoordinate Target { get; }
    }
}
