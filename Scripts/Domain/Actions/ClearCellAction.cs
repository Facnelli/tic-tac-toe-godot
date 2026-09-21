using System;
using TicTacToeRoguelike.Domain.Boards;
using TicTacToeRoguelike.Domain.Runes;
using TicTacToeRoguelike.Domain.Scoring;

namespace TicTacToeRoguelike.Domain.Actions
{
    public sealed class ClearCellAction : GameAction<BoardCoordinate>, IRuneSourcedAction, IBoardTargetedAction
    {
        public RuneInstanceId SourceInstanceId { get; }
        public ClearCellAction(ScoreActor actor, GameActionOrigin origin, long turnId, long boardVersion, BoardCoordinate target, RuneInstanceId source)
            : base(actor, GameActionType.ClearCell, origin, turnId, boardVersion, target)
        { if (string.IsNullOrWhiteSpace(source.Value)) throw new ArgumentException("Rune source required."); SourceInstanceId = source; }
    }
}
