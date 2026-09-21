using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using TicTacToeRoguelike.Domain.Actions;
using TicTacToeRoguelike.Domain.Moves;
using TicTacToeRoguelike.Domain.Turns;
using TicTacToeRoguelike.Domain.Boards;

namespace TicTacToeRoguelike.Application.Actions
{
    public sealed class ActionCatalog
    {
        private readonly ReadOnlyCollection<IGameActionProvider> _providers;
        private readonly Dictionary<GameActionType, IGameActionHandler> _handlers;

        public ActionCatalog(
            IEnumerable<IGameActionProvider> providers,
            IEnumerable<IGameActionHandler> handlers)
        {
            if (providers == null) throw new ArgumentNullException(nameof(providers));
            if (handlers == null) throw new ArgumentNullException(nameof(handlers));

            List<IGameActionProvider> providerList = new List<IGameActionProvider>();
            foreach (IGameActionProvider provider in providers)
            {
                if (provider == null) throw new ArgumentException("O catálogo não aceita provider nulo.", nameof(providers));
                providerList.Add(provider);
            }
            _providers = new ReadOnlyCollection<IGameActionProvider>(providerList);

            _handlers = new Dictionary<GameActionType, IGameActionHandler>();
            foreach (IGameActionHandler handler in handlers)
            {
                if (handler == null) throw new ArgumentException("O catálogo não aceita handler nulo.", nameof(handlers));
                if (handler.ActionType == GameActionType.None)
                    throw new ArgumentException("Um handler precisa declarar um tipo de ação válido.", nameof(handlers));
                if (_handlers.ContainsKey(handler.ActionType))
                    throw new ArgumentException($"Já existe handler para {handler.ActionType}.", nameof(handlers));
                _handlers.Add(handler.ActionType, handler);
            }
        }

        public static ActionCatalog CreateDefault()
        {
            MoveValidator validator = new MoveValidator();
            MoveService service = new MoveService(validator);
            return CreateDefault(validator, service);
        }

        public static ActionCatalog CreateDefault(MoveValidator validator, MoveService service)
        {
            if (validator == null) throw new ArgumentNullException(nameof(validator));
            if (service == null) throw new ArgumentNullException(nameof(service));

            return new ActionCatalog(
                new IGameActionProvider[] { new NormalMoveActionProvider(validator) },
                new IGameActionHandler[] { new PlaceMarkActionHandler(service) });
        }

        public IReadOnlyList<GameAction> GetAvailableActions(
            BoardState board,
            TurnContext turnContext,
            GameActionOrigin origin)
        {
            GameActionProviderContext context =
                new GameActionProviderContext(board, turnContext, origin);

            long version = board.Version;
            List<GameAction> result = new List<GameAction>();

            for (int p = 0; p < _providers.Count; p++)
            {
                IReadOnlyList<GameAction> provided = _providers[p].GetAvailableActions(context)
                    ?? throw new InvalidOperationException("IGameActionProvider retornou null.");

                if (board.Version != version)
                    throw new InvalidOperationException("Um provider alterou o BoardState ao enumerar ações.");

                for (int i = 0; i < provided.Count; i++)
                {
                    GameAction action = provided[i]
                        ?? throw new InvalidOperationException("Um provider retornou ação nula.");

                    if (action.Actor != turnContext.Actor ||
                        action.ExpectedTurnId != turnContext.TurnId ||
                        action.ExpectedBoardVersion != version)
                    {
                        throw new InvalidOperationException("Um provider produziu ação para outro ator, Turno ou BoardVersion.");
                    }

                    if (!_handlers.ContainsKey(action.ActionType))
                        throw new InvalidOperationException("An offered action has no registered handler.");
                    result.Add(action);
                }
            }

            return new ReadOnlyCollection<GameAction>(result);
        }

        public bool TryGetHandler(GameAction action, out IGameActionHandler handler)
        {
            if (action == null)
            {
                handler = null;
                return false;
            }

            return _handlers.TryGetValue(action.ActionType, out handler);
        }
    }
}
