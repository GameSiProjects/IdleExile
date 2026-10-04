using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using IdleExile.Core.Models;
using IdleExile.Core.Services;
using IdleExile.Core.Simulation;
using IdleExile.Core.State;

namespace IdleExile.UI.Components;

/// <summary>
/// Controller for the Atlas & Crucible Map Device View (Task 4.1 & Phase 4 UI).
/// Consolidates Map Device activation altar, runic celestial ring,
/// full Map Modifier telemetry & Threat Badges, and Available Map Slates drawer.
/// Conforms to the 410px vertical pixel budget in TabWorkspaceContainer.
/// </summary>
public partial class AtlasView : Control
{
    // ─────────────────────────────────────────────
    //  Signals & Events
    // ─────────────────────────────────────────────

    /// <summary>Fired when the player activates the slotted map run.</summary>
    public event Action<MapDefinition, IReadOnlyList<RolledMapModifier>, string>? OnMapActivated;

    /// <summary>Fired when a map slate is slotted into the device.</summary>
    public event Action<MapDefinition>? OnMapSlotted;

    /// <summary>Fired when emergency portal escape is triggered.</summary>
    public event Action? OnEmergencyPortalTriggered;

    // ─────────────────────────────────────────────
    //  State
    // ─────────────────────────────────────────────

    public DatabaseLoader? Database { get; private set; }
    public PlayerState? Player { get; private set; }
    public FtueManager? Ftue { get; private set; }

    public MapDefinition? SlottedMap { get; private set; }
    private readonly List<RolledMapModifier> _slottedModifiers = new();
    public IReadOnlyList<RolledMapModifier> SlottedModifiers => _slottedModifiers;
    public string SlottedRarity { get; private set; } = "Normal";

    public bool IsPortalActive { get; private set; } = false;
    public bool IsGoldenPulse { get; private set; } = false;
    public string CurrentTierFilter { get; private set; } = "All";

    private Tween? _pulseTween;
    private readonly Dictionary<Button, MapDefinition> _mapCards = new();

    // ─────────────────────────────────────────────
    //  Node References
    // ─────────────────────────────────────────────

    // Tier Filter Buttons
    private Button? _tierAllBtn;
    private Button? _tierT0Btn;
    private Button? _tierWhiteBtn;
    private Button? _tierYellowBtn;
    private Button? _tierRedBtn;
    private Button? _tierPinnacleBtn;

    // Central Altar
    private RunicDeviceCanvas? _runicCanvas;
    private Label? _slottedTierBadge;
    private Label? _slottedMapNameLabel;
    private Label? _slottedBossLabel;
    private Label? _slottedInfoLabel;
    private Button? _activateMapBtn;
    private Button? _emergencyPortalBtn;
    private Button? _craftRerollBtn;

    // Yield Metrics
    private Label? _qtyValueLabel;
    private Label? _rarityValueLabel;
    private Label? _packSizeValueLabel;
    private Label? _mobLevelValueLabel;

    // Modifiers & Threat Telemetry
    private HBoxContainer? _threatBadgesHBox;
    private VBoxContainer? _modifiersVBox;
    private Label? _noModsLabel;

    // Map Queue Tray Nodes (Task 4.2)
    public MapQueueManager? QueueManager { get; private set; }
    private PanelContainer? _mapQueuePanel;
    private Label? _queueTitleLabel;
    private Label? _queueCountdownLabel;
    private Button? _queueFastForwardBtn;
    private Button? _queueCancelCountdownBtn;
    private CheckBox? _queueAutoAdvanceCheck;
    private CheckBox? _queueContinuousCheck;
    private Button? _queueEnqueueBtn;
    private Button? _queueClearBtn;
    private HBoxContainer? _queueSlotsHBox;
    private Control? _queueSpacer;
    private readonly Button[] _queueSlotButtons = new Button[MapQueueManager.MaxCapacity];

    // Available Slates Drawer
    private HBoxContainer? _mapSlatesHBox;
    private Label? _availableMapsCountLabel;

    public override void _Ready()
    {
        // 1. Bind Tier Filter Buttons
        _tierAllBtn = GetNodeOrNull<Button>("%TierAllBtn");
        _tierT0Btn = GetNodeOrNull<Button>("%TierT0Btn");
        _tierWhiteBtn = GetNodeOrNull<Button>("%TierWhiteBtn");
        _tierYellowBtn = GetNodeOrNull<Button>("%TierYellowBtn");
        _tierRedBtn = GetNodeOrNull<Button>("%TierRedBtn");
        _tierPinnacleBtn = GetNodeOrNull<Button>("%TierPinnacleBtn");

        if (_tierAllBtn != null) _tierAllBtn.Pressed += () => SetTierFilter("All");
        if (_tierT0Btn != null) _tierT0Btn.Pressed += () => SetTierFilter("T0");
        if (_tierWhiteBtn != null) _tierWhiteBtn.Pressed += () => SetTierFilter("White");
        if (_tierYellowBtn != null) _tierYellowBtn.Pressed += () => SetTierFilter("Yellow");
        if (_tierRedBtn != null) _tierRedBtn.Pressed += () => SetTierFilter("Red");
        if (_tierPinnacleBtn != null) _tierPinnacleBtn.Pressed += () => SetTierFilter("Pinnacle");

        // 2. Bind Altar Nodes
        _runicCanvas = GetNodeOrNull<RunicDeviceCanvas>("%RunicDeviceCanvas");
        _slottedTierBadge = GetNodeOrNull<Label>("%SlottedTierBadge");
        _slottedMapNameLabel = GetNodeOrNull<Label>("%SlottedMapNameLabel");
        _slottedBossLabel = GetNodeOrNull<Label>("%SlottedBossLabel");
        _slottedInfoLabel = GetNodeOrNull<Label>("%SlottedInfoLabel");
        _activateMapBtn = GetNodeOrNull<Button>("%ActivateMapBtn");
        _emergencyPortalBtn = GetNodeOrNull<Button>("%EmergencyPortalBtn");
        _craftRerollBtn = GetNodeOrNull<Button>("%CraftRerollBtn");

        if (_activateMapBtn != null)
        {
            _activateMapBtn.Pressed += OnActivatePressed;
        }

        if (_emergencyPortalBtn != null)
        {
            _emergencyPortalBtn.Pressed += OnEmergencyPortalPressed;
        }

        if (_craftRerollBtn != null)
        {
            _craftRerollBtn.Pressed += OnCraftRerollPressed;
        }

        // 3. Bind Metrics & Threat Nodes
        _qtyValueLabel = GetNodeOrNull<Label>("%QtyValueLabel");
        _rarityValueLabel = GetNodeOrNull<Label>("%RarityValueLabel");
        _packSizeValueLabel = GetNodeOrNull<Label>("%PackSizeValueLabel");
        _mobLevelValueLabel = GetNodeOrNull<Label>("%MobLevelValueLabel");
        _threatBadgesHBox = GetNodeOrNull<HBoxContainer>("%ThreatBadgesHBox");
        _modifiersVBox = GetNodeOrNull<VBoxContainer>("%ModifiersVBox");
        _noModsLabel = GetNodeOrNull<Label>("%NoModsLabel");

        // 4. Bind Map Queue Tray Nodes (Task 4.2)
        _mapQueuePanel = GetNodeOrNull<PanelContainer>("%MapQueuePanel");
        _queueTitleLabel = GetNodeOrNull<Label>("%QueueTitleLabel");
        _queueCountdownLabel = GetNodeOrNull<Label>("%QueueCountdownLabel");
        _queueFastForwardBtn = GetNodeOrNull<Button>("%QueueFastForwardBtn");
        _queueCancelCountdownBtn = GetNodeOrNull<Button>("%QueueCancelCountdownBtn");
        _queueAutoAdvanceCheck = GetNodeOrNull<CheckBox>("%QueueAutoAdvanceCheck");
        _queueContinuousCheck = GetNodeOrNull<CheckBox>("%QueueContinuousCheck");
        _queueEnqueueBtn = GetNodeOrNull<Button>("%QueueEnqueueBtn");
        _queueClearBtn = GetNodeOrNull<Button>("%QueueClearBtn");
        _queueSlotsHBox = GetNodeOrNull<HBoxContainer>("%QueueSlotsHBox");
        _queueSpacer = GetNodeOrNull<Control>("%QueueSpacer");

        if (_queueEnqueueBtn != null)
        {
            _queueEnqueueBtn.Pressed += EnqueueCurrentSlottedMap;
        }

        if (_queueClearBtn != null)
        {
            _queueClearBtn.Pressed += () => QueueManager?.Clear();
        }

        if (_queueAutoAdvanceCheck != null)
        {
            _queueAutoAdvanceCheck.Toggled += toggled =>
            {
                if (QueueManager != null) QueueManager.AutoAdvanceEnabled = toggled;
            };
        }

        if (_queueContinuousCheck != null)
        {
            _queueContinuousCheck.Toggled += toggled =>
            {
                if (QueueManager != null) QueueManager.ContinuousRunEnabled = toggled;
            };
        }

        if (_queueFastForwardBtn != null)
        {
            _queueFastForwardBtn.Pressed += () => QueueManager?.FastForwardCountdown();
        }

        if (_queueCancelCountdownBtn != null)
        {
            _queueCancelCountdownBtn.Pressed += () => QueueManager?.CancelCountdown();
        }

        // Instantiate 10 queue slot buttons
        if (_queueSlotsHBox != null)
        {
            for (int i = 0; i < MapQueueManager.MaxCapacity; i++)
            {
                int slotIdx = i;
                var slotBtn = CreateQueueSlotButton(slotIdx);
                _queueSlotButtons[slotIdx] = slotBtn;
                _queueSlotsHBox.AddChild(slotBtn);
            }
        }

        // 5. Bind Available Slates Shelf
        _mapSlatesHBox = GetNodeOrNull<HBoxContainer>("%MapSlatesHBox");
        _availableMapsCountLabel = GetNodeOrNull<Label>("%AvailableMapsCountLabel");

        UpdateTierFilterButtonStyles();
        UpdateQueueDisplay();
    }

    public override void _UnhandledKeyInput(InputEvent @event)
    {
        if (@event is InputEventKey keyEvent && keyEvent.Pressed && !keyEvent.Echo)
        {
            if (keyEvent.Keycode == Key.Space && IsPortalActive)
            {
                OnEmergencyPortalPressed();
                GetViewport()?.SetInputAsHandled();
            }
        }
    }

    /// <summary>
    /// Initializes the AtlasView controller with system database and player state.
    /// </summary>
    public void Initialize(DatabaseLoader db, PlayerState player, FtueManager? ftue = null)
    {
        Initialize(db, player, null, ftue);
    }

    /// <summary>
    /// Initializes the AtlasView controller with system database, player state, and MapQueueManager.
    /// </summary>
    public void Initialize(DatabaseLoader db, PlayerState player, MapQueueManager? queueManager, FtueManager? ftue = null)
    {
        Database = db ?? throw new ArgumentNullException(nameof(db));
        Player = player ?? throw new ArgumentNullException(nameof(player));
        Ftue = ftue;

        QueueManager = queueManager ?? new MapQueueManager();
        QueueManager.OnQueueChanged += UpdateQueueDisplay;
        QueueManager.OnCountdownStarted += seconds => ShowCountdownUI(seconds);
        QueueManager.OnCountdownTick += seconds => UpdateCountdownUI(seconds);
        QueueManager.OnCountdownCancelled += HideCountdownUI;
        QueueManager.OnMapQueueAdvanced += item =>
        {
            HideCountdownUI();
            SlotMap(item.Map, item.Modifiers, item.Rarity);
        };

        if (_queueAutoAdvanceCheck != null)
        {
            _queueAutoAdvanceCheck.ButtonPressed = QueueManager.AutoAdvanceEnabled;
        }

        if (_queueContinuousCheck != null)
        {
            _queueContinuousCheck.ButtonPressed = QueueManager.ContinuousRunEnabled;
        }

        QueueManager.ContinuousRunFallbackProvider = () =>
        {
            if (Player != null && Player.MapInventory.Count > 0 && Database != null)
            {
                var mapId = Player.MapInventory[0];
                if (Database.Maps.TryGetValue(mapId, out var invMap))
                {
                    Player.MapInventory.RemoveAt(0);
                    return new MapQueueItem(0, invMap, Array.Empty<RolledMapModifier>(), "Normal");
                }
            }
            if (Database != null && Database.Maps.TryGetValue("endless_hollow", out var hollowDef))
            {
                return new MapQueueItem(0, hollowDef, Array.Empty<RolledMapModifier>(), "Normal");
            }
            return null;
        };

        RefreshAvailableMaps();
        UpdateQueueDisplay();

        // If no map is currently slotted, slot starter Strand or first available map
        if (SlottedMap == null)
        {
            if (Database.Maps.TryGetValue("strand", out var starterStrand))
            {
                SlotMap(starterStrand, rarity: "Normal");
            }
            else if (Database.Maps.TryGetValue("endless_hollow", out var failSafe))
            {
                SlotMap(failSafe, rarity: "Normal");
            }
        }
    }

    /// <summary>
    /// Slots a map slate into the Crucible Map Device.
    /// Updates the central altar display, metrics banner, threat badges, and modifier list.
    /// </summary>
    public void SlotMap(MapDefinition map, IReadOnlyList<RolledMapModifier>? modifiers = null, string rarity = "Normal")
    {
        SlottedMap = map ?? throw new ArgumentNullException(nameof(map));
        SlottedRarity = rarity;

        _slottedModifiers.Clear();
        if (modifiers != null && modifiers.Count > 0)
        {
            _slottedModifiers.AddRange(modifiers);
        }

        UpdateAltarDisplay();
        UpdateMetricsDisplay();
        UpdateThreatBadges();
        UpdateModifiersList();
        UpdateMapCardSelections();

        OnMapSlotted?.Invoke(map);
    }

    /// <summary>
    /// Sets golden pulse guidance on the activation button and runic ring.
    /// </summary>
    public void SetGoldenPulse(bool enable)
    {
        IsGoldenPulse = enable;

        _pulseTween?.Kill();
        _pulseTween = null;

        if (_runicCanvas != null)
        {
            _runicCanvas.IsGoldenPulse = enable;
        }

        if (_activateMapBtn == null) return;

        if (enable && IsInsideTree())
        {
            _pulseTween = CreateTween().SetLoops();
            _pulseTween.TweenProperty(_activateMapBtn, "modulate", new Color(1.35f, 1.25f, 0.65f, 1.0f), 0.65f)
                       .SetTrans(Tween.TransitionType.Sine)
                       .SetEase(Tween.EaseType.InOut);
            _pulseTween.TweenProperty(_activateMapBtn, "modulate", new Color(1.0f, 1.0f, 1.0f, 0.85f), 0.65f)
                       .SetTrans(Tween.TransitionType.Sine)
                       .SetEase(Tween.EaseType.InOut);
        }
        else
        {
            _activateMapBtn.Modulate = new Color(1f, 1f, 1f, 1f);
        }
    }

    /// <summary>
    /// Sets portal activity state, updating button texts and runic vortex effects.
    /// </summary>
    public void SetPortalActive(bool isActive)
    {
        IsPortalActive = isActive;

        if (_runicCanvas != null)
        {
            _runicCanvas.IsPortalActive = isActive;
        }

        if (_activateMapBtn != null)
        {
            if (isActive)
            {
                _activateMapBtn.Text = "🌀 PORTAL ACTIVE";
                _activateMapBtn.Disabled = true;
            }
            else
            {
                _activateMapBtn.Text = "⚔ ACTIVATE MAP";
                _activateMapBtn.Disabled = (SlottedMap == null);
            }
        }

        if (_emergencyPortalBtn != null)
        {
            _emergencyPortalBtn.Disabled = !isActive;
        }
    }

    /// <summary>
    /// Updates the active Tier filter for browsing available maps.
    /// </summary>
    public void SetTierFilter(string filter)
    {
        CurrentTierFilter = filter;
        UpdateTierFilterButtonStyles();
        RefreshAvailableMaps();
    }

    /// <summary>
    /// Re-populates the available map slates shelf according to player inventory and tier filter.
    /// </summary>
    public void RefreshAvailableMaps()
    {
        if (_mapSlatesHBox == null) return;

        // Clear existing card buttons
        foreach (Node child in _mapSlatesHBox.GetChildren())
        {
            child.QueueFree();
        }
        _mapCards.Clear();

        var mapsToDisplay = new List<MapDefinition>();

        // 1. Guaranteed Fail-Safe Endless Hollow (T0) is always available
        if (Database != null && Database.Maps.TryGetValue("endless_hollow", out var hollowMap))
        {
            if (MatchesTierFilter(hollowMap, CurrentTierFilter))
            {
                mapsToDisplay.Add(hollowMap);
            }
        }

        // 2. Available Maps in Player possession or database matching filter
        if (Database != null)
        {
            foreach (var kvp in Database.Maps)
            {
                if (kvp.Key == "endless_hollow") continue;
                var mapDef = kvp.Value;
                if (MatchesTierFilter(mapDef, CurrentTierFilter))
                {
                    mapsToDisplay.Add(mapDef);
                }
            }
        }

        // 3. Populate cards
        foreach (var map in mapsToDisplay)
        {
            var cardBtn = CreateMapSlateCard(map);
            _mapSlatesHBox.AddChild(cardBtn);
            _mapCards[cardBtn] = map;
        }

        if (_availableMapsCountLabel != null)
        {
            _availableMapsCountLabel.Text = $"Available: {mapsToDisplay.Count} Maps";
        }

        UpdateMapCardSelections();
    }

    /// <summary>
    /// Crafts / re-rolls random modifiers on the slotted map (crafting simulation).
    /// </summary>
    public void RerollCurrentMapModifiers(string rarity = "Rare")
    {
        if (SlottedMap == null || Database == null || Database.MapModifiers.Count == 0) return;

        var availableMods = Database.MapModifiers.ToList();
        var rng = new Random();
        int modCount = rarity == "Magic" ? rng.Next(1, 3) : rng.Next(3, 5);

        // Shuffle and pick unique modifiers
        var pickedDefs = availableMods.OrderBy(_ => rng.Next()).Take(modCount).ToList();
        var rolledList = new List<RolledMapModifier>();

        foreach (var def in pickedDefs)
        {
            float val = def.Min + (float)(rng.NextDouble() * (def.Max - def.Min));
            val = MathF.Round(val, 0);
            rolledList.Add(new RolledMapModifier(def, val));
        }

        SlotMap(SlottedMap, rolledList, rarity);
    }

    // ─────────────────────────────────────────────
    //  Internal Presentation Helpers
    // ─────────────────────────────────────────────

    private void UpdateAltarDisplay()
    {
        if (SlottedMap == null)
        {
            if (_slottedTierBadge != null) _slottedTierBadge.Text = "T-";
            if (_slottedMapNameLabel != null) _slottedMapNameLabel.Text = "No Map Slotted";
            if (_slottedBossLabel != null) _slottedBossLabel.Text = "Select a map slate from below";
            if (_slottedInfoLabel != null) _slottedInfoLabel.Text = "Crucible Device Empty";
            return;
        }

        var map = SlottedMap;

        if (_slottedTierBadge != null)
        {
            _slottedTierBadge.Text = $"T{map.Tier}";
            var tierStyle = new StyleBoxFlat
            {
                CornerRadiusTopLeft = 3,
                CornerRadiusTopRight = 3,
                CornerRadiusBottomRight = 3,
                CornerRadiusBottomLeft = 3,
                BorderWidthLeft = 1,
                BorderWidthTop = 1,
                BorderWidthRight = 1,
                BorderWidthBottom = 1
            };

            if (map.Tier == 0)
            {
                tierStyle.BgColor = new Color(0.10f, 0.22f, 0.22f, 0.95f);
                tierStyle.BorderColor = new Color(0.25f, 0.85f, 0.85f, 1.0f);
                _slottedTierBadge.Modulate = new Color(0.35f, 0.95f, 0.95f);
            }
            else if (map.Tier <= 5)
            {
                tierStyle.BgColor = new Color(0.12f, 0.13f, 0.16f, 0.95f);
                tierStyle.BorderColor = new Color(0.75f, 0.78f, 0.85f, 1.0f);
                _slottedTierBadge.Modulate = new Color(0.90f, 0.92f, 0.95f);
            }
            else if (map.Tier <= 10)
            {
                tierStyle.BgColor = new Color(0.20f, 0.16f, 0.06f, 0.95f);
                tierStyle.BorderColor = new Color(0.92f, 0.72f, 0.22f, 1.0f);
                _slottedTierBadge.Modulate = new Color(1.0f, 0.85f, 0.35f);
            }
            else if (map.Tier <= 15)
            {
                tierStyle.BgColor = new Color(0.22f, 0.08f, 0.08f, 0.95f);
                tierStyle.BorderColor = new Color(0.92f, 0.28f, 0.28f, 1.0f);
                _slottedTierBadge.Modulate = new Color(1.0f, 0.45f, 0.45f);
            }
            else
            {
                tierStyle.BgColor = new Color(0.18f, 0.06f, 0.25f, 0.95f);
                tierStyle.BorderColor = new Color(0.82f, 0.32f, 0.95f, 1.0f);
                _slottedTierBadge.Modulate = new Color(0.92f, 0.50f, 1.0f);
            }

            _slottedTierBadge.AddThemeStyleboxOverride("normal", tierStyle);
        }

        if (_slottedMapNameLabel != null)
        {
            _slottedMapNameLabel.Text = map.Name;
            _slottedMapNameLabel.Modulate = SlottedRarity.ToLowerInvariant() switch
            {
                "rare" => new Color(1.0f, 0.92f, 0.45f),      // Gold Yellow
                "magic" => new Color(0.55f, 0.68f, 1.0f),     // Ice Blue
                "corrupted" => new Color(0.95f, 0.28f, 0.45f), // Crimson
                _ => new Color(0.92f, 0.92f, 0.95f)           // White
            };
        }

        if (_slottedBossLabel != null)
        {
            _slottedBossLabel.Text = !string.IsNullOrEmpty(map.BossName) ? $"👑 {map.BossName}" : "⚔ Unknown Pack";
        }

        if (_slottedInfoLabel != null)
        {
            string rarityText = map.IsFailSafe ? "Endless Void Map (T0 Fail-Safe)" : $"{SlottedRarity} Map Slate";
            _slottedInfoLabel.Text = $"Monster Level {map.MonsterLevel} • {rarityText}";
        }
    }

    private void UpdateMetricsDisplay()
    {
        if (SlottedMap == null) return;

        int totalQty = SlottedMap.BaseQuantity + _slottedModifiers.Sum(m => m.Definition.BonusQuantity);
        int totalPack = SlottedMap.BasePackSize + _slottedModifiers.Sum(m => m.Definition.BonusPackSize);
        int bonusRarity = SlottedRarity.ToLowerInvariant() switch
        {
            "rare" => 35,
            "magic" => 15,
            _ => 0
        };

        if (_qtyValueLabel != null) _qtyValueLabel.Text = $"+{totalQty}%";
        if (_rarityValueLabel != null) _rarityValueLabel.Text = $"+{bonusRarity}%";
        if (_packSizeValueLabel != null) _packSizeValueLabel.Text = $"+{totalPack}%";
        if (_mobLevelValueLabel != null) _mobLevelValueLabel.Text = $"Lv.{SlottedMap.MonsterLevel}";
    }

    private void UpdateThreatBadges()
    {
        if (_threatBadgesHBox == null) return;

        foreach (Node child in _threatBadgesHBox.GetChildren())
        {
            child.QueueFree();
        }

        var threats = new List<ThreatBadgeInfo>();
        foreach (var mod in _slottedModifiers)
        {
            var badge = ThreatBadgeInfo.FromModifier(mod);
            if (badge != null && !threats.Any(t => t.Key == badge.Key))
            {
                threats.Add(badge);
            }
        }

        if (threats.Count == 0)
        {
            var safeLabel = new Label
            {
                Text = "🛡️ Safe Zone (No Dangerous Mods)",
                Modulate = new Color(0.45f, 0.85f, 0.55f, 0.85f),
                VerticalAlignment = VerticalAlignment.Center
            };
            safeLabel.AddThemeFontSizeOverride("font_size", 10);
            _threatBadgesHBox.AddChild(safeLabel);
            return;
        }

        foreach (var badge in threats)
        {
            var badgeContainer = new PanelContainer();
            var badgeStyle = new StyleBoxFlat
            {
                BgColor = new Color(badge.BadgeColor.R * 0.25f, badge.BadgeColor.G * 0.25f, badge.BadgeColor.B * 0.25f, 0.85f),
                BorderWidthLeft = 1,
                BorderWidthTop = 1,
                BorderWidthRight = 1,
                BorderWidthBottom = 1,
                BorderColor = badge.BadgeColor,
                CornerRadiusTopLeft = 3,
                CornerRadiusTopRight = 3,
                CornerRadiusBottomRight = 3,
                CornerRadiusBottomLeft = 3,
                ContentMarginLeft = 5,
                ContentMarginRight = 5,
                ContentMarginTop = 1,
                ContentMarginBottom = 1
            };
            badgeContainer.AddThemeStyleboxOverride("panel", badgeStyle);
            badgeContainer.TooltipText = badge.TooltipDescription;

            var label = new Label
            {
                Text = $"{badge.IconText} {badge.ShortText}",
                Modulate = badge.BadgeColor
            };
            label.AddThemeFontSizeOverride("font_size", 10);
            badgeContainer.AddChild(label);

            _threatBadgesHBox.AddChild(badgeContainer);
        }
    }

    private void UpdateModifiersList()
    {
        if (_modifiersVBox == null) return;

        foreach (Node child in _modifiersVBox.GetChildren())
        {
            if (child != _noModsLabel)
            {
                child.QueueFree();
            }
        }

        if (_slottedModifiers.Count == 0)
        {
            if (_noModsLabel != null) _noModsLabel.Visible = true;
            return;
        }

        if (_noModsLabel != null) _noModsLabel.Visible = false;

        foreach (var mod in _slottedModifiers)
        {
            var modRow = new HBoxContainer();
            modRow.AddThemeConstantOverride("separation", 6);

            var bullet = new Label
            {
                Text = "•",
                Modulate = new Color(0.85f, 0.72f, 0.35f)
            };
            bullet.AddThemeFontSizeOverride("font_size", 11);
            modRow.AddChild(bullet);

            var textLabel = new Label
            {
                Text = $"{mod.FormattedName} (+{mod.Definition.BonusQuantity}% Qty, +{mod.Definition.BonusPackSize}% Pack)",
                Modulate = new Color(0.90f, 0.90f, 0.95f),
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                AutowrapMode = TextServer.AutowrapMode.WordSmart
            };
            textLabel.AddThemeFontSizeOverride("font_size", 11);
            modRow.AddChild(textLabel);

            _modifiersVBox.AddChild(modRow);
        }
    }

    private Button CreateMapSlateCard(MapDefinition map)
    {
        var btn = new Button
        {
            CustomMinimumSize = new Vector2(96, 56),
            Flat = false,
            ToggleMode = false
        };

        var styleNormal = new StyleBoxFlat
        {
            BgColor = new Color(0.08f, 0.085f, 0.11f, 0.95f),
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            BorderColor = new Color(0.20f, 0.22f, 0.28f, 1.0f),
            CornerRadiusTopLeft = 3,
            CornerRadiusTopRight = 3,
            CornerRadiusBottomRight = 3,
            CornerRadiusBottomLeft = 3,
            ContentMarginLeft = 4,
            ContentMarginRight = 4,
            ContentMarginTop = 3,
            ContentMarginBottom = 3
        };
        btn.AddThemeStyleboxOverride("normal", styleNormal);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 2);

        var topHBox = new HBoxContainer();
        var tierBadge = new Label
        {
            Text = $"T{map.Tier}",
            Modulate = map.Tier == 0 ? new Color(0.3f, 0.9f, 0.9f) : (map.Tier >= 11 ? new Color(1f, 0.4f, 0.4f) : new Color(0.9f, 0.8f, 0.3f))
        };
        tierBadge.AddThemeFontSizeOverride("font_size", 10);
        topHBox.AddChild(tierBadge);

        var mobLvLabel = new Label
        {
            Text = $"Lv.{map.MonsterLevel}",
            Modulate = new Color(0.6f, 0.62f, 0.7f),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        mobLvLabel.AddThemeFontSizeOverride("font_size", 9);
        topHBox.AddChild(mobLvLabel);

        var qBtn = new Button
        {
            Text = "+Q",
            TooltipText = $"Add {map.Name} to Map Queue",
            FocusMode = FocusModeEnum.None
        };
        qBtn.AddThemeFontSizeOverride("font_size", 9);
        var qStyle = new StyleBoxFlat
        {
            BgColor = new Color(0.12f, 0.14f, 0.18f, 0.95f),
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            BorderColor = new Color(0.35f, 0.45f, 0.65f, 0.85f),
            CornerRadiusTopLeft = 2,
            CornerRadiusTopRight = 2,
            CornerRadiusBottomRight = 2,
            CornerRadiusBottomLeft = 2,
            ContentMarginLeft = 3,
            ContentMarginRight = 3,
            ContentMarginTop = 0,
            ContentMarginBottom = 0
        };
        qBtn.AddThemeStyleboxOverride("normal", qStyle);
        qBtn.Pressed += () =>
        {
            EnqueueMap(map, rarity: "Normal");
        };
        topHBox.AddChild(qBtn);
        vbox.AddChild(topHBox);

        var nameLabel = new Label
        {
            Text = map.Name,
            Modulate = new Color(0.92f, 0.92f, 0.96f),
            TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis
        };
        nameLabel.AddThemeFontSizeOverride("font_size", 11);
        vbox.AddChild(nameLabel);

        var qtyLabel = new Label
        {
            Text = $"+{map.BaseQuantity}% Qty",
            Modulate = new Color(0.85f, 0.72f, 0.35f, 0.85f)
        };
        qtyLabel.AddThemeFontSizeOverride("font_size", 9);
        vbox.AddChild(qtyLabel);

        btn.AddChild(vbox);
        btn.Pressed += () =>
        {
            SlotMap(map, rarity: "Normal");
        };

        btn.TooltipText = $"{map.Name} (Tier {map.Tier})\nMonster Level: {map.MonsterLevel}\nBoss: {map.BossName ?? "None"}\nBase Quantity: +{map.BaseQuantity}%\nBase Pack Size: +{map.BasePackSize}%\n\nClick to slot into Map Device.";

        return btn;
    }

    private void UpdateMapCardSelections()
    {
        foreach (var kvp in _mapCards)
        {
            var btn = kvp.Key;
            var map = kvp.Value;
            bool isSlotted = (SlottedMap != null && SlottedMap.Id == map.Id);

            var style = new StyleBoxFlat
            {
                BgColor = isSlotted ? new Color(0.16f, 0.14f, 0.08f, 0.98f) : new Color(0.08f, 0.085f, 0.11f, 0.95f),
                BorderWidthLeft = isSlotted ? 2 : 1,
                BorderWidthTop = isSlotted ? 2 : 1,
                BorderWidthRight = isSlotted ? 2 : 1,
                BorderWidthBottom = isSlotted ? 2 : 1,
                BorderColor = isSlotted ? new Color(0.95f, 0.78f, 0.28f, 1.0f) : new Color(0.20f, 0.22f, 0.28f, 1.0f),
                CornerRadiusTopLeft = 3,
                CornerRadiusTopRight = 3,
                CornerRadiusBottomRight = 3,
                CornerRadiusBottomLeft = 3,
                ContentMarginLeft = 4,
                ContentMarginRight = 4,
                ContentMarginTop = 3,
                ContentMarginBottom = 3
            };
            btn.AddThemeStyleboxOverride("normal", style);
        }
    }

    private static bool MatchesTierFilter(MapDefinition map, string filter)
    {
        return filter switch
        {
            "T0" => map.Tier == 0,
            "White" => map.Tier >= 1 && map.Tier <= 5,
            "Yellow" => map.Tier >= 6 && map.Tier <= 10,
            "Red" => map.Tier >= 11 && map.Tier <= 15,
            "Pinnacle" => map.Tier >= 16,
            _ => true // "All"
        };
    }

    private void UpdateTierFilterButtonStyles()
    {
        UpdateFilterBtnStyle(_tierAllBtn, CurrentTierFilter == "All");
        UpdateFilterBtnStyle(_tierT0Btn, CurrentTierFilter == "T0");
        UpdateFilterBtnStyle(_tierWhiteBtn, CurrentTierFilter == "White");
        UpdateFilterBtnStyle(_tierYellowBtn, CurrentTierFilter == "Yellow");
        UpdateFilterBtnStyle(_tierRedBtn, CurrentTierFilter == "Red");
        UpdateFilterBtnStyle(_tierPinnacleBtn, CurrentTierFilter == "Pinnacle");
    }

    private static void UpdateFilterBtnStyle(Button? btn, bool isSelected)
    {
        if (btn == null) return;
        var style = new StyleBoxFlat
        {
            BgColor = isSelected ? new Color(0.18f, 0.15f, 0.09f, 0.95f) : new Color(0.08f, 0.085f, 0.11f, 0.85f),
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            BorderColor = isSelected ? new Color(0.92f, 0.72f, 0.25f, 0.95f) : new Color(0.20f, 0.22f, 0.28f, 0.85f),
            CornerRadiusTopLeft = 3,
            CornerRadiusTopRight = 3,
            CornerRadiusBottomRight = 3,
            CornerRadiusBottomLeft = 3,
            ContentMarginLeft = 6,
            ContentMarginRight = 6,
            ContentMarginTop = 2,
            ContentMarginBottom = 2
        };
        btn.AddThemeStyleboxOverride("normal", style);
        btn.Modulate = isSelected ? new Color(1.0f, 0.90f, 0.45f) : new Color(0.75f, 0.78f, 0.85f);
    }

    private Button CreateQueueSlotButton(int slotIndex)
    {
        var btn = new Button
        {
            CustomMinimumSize = new Vector2(36, 32),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            FocusMode = FocusModeEnum.None,
            ToggleMode = false
        };

        var vbox = new VBoxContainer
        {
            Name = "VBoxContainer"
        };
        vbox.AddThemeConstantOverride("separation", 0);
        vbox.MouseFilter = MouseFilterEnum.Ignore;

        var topLabel = new Label
        {
            Name = "TopLabel",
            HorizontalAlignment = HorizontalAlignment.Center,
            MouseFilter = MouseFilterEnum.Ignore
        };
        topLabel.AddThemeFontSizeOverride("font_size", 9);
        vbox.AddChild(topLabel);

        var bottomLabel = new Label
        {
            Name = "BottomLabel",
            HorizontalAlignment = HorizontalAlignment.Center,
            MouseFilter = MouseFilterEnum.Ignore
        };
        bottomLabel.AddThemeFontSizeOverride("font_size", 9);
        vbox.AddChild(bottomLabel);

        btn.AddChild(vbox);

        btn.Pressed += () =>
        {
            if (QueueManager != null)
            {
                var item = QueueManager.GetSlot(slotIndex);
                if (item != null)
                {
                    SlotMap(item.Map, item.Modifiers, item.Rarity);
                }
                else
                {
                    EnqueueCurrentSlottedMap();
                }
            }
        };

        btn.GuiInput += @event =>
        {
            if (@event is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Right)
            {
                if (QueueManager != null && QueueManager.GetSlot(slotIndex) != null)
                {
                    QueueManager.RemoveAt(slotIndex);
                    GetViewport()?.SetInputAsHandled();
                }
            }
        };

        return btn;
    }

    public void UpdateQueueDisplay()
    {
        int count = QueueManager?.Count ?? 0;
        if (_queueTitleLabel != null)
        {
            _queueTitleLabel.Text = $"MAP QUEUE ({count}/10)";
        }

        if (_queueEnqueueBtn != null)
        {
            _queueEnqueueBtn.Disabled = (QueueManager?.IsFull ?? false) || SlottedMap == null;
        }

        if (_queueClearBtn != null)
        {
            _queueClearBtn.Disabled = (count == 0);
        }

        for (int i = 0; i < MapQueueManager.MaxCapacity; i++)
        {
            var btn = _queueSlotButtons[i];
            if (btn == null) continue;

            var topLabel = btn.GetNodeOrNull<Label>("VBoxContainer/TopLabel") ?? btn.FindChild("TopLabel", recursive: true, owned: false) as Label;
            var bottomLabel = btn.GetNodeOrNull<Label>("VBoxContainer/BottomLabel") ?? btn.FindChild("BottomLabel", recursive: true, owned: false) as Label;

            var item = QueueManager?.GetSlot(i);
            if (item != null)
            {
                var style = new StyleBoxFlat
                {
                    BgColor = new Color(0.10f, 0.11f, 0.14f, 0.95f),
                    BorderWidthLeft = (i == 0 && (QueueManager?.IsCountdownActive ?? false)) ? 2 : 1,
                    BorderWidthTop = (i == 0 && (QueueManager?.IsCountdownActive ?? false)) ? 2 : 1,
                    BorderWidthRight = (i == 0 && (QueueManager?.IsCountdownActive ?? false)) ? 2 : 1,
                    BorderWidthBottom = (i == 0 && (QueueManager?.IsCountdownActive ?? false)) ? 2 : 1,
                    CornerRadiusTopLeft = 3,
                    CornerRadiusTopRight = 3,
                    CornerRadiusBottomRight = 3,
                    CornerRadiusBottomLeft = 3,
                    ContentMarginLeft = 2,
                    ContentMarginRight = 2,
                    ContentMarginTop = 1,
                    ContentMarginBottom = 1
                };

                Color rarityColor = item.Rarity.ToLowerInvariant() switch
                {
                    "rare" => new Color(1.0f, 0.85f, 0.35f),
                    "magic" => new Color(0.45f, 0.75f, 1.0f),
                    "corrupted" => new Color(0.95f, 0.28f, 0.45f),
                    _ => new Color(0.75f, 0.78f, 0.85f)
                };

                if (i == 0 && (QueueManager?.IsCountdownActive ?? false))
                {
                    style.BorderColor = new Color(1f, 0.9f, 0.4f, 1f);
                    style.ShadowColor = new Color(1f, 0.85f, 0.3f, 0.3f);
                    style.ShadowSize = 3;
                }
                else
                {
                    style.BorderColor = rarityColor;
                }

                btn.AddThemeStyleboxOverride("normal", style);

                if (topLabel != null)
                {
                    topLabel.Text = $"T{item.Tier}";
                    topLabel.Modulate = item.Tier == 0 ? new Color(0.35f, 0.95f, 0.95f) : rarityColor;
                }

                if (bottomLabel != null)
                {
                    string shortName = item.Map.Name.Length > 4 ? item.Map.Name.Substring(0, 3) : item.Map.Name;
                    bottomLabel.Text = shortName;
                    bottomLabel.Modulate = new Color(0.85f, 0.88f, 0.92f);
                }

                btn.TooltipText = $"Slot #{i + 1}: {item.Map.Name} (T{item.Tier} {item.Rarity})\nQuantity: +{item.TotalQuantity}% | Pack: +{item.TotalPackSize}%\nAffixes: {item.Modifiers.Count}\n\n• Left-Click: Inspect on Altar\n• Right-Click: Remove from Queue";
            }
            else
            {
                var emptyStyle = new StyleBoxFlat
                {
                    BgColor = new Color(0.06f, 0.065f, 0.085f, 0.85f),
                    BorderWidthLeft = 1,
                    BorderWidthTop = 1,
                    BorderWidthRight = 1,
                    BorderWidthBottom = 1,
                    BorderColor = new Color(0.18f, 0.20f, 0.25f, 0.7f),
                    CornerRadiusTopLeft = 3,
                    CornerRadiusTopRight = 3,
                    CornerRadiusBottomRight = 3,
                    CornerRadiusBottomLeft = 3,
                    ContentMarginLeft = 2,
                    ContentMarginRight = 2,
                    ContentMarginTop = 1,
                    ContentMarginBottom = 1
                };
                btn.AddThemeStyleboxOverride("normal", emptyStyle);

                if (topLabel != null)
                {
                    topLabel.Text = $"#{i + 1}";
                    topLabel.Modulate = new Color(0.45f, 0.48f, 0.55f);
                }

                if (bottomLabel != null)
                {
                    bottomLabel.Text = "+";
                    bottomLabel.Modulate = new Color(0.35f, 0.38f, 0.45f);
                }

                btn.TooltipText = $"Slot #{i + 1} (Empty)\nClick to enqueue current slotted map, or click [+Q] on a map card below.";
            }
        }
    }

    /// <summary>
    /// Enqueues the currently slotted map with its current rolled modifiers and rarity.
    /// </summary>
    public void EnqueueCurrentSlottedMap()
    {
        if (SlottedMap == null || QueueManager == null || QueueManager.IsFull) return;

        QueueManager.Enqueue(SlottedMap, SlottedModifiers, SlottedRarity);
    }

    /// <summary>
    /// Enqueues a specific map into the queue.
    /// </summary>
    public void EnqueueMap(MapDefinition map, IReadOnlyList<RolledMapModifier>? modifiers = null, string rarity = "Normal")
    {
        if (map == null || QueueManager == null || QueueManager.IsFull) return;

        QueueManager.Enqueue(map, modifiers, rarity);
    }

    public void ShowCountdownUI(float seconds)
    {
        if (_queueCountdownLabel != null)
        {
            _queueCountdownLabel.Text = $"⏳ Next in {seconds:F1}s...";
            _queueCountdownLabel.Visible = true;
        }
        if (_queueFastForwardBtn != null) _queueFastForwardBtn.Visible = true;
        if (_queueCancelCountdownBtn != null) _queueCancelCountdownBtn.Visible = true;
        if (_queueSpacer != null) _queueSpacer.Visible = false;

        UpdateQueueDisplay();
    }

    public void UpdateCountdownUI(float seconds)
    {
        if (_queueCountdownLabel != null)
        {
            _queueCountdownLabel.Text = $"⏳ Next in {seconds:F1}s...";
        }
    }

    public void HideCountdownUI()
    {
        if (_queueCountdownLabel != null) _queueCountdownLabel.Visible = false;
        if (_queueFastForwardBtn != null) _queueFastForwardBtn.Visible = false;
        if (_queueCancelCountdownBtn != null) _queueCancelCountdownBtn.Visible = false;
        if (_queueSpacer != null) _queueSpacer.Visible = true;

        UpdateQueueDisplay();
    }

    private void OnActivatePressed()
    {
        if (IsPortalActive || SlottedMap == null) return;

        QueueManager?.CancelCountdown();
        SetGoldenPulse(false);
        SetPortalActive(true);
        OnMapActivated?.Invoke(SlottedMap, SlottedModifiers, SlottedRarity);
    }

    private void OnEmergencyPortalPressed()
    {
        if (!IsPortalActive) return;

        SetPortalActive(false);
        OnEmergencyPortalTriggered?.Invoke();
    }

    private void OnCraftRerollPressed()
    {
        if (IsPortalActive) return;
        RerollCurrentMapModifiers("Rare");
    }
}
