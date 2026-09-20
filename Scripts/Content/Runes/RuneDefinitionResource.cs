using System;
using Godot;
using TicTacToeRoguelike.Domain.Runes;

namespace TicTacToeRoguelike.Content.Runes
{
    [GlobalClass]
    public sealed partial class RuneDefinitionResource :
        Resource
    {
        [Export]
        public string DefinitionId { get; set; } =
            "rune.unnamed";

        [Export]
        public string DisplayName { get; set; } =
            "Runa";

        [Export(PropertyHint.MultilineText)]
        public string Description { get; set; } =
            string.Empty;

        [Export]
        public RuneRarity Rarity { get; set; } =
            RuneRarity.Common;

        [Export]
        public string Glyph { get; set; } =
            "ᚱ";

        public RuneDefinition ToDomain()
        {
            return new RuneDefinition(
                new RuneDefinitionId(
                    DefinitionId),
                DisplayName,
                Description,
                Rarity,
                Glyph);
        }
    }

    public static class StarterRuneCatalog
    {
        private const string PilotRuneResourcePath =
            "res://Content/Runes/Definitions/PilotIndependentMultiplier.tres";
        public static RuneInventoryState
            CreatePlayerInventory()
        {
            RuneInventoryState inventory =
                new RuneInventoryState(
                    TicTacToeRoguelike.Domain.Scoring
                        .ScoreActor.Player);

            Add(
                inventory,
                "rune.echo-stone",
                "Eco de Pedra",
                "Uma runa estável de teste. Os efeitos entram no próximo marco.",
                RuneRarity.Common,
                "ᚠ");

            Add(
                inventory,
                "rune.dawn-seal",
                "Selo da Aurora",
                "Protótipo de runa abençoada, ainda sem alterar a pontuação.",
                RuneRarity.Rare,
                "ᛉ",
                RuneAttributeId.Blessed);

            Add(
                inventory,
                "rune.ethereal-whisper",
                "Sussurro Etéreo",
                "Runa intangível: permanece no inventário sem consumir uma das cinco vagas.",
                RuneRarity.Legendary,
                "ᛟ",
                RuneAttributeId.Intangible);

            AddPilot(inventory);

            return inventory;
        }

        public static RuneInventoryState
            CreateEnemyInventory()
        {
            RuneInventoryState inventory =
                new RuneInventoryState(
                    TicTacToeRoguelike.Domain.Scoring
                        .ScoreActor.Enemy);

            Add(
                inventory,
                "rune.grey-mark",
                "Marca Cinzenta",
                "Runa de teste do oponente. Ainda não possui efeito ativo.",
                RuneRarity.Common,
                "ᚦ");

            Add(
                inventory,
                "rune.cracked-fragment",
                "Fragmento Rachado",
                "Protótipo de runa quebrada do oponente.",
                RuneRarity.Rare,
                "ᚾ",
                RuneAttributeId.Broken);

            AddPilot(inventory);

            return inventory;
        }

        private static void AddPilot(
            RuneInventoryState inventory)
        {
            RuneDefinitionResource resource =
                ResourceLoader.Load<RuneDefinitionResource>(
                    PilotRuneResourcePath);

            if (resource == null)
            {
                throw new InvalidOperationException(
                    $"Não foi possível carregar a runa piloto em {PilotRuneResourcePath}.");
            }

            RuneDefinition definition =
                resource.ToDomain();

            if (definition.Id !=
                RuneDefinitionIds
                    .PilotIndependentMultiplier)
            {
                throw new InvalidOperationException(
                    "O asset da runa piloto possui DefinitionId incorreto.");
            }

            Add(
                inventory,
                definition);
        }

        private static void Add(
            RuneInventoryState inventory,
            string id,
            string name,
            string description,
            RuneRarity rarity,
            string glyph,
            params RuneAttributeId[] attributes)
        {
            RuneDefinition definition =
                new RuneDefinition(
                    new RuneDefinitionId(id),
                    name,
                    description,
                    rarity,
                    glyph);

            Add(
                inventory,
                definition,
                attributes);
        }

        private static void Add(
            RuneInventoryState inventory,
            RuneDefinition definition,
            params RuneAttributeId[] attributes)
        {
            RuneInventoryAddResult result =
                inventory.TryAdd(
                    RuneInstance.Create(
                        definition,
                        attributes));

            if (result !=
                RuneInventoryAddResult.Added)
            {
                throw new InvalidOperationException(
                    $"Falha ao montar inventário inicial: {result}.");
            }
        }
    }
}
