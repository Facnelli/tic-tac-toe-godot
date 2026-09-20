using System;
using System.Collections.Generic;
using Godot;
using TicTacToeRoguelike.Domain.Runes;

namespace TicTacToeRoguelike.Presentation.Runes
{
    public sealed partial class RuneInventoryView :
        HBoxContainer
    {
        private static readonly Color Stone =
            new Color(0.085f, 0.090f, 0.095f, 0.98f);

        private static readonly Color EmptyStone =
            new Color(0.035f, 0.040f, 0.044f, 0.78f);

        private readonly RuneInventoryState _inventory;

        public RuneInventoryView(
            RuneInventoryState inventory)
        {
            _inventory = inventory ??
                throw new ArgumentNullException(
                    nameof(inventory));

            Alignment = AlignmentMode.Begin;
            AddThemeConstantOverride(
                "separation",
                8);

            Rebuild();
        }

        public void Rebuild()
        {
            foreach (Node child in GetChildren())
            {
                RemoveChild(child);
                child.QueueFree();
            }

            int normalShown = 0;

            for (int i = 0;
                 i < _inventory.Runes.Count;
                 i++)
            {
                RuneInstance rune =
                    _inventory.Runes[i];

                if (rune.IsIntangible)
                    continue;

                AddChild(CreateRuneStone(rune));
                normalShown++;
            }

            while (normalShown <
                   _inventory.Capacity)
            {
                AddChild(CreateEmptySlot());
                normalShown++;
            }

            for (int i = 0;
                 i < _inventory.Runes.Count;
                 i++)
            {
                RuneInstance rune =
                    _inventory.Runes[i];

                if (!rune.IsIntangible)
                    continue;

                AddChild(CreateRuneStone(rune));
            }
        }

        private static Control CreateRuneStone(
            RuneInstance rune)
        {
            PanelContainer stone =
                new PanelContainer
                {
                    CustomMinimumSize =
                        new Vector2(54f, 54f),
                    MouseFilter =
                        MouseFilterEnum.Stop,
                    TooltipText =
                        BuildTooltip(rune)
                };

            StyleBoxFlat style =
                new StyleBoxFlat
                {
                    BgColor = Stone,
                    BorderColor =
                        GetRarityColor(
                            rune.Definition.Rarity),
                    BorderWidthLeft = 2,
                    BorderWidthTop = 2,
                    BorderWidthRight = 2,
                    BorderWidthBottom = 2,
                    CornerRadiusTopLeft = 14,
                    CornerRadiusTopRight = 8,
                    CornerRadiusBottomLeft = 9,
                    CornerRadiusBottomRight = 15,
                    ContentMarginLeft = 4,
                    ContentMarginRight = 4,
                    ContentMarginTop = 4,
                    ContentMarginBottom = 4
                };

            if (rune.IsIntangible)
            {
                style.BorderWidthLeft = 1;
                style.BorderWidthTop = 1;
                style.BorderWidthRight = 1;
                style.BorderWidthBottom = 1;
            }

            stone.AddThemeStyleboxOverride(
                "panel",
                style);

            Label glyph =
                new Label
                {
                    Text =
                        rune.Definition.Glyph,
                    HorizontalAlignment =
                        HorizontalAlignment.Center,
                    VerticalAlignment =
                        VerticalAlignment.Center,
                    MouseFilter =
                        MouseFilterEnum.Ignore
                };

            glyph.AddThemeFontSizeOverride(
                "font_size",
                27);

            glyph.AddThemeColorOverride(
                "font_color",
                GetRarityColor(
                    rune.Definition.Rarity));

            stone.AddChild(glyph);
            return stone;
        }

        private static Control CreateEmptySlot()
        {
            PanelContainer slot =
                new PanelContainer
                {
                    CustomMinimumSize =
                        new Vector2(54f, 54f),
                    MouseFilter =
                        MouseFilterEnum.Ignore
                };

            StyleBoxFlat style =
                new StyleBoxFlat
                {
                    BgColor = EmptyStone,
                    BorderColor =
                        new Color(
                            0.24f,
                            0.24f,
                            0.23f,
                            0.65f),
                    BorderWidthLeft = 1,
                    BorderWidthTop = 1,
                    BorderWidthRight = 1,
                    BorderWidthBottom = 1,
                    CornerRadiusTopLeft = 12,
                    CornerRadiusTopRight = 7,
                    CornerRadiusBottomLeft = 8,
                    CornerRadiusBottomRight = 13
                };

            slot.AddThemeStyleboxOverride(
                "panel",
                style);

            return slot;
        }

        private static string BuildTooltip(
            RuneInstance rune)
        {
            List<string> attributes =
                new List<string>();

            for (int i = 0;
                 i < rune.Attributes.Values.Count;
                 i++)
            {
                attributes.Add(
                    TranslateAttribute(
                        rune.Attributes.Values[i]));
            }

            string attributeText =
                attributes.Count == 0
                    ? "Sem atributos"
                    : string.Join(
                        ", ",
                        attributes);

            return
                $"{rune.Definition.DisplayName}\n" +
                $"{TranslateRarity(rune.Definition.Rarity)}\n" +
                $"{attributeText}\n\n" +
                rune.Definition.Description;
        }

        private static string TranslateRarity(
            RuneRarity rarity)
        {
            switch (rarity)
            {
                case RuneRarity.Common:
                    return "Comum";
                case RuneRarity.Rare:
                    return "Rara";
                case RuneRarity.Legendary:
                    return "Lendária";
                case RuneRarity.Divine:
                    return "Divina";
                default:
                    return rarity.ToString();
            }
        }

        private static string TranslateAttribute(
            RuneAttributeId attribute)
        {
            switch (attribute)
            {
                case RuneAttributeId.Blessed:
                    return "Abençoada";
                case RuneAttributeId.Cursed:
                    return "Amaldiçoada";
                case RuneAttributeId.Broken:
                    return "Quebrada";
                case RuneAttributeId.Intangible:
                    return "Intangível";
                default:
                    return attribute.ToString();
            }
        }

        private static Color GetRarityColor(
            RuneRarity rarity)
        {
            switch (rarity)
            {
                case RuneRarity.Rare:
                    return new Color(
                        0.28f,
                        0.72f,
                        0.93f,
                        1f);

                case RuneRarity.Legendary:
                    return new Color(
                        0.86f,
                        0.51f,
                        0.97f,
                        1f);

                case RuneRarity.Divine:
                    return new Color(
                        1f,
                        0.82f,
                        0.28f,
                        1f);

                default:
                    return new Color(
                        0.72f,
                        0.70f,
                        0.64f,
                        1f);
            }
        }
    }
}
