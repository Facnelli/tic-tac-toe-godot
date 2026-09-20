using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using TicTacToeRoguelike.Domain.Scoring;

namespace TicTacToeRoguelike.Domain.Runes
{
    public enum RuneInventoryAddResult
    {
        Added = 0,
        CapacityReached = 1,
        DuplicateInstance = 2
    }

    public enum RuneRemovalReason
    {
        Unknown = 0,
        Discarded = 1,
        Replaced = 2,
        Sacrificed = 3,
        Destroyed = 4,
        EncounterSetup = 5
    }

    public sealed class RuneRemovalResult
    {
        public RuneInstance Rune { get; }
        public RuneRemovalReason Reason { get; }

        public RuneRemovalResult(
            RuneInstance rune,
            RuneRemovalReason reason)
        {
            Rune = rune ??
                throw new ArgumentNullException(
                    nameof(rune));

            Reason = reason;
        }
    }

    public sealed class RuneInventoryState
    {
        public const int DefaultCapacity = 5;

        private readonly List<RuneInstance> _runes;
        private readonly ReadOnlyCollection<RuneInstance> _view;

        public ScoreActor Owner { get; }
        public int Capacity { get; }
        public IReadOnlyList<RuneInstance> Runes => _view;

        public int Count => _runes.Count;

        public int OccupiedSlots
        {
            get
            {
                int count = 0;

                for (int i = 0; i < _runes.Count; i++)
                {
                    if (!_runes[i].IsIntangible)
                        count++;
                }

                return count;
            }
        }

        public int FreeSlots =>
            Math.Max(0, Capacity - OccupiedSlots);

        public RuneInventoryState(
            ScoreActor owner,
            int capacity = DefaultCapacity)
        {
            if (owner != ScoreActor.Player &&
                owner != ScoreActor.Enemy)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(owner),
                    owner,
                    "Inventários de runas pertencem ao Player ou Enemy.");
            }

            if (capacity <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(capacity),
                    capacity,
                    "A capacidade precisa ser maior que zero.");
            }

            Owner = owner;
            Capacity = capacity;
            _runes = new List<RuneInstance>();
            _view = _runes.AsReadOnly();
        }

        public bool Contains(
            RuneInstanceId instanceId)
        {
            return IndexOf(instanceId) >= 0;
        }

        public RuneInventoryAddResult TryAdd(
            RuneInstance rune)
        {
            if (rune == null)
            {
                throw new ArgumentNullException(
                    nameof(rune));
            }

            if (Contains(rune.InstanceId))
            {
                return RuneInventoryAddResult
                    .DuplicateInstance;
            }

            if (!rune.IsIntangible &&
                OccupiedSlots >= Capacity)
            {
                return RuneInventoryAddResult
                    .CapacityReached;
            }

            _runes.Add(rune);
            return RuneInventoryAddResult.Added;
        }

        public bool TryRemove(
            RuneInstanceId instanceId,
            RuneRemovalReason reason,
            out RuneRemovalResult result)
        {
            int index = IndexOf(instanceId);

            if (index < 0)
            {
                result = null;
                return false;
            }

            RuneInstance rune = _runes[index];
            _runes.RemoveAt(index);

            result = new RuneRemovalResult(
                rune,
                reason);

            return true;
        }

        private int IndexOf(
            RuneInstanceId instanceId)
        {
            for (int i = 0; i < _runes.Count; i++)
            {
                if (_runes[i].InstanceId ==
                    instanceId)
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
