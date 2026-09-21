using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using TicTacToeRoguelike.Domain.Actions;
using TicTacToeRoguelike.Domain.Boards;
using TicTacToeRoguelike.Domain.Runes;

namespace TicTacToeRoguelike.Presentation.Runes
{
    /// <summary>
    /// Guarda somente o modo de targeting escolhido pelo jogador.
    ///
    /// Sem runa selecionada, um clique no tabuleiro procura exclusivamente uma
    /// PlaceMarkAction. Depois que uma runa é selecionada, o mesmo clique procura
    /// exclusivamente uma ação de tabuleiro originada daquela instância.
    ///
    /// Nenhuma regra de efeito vive aqui. A classe apenas escolhe entre ações que
    /// o ActionCatalog já declarou legais para o TurnContext atual.
    /// </summary>
    public sealed class RuneTargetingSelection
    {
        public RuneInstanceId? SelectedSource { get; private set; }

        public bool HasSelection => SelectedSource.HasValue;

        public void Reset()
        {
            SelectedSource = null;
        }

        /// <summary>
        /// Seleciona uma runa que atualmente oferece ao menos uma ação clicável
        /// no tabuleiro. Clicar novamente na mesma runa cancela o modo especial.
        /// </summary>
        public bool Toggle(
            RuneInstanceId source,
            IReadOnlyList<GameAction> legalActions)
        {
            if (legalActions == null)
                throw new ArgumentNullException(nameof(legalActions));

            if (SelectedSource.HasValue &&
                SelectedSource.Value == source)
            {
                Reset();
                return true;
            }

            if (!OffersBoardTargetedAction(
                    legalActions,
                    source))
            {
                return false;
            }

            SelectedSource = source;
            return true;
        }

        /// <summary>
        /// Remove automaticamente uma seleção que deixou de possuir ação legal,
        /// por exemplo após gastar a carga da runa ou encerrar o Turno.
        /// </summary>
        public void Refresh(
            IReadOnlyList<GameAction> legalActions)
        {
            if (legalActions == null)
                throw new ArgumentNullException(nameof(legalActions));

            if (SelectedSource.HasValue &&
                !OffersBoardTargetedAction(
                    legalActions,
                    SelectedSource.Value))
            {
                Reset();
            }
        }

        /// <summary>
        /// Devolve as instâncias de runa que podem entrar em modo de targeting no
        /// estado atual. A ordem segue a primeira ocorrência no catálogo.
        /// </summary>
        public IReadOnlyList<RuneInstanceId>
            GetSelectableSources(
                IReadOnlyList<GameAction> legalActions)
        {
            if (legalActions == null)
                throw new ArgumentNullException(nameof(legalActions));

            List<RuneInstanceId> result =
                new List<RuneInstanceId>();

            HashSet<string> seen =
                new HashSet<string>(
                    StringComparer.Ordinal);

            for (int i = 0;
                 i < legalActions.Count;
                 i++)
            {
                GameAction action =
                    legalActions[i];

                if (!(action is IRuneSourcedAction sourced) ||
                    !(action is IBoardTargetedAction))
                {
                    continue;
                }

                if (seen.Add(
                        sourced.SourceInstanceId.Value))
                {
                    result.Add(
                        sourced.SourceInstanceId);
                }
            }

            return new ReadOnlyCollection<RuneInstanceId>(
                result);
        }

        /// <summary>
        /// Resolve o clique do jogador sem misturar ação normal e ação de runa.
        /// </summary>
        public GameAction SelectTarget(
            IReadOnlyList<GameAction> legalActions,
            BoardCoordinate target)
        {
            if (legalActions == null)
                throw new ArgumentNullException(nameof(legalActions));

            if (!SelectedSource.HasValue)
            {
                for (int i = 0;
                     i < legalActions.Count;
                     i++)
                {
                    if (legalActions[i] is PlaceMarkAction place &&
                        place.Target == target)
                    {
                        return place;
                    }
                }

                return null;
            }

            RuneInstanceId selected =
                SelectedSource.Value;

            for (int i = 0;
                 i < legalActions.Count;
                 i++)
            {
                GameAction action =
                    legalActions[i];

                if (!(action is IRuneSourcedAction sourced) ||
                    sourced.SourceInstanceId != selected ||
                    !(action is IBoardTargetedAction boardTargeted) ||
                    boardTargeted.Target != target)
                {
                    continue;
                }

                return action;
            }

            return null;
        }

        private static bool OffersBoardTargetedAction(
            IReadOnlyList<GameAction> legalActions,
            RuneInstanceId source)
        {
            return legalActions.Any(
                action =>
                    action is IRuneSourcedAction sourced &&
                    sourced.SourceInstanceId == source &&
                    action is IBoardTargetedAction);
        }
    }
}
