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

        private const float StoneSize = 54f;
        private const float SlotHeight = 64f;
        private const float RestYOffset = 8f;

        private readonly RuneInventoryState _inventory;
        private readonly bool _allowActivation;
        private readonly Dictionary<string, RuneStoneVisual>
            _stonesByInstanceId =
                new Dictionary<string, RuneStoneVisual>(
                    StringComparer.Ordinal);
        private readonly Dictionary<string, Tween>
            _pulseTweens =
                new Dictionary<string, Tween>(
                    StringComparer.Ordinal);

        public event Action<RuneInstanceId> RuneActivated;

        public RuneInventoryView(
            RuneInventoryState inventory,
            bool allowActivation = false)
        {
            _inventory = inventory ??
                throw new ArgumentNullException(
                    nameof(inventory));

            _allowActivation = allowActivation;

            Alignment = BoxContainer.AlignmentMode.Begin;
            AddThemeConstantOverride(
                "separation",
                8);

            Rebuild();
        }

        public void Rebuild()
        {
            foreach (Tween tween in _pulseTweens.Values)
                tween?.Kill();

            _pulseTweens.Clear();
            _stonesByInstanceId.Clear();

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

                RuneStoneVisual visual =
                    CreateRuneStone(rune);

                _stonesByInstanceId[
                    rune.InstanceId.Value] =
                    visual;

                AddChild(visual.Slot);
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

                RuneStoneVisual visual =
                    CreateRuneStone(rune);

                _stonesByInstanceId[
                    rune.InstanceId.Value] =
                    visual;

                AddChild(visual.Slot);
            }
        }

        /// <summary>
        /// Atualiza quais pedras podem iniciar um targeting e qual está armada.
        /// Runas passivas continuam visíveis e com tooltip, mas não entram no modo
        /// especial ao clique.
        /// </summary>
        public void SetTargetingState(
            IReadOnlyList<RuneInstanceId> selectableSources,
            RuneInstanceId? selectedSource)
        {
            if (selectableSources == null)
            {
                throw new ArgumentNullException(
                    nameof(selectableSources));
            }

            HashSet<string> selectable =
                new HashSet<string>(
                    StringComparer.Ordinal);

            for (int i = 0;
                 i < selectableSources.Count;
                 i++)
            {
                selectable.Add(
                    selectableSources[i].Value);
            }

            foreach (KeyValuePair<string, RuneStoneVisual> pair
                     in _stonesByInstanceId)
            {
                RuneStoneVisual visual =
                    pair.Value;

                visual.Selectable =
                    _allowActivation &&
                    selectable.Contains(pair.Key);

                visual.Selected =
                    visual.Selectable &&
                    selectedSource.HasValue &&
                    selectedSource.Value.Value ==
                    pair.Key;

                ApplyInteractionVisual(visual);
            }
        }

        public bool PulseSource(
            string sourceInstanceId)
        {
            if (string.IsNullOrWhiteSpace(
                    sourceInstanceId))
            {
                return false;
            }

            if (!_stonesByInstanceId.TryGetValue(
                    sourceInstanceId,
                    out RuneStoneVisual visual))
            {
                return false;
            }

            if (_pulseTweens.TryGetValue(
                    sourceInstanceId,
                    out Tween previous))
            {
                previous?.Kill();
            }

            Control stone =
                visual.Stone;

            stone.PivotOffset =
                stone.Size * 0.5f;

            Vector2 restingScale =
                visual.Selected
                    ? new Vector2(1.06f, 1.06f)
                    : Vector2.One;

            stone.Scale = restingScale;

            Tween tween = CreateTween();

            tween.TweenProperty(
                    stone,
                    "scale",
                    new Vector2(1.28f, 1.28f),
                    0.12d)
                .SetTrans(
                    Tween.TransitionType.Back)
                .SetEase(
                    Tween.EaseType.Out);

            tween.TweenProperty(
                    stone,
                    "scale",
                    restingScale,
                    0.18d)
                .SetTrans(
                    Tween.TransitionType.Quad)
                .SetEase(
                    Tween.EaseType.Out);

            tween.TweenCallback(
                Callable.From(
                    () => _pulseTweens.Remove(
                        sourceInstanceId)));

            _pulseTweens[sourceInstanceId] =
                tween;

            return true;
        }

        private RuneStoneVisual CreateRuneStone(
            RuneInstance rune)
        {
            Control slot =
                new Control
                {
                    CustomMinimumSize =
                        new Vector2(
                            StoneSize,
                            SlotHeight),
                    MouseFilter =
                        MouseFilterEnum.Ignore
                };

            PanelContainer stone =
                new PanelContainer
                {
                    CustomMinimumSize =
                        new Vector2(
                            StoneSize,
                            StoneSize),
                    Size =
                        new Vector2(
                            StoneSize,
                            StoneSize),
                    Position =
                        new Vector2(
                            0f,
                            RestYOffset),
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
            slot.AddChild(stone);

            RuneStoneVisual visual =
                new RuneStoneVisual(
                    rune,
                    slot,
                    stone,
                    style);

            if (_allowActivation)
            {
                stone.GuiInput +=
                    inputEvent =>
                        OnRuneGuiInput(
                            visual,
                            inputEvent);
            }

            ApplyInteractionVisual(visual);
            return visual;
        }

        private void OnRuneGuiInput(
            RuneStoneVisual visual,
            InputEvent inputEvent)
        {
            if (!_allowActivation ||
                !visual.Selectable ||
                !(inputEvent is InputEventMouseButton mouse) ||
                mouse.ButtonIndex != MouseButton.Left ||
                !mouse.Pressed)
            {
                return;
            }

            AcceptEvent();
            RuneActivated?.Invoke(
                visual.Rune.InstanceId);
        }

        private static void ApplyInteractionVisual(
            RuneStoneVisual visual)
        {
            visual.Stone.Position =
                new Vector2(
                    0f,
                    visual.Selected
                        ? 0f
                        : RestYOffset);

            visual.Stone.Scale =
                visual.Selected
                    ? new Vector2(1.06f, 1.06f)
                    : Vector2.One;

            visual.Stone.PivotOffset =
                new Vector2(
                    StoneSize * 0.5f,
                    StoneSize * 0.5f);

            visual.Stone.MouseDefaultCursorShape =
                visual.Selectable
                    ? CursorShape.PointingHand
                    : CursorShape.Arrow;

            int borderWidth =
                visual.Selected
                    ? 3
                    : visual.Rune.IsIntangible
                        ? 1
                        : 2;

            visual.Style.BorderWidthLeft = borderWidth;
            visual.Style.BorderWidthTop = borderWidth;
            visual.Style.BorderWidthRight = borderWidth;
            visual.Style.BorderWidthBottom = borderWidth;

            visual.Style.BgColor =
                visual.Selected
                    ? new Color(
                        0.14f,
                        0.13f,
                        0.11f,
                        1f)
                    : Stone;
        }

        private static Control CreateEmptySlot()
        {
            Control wrapper =
                new Control
                {
                    CustomMinimumSize =
                        new Vector2(
                            StoneSize,
                            SlotHeight),
                    MouseFilter =
                        MouseFilterEnum.Ignore
                };

            PanelContainer slot =
                new PanelContainer
                {
                    CustomMinimumSize =
                        new Vector2(
                            StoneSize,
                            StoneSize),
                    Size =
                        new Vector2(
                            StoneSize,
                            StoneSize),
                    Position =
                        new Vector2(
                            0f,
                            RestYOffset),
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

            wrapper.AddChild(slot);
            return wrapper;
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

        private sealed class RuneStoneVisual
        {
            public RuneInstance Rune { get; }
            public Control Slot { get; }
            public PanelContainer Stone { get; }
            public StyleBoxFlat Style { get; }
            public bool Selectable { get; set; }
            public bool Selected { get; set; }

            public RuneStoneVisual(
                RuneInstance rune,
                Control slot,
                PanelContainer stone,
                StyleBoxFlat style)
            {
                Rune = rune;
                Slot = slot;
                Stone = stone;
                Style = style;
            }
        }
    }
}
