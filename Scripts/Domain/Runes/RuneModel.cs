using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace TicTacToeRoguelike.Domain.Runes
{
    public enum RuneRarity
    {
        Common = 0,
        Rare = 1,
        Legendary = 2,
        Divine = 3
    }

    public enum RuneAttributeId
    {
        Blessed = 0,
        Cursed = 1,
        Broken = 2,
        Intangible = 3
    }

    public readonly struct RuneDefinitionId :
        IEquatable<RuneDefinitionId>
    {
        public string Value { get; }

        public RuneDefinitionId(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException(
                    "RuneDefinitionId não pode ser vazio.",
                    nameof(value));
            }

            Value = value.Trim();
        }

        public bool Equals(RuneDefinitionId other) =>
            string.Equals(
                Value,
                other.Value,
                StringComparison.Ordinal);

        public override bool Equals(object obj) =>
            obj is RuneDefinitionId other &&
            Equals(other);

        public override int GetHashCode() =>
            Value == null
                ? 0
                : StringComparer.Ordinal.GetHashCode(Value);

        public override string ToString() => Value ?? string.Empty;

        public static bool operator ==(
            RuneDefinitionId left,
            RuneDefinitionId right) =>
            left.Equals(right);

        public static bool operator !=(
            RuneDefinitionId left,
            RuneDefinitionId right) =>
            !left.Equals(right);
    }

    public readonly struct RuneInstanceId :
        IEquatable<RuneInstanceId>
    {
        public string Value { get; }

        public RuneInstanceId(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException(
                    "RuneInstanceId não pode ser vazio.",
                    nameof(value));
            }

            Value = value.Trim();
        }

        public static RuneInstanceId New() =>
            new RuneInstanceId(
                Guid.NewGuid().ToString("N"));

        public bool Equals(RuneInstanceId other) =>
            string.Equals(
                Value,
                other.Value,
                StringComparison.Ordinal);

        public override bool Equals(object obj) =>
            obj is RuneInstanceId other &&
            Equals(other);

        public override int GetHashCode() =>
            Value == null
                ? 0
                : StringComparer.Ordinal.GetHashCode(Value);

        public override string ToString() => Value ?? string.Empty;

        public static bool operator ==(
            RuneInstanceId left,
            RuneInstanceId right) =>
            left.Equals(right);

        public static bool operator !=(
            RuneInstanceId left,
            RuneInstanceId right) =>
            !left.Equals(right);
    }

    public sealed class RuneAttributeSet
    {
        private readonly List<RuneAttributeId> _ordered;
        private readonly HashSet<RuneAttributeId> _lookup;
        private readonly ReadOnlyCollection<RuneAttributeId> _view;

        public IReadOnlyList<RuneAttributeId> Values => _view;
        public int Count => _ordered.Count;

        public RuneAttributeSet(
            IEnumerable<RuneAttributeId> attributes = null)
        {
            _ordered = new List<RuneAttributeId>();
            _lookup = new HashSet<RuneAttributeId>();

            if (attributes != null)
            {
                foreach (RuneAttributeId attribute in attributes)
                {
                    Validate(attribute);

                    if (_lookup.Add(attribute))
                        _ordered.Add(attribute);
                }
            }

            _view = _ordered.AsReadOnly();
        }

        public bool Contains(RuneAttributeId attribute) =>
            _lookup.Contains(attribute);

        private static void Validate(RuneAttributeId attribute)
        {
            if (!Enum.IsDefined(typeof(RuneAttributeId), attribute))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(attribute),
                    attribute,
                    "Atributo de runa inválido.");
            }
        }
    }

    public sealed class RuneDefinition
    {
        public RuneDefinitionId Id { get; }
        public string DisplayName { get; }
        public string Description { get; }
        public RuneRarity Rarity { get; }
        public string Glyph { get; }

        public RuneDefinition(
            RuneDefinitionId id,
            string displayName,
            string description,
            RuneRarity rarity,
            string glyph = "ᚱ")
        {
            if (string.IsNullOrWhiteSpace(displayName))
            {
                throw new ArgumentException(
                    "A runa precisa possuir nome.",
                    nameof(displayName));
            }

            if (!Enum.IsDefined(typeof(RuneRarity), rarity))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(rarity),
                    rarity,
                    "Raridade de runa inválida.");
            }

            Id = id;
            DisplayName = displayName.Trim();
            Description = description?.Trim() ?? string.Empty;
            Rarity = rarity;
            Glyph = string.IsNullOrWhiteSpace(glyph)
                ? "ᚱ"
                : glyph.Trim();
        }
    }

    public sealed class RuneInstance
    {
        public RuneInstanceId InstanceId { get; }
        public RuneDefinition Definition { get; }
        public RuneAttributeSet Attributes { get; }

        public bool IsIntangible =>
            Attributes.Contains(
                RuneAttributeId.Intangible);

        public RuneInstance(
            RuneInstanceId instanceId,
            RuneDefinition definition,
            RuneAttributeSet attributes = null)
        {
            Definition = definition ??
                throw new ArgumentNullException(
                    nameof(definition));

            InstanceId = instanceId;
            Attributes = attributes ??
                new RuneAttributeSet();
        }

        public static RuneInstance Create(
            RuneDefinition definition,
            params RuneAttributeId[] attributes) =>
            new RuneInstance(
                RuneInstanceId.New(),
                definition,
                new RuneAttributeSet(attributes));
    }
}
