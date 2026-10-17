using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using IdleExile.Core.Models;
using IdleExile.Core.Simulation;

namespace IdleExile.UI.Components;

/// <summary>
/// Data record defining a telemetry threat badge displayed in the map header.
/// Provides visual icon, warning text, border/text colors, and informative tooltip.
/// </summary>
public sealed record ThreatBadgeInfo(
    string Key,
    string IconText,
    string ShortText,
    Color BadgeColor,
    string TooltipDescription
)
{
    /// <summary>
    /// Factory that inspects a <see cref="RolledMapModifier"/> and returns an appropriate
    /// threat badge if the modifier constitutes a high/medium combat danger.
    /// </summary>
    public static ThreatBadgeInfo? FromModifier(RolledMapModifier mod)
    {
        string id = mod.Definition.Id.ToLowerInvariant();
        float val = mod.RolledValue;

        if (id.Contains("reflect_elemental"))
        {
            return new ThreatBadgeInfo(
                Key: "reflect_ele",
                IconText: "⚠️",
                ShortText: "Ele Refl",
                BadgeColor: new Color(1.0f, 0.25f, 0.25f), // Crimson Red
                TooltipDescription: $"DANGER: Monsters reflect {val}% of Elemental Damage!\nSelf-cast/attack elemental builds will suffer fatal self-damage."
            );
        }

        if (id.Contains("reflect_physical"))
        {
            return new ThreatBadgeInfo(
                Key: "reflect_phys",
                IconText: "⚠️",
                ShortText: "Phys Refl",
                BadgeColor: new Color(1.0f, 0.25f, 0.25f),
                TooltipDescription: $"DANGER: Monsters reflect {val}% of Physical Damage!\nPhysical attack/spell builds will suffer fatal self-damage."
            );
        }

        if (id.Contains("reduced_max_res"))
        {
            return new ThreatBadgeInfo(
                Key: "minus_max_res",
                IconText: "🛡️",
                ShortText: "-Max Res",
                BadgeColor: new Color(0.25f, 0.80f, 1.0f), // Cyan Ice
                TooltipDescription: $"DANGER: Players have -{val}% to Maximum Elemental Resistances.\nElemental damage taken is dramatically increased!"
            );
        }

        if (id.Contains("no_leech") || id.Contains("cannot_leech"))
        {
            return new ThreatBadgeInfo(
                Key: "no_leech",
                IconText: "🩸",
                ShortText: "No Leech",
                BadgeColor: new Color(1.0f, 0.15f, 0.35f), // Blood Red
                TooltipDescription: "DANGER: Cannot Leech Life or Mana!\nLife sustain relies strictly on Life Flasks and passive regeneration."
            );
        }

        if (id.Contains("extra_damage_cold"))
        {
            return new ThreatBadgeInfo(
                Key: "extra_cold",
                IconText: "❄️",
                ShortText: "+Cold",
                BadgeColor: new Color(0.50f, 0.85f, 1.0f),
                TooltipDescription: $"Monsters deal {val}% extra damage as Cold.\nIncreases elemental hit damage and risk of being Chilled or Frozen."
            );
        }

        if (id.Contains("extra_damage_fire"))
        {
            return new ThreatBadgeInfo(
                Key: "extra_fire",
                IconText: "🔥",
                ShortText: "+Fire",
                BadgeColor: new Color(1.0f, 0.45f, 0.20f),
                TooltipDescription: $"Monsters deal {val}% extra damage as Fire.\nIncreases elemental hit damage and Ignite DoT risk."
            );
        }

        if (id.Contains("extra_damage_lightning"))
        {
            return new ThreatBadgeInfo(
                Key: "extra_light",
                IconText: "⚡",
                ShortText: "+Light",
                BadgeColor: new Color(1.0f, 0.85f, 0.25f),
                TooltipDescription: $"Monsters deal {val}% extra damage as Lightning.\nHigh damage variance and risk of heavy Shock debuff (+% DMG taken)."
            );
        }

        if (id.Contains("extra_damage_chaos"))
        {
            return new ThreatBadgeInfo(
                Key: "extra_chaos",
                IconText: "🧪",
                ShortText: "+Chaos",
                BadgeColor: new Color(0.85f, 0.25f, 0.95f),
                TooltipDescription: $"Monsters deal {val}% extra damage as Chaos.\nChaos damage bypasses Energy Shield unless Chaos Inoculation is active."
            );
        }

        if (id.Contains("cannot_be_frozen") || id.Contains("cannot_be_chilled"))
        {
            return new ThreatBadgeInfo(
                Key: "no_freeze",
                IconText: "❄️❌",
                ShortText: "No Freeze",
                BadgeColor: new Color(0.60f, 0.75f, 0.95f),
                TooltipDescription: "Monsters cannot be Frozen or Chilled.\nCold crowd-control defensive layers are nullified."
            );
        }

        if (id.Contains("cannot_be_ignited"))
        {
            return new ThreatBadgeInfo(
                Key: "no_ignite",
                IconText: "🔥❌",
                ShortText: "No Ignite",
                BadgeColor: new Color(0.95f, 0.65f, 0.50f),
                TooltipDescription: "Monsters cannot be Ignited.\nIgnite-based fire damage over time builds deal 0 DoT damage."
            );
        }

        if (id.Contains("cannot_be_shocked"))
        {
            return new ThreatBadgeInfo(
                Key: "no_shock",
                IconText: "⚡❌",
                ShortText: "No Shock",
                BadgeColor: new Color(0.95f, 0.85f, 0.40f),
                TooltipDescription: "Monsters cannot be Shocked.\nShock damage amplification cannot be applied."
            );
        }

        if (id.Contains("immune_to_ailments"))
        {
            return new ThreatBadgeInfo(
                Key: "immune_ailments",
                IconText: "🛡️❌",
                ShortText: "Immune Ailments",
                BadgeColor: new Color(0.80f, 0.55f, 0.85f),
                TooltipDescription: "Monsters are immune to all Status Ailments (Ignite, Chill, Freeze, Shock, Bleed, Poison)."
            );
        }

        if (id.Contains("increased_speed"))
        {
            return new ThreatBadgeInfo(
                Key: "turbo",
                IconText: "💨",
                ShortText: "Turbo",
                BadgeColor: new Color(0.95f, 0.80f, 0.30f),
                TooltipDescription: $"Monsters have {val}% increased Movement and Attack Speed."
            );
        }

        // Generic fallback for dangerous high-quantity affixes (>30% bonus quantity)
        if (mod.Definition.BonusQuantity >= 30)
        {
            return new ThreatBadgeInfo(
                Key: mod.Definition.Id,
                IconText: "⚠️",
                ShortText: "High Danger",
                BadgeColor: new Color(0.95f, 0.60f, 0.20f),
                TooltipDescription: mod.FormattedName
            );
        }

        return null;
    }
}

/// <summary>
/// Map Header & Threat Telemetry Component (Task 2.2).
/// Displays:
/// 1. Map Name with rarity color coding (Normal, Magic, Rare, Unique, Corrupted).
/// 2. Tier Badge with tier color coding (White T1-T5, Yellow T6-T10, Red T11-T15, Purple T16+).
/// 3. Threat Badges with icons, danger levels, and hover tooltips for rapid risk assessment.
/// 4. Bonus statistics (IIQ, IIR, Pack Size) with comprehensive full map telemetry tooltip.
/// 5. Monster Pack Clear Progress Bar (0% to 100%) with Boss Spawn threshold indicator.
/// </summary>
public partial class MapHeaderBar : PanelContainer
{
    // --- Node References ---
    private PanelContainer? _tierBadge;
    private Label? _tierBadgeLabel;
    private Label? _mapNameLabel;
    private HBoxContainer? _threatBadgesHBox;
    private Label? _quantityBonusLabel;
    private ProgressBar? _mapProgressBar;
    private Label? _progressPercentLabel;
    private PanelContainer? _bossIndicator;
    private Label? _bossIndicatorLabel;

    // --- State & Telemetry Snapshot ---
    public string CurrentMapName { get; private set; } = "The Twilight Strand";
    public int CurrentTier { get; private set; } = 1;
    public string CurrentRarity { get; private set; } = "Normal";
    public int CurrentIIQ { get; private set; } = 0;
    public int CurrentIIR { get; private set; } = 0;
    public int CurrentPackSize { get; private set; } = 0;
    public float CurrentProgress { get; private set; } = 0f;
    public string? CurrentBossName { get; private set; }
    public bool IsBossActive => CurrentProgress >= 100f;
    public int ActiveThreatCount => _activeThreats.Count;

    // --- Sanctuary State & Telemetry ---
    public bool IsInSanctuary { get; private set; } = false;
    public int SanctuaryTier { get; private set; } = 1;
    public string SanctuaryTierName { get; private set; } = "Hollow Crypt";
    public long SanctuaryDust { get; private set; } = 0;
    public int QueuedMapCount { get; private set; } = 0;

    private readonly List<ThreatBadgeInfo> _activeThreats = new();
    private readonly List<string> _activeModifierDescriptions = new();

    public override void _Ready()
    {
        BindNodes();
    }

    private void BindNodes()
    {
        _tierBadge = GetNodeOrNull<PanelContainer>("%TierBadge") ?? GetNodeOrNull<PanelContainer>("HeaderMargin/HeaderVBox/MapInfoHBox/TierBadge");
        _tierBadgeLabel = GetNodeOrNull<Label>("%TierBadgeLabel") ?? GetNodeOrNull<Label>("HeaderMargin/HeaderVBox/MapInfoHBox/TierBadge/TierBadgeLabel");
        _mapNameLabel = GetNodeOrNull<Label>("%MapNameLabel") ?? GetNodeOrNull<Label>("HeaderMargin/HeaderVBox/MapInfoHBox/MapNameLabel");
        _threatBadgesHBox = GetNodeOrNull<HBoxContainer>("%ThreatBadgesHBox") ?? GetNodeOrNull<HBoxContainer>("HeaderMargin/HeaderVBox/MapInfoHBox/ThreatBadgesHBox");
        _quantityBonusLabel = GetNodeOrNull<Label>("%QuantityBonusLabel") ?? GetNodeOrNull<Label>("HeaderMargin/HeaderVBox/MapInfoHBox/QuantityBonusLabel");
        _mapProgressBar = GetNodeOrNull<ProgressBar>("%MapProgressBar") ?? GetNodeOrNull<ProgressBar>("HeaderMargin/HeaderVBox/ProgressHBox/MapProgressBar") ?? GetNodeOrNull<ProgressBar>("HeaderMargin/HeaderVBox/MapProgressBar");
        _progressPercentLabel = GetNodeOrNull<Label>("%ProgressPercentLabel") ?? GetNodeOrNull<Label>("HeaderMargin/HeaderVBox/ProgressHBox/ProgressPercentLabel");
        _bossIndicator = GetNodeOrNull<PanelContainer>("%BossIndicator") ?? GetNodeOrNull<PanelContainer>("HeaderMargin/HeaderVBox/ProgressHBox/BossIndicator");
        _bossIndicatorLabel = GetNodeOrNull<Label>("%BossIndicatorLabel") ?? GetNodeOrNull<Label>("HeaderMargin/HeaderVBox/ProgressHBox/BossIndicator/BossIndicatorLabel");

        // If instantiated programmatically without scene hierarchy, build hierarchy dynamically
        if (_tierBadgeLabel == null || _mapNameLabel == null || _mapProgressBar == null)
        {
            BuildDefaultHierarchy();
        }
    }

    /// <summary>
    /// Programmatically constructs default Control hierarchy if MapHeaderBar was instantiated via new MapHeaderBar()
    /// rather than from a Godot .tscn scene file.
    /// </summary>
    private void BuildDefaultHierarchy()
    {
        CustomMinimumSize = new Vector2(640, 32);

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 12);
        margin.AddThemeConstantOverride("margin_right", 12);
        margin.AddThemeConstantOverride("margin_top", 2);
        margin.AddThemeConstantOverride("margin_bottom", 2);
        AddChild(margin);

        var headerVBox = new VBoxContainer();
        headerVBox.AddThemeConstantOverride("separation", 3);
        margin.AddChild(headerVBox);

        // Row 1: Map Info
        var mapInfoHBox = new HBoxContainer();
        mapInfoHBox.AddThemeConstantOverride("separation", 6);
        headerVBox.AddChild(mapInfoHBox);

        _tierBadge = new PanelContainer();
        _tierBadge.CustomMinimumSize = new Vector2(24, 16);
        mapInfoHBox.AddChild(_tierBadge);

        _tierBadgeLabel = new Label();
        _tierBadgeLabel.Text = "T1";
        _tierBadgeLabel.HorizontalAlignment = HorizontalAlignment.Center;
        _tierBadgeLabel.VerticalAlignment = VerticalAlignment.Center;
        _tierBadgeLabel.AddThemeFontSizeOverride("font_size", 10);
        _tierBadge.AddChild(_tierBadgeLabel);

        _mapNameLabel = new Label();
        _mapNameLabel.Text = "Twilight Strand";
        _mapNameLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _mapNameLabel.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        _mapNameLabel.AddThemeFontSizeOverride("font_size", 11);
        mapInfoHBox.AddChild(_mapNameLabel);

        _threatBadgesHBox = new HBoxContainer();
        _threatBadgesHBox.AddThemeConstantOverride("separation", 4);
        mapInfoHBox.AddChild(_threatBadgesHBox);

        _quantityBonusLabel = new Label();
        _quantityBonusLabel.Text = "IIQ: +0%";
        _quantityBonusLabel.AddThemeFontSizeOverride("font_size", 10);
        mapInfoHBox.AddChild(_quantityBonusLabel);

        // Row 2: Progress & Boss indicator
        var progressHBox = new HBoxContainer();
        progressHBox.AddThemeConstantOverride("separation", 6);
        headerVBox.AddChild(progressHBox);

        _mapProgressBar = new ProgressBar();
        _mapProgressBar.CustomMinimumSize = new Vector2(0, 6);
        _mapProgressBar.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _mapProgressBar.ShowPercentage = false;
        progressHBox.AddChild(_mapProgressBar);

        _progressPercentLabel = new Label();
        _progressPercentLabel.Text = "0%";
        _progressPercentLabel.AddThemeFontSizeOverride("font_size", 9);
        _progressPercentLabel.CustomMinimumSize = new Vector2(28, 0);
        _progressPercentLabel.HorizontalAlignment = HorizontalAlignment.Right;
        progressHBox.AddChild(_progressPercentLabel);

        _bossIndicator = new PanelContainer();
        progressHBox.AddChild(_bossIndicator);

        _bossIndicatorLabel = new Label();
        _bossIndicatorLabel.Text = "☠ BOSS";
        _bossIndicatorLabel.AddThemeFontSizeOverride("font_size", 9);
        _bossIndicator.AddChild(_bossIndicatorLabel);
    }

    /// <summary>
    /// Displays The Crucible Sanctuary safe haven status in the header bar when leaving a map.
    /// Updates tier badge to show Sanctuary tier (S1-S4), replaces map name with Sanctuary name,
    /// clears dangerous threats, displays Thaumaturgic Dust & Map Queue count, sets 100% recovery bar,
    /// and switches boss badge to Safe Haven indicator.
    /// </summary>
    public void SetSanctuary(
        int tier = 1,
        string tierName = "Hollow Crypt",
        long thaumaturgicDust = 0,
        int queuedMaps = 0,
        float infusedBonus = 0f,
        int infusedMapsRemaining = 0,
        long sulphite = 0,
        long maxSulphite = 10000,
        int deepestDepth = 0,
        int voidstones = 0
    )
    {
        IsInSanctuary = true;
        SanctuaryTier = tier;
        SanctuaryTierName = tierName;
        SanctuaryDust = thaumaturgicDust;
        QueuedMapCount = queuedMaps;
        CurrentMapName = Loc.Tr("UI_SANCTUARY_HEADER_NAME");
        CurrentTier = tier;
        CurrentRarity = "Sanctuary";
        CurrentBossName = null;
        CurrentProgress = 100f;
        CurrentIIQ = 0;
        CurrentIIR = 0;
        CurrentPackSize = 0;
        _activeModifierDescriptions.Clear();

        // 1. Update Tier Badge to Sanctuary Tier (S1, S2, S3, S4)
        if (_tierBadgeLabel != null)
        {
            _tierBadgeLabel.Text = $"S{tier}";
            Color sanColor = GetSanctuaryTierColor(tier);
            _tierBadgeLabel.Modulate = sanColor;

            if (_tierBadge != null)
            {
                var badgeStyle = _tierBadge.GetThemeStylebox("panel") as StyleBoxFlat;
                if (badgeStyle != null)
                {
                    badgeStyle.BorderColor = sanColor;
                }
                _tierBadge.TooltipText = $"The Crucible Sanctuary (Tier {tier}: {tierName})\nSafe Haven • 100% Vitals Recovery\nZero Combat Threat";
            }
        }

        // 2. Update Map Name to Sanctuary Name
        if (_mapNameLabel != null)
        {
            _mapNameLabel.Text = Loc.Tr("UI_SANCTUARY_HEADER_NAME");
            _mapNameLabel.Modulate = new Color(0.40f, 0.85f, 1.0f); // Serene Azure / Cyan
            _mapNameLabel.TooltipText = $"The Crucible Sanctuary (Tier {tier}: {tierName})\nZone Type: Safe Haven\nAll vitals fully restored (100%). Zero hostiles.";
        }

        // 3. Update Threat Badges to show Safe Haven badge
        _activeThreats.Clear();
        if (_threatBadgesHBox != null)
        {
            foreach (Node child in _threatBadgesHBox.GetChildren())
            {
                _threatBadgesHBox.RemoveChild(child);
                child.QueueFree();
            }

            var safeBadge = new ThreatBadgeInfo(
                Key: "safe_haven",
                IconText: "🛡️",
                ShortText: Loc.Tr("UI_SANCTUARY_BADGE_SAFE"),
                BadgeColor: new Color(0.35f, 0.85f, 0.55f),
                TooltipDescription: Loc.Tr("UI_SANCTUARY_BADGE_TOOLTIP")
            );
            _activeThreats.Add(safeBadge);
            _threatBadgesHBox.AddChild(CreateThreatBadgeNode(safeBadge));
        }

        // 4. Update Quantity & Sanctuary Telemetry label
        if (_quantityBonusLabel != null)
        {
            string queueStr = queuedMaps > 0 ? Loc.Format("UI_SANCTUARY_QUEUE_COUNT", queuedMaps) : Loc.Tr("UI_SANCTUARY_QUEUE_IDLE");
            string dustStr = $"Dust: {thaumaturgicDust:N0}";
            if (infusedBonus > 0f && infusedMapsRemaining > 0)
            {
                _quantityBonusLabel.Text = $"{dustStr} • +{infusedBonus:F0}% IIQ ({infusedMapsRemaining}m) • {queueStr}";
            }
            else
            {
                _quantityBonusLabel.Text = $"{dustStr} • {queueStr}";
            }
            _quantityBonusLabel.Modulate = new Color(0.45f, 0.90f, 0.65f); // Mint Green
            _quantityBonusLabel.TooltipText = BuildSanctuaryTelemetryTooltip(
                tier, tierName, thaumaturgicDust, queuedMaps, infusedBonus, infusedMapsRemaining, sulphite, maxSulphite, deepestDepth, voidstones
            );
        }

        // 5. Update Progress Bar to 100% Safe Haven
        if (_mapProgressBar != null)
        {
            _mapProgressBar.Value = 100f;
            var fillStyle = _mapProgressBar.GetThemeStylebox("fill") as StyleBoxFlat;
            if (fillStyle != null)
            {
                fillStyle.BgColor = new Color(0.25f, 0.75f, 0.65f); // Tranquil emerald/teal
            }
            _mapProgressBar.TooltipText = Loc.Tr("UI_SANCTUARY_PROGRESS_TOOLTIP");
        }

        if (_progressPercentLabel != null)
        {
            _progressPercentLabel.Text = "100%";
            _progressPercentLabel.Modulate = new Color(0.40f, 0.85f, 0.70f);
        }

        // 6. Update Boss Indicator to Safe Haven Indicator
        if (_bossIndicator != null && _bossIndicatorLabel != null)
        {
            _bossIndicatorLabel.Text = Loc.Tr("UI_SANCTUARY_BOSS_SAFE");
            _bossIndicatorLabel.Modulate = new Color(0.45f, 0.85f, 0.65f);
            _bossIndicator.TooltipText = Loc.Tr("UI_SANCTUARY_BOSS_SAFE_TOOLTIP");

            var safeStyle = new StyleBoxFlat
            {
                BgColor = new Color(0.06f, 0.12f, 0.10f, 0.90f),
                BorderColor = new Color(0.25f, 0.75f, 0.55f),
                BorderWidthLeft = 1,
                BorderWidthTop = 1,
                BorderWidthRight = 1,
                BorderWidthBottom = 1,
                CornerRadiusTopLeft = 2,
                CornerRadiusTopRight = 2,
                CornerRadiusBottomRight = 2,
                CornerRadiusBottomLeft = 2,
                ContentMarginLeft = 4,
                ContentMarginRight = 4,
                ContentMarginTop = 1,
                ContentMarginBottom = 1
            };
            _bossIndicator.AddThemeStyleboxOverride("panel", safeStyle);
        }
    }

    /// <summary>
    /// Overload that configures Sanctuary header telemetry from a <see cref="SanctuaryState"/> object.
    /// </summary>
    public void SetSanctuary(SanctuaryState sanctuary, int queuedMaps = 0, string? tierName = null)
    {
        tierName ??= GetDefaultSanctuaryTierName(sanctuary.CurrentTier);
        SetSanctuary(
            tier: sanctuary.CurrentTier,
            tierName: tierName,
            thaumaturgicDust: sanctuary.ThaumaturgicDust,
            queuedMaps: queuedMaps,
            infusedBonus: sanctuary.InfusedQuantityBonus,
            infusedMapsRemaining: sanctuary.InfusedMapsRemaining,
            sulphite: sanctuary.VoltaxicSulphite,
            maxSulphite: sanctuary.MaxVoltaxicSulphite,
            deepestDepth: sanctuary.DeepestDepth,
            voidstones: sanctuary.VoidstonesCount
        );
    }

    public static string GetDefaultSanctuaryTierName(int tier) => tier switch
    {
        >= 4 => "Celestial Void",
        3 => "Eldritch Sanctum",
        2 => "Awakened Foundry",
        _ => "Hollow Crypt"
    };

    public static Color GetSanctuaryTierColor(int tier) => tier switch
    {
        >= 4 => new Color(1.0f, 0.85f, 0.30f),   // Celestial Gold
        3 => new Color(0.75f, 0.45f, 1.0f),     // Eldritch Cosmic Violet
        2 => new Color(0.35f, 0.90f, 0.70f),     // Awakened Runic Emerald/Cyan
        _ => new Color(0.65f, 0.78f, 0.88f)      // Hollow Crypt Slate Cyan
    };

    public static string BuildSanctuaryTelemetryTooltip(
        int tier,
        string tierName,
        long thaumaturgicDust,
        int queuedMaps,
        float infusedBonus,
        int infusedMapsRemaining,
        long sulphite,
        long maxSulphite,
        int deepestDepth,
        int voidstones
    )
    {
        var lines = new List<string>
        {
            "═════ THE CRUCIBLE SANCTUARY ═════",
            $"Sanctuary Tier: Tier {tier} ({tierName})",
            "Zone Status: Safe Haven (100% Recovered)",
            "─────────────────────────────────",
            $"• Thaumaturgic Dust: {thaumaturgicDust:N0}",
            $"• Map Queue: {queuedMaps} Maps Ready",
            infusedBonus > 0f && infusedMapsRemaining > 0
                ? $"• Map Infusion: +{infusedBonus:F0}% IIQ ({infusedMapsRemaining} maps remaining)"
                : "• Map Infusion: Inactive",
            $"• Voltaxic Sulphite: {sulphite:N0} / {maxSulphite:N0}",
            $"• Deepest Abyss Depth: {deepestDepth}m",
            $"• Voidstones Unlocked: {voidstones}/4",
            "─────────────────────────────────",
            "Press [Space] to Launch Map • [H] Sanctuary"
        };
        return string.Join("\n", lines);
    }

    /// <summary>
    /// Configures the Map Header using a complete <see cref="MapRunConfig"/> from the combat core.
    /// Automatically extracts map definitions, rolled modifiers, item quantity/pack size bonuses,
    /// and generates relevant Threat Badges.
    /// </summary>
    public void SetMap(MapRunConfig config, float progressPercent = 0f, string rarity = "Normal", string? leagueEncounter = null)
    {
        var threatBadges = new List<ThreatBadgeInfo>();
        var modifierLines = new List<string>();

        foreach (var mod in config.Modifiers)
        {
            modifierLines.Add(mod.FormattedName);

            var badge = ThreatBadgeInfo.FromModifier(mod);
            if (badge != null)
            {
                threatBadges.Add(badge);
            }
        }

        int iiqBonus = config.TotalQuantity;
        int packSizeBonus = config.TotalPackSize - config.Map.BasePackSize;

        UpdateTelemetry(
            mapName: config.Map.Name,
            tier: config.Map.Tier,
            rarity: rarity,
            iiq: iiqBonus,
            iir: 0,
            packSize: packSizeBonus,
            progressPercent: progressPercent,
            threatBadges: threatBadges,
            bossName: config.Map.BossName,
            modifierLines: modifierLines,
            effectiveMonsterLevel: config.EffectiveMonsterLevel,
            leagueEncounter: leagueEncounter
        );
    }

    /// <summary>
    /// Configures the Map Header using a <see cref="MapDefinition"/> and optional rolled modifier list.
    /// </summary>
    public void SetMap(MapDefinition map, IReadOnlyList<RolledMapModifier>? modifiers = null, float progressPercent = 0f, string rarity = "Normal", string? leagueEncounter = null)
    {
        var config = new MapRunConfig(map, modifiers ?? Array.Empty<RolledMapModifier>());
        SetMap(config, progressPercent, rarity, leagueEncounter);
    }

    /// <summary>
    /// Primary telemetry update method. Updates all visuals: tier badge color, map name rarity color,
    /// threat badges, IIQ label, progress bar value, and boss spawn state.
    /// </summary>
    public void UpdateTelemetry(
        string mapName,
        int tier,
        string rarity = "Normal",
        int iiq = 0,
        int iir = 0,
        int packSize = 0,
        float progressPercent = 0f,
        IReadOnlyList<ThreatBadgeInfo>? threatBadges = null,
        string? bossName = null,
        IReadOnlyList<string>? modifierLines = null,
        int? effectiveMonsterLevel = null,
        string? leagueEncounter = null
    )
    {
        CurrentMapName = mapName;
        CurrentTier = tier;
        CurrentRarity = rarity;
        CurrentIIQ = iiq;
        CurrentIIR = iir;
        CurrentPackSize = packSize;
        CurrentBossName = bossName;
        IsInSanctuary = false;

        _activeModifierDescriptions.Clear();
        if (modifierLines != null)
        {
            _activeModifierDescriptions.AddRange(modifierLines);
        }

        // 1. Update Tier Badge
        UpdateTierBadge(tier, effectiveMonsterLevel);

        // 2. Update Map Name & Rarity
        UpdateMapName(mapName, rarity);

        // 3. Update Threat Badges
        UpdateThreatBadges(threatBadges);

        // 4. Update Quantity & Full Tooltip
        UpdateQuantityAndTooltip(iiq, iir, packSize, effectiveMonsterLevel, leagueEncounter);

        // 5. Update Progress Bar & Boss Indicator
        SetProgress(progressPercent);
    }

    /// <summary>
    /// Updates the Monster Pack Clear Progress Bar (0% to 100%) and updates Boss Summon state.
    /// </summary>
    public void SetProgress(float progressPercent)
    {
        CurrentProgress = Mathf.Clamp(progressPercent, 0f, 100f);

        if (_mapProgressBar != null)
        {
            _mapProgressBar.Value = CurrentProgress;

            // Highlight bar color when reaching 100% (Boss active)
            var fillStyle = _mapProgressBar.GetThemeStylebox("fill") as StyleBoxFlat;
            if (fillStyle != null)
            {
                fillStyle.BgColor = CurrentProgress >= 100f
                    ? new Color(0.95f, 0.25f, 0.15f) // Glowing Crimson for Boss
                    : new Color(0.85f, 0.65f, 0.25f); // Amber Gold for normal pack progress
            }
        }

        if (_progressPercentLabel != null)
        {
            _progressPercentLabel.Text = $"{Mathf.RoundToInt(CurrentProgress)}%";
            _progressPercentLabel.Modulate = CurrentProgress >= 100f
                ? new Color(1.0f, 0.4f, 0.3f)
                : new Color(0.75f, 0.75f, 0.80f);
        }

        UpdateBossIndicator(CurrentProgress);
    }

    private void UpdateTierBadge(int tier, int? effectiveMonsterLevel)
    {
        if (_tierBadgeLabel == null) return;

        _tierBadgeLabel.Text = $"T{tier}";

        Color tierColor = GetTierColor(tier);
        _tierBadgeLabel.Modulate = tierColor;

        if (_tierBadge != null)
        {
            var badgeStyle = _tierBadge.GetThemeStylebox("panel") as StyleBoxFlat;
            if (badgeStyle != null)
            {
                badgeStyle.BorderColor = tierColor;
            }

            string tierCategory = tier switch
            {
                >= 16 => "Pinnacle / Void Tier",
                >= 11 => "Red Map Tier",
                >= 6 => "Yellow Map Tier",
                _ => "White Map Tier"
            };

            string mlvlStr = effectiveMonsterLevel.HasValue ? $"\nMonster Level: {effectiveMonsterLevel.Value}" : "";
            _tierBadge.TooltipText = $"Map Tier: {tier} ({tierCategory}){mlvlStr}";
        }
    }

    private void UpdateMapName(string mapName, string rarity)
    {
        if (_mapNameLabel == null) return;

        _mapNameLabel.Text = string.Equals(rarity, "Normal", StringComparison.OrdinalIgnoreCase)
            ? mapName
            : $"{mapName} • {rarity}";

        Color rarityColor = GetRarityColor(rarity);
        _mapNameLabel.Modulate = rarityColor;
        _mapNameLabel.TooltipText = $"Map: {mapName}\nRarity: {rarity}";
    }

    private void UpdateThreatBadges(IReadOnlyList<ThreatBadgeInfo>? threatBadges)
    {
        _activeThreats.Clear();
        if (_threatBadgesHBox == null) return;

        // Clear existing badge nodes
        foreach (Node child in _threatBadgesHBox.GetChildren())
        {
            _threatBadgesHBox.RemoveChild(child);
            child.QueueFree();
        }

        if (threatBadges == null || threatBadges.Count == 0) return;

        foreach (var badge in threatBadges)
        {
            _activeThreats.Add(badge);
            var badgeNode = CreateThreatBadgeNode(badge);
            _threatBadgesHBox.AddChild(badgeNode);
        }
    }

    private PanelContainer CreateThreatBadgeNode(ThreatBadgeInfo badge)
    {
        var panel = new PanelContainer();
        panel.MouseFilter = MouseFilterEnum.Pass;
        panel.TooltipText = $"{badge.IconText} {badge.ShortText}\n{badge.TooltipDescription}";

        var style = new StyleBoxFlat
        {
            BgColor = new Color(0.08f, 0.08f, 0.11f, 0.95f),
            BorderColor = badge.BadgeColor,
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            CornerRadiusTopLeft = 2,
            CornerRadiusTopRight = 2,
            CornerRadiusBottomRight = 2,
            CornerRadiusBottomLeft = 2,
            ContentMarginLeft = 4,
            ContentMarginRight = 4,
            ContentMarginTop = 1,
            ContentMarginBottom = 1
        };
        panel.AddThemeStyleboxOverride("panel", style);

        var label = new Label
        {
            Text = $"{badge.IconText} {badge.ShortText}",
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Modulate = badge.BadgeColor
        };
        label.AddThemeFontSizeOverride("font_size", 9);
        panel.AddChild(label);

        return panel;
    }

    private void UpdateQuantityAndTooltip(int iiq, int iir, int packSize, int? effectiveMonsterLevel, string? leagueEncounter)
    {
        if (_quantityBonusLabel != null)
        {
            string iiqText = $"IIQ: +{iiq}%";
            if (packSize > 0)
            {
                iiqText += $" • Pack: +{packSize}%";
            }
            _quantityBonusLabel.Text = iiqText;
            _quantityBonusLabel.Modulate = new Color(0.46f, 1.0f, 0.01f); // Vibrant PoE Green

            // Assemble comprehensive telemetry tooltip
            _quantityBonusLabel.TooltipText = BuildTelemetryTooltip(iiq, iir, packSize, effectiveMonsterLevel, leagueEncounter);
        }
    }

    private string BuildTelemetryTooltip(int iiq, int iir, int packSize, int? effectiveMonsterLevel, string? leagueEncounter)
    {
        var lines = new List<string>
        {
            $"═════ MAP TELEMETRY ═════",
            $"Map: {CurrentMapName} (Tier {CurrentTier})",
            $"Rarity: {CurrentRarity}"
        };

        if (effectiveMonsterLevel.HasValue)
        {
            lines.Add($"Monster Level: {effectiveMonsterLevel.Value}");
        }

        if (!string.IsNullOrWhiteSpace(CurrentBossName))
        {
            lines.Add($"Map Boss: {CurrentBossName}");
        }

        if (!string.IsNullOrWhiteSpace(leagueEncounter))
        {
            lines.Add($"League Encounter: {leagueEncounter}");
        }

        lines.Add($"─────────────────────────");
        lines.Add($"Item Quantity: +{iiq}%");
        if (iir > 0) lines.Add($"Item Rarity: +{iir}%");
        lines.Add($"Pack Size Bonus: +{packSize}%");

        if (_activeModifierDescriptions.Count > 0)
        {
            lines.Add($"─────────────────────────");
            lines.Add($"Active Modifiers ({_activeModifierDescriptions.Count}):");
            foreach (var mod in _activeModifierDescriptions)
            {
                lines.Add($" • {mod}");
            }
        }

        if (_activeThreats.Count > 0)
        {
            lines.Add($"─────────────────────────");
            lines.Add($"Threat Warnings ({_activeThreats.Count}):");
            foreach (var threat in _activeThreats)
            {
                lines.Add($" [{threat.IconText} {threat.ShortText}] {threat.TooltipDescription.Replace("\n", " ")}");
            }
        }

        return string.Join("\n", lines);
    }

    private void UpdateBossIndicator(float progressPercent)
    {
        if (_bossIndicator == null || _bossIndicatorLabel == null) return;

        bool bossActive = progressPercent >= 100f;
        string bossName = string.IsNullOrWhiteSpace(CurrentBossName) ? "Map Boss" : CurrentBossName;

        if (bossActive)
        {
            _bossIndicatorLabel.Text = "☠ BOSS ACTIVE!";
            _bossIndicatorLabel.Modulate = new Color(1.0f, 0.85f, 0.25f); // Amber Gold
            _bossIndicator.TooltipText = $"BOSS ENCOUNTER IN PROGRESS!\nDefeat {bossName} to complete the map!";

            var activeStyle = new StyleBoxFlat
            {
                BgColor = new Color(0.22f, 0.06f, 0.06f, 0.95f),
                BorderColor = new Color(1.0f, 0.25f, 0.15f),
                BorderWidthLeft = 1,
                BorderWidthTop = 1,
                BorderWidthRight = 1,
                BorderWidthBottom = 1,
                CornerRadiusTopLeft = 2,
                CornerRadiusTopRight = 2,
                CornerRadiusBottomRight = 2,
                CornerRadiusBottomLeft = 2,
                ContentMarginLeft = 4,
                ContentMarginRight = 4,
                ContentMarginTop = 1,
                ContentMarginBottom = 1
            };
            _bossIndicator.AddThemeStyleboxOverride("panel", activeStyle);
        }
        else
        {
            _bossIndicatorLabel.Text = "☠ BOSS";
            _bossIndicatorLabel.Modulate = new Color(0.50f, 0.52f, 0.58f); // Muted
            _bossIndicator.TooltipText = $"Clear monsters to 100% to summon {bossName}.\nCurrent Progress: {Mathf.RoundToInt(progressPercent)}%";

            var inactiveStyle = new StyleBoxFlat
            {
                BgColor = new Color(0.08f, 0.08f, 0.10f, 0.80f),
                BorderColor = new Color(0.22f, 0.24f, 0.30f),
                BorderWidthLeft = 1,
                BorderWidthTop = 1,
                BorderWidthRight = 1,
                BorderWidthBottom = 1,
                CornerRadiusTopLeft = 2,
                CornerRadiusTopRight = 2,
                CornerRadiusBottomRight = 2,
                CornerRadiusBottomLeft = 2,
                ContentMarginLeft = 4,
                ContentMarginRight = 4,
                ContentMarginTop = 1,
                ContentMarginBottom = 1
            };
            _bossIndicator.AddThemeStyleboxOverride("panel", inactiveStyle);
        }
    }

    /// <summary>
    /// Returns the canonical color for a map tier according to Path of Exile conventions:
    /// T1-T5: White/Grey | T6-T10: Yellow/Gold | T11-T15: Red/Crimson | T16+: Void/Purple.
    /// </summary>
    public static Color GetTierColor(int tier) => tier switch
    {
        >= 16 => new Color(0.75f, 0.35f, 0.95f), // T16+ Pinnacle / Void Purple
        >= 11 => new Color(0.95f, 0.30f, 0.30f), // T11-T15 Red
        >= 6 => new Color(1.0f, 0.85f, 0.30f),   // T6-T10 Yellow
        _ => new Color(0.85f, 0.88f, 0.92f)      // T0-T5 White
    };

    /// <summary>
    /// Returns the canonical color for an item/map rarity tier:
    /// Normal: White | Magic: Blue | Rare: Yellow | Unique: Orange | Corrupted: Crimson.
    /// </summary>
    public static Color GetRarityColor(string rarity) => rarity.ToLowerInvariant() switch
    {
        "unique" => new Color(0.69f, 0.38f, 0.15f), // Unique Orange (#AF6026)
        "rare" => new Color(1.0f, 1.0f, 0.45f),     // Rare Yellow (#FFFF73)
        "magic" => new Color(0.55f, 0.55f, 1.0f),   // Magic Blue (#8888FF)
        "corrupted" => new Color(0.95f, 0.25f, 0.25f), // Corrupted Red
        _ => new Color(0.95f, 0.95f, 0.95f)         // Normal White
    };
}
