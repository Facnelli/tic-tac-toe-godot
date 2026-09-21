using System;
using System.Text;
using TicTacToeRoguelike.Domain.Effects.Capabilities;
using TicTacToeRoguelike.Domain.Runes;

namespace TicTacToeRoguelike.Infrastructure.Random
{
    // Versioned, stateless streams. Stable across processes and .NET versions.
    public sealed class SeededRandomSource : IRandomSource
    {
        public ulong Seed { get; }
        public SeededRandomSource(ulong seed) { Seed = seed; }
        public int Roll(string stream, int index, int exclusiveMaximum)
        {
            if (string.IsNullOrEmpty(stream)) throw new ArgumentException("Stream required.", nameof(stream));
            if (index < 0 || exclusiveMaximum <= 0) throw new ArgumentOutOfRangeException(nameof(index));
            ulong hash = 14695981039346656037UL ^ Seed;
            foreach (byte b in Encoding.UTF8.GetBytes(stream)) hash = unchecked((hash ^ b) * 1099511628211UL);
            hash ^= (ulong)index;
            // Rejection sampling avoids modulo bias.
            ulong bound = (ulong)exclusiveMaximum;
            ulong threshold = unchecked(0UL - bound) % bound;
            while (true)
            {
                hash = unchecked(hash + 0x9E3779B97F4A7C15UL);
                ulong value = hash;
                value = unchecked((value ^ (value >> 30)) * 0xBF58476D1CE4E5B9UL);
                value = unchecked((value ^ (value >> 27)) * 0x94D049BB133111EBUL);
                value ^= value >> 31;
                if (value >= threshold) return (int)(value % bound);
            }
        }
    }
    public sealed class RuneInstanceIdSequence
    {
        private readonly string _scope;
        private long _next;
        public RuneInstanceIdSequence(string scope)
        { if (string.IsNullOrWhiteSpace(scope)) throw new ArgumentException("Scope required."); _scope = scope; }
        public RuneInstanceId Next() => new RuneInstanceId($"{_scope}:{checked(++_next):D8}");
    }
}
