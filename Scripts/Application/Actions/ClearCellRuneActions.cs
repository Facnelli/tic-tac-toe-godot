using System;
using System.Collections.Generic;
using System.Linq;
using TicTacToeRoguelike.Domain.Actions;
using TicTacToeRoguelike.Domain.Boards;
using TicTacToeRoguelike.Domain.Runes;
using TicTacToeRoguelike.Domain.Scoring;

namespace TicTacToeRoguelike.Application.Actions
{
    // Encounter-scoped charges. Consultation never consumes or resets a charge.
    public sealed class RuneActionUsage
    {
        private readonly Dictionary<(ScoreActor, string), int> _used = new Dictionary<(ScoreActor, string), int>();
        public void BeginRound() => _used.Clear();
        public int Used(ScoreActor owner, RuneInstanceId rune) => _used.TryGetValue((owner, rune.Value), out int value) ? value : 0;
        public void Consume(ScoreActor owner, RuneInstanceId rune) => _used[(owner, rune.Value)] = checked(Used(owner, rune) + 1);
    }
    public sealed class ClearCellRuneActions : IGameActionProvider, IGameActionHandler
    {
        public const string PilotDefinitionId = "rune.pilot.clear-cell";
        private readonly RuneInventoryState _player;
        private readonly RuneInventoryState _enemy;
        private readonly RuneActionUsage _usage;
        private readonly string _definitionId;
        private readonly int _usesPerRound;
        private readonly BoardMutationService _mutations = new BoardMutationService();
        public GameActionType ActionType => GameActionType.ClearCell;
        public ClearCellRuneActions(RuneInventoryState player, RuneInventoryState enemy, RuneActionUsage usage,
            string definitionId = PilotDefinitionId, int usesPerRound = 1)
        {
            if (player == null || enemy == null || usage == null) throw new ArgumentNullException();
            if (player.Owner != ScoreActor.Player || enemy.Owner != ScoreActor.Enemy) throw new ArgumentException("Wrong inventories.");
            if (string.IsNullOrWhiteSpace(definitionId) || usesPerRound < 1) throw new ArgumentOutOfRangeException(nameof(usesPerRound));
            _player = player; _enemy = enemy; _usage = usage; _definitionId = definitionId; _usesPerRound = usesPerRound;
        }
        private RuneInventoryState Inventory(ScoreActor actor) => actor == ScoreActor.Player ? _player : _enemy;
        private bool CanUse(ScoreActor actor, RuneInstanceId source) => Inventory(actor).Runes.Any(r =>
            r.InstanceId == source && r.Definition.Id.Value == _definitionId) && _usage.Used(actor, source) < _usesPerRound;
        public IReadOnlyList<GameAction> GetAvailableActions(GameActionProviderContext context)
        {
            var result = new List<GameAction>();
            foreach (var rune in Inventory(context.TurnContext.Actor).Runes.OrderBy(r => r.InstanceId.Value, StringComparer.Ordinal))
            {
                if (!CanUse(context.TurnContext.Actor, rune.InstanceId)) continue;
                foreach (var coordinate in context.Board.Definition.Cells)
                    if (context.Board.GetCell(coordinate).Marks != CellMark.None)
                        result.Add(new ClearCellAction(context.TurnContext.Actor, context.Origin, context.TurnContext.TurnId,
                            context.Board.Version, coordinate, rune.InstanceId));
            }
            return result.AsReadOnly();
        }
        public ActionResult Execute(GameAction action, ActionExecutionContext context)
        {
            if (!(action is ClearCellAction clear)) return ActionResult.Rejected(action, ActionRejectionReason.ActionTypeNotSupported, context.Board.Version);
            if (!CanUse(action.Actor, clear.SourceInstanceId)) return ActionResult.Rejected(action, ActionRejectionReason.ActionNotAvailable, context.Board.Version);
            if (!context.Board.TryGetCell(clear.Target, out var cell)) return ActionResult.Rejected(action, ActionRejectionReason.CellDoesNotExist, context.Board.Version);
            if (cell.Marks == CellMark.None) return ActionResult.Rejected(action, ActionRejectionReason.ActionNotAvailable, context.Board.Version);
            long before = context.Board.Version;
            var changes = _mutations.ApplyAuthorized(context.Board, new[] { BoardMutationCommand.ClearMarks(clear.Target) });
            _usage.Consume(action.Actor, clear.SourceInstanceId);
            return ActionResult.Applied(action, before, context.Board.Version, changes);
        }
    }
}
