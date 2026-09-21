using System;
using System.Collections.Generic;

namespace TicTacToeRoguelike.Domain.Effects.Capabilities
{
    public interface IRandomSource
    {
        ulong Seed { get; }
        int Roll(string stream, int index, int exclusiveMaximum);
    }
    public sealed class RandomRoll
    {
        public ulong Seed { get; }
        public string Stream { get; }
        public int Index { get; }
        public int Maximum { get; }
        public int Value { get; }
        public RandomRoll(ulong seed, string stream, int index, int maximum, int value)
        { Seed = seed; Stream = stream; Index = index; Maximum = maximum; Value = value; }
    }
    public sealed class EffectRandom
    {
        private readonly IRandomSource _source;
        private readonly List<RandomRoll> _rolls = new List<RandomRoll>();
        public IReadOnlyList<RandomRoll> Rolls => _rolls.AsReadOnly();
        public EffectRandom(IRandomSource source) { _source = source ?? throw new ArgumentNullException(nameof(source)); }
        public int Roll(EffectEvent evt, EffectSource source, string stream, int index, int exclusiveMaximum)
        {
            if (string.IsNullOrWhiteSpace(stream)) throw new ArgumentException("Stream required.", nameof(stream));
            string key = $"{evt.Id.Length}:{evt.Id}:{source.Key}:{stream}";
            int value = _source.Roll(key, index, exclusiveMaximum);
            _rolls.Add(new RandomRoll(_source.Seed, key, index, exclusiveMaximum, value));
            return value;
        }
    }
}
