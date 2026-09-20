using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using TicTacToeRoguelike.Domain.Boards;
using TicTacToeRoguelike.Domain.Runes;
using TicTacToeRoguelike.Domain.Scoring;

namespace TicTacToeRoguelike.Domain.Effects
{
    public enum EffectEventKind
    {
        ScoreRequested = 0,
        TurnStarted = 1,
        BeforeAction = 2,
        AfterAction = 3,
        RoundResolved = 4
    }

    public sealed class EffectContext
    {
        private readonly ReadOnlyCollection<RuneInstance> _ownRunes;
        private readonly ReadOnlyCollection<RuneInstance> _opponentRunes;

        public EffectEventKind EventKind { get; }
        public ScoreActor Participant { get; }
        public BoardState BoardSnapshot { get; }
        public IReadOnlyList<RuneInstance> OwnRunes => _ownRunes;
        public IReadOnlyList<RuneInstance> OpponentRunes => _opponentRunes;
        public int RoundNumber { get; }
        public bool IsWinner { get; }

        public EffectContext(
            EffectEventKind eventKind,
            ScoreActor participant,
            BoardState board,
            IEnumerable<RuneInstance> ownRunes,
            IEnumerable<RuneInstance> opponentRunes,
            int roundNumber,
            bool isWinner)
        {
            if (!Enum.IsDefined(typeof(EffectEventKind), eventKind))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(eventKind),
                    eventKind,
                    "Evento de efeito inválido.");
            }

            if (participant != ScoreActor.Player &&
                participant != ScoreActor.Enemy)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(participant),
                    participant,
                    "O contexto de efeito precisa pertencer ao Player ou Enemy.");
            }

            if (board == null)
            {
                throw new ArgumentNullException(nameof(board));
            }

            if (roundNumber < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(roundNumber),
                    roundNumber,
                    "A rodada precisa começar em 1.");
            }

            EventKind = eventKind;
            Participant = participant;
            BoardSnapshot = board.Clone();
            RoundNumber = roundNumber;
            IsWinner = isWinner;

            _ownRunes = CopyRunes(
                ownRunes,
                nameof(ownRunes));

            _opponentRunes = CopyRunes(
                opponentRunes,
                nameof(opponentRunes));
        }

        internal EffectContext CreateIsolatedCopy()
        {
            return new EffectContext(
                EventKind,
                Participant,
                BoardSnapshot,
                _ownRunes,
                _opponentRunes,
                RoundNumber,
                IsWinner);
        }

        private static ReadOnlyCollection<RuneInstance> CopyRunes(
            IEnumerable<RuneInstance> runes,
            string parameterName)
        {
            if (runes == null)
            {
                throw new ArgumentNullException(parameterName);
            }

            List<RuneInstance> copy =
                new List<RuneInstance>();

            foreach (RuneInstance rune in runes)
            {
                if (rune == null)
                {
                    throw new ArgumentException(
                        "A coleção de runas não pode conter null.",
                        parameterName);
                }

                copy.Add(rune);
            }

            return copy.AsReadOnly();
        }
    }

    public sealed class EffectOutput
    {
        private readonly ReadOnlyCollection<ScoreContribution>
            _scoreContributions;

        public IReadOnlyList<ScoreContribution> ScoreContributions =>
            _scoreContributions;

        public bool HasScoreContributions =>
            _scoreContributions.Count > 0;

        public EffectOutput(
            IEnumerable<ScoreContribution> scoreContributions = null)
        {
            List<ScoreContribution> copy =
                new List<ScoreContribution>();

            if (scoreContributions != null)
            {
                foreach (ScoreContribution contribution in scoreContributions)
                {
                    if (contribution == null)
                    {
                        throw new ArgumentException(
                            "A saída de efeito não pode conter contribuição nula.",
                            nameof(scoreContributions));
                    }

                    copy.Add(contribution);
                }
            }

            _scoreContributions = copy.AsReadOnly();
        }

        public static EffectOutput None() =>
            new EffectOutput();
    }

    public interface IGameEffectHandler
    {
        string HandlerId { get; }
        EffectEventKind EventKind { get; }
        int Priority { get; }

        bool CanHandle(EffectContext context);
        EffectOutput Resolve(EffectContext context);
    }

    public sealed class EffectExecutionStep
    {
        private readonly ReadOnlyCollection<string>
            _scoreContributionSourceIds;

        public int Order { get; }
        public string HandlerId { get; }
        public int Priority { get; }
        public bool WasApplicable { get; }
        public IReadOnlyList<string> ScoreContributionSourceIds =>
            _scoreContributionSourceIds;

        internal EffectExecutionStep(
            int order,
            string handlerId,
            int priority,
            bool wasApplicable,
            IEnumerable<string> scoreContributionSourceIds)
        {
            Order = order;
            HandlerId = handlerId;
            Priority = priority;
            WasApplicable = wasApplicable;

            _scoreContributionSourceIds =
                new List<string>(
                    scoreContributionSourceIds ??
                    Array.Empty<string>())
                .AsReadOnly();
        }
    }

    public sealed class EffectExecutionReport
    {
        private readonly ReadOnlyCollection<EffectExecutionStep> _steps;
        private readonly ReadOnlyCollection<ScoreContribution>
            _scoreContributions;

        public EffectEventKind EventKind { get; }
        public ScoreActor Participant { get; }
        public long BoardVersion { get; }

        public IReadOnlyList<EffectExecutionStep> Steps => _steps;
        public IReadOnlyList<ScoreContribution> ScoreContributions =>
            _scoreContributions;

        public bool HadApplicableEffects
        {
            get
            {
                for (int i = 0; i < _steps.Count; i++)
                {
                    if (_steps[i].WasApplicable)
                        return true;
                }

                return false;
            }
        }

        internal EffectExecutionReport(
            EffectContext context,
            IList<EffectExecutionStep> steps,
            IList<ScoreContribution> scoreContributions)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            EventKind = context.EventKind;
            Participant = context.Participant;
            BoardVersion = context.BoardSnapshot.Version;

            _steps =
                new List<EffectExecutionStep>(steps)
                .AsReadOnly();

            _scoreContributions =
                new List<ScoreContribution>(scoreContributions)
                .AsReadOnly();
        }

        public static EffectExecutionReport CreateEmpty(
            EffectEventKind eventKind,
            ScoreActor participant,
            BoardState board,
            int roundNumber,
            bool isWinner)
        {
            EffectContext context =
                new EffectContext(
                    eventKind,
                    participant,
                    board,
                    Array.Empty<RuneInstance>(),
                    Array.Empty<RuneInstance>(),
                    roundNumber,
                    isWinner);

            return new EffectExecutionReport(
                context,
                new List<EffectExecutionStep>(),
                new List<ScoreContribution>());
        }
    }

    public sealed class EffectEngine
    {
        private readonly ReadOnlyCollection<IGameEffectHandler> _handlers;

        public IReadOnlyList<IGameEffectHandler> Handlers => _handlers;

        public EffectEngine(
            IEnumerable<IGameEffectHandler> handlers = null)
        {
            List<IGameEffectHandler> ordered =
                new List<IGameEffectHandler>();

            HashSet<string> ids =
                new HashSet<string>(
                    StringComparer.Ordinal);

            if (handlers != null)
            {
                foreach (IGameEffectHandler handler in handlers)
                {
                    if (handler == null)
                    {
                        throw new ArgumentException(
                            "A coleção de handlers não pode conter null.",
                            nameof(handlers));
                    }

                    if (string.IsNullOrWhiteSpace(handler.HandlerId))
                    {
                        throw new ArgumentException(
                            "Todo handler precisa possuir HandlerId estável.",
                            nameof(handlers));
                    }

                    if (!Enum.IsDefined(
                            typeof(EffectEventKind),
                            handler.EventKind))
                    {
                        throw new ArgumentException(
                            "Um handler declarou um evento desconhecido.",
                            nameof(handlers));
                    }

                    if (!ids.Add(handler.HandlerId))
                    {
                        throw new ArgumentException(
                            $"HandlerId duplicado: {handler.HandlerId}.",
                            nameof(handlers));
                    }

                    ordered.Add(handler);
                }
            }

            ordered.Sort(CompareHandlers);
            _handlers = ordered.AsReadOnly();
        }

        public EffectExecutionReport Resolve(
            EffectContext context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            List<EffectExecutionStep> steps =
                new List<EffectExecutionStep>();

            List<ScoreContribution> contributions =
                new List<ScoreContribution>();

            int order = 1;

            for (int i = 0; i < _handlers.Count; i++)
            {
                IGameEffectHandler handler =
                    _handlers[i];

                if (handler.EventKind != context.EventKind)
                    continue;

                EffectContext predicateContext =
                    context.CreateIsolatedCopy();

                bool applicable =
                    handler.CanHandle(
                        predicateContext);

                List<string> sourceIds =
                    new List<string>();

                if (applicable)
                {
                    EffectContext resolutionContext =
                        context.CreateIsolatedCopy();

                    EffectOutput output =
                        handler.Resolve(
                            resolutionContext) ??
                        throw new InvalidOperationException(
                            $"O handler {handler.HandlerId} retornou null.");

                    for (int contributionIndex = 0;
                         contributionIndex <
                         output.ScoreContributions.Count;
                         contributionIndex++)
                    {
                        ScoreContribution contribution =
                            output.ScoreContributions[
                                contributionIndex];

                        if (context.EventKind ==
                                EffectEventKind.ScoreRequested &&
                            contribution.Target !=
                                context.Participant)
                        {
                            throw new InvalidOperationException(
                                $"O handler {handler.HandlerId} produziu uma " +
                                "contribuição para outro participante.");
                        }

                        contributions.Add(contribution);
                        sourceIds.Add(
                            contribution.SourceId);
                    }
                }

                steps.Add(
                    new EffectExecutionStep(
                        order,
                        handler.HandlerId,
                        handler.Priority,
                        applicable,
                        sourceIds));

                order++;
            }

            return new EffectExecutionReport(
                context,
                steps,
                contributions);
        }

        private static int CompareHandlers(
            IGameEffectHandler left,
            IGameEffectHandler right)
        {
            int eventComparison =
                left.EventKind.CompareTo(
                    right.EventKind);

            if (eventComparison != 0)
                return eventComparison;

            int priorityComparison =
                left.Priority.CompareTo(
                    right.Priority);

            if (priorityComparison != 0)
                return priorityComparison;

            return string.CompareOrdinal(
                left.HandlerId,
                right.HandlerId);
        }
    }
}
