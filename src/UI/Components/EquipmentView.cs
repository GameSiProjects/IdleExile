using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using IdleExile.Core.Models;
using IdleExile.Core.Services;
using IdleExile.Core.State;

namespace IdleExile.UI.Components;

/// <summary>
/// Mini-Window Equipment View Controller (440 x 780 px).
/// Displays the 10 fixed equipment slots, live survival & offensive benchmarks,
/// and the Smart Tooltip with Delta EHP / Delta DPS quick comparison.
/// Conforms to docs/11_equipment_system_design.md Section 8 and Task 3.4.
/// </summary>
public partial class EquipmentView : Control
{
    // ─────────────────────────────────────────────
    //  Signals & Events
    // ─────────────────────────────────────────────

    public event Action? OnCloseRequested;
    public event Action<GearSlot, IGearItem>? OnItemEquipped;
    public event Action<IGearItem>? OnItemLockToggled;
    public event Action<GearSlot, IGearItem>? OnItemStashed;

    public void RequestClose() => OnCloseRequested?.Invoke();

    // ─────────────────────────────────────────────
    //  State
    // ─────────────────────────────────────────────

    public PlayerState? Player { get; private set; }
    public GearSlot SelectedSlot { get; private set; } = GearSlot.BodyArmour;
    public IGearItem? InspectedItem { get; private set; }
    public List<IGearItem> CandidateItems { get; } = new();

    /// <summary>When true, the Sticky Drawer affix list only shows T1/T2 rows (summary mode).</summary>
    public bool ShowHighTierAffixesOnly { get; private set; }

    /// <summary>Last comparison rendered in the Sticky Drawer (null when inspecting equipped item / empty slot).</summary>
    public EquipmentComparisonResult? LastComparison { get; private set; }

    /// <summary>Current verdict badge rendered in the Sticky Drawer header.</summary>
    public DeltaVerdict CurrentVerdict { get; private set; } = DeltaVerdict.EmptySlot;

    /// <summary>Fixed pixel height of the Smart Tooltip Sticky Drawer (Task 2.2).</summary>
    public const float StickyDrawerHeight = 140f;

    private const float AffixScrollStep = 26f;
    private const double AffixScrollDuration = 0.14;
    private const double DrawerPulseDuration = 0.18;

    private static readonly Color DeltaUpColor = new(0.35f, 0.85f, 0.45f);
    private static readonly Color DeltaDownColor = new(0.95f, 0.4f, 0.35f);
    private static readonly Color DeltaNeutralColor = new(0.78f, 0.8f, 0.85f);
    private static readonly Color DeltaTradeOffColor = new(0.95f, 0.72f, 0.3f);
    private static readonly Color HighTierAffixColor = new(1.0f, 0.86f, 0.42f);
    private static readonly Color PrefixAffixColor = new(0.55f, 0.75f, 0.95f);
    private static readonly Color SuffixAffixColor = new(0.9f, 0.75f, 0.45f);

    // ─────────────────────────────────────────────
    //  UI Node References
    // ─────────────────────────────────────────────

    private Button? _closeButton;
    private Button? _helpButton;
    private PanelContainer? _helpOverlay;
    private Button? _helpCloseButton;

    // 10 Slot Buttons
    private readonly Dictionary<GearSlot, Button> _slotButtons = new();
    private readonly Dictionary<GearSlot, Label> _slotItemLabels = new();
    private readonly Dictionary<GearSlot, ColorRect> _slotRarityBorders = new();

    // Benchmarks
    private Label? _ehpBenchmarkLabel;
    private Label? _armourBenchmarkLabel;
    private Label? _evasionBenchmarkLabel;
    private Label? _esBenchmarkLabel;
    private Label? _fireResBenchmarkLabel;
    private Label? _coldResBenchmarkLabel;
    private Label? _lightResBenchmarkLabel;
    private Label? _chaosResBenchmarkLabel;
    private Label? _totalDpsBenchmarkLabel;
    private Label? _strStatLabel;
    private Label? _dexStatLabel;
    private Label? _intStatLabel;

    // Smart Tooltip — Sticky Drawer (Task 2.2)
    private PanelContainer? _smartDeltaDrawer;
    private Label? _tooltipTitleLabel;
    private Label? _tooltipSubtitleLabel;
    private Label? _tooltipBaseStatsLabel;
    private Label? _tooltipImplicitLabel;
    private VBoxContainer? _tooltipAffixListVBox;
    private PanelContainer? _deltaPreviewCard;
    private StyleBoxFlat? _deltaCardStyle;
    private Label? _deltaVerdictLabel;
    private Label? _deltaEhpLabel;
    private Label? _deltaEhpDetailLabel;
    private Label? _deltaDpsLabel;
    private Label? _deltaDpsDetailLabel;
    private Label? _deltaSummaryLabel;
    private Label? _affixHeaderLabel;
    private Button? _affixFilterToggle;
    private ScrollContainer? _affixScroll;
    private Tween? _affixScrollTween;
    private float _affixScrollTarget;
    private Tween? _drawerPulseTween;
    private string? _lastRenderedItemId;

    // Action Buttons
    private Button? _equipButton;
    private Button? _lockButton;
    private Button? _stashButton;

    // Candidate Items Drawer
    private HBoxContainer? _candidateHBox;

    public override void _Ready()
    {
        BindNodes();
        HookEvents();
        PopulateDefaultCandidatesIfEmpty();
    }

    private void BindNodes()
    {
        _closeButton = GetNodeOrNull<Button>("%CloseButton");
        _helpButton = GetNodeOrNull<Button>("%HelpButton");
        _helpOverlay = GetNodeOrNull<PanelContainer>("%HelpOverlay");
        _helpCloseButton = GetNodeOrNull<Button>("%HelpCloseButton");

        // Bind 10 Slot Buttons
        BindSlotButton(GearSlot.Helmet, "%SlotHelmet", "%SlotHelmetLabel", "%SlotHelmetBorder");
        BindSlotButton(GearSlot.MainHand, "%SlotMainHand", "%SlotMainHandLabel", "%SlotMainHandBorder");
        BindSlotButton(GearSlot.BodyArmour, "%SlotBodyArmour", "%SlotBodyArmourLabel", "%SlotBodyArmourBorder");
        BindSlotButton(GearSlot.OffHand, "%SlotOffHand", "%SlotOffHandLabel", "%SlotOffHandBorder");
        BindSlotButton(GearSlot.Gloves, "%SlotGloves", "%SlotGlovesLabel", "%SlotGlovesBorder");
        BindSlotButton(GearSlot.Boots, "%SlotBoots", "%SlotBootsLabel", "%SlotBootsBorder");
        BindSlotButton(GearSlot.Amulet, "%SlotAmulet", "%SlotAmuletLabel", "%SlotAmuletBorder");
        BindSlotButton(GearSlot.Ring1, "%SlotRing1", "%SlotRing1Label", "%SlotRing1Border");
        BindSlotButton(GearSlot.Ring2, "%SlotRing2", "%SlotRing2Label", "%SlotRing2Border");
        BindSlotButton(GearSlot.Belt, "%SlotBelt", "%SlotBeltLabel", "%SlotBeltBorder");

        // Benchmarks
        _ehpBenchmarkLabel = GetNodeOrNull<Label>("%EhpBenchmarkLabel");
        _armourBenchmarkLabel = GetNodeOrNull<Label>("%ArmourBenchmarkLabel");
        _evasionBenchmarkLabel = GetNodeOrNull<Label>("%EvasionBenchmarkLabel");
        _esBenchmarkLabel = GetNodeOrNull<Label>("%EsBenchmarkLabel");
        _fireResBenchmarkLabel = GetNodeOrNull<Label>("%FireResBenchmarkLabel");
        _coldResBenchmarkLabel = GetNodeOrNull<Label>("%ColdResBenchmarkLabel");
        _lightResBenchmarkLabel = GetNodeOrNull<Label>("%LightResBenchmarkLabel");
        _chaosResBenchmarkLabel = GetNodeOrNull<Label>("%ChaosResBenchmarkLabel");
        _totalDpsBenchmarkLabel = GetNodeOrNull<Label>("%TotalDpsBenchmarkLabel");
        _strStatLabel = GetNodeOrNull<Label>("%StrStatLabel");
        _dexStatLabel = GetNodeOrNull<Label>("%DexStatLabel");
        _intStatLabel = GetNodeOrNull<Label>("%IntStatLabel");

        // Tooltip — Sticky Drawer
        _smartDeltaDrawer = GetNodeOrNull<PanelContainer>("%SmartDeltaDrawer");
        _tooltipTitleLabel = GetNodeOrNull<Label>("%TooltipTitleLabel");
        _tooltipSubtitleLabel = GetNodeOrNull<Label>("%TooltipSubtitleLabel");
        _tooltipBaseStatsLabel = GetNodeOrNull<Label>("%TooltipBaseStatsLabel");
        _tooltipImplicitLabel = GetNodeOrNull<Label>("%TooltipImplicitLabel");
        _tooltipAffixListVBox = GetNodeOrNull<VBoxContainer>("%TooltipAffixListVBox");
        _deltaPreviewCard = GetNodeOrNull<PanelContainer>("%DeltaPreviewCard");
        _deltaVerdictLabel = GetNodeOrNull<Label>("%DeltaVerdictLabel");
        _deltaEhpLabel = GetNodeOrNull<Label>("%DeltaEhpLabel");
        _deltaEhpDetailLabel = GetNodeOrNull<Label>("%DeltaEhpDetailLabel");
        _deltaDpsLabel = GetNodeOrNull<Label>("%DeltaDpsLabel");
        _deltaDpsDetailLabel = GetNodeOrNull<Label>("%DeltaDpsDetailLabel");
        _deltaSummaryLabel = GetNodeOrNull<Label>("%DeltaSummaryLabel");
        _affixHeaderLabel = GetNodeOrNull<Label>("%AffixHeaderLabel");
        _affixFilterToggle = GetNodeOrNull<Button>("%AffixFilterToggle");
        _affixScroll = GetNodeOrNull<ScrollContainer>("%AffixScroll");

        // Duplicate the delta card stylebox so verdict tinting never mutates the shared scene resource
        if (_deltaPreviewCard?.GetThemeStylebox("panel") is StyleBoxFlat cardStyle)
        {
            _deltaCardStyle = (StyleBoxFlat)cardStyle.Duplicate();
            _deltaPreviewCard.AddThemeStyleboxOverride("panel", _deltaCardStyle);
        }

        // Action Buttons
        _equipButton = GetNodeOrNull<Button>("%EquipButton");
        _lockButton = GetNodeOrNull<Button>("%LockButton");
        _stashButton = GetNodeOrNull<Button>("%StashButton");

        // Candidate Drawer
        _candidateHBox = GetNodeOrNull<HBoxContainer>("%CandidateHBox");
    }

    private void BindSlotButton(GearSlot slot, string buttonPath, string labelPath, string borderPath)
    {
        var btn = GetNodeOrNull<Button>(buttonPath);
        if (btn != null)
        {
            _slotButtons[slot] = btn;
            btn.Pressed += () => SelectSlot(slot);
        }

        var lbl = GetNodeOrNull<Label>(labelPath);
        if (lbl != null)
        {
            _slotItemLabels[slot] = lbl;
        }

        var border = GetNodeOrNull<ColorRect>(borderPath);
        if (border != null)
        {
            _slotRarityBorders[slot] = border;
        }
    }

    private void HookEvents()
    {
        if (_closeButton != null)
        {
            _closeButton.Pressed += () => OnCloseRequested?.Invoke();
        }

        if (_helpButton != null)
        {
            _helpButton.Pressed += () =>
            {
                if (_helpOverlay != null) _helpOverlay.Visible = true;
            };
        }

        if (_helpCloseButton != null)
        {
            _helpCloseButton.Pressed += () =>
            {
                if (_helpOverlay != null) _helpOverlay.Visible = false;
            };
        }

        if (_equipButton != null)
        {
            _equipButton.Pressed += OnEquipButtonPressed;
        }

        if (_lockButton != null)
        {
            _lockButton.Pressed += OnLockButtonPressed;
        }

        if (_stashButton != null)
        {
            _stashButton.Pressed += OnStashButtonPressed;
        }

        if (_affixFilterToggle != null)
        {
            _affixFilterToggle.Toggled += SetHighTierAffixFilter;
        }

        if (_affixScroll != null)
        {
            _affixScroll.GuiInput += OnAffixScrollGuiInput;
        }
    }

    /// <summary>
    /// Toggles the Sticky Drawer affix list between full smooth-scroll mode and T1/T2 summary mode.
    /// </summary>
    public void SetHighTierAffixFilter(bool highTierOnly)
    {
        if (ShowHighTierAffixesOnly == highTierOnly) return;
        ShowHighTierAffixesOnly = highTierOnly;

        if (_affixFilterToggle != null && _affixFilterToggle.ButtonPressed != highTierOnly)
        {
            _affixFilterToggle.SetPressedNoSignal(highTierOnly);
        }

        RefreshTooltip();
    }

    /// <summary>
    /// Attaches the live PlayerState and refreshes the view.
    /// </summary>
    public void SetPlayer(PlayerState player)
    {
        if (Player != null)
        {
            Player.Gear.OnEquipmentChanged -= RefreshView;
            Player.OnResourceChanged -= OnPlayerResourceChanged;
        }

        Player = player ?? throw new ArgumentNullException(nameof(player));
        Player.Gear.OnEquipmentChanged += RefreshView;
        Player.OnResourceChanged += OnPlayerResourceChanged;

        PopulateDefaultCandidatesIfEmpty();
        RefreshView();
    }

    private void OnPlayerResourceChanged(float life, float es, float mana)
    {
        RefreshBenchmarks();
    }

    /// <summary>
    /// Selects an equipment slot, inspecting its currently equipped item or showing empty slot info.
    /// </summary>
    public void SelectSlot(GearSlot slot)
    {
        SelectedSlot = slot;
        var equipped = Player?.Gear.GetItem(slot);
        InspectedItem = equipped;

        RefreshSlotButtons();
        RefreshCandidates();
        RefreshTooltip();
    }

    /// <summary>
    /// Selects a candidate item to inspect and compares it with the currently equipped item in the matching slot.
    /// </summary>
    public void InspectCandidate(IGearItem candidate)
    {
        if (candidate == null) return;
        if (SelectedSlot == GearSlot.OffHand && IsCandidateValidForSlot(candidate, GearSlot.OffHand))
        {
            // Retain OffHand slot selection for dual wielding, shield, or quiver
        }
        else if (SelectedSlot == GearSlot.Ring2 && candidate.TargetSlot == GearSlot.Ring1)
        {
            // Retain Ring2 selection
        }
        else
        {
            SelectedSlot = candidate.TargetSlot;
        }

        InspectedItem = candidate;

        RefreshSlotButtons();
        RefreshTooltip();
    }

    /// <summary>
    /// Adds a candidate item (from loot drop or stash) to the preview drawer.
    /// </summary>
    public void AddCandidateItem(IGearItem item)
    {
        if (item == null) return;
        if (!CandidateItems.Any(i => i.InstanceId == item.InstanceId))
        {
            CandidateItems.Add(item);
            RefreshCandidates();
        }
    }

    /// <summary>
    /// Full view refresh.
    /// </summary>
    public void RefreshView()
    {
        RefreshSlotButtons();
        RefreshBenchmarks();
        RefreshCandidates();
        RefreshTooltip();
    }

    // ─────────────────────────────────────────────
    //  Slot Buttons & OffHand 2H Lockout
    // ─────────────────────────────────────────────

    private void RefreshSlotButtons()
    {
        if (Player == null) return;

        bool isOffHandDisabled = Player.Gear.IsOffHandDisabledByTwoHander;

        foreach (var (slot, btn) in _slotButtons)
        {
            var item = Player.Gear.GetItem(slot);
            bool isSelected = (slot == SelectedSlot);

            // Handle Off-Hand 2H Lockout
            if (slot == GearSlot.OffHand && isOffHandDisabled)
            {
                btn.Disabled = true;
                btn.Modulate = new Color(0.4f, 0.4f, 0.4f, 0.45f);
                if (_slotItemLabels.TryGetValue(slot, out var lbl))
                {
                    lbl.Text = "🔒 [2H Lock]";
                    lbl.Modulate = new Color(0.8f, 0.3f, 0.3f);
                }
                if (_slotRarityBorders.TryGetValue(slot, out var border))
                {
                    border.Color = new Color(0.3f, 0.15f, 0.15f);
                }
                continue;
            }

            btn.Disabled = false;
            btn.Modulate = isSelected ? new Color(1.2f, 1.2f, 1.2f, 1.0f) : Colors.White;

            // Update button icon & text
            btn.Text = slot switch
            {
                GearSlot.MainHand => item is WeaponItemInstance w ? $"{w.WeaponIcon} MAIN" : "MAIN",
                GearSlot.OffHand => item is WeaponItemInstance ow ? $"{ow.WeaponIcon} OFF"
                                 : item is ArmourItemInstance s && s.BaseDefinition.ArmourSlot == ArmourSlot.Shield ? "🛡️ SHIELD"
                                 : "OFF",
                GearSlot.Helmet => item != null ? "🪖 HELM" : "HELM",
                GearSlot.BodyArmour => item != null ? "🥋 BODY" : "BODY",
                GearSlot.Gloves => item != null ? "🧤 GLOVE" : "GLOVE",
                GearSlot.Boots => item != null ? "🥾 BOOT" : "BOOT",
                GearSlot.Amulet => item != null ? "📿 AMU" : "AMU",
                GearSlot.Ring1 => item != null ? "💍 R1" : "R1",
                GearSlot.Ring2 => item != null ? "💍 R2" : "R2",
                GearSlot.Belt => item != null ? "🎗️ BELT" : "BELT",
                _ => btn.Text
            };

            string itemName = item != null ? item.Name : "(Trống)";
            if (item != null && item.IsLocked)
            {
                itemName = $"🔒 {itemName}";
            }

            if (_slotItemLabels.TryGetValue(slot, out var itemLbl))
            {
                itemLbl.Text = itemName;
                itemLbl.Modulate = GetRarityColor(item?.Rarity ?? "Normal");
            }

            if (_slotRarityBorders.TryGetValue(slot, out var borderRect))
            {
                borderRect.Color = isSelected ? new Color(0.87f, 0.70f, 0.26f) : GetRarityColor(item?.Rarity ?? "Normal");
            }
        }
    }

    // ─────────────────────────────────────────────
    //  Benchmarks & Soft Attributes
    // ─────────────────────────────────────────────

    private void RefreshBenchmarks()
    {
        if (Player == null) return;

        float ehp = EquipmentComparisonService.CalculateEffectiveHealthPool(Player);
        float dps = EquipmentComparisonService.CalculateTotalDps(Player);

        if (_ehpBenchmarkLabel != null)
            _ehpBenchmarkLabel.Text = $"{ehp:N0}";

        if (_armourBenchmarkLabel != null)
            _armourBenchmarkLabel.Text = $"Armour: {Player.Defenses.Armour:F0}";

        if (_evasionBenchmarkLabel != null)
            _evasionBenchmarkLabel.Text = $"Evasion: {Player.Defenses.EvasionChance:F0}%";

        if (_esBenchmarkLabel != null)
            _esBenchmarkLabel.Text = $"ES: {Player.MaxEnergyShield:F0}";

        // Resistances with overcap representation
        UpdateResLabel(_fireResBenchmarkLabel, "Fire", Player.Defenses.FireResistance, Player.Defenses.MaxFireResistance);
        UpdateResLabel(_coldResBenchmarkLabel, "Cold", Player.Defenses.ColdResistance, Player.Defenses.MaxColdResistance);
        UpdateResLabel(_lightResBenchmarkLabel, "Light", Player.Defenses.LightningResistance, Player.Defenses.MaxLightningResistance);

        if (_chaosResBenchmarkLabel != null)
        {
            _chaosResBenchmarkLabel.Text = $"Chaos: {Player.Defenses.ChaosResistance:F0}%";
            _chaosResBenchmarkLabel.Modulate = new Color(0.8f, 0.45f, 0.9f);
        }

        if (_totalDpsBenchmarkLabel != null)
            _totalDpsBenchmarkLabel.Text = $"{dps:F1} DPS";

        // Soft Attributes (Task 3.1 & GDD Section 5)
        int str = Player.TotalStrength;
        int dex = Player.TotalDexterity;
        int intel = Player.TotalIntelligence;

        if (_strStatLabel != null)
            _strStatLabel.Text = $"STR: {str} (+{(str / 10f) * 5:F0} Life, +{(str / 10f) * 2:F0}% Armour)";

        if (_dexStatLabel != null)
            _dexStatLabel.Text = $"DEX: {dex} (+{(dex / 10f) * 20:F0} Acc, +{(dex / 10f) * 2:F0}% Eva)";

        if (_intStatLabel != null)
            _intStatLabel.Text = $"INT: {intel} (+{(intel / 10f) * 5:F0} Mana, +{(intel / 10f) * 2:F0}% ES)";
    }

    private static void UpdateResLabel(Label? label, string name, float val, float cap)
    {
        if (label == null) return;
        if (val >= cap)
        {
            float over = val - cap;
            label.Text = over > 0f ? $"{name}: {cap:F0}% (+{over:F0}%)" : $"{name}: {cap:F0}%";
            label.Modulate = new Color(0.35f, 0.85f, 0.45f); // Green cap safe
        }
        else
        {
            label.Text = $"{name}: {val:F0}% (Thiếu)";
            label.Modulate = new Color(0.95f, 0.45f, 0.35f); // Warning red
        }
    }

    // ─────────────────────────────────────────────
    //  Candidate Drawer
    // ─────────────────────────────────────────────

    private void RefreshCandidates()
    {
        if (_candidateHBox == null) return;

        foreach (Node child in _candidateHBox.GetChildren())
        {
            child.QueueFree();
        }

        var matchingCandidates = CandidateItems
            .Where(i => IsCandidateValidForSlot(i, SelectedSlot))
            .ToList();

        if (matchingCandidates.Count == 0)
        {
            var emptyLabel = new Label
            {
                Text = "Không có đồ dự phòng trong rương",
                HorizontalAlignment = HorizontalAlignment.Center
            };
            emptyLabel.AddThemeColorOverride("font_color", new Color(0.5f, 0.5f, 0.55f));
            emptyLabel.AddThemeFontSizeOverride("font_size", 10);
            _candidateHBox.AddChild(emptyLabel);
            return;
        }

        foreach (var item in matchingCandidates)
        {
            var btn = new Button
            {
                Text = $"{item.Name}\n[{item.Rarity}]",
                CustomMinimumSize = new Vector2(105, 34),
                Alignment = HorizontalAlignment.Center
            };
            btn.AddThemeFontSizeOverride("font_size", 10);
            btn.AddThemeColorOverride("font_color", GetRarityColor(item.Rarity));

            bool isInspected = (InspectedItem != null && InspectedItem.InstanceId == item.InstanceId);
            if (isInspected)
            {
                btn.Modulate = new Color(1.3f, 1.3f, 1.0f);
            }

            btn.Pressed += () => InspectCandidate(item);
            _candidateHBox.AddChild(btn);
        }
    }

    private bool IsCandidateValidForSlot(IGearItem item, GearSlot slot)
    {
        if (item == null) return false;
        if (slot == GearSlot.Ring2 && item.TargetSlot == GearSlot.Ring1) return true;
        if (slot == GearSlot.OffHand)
        {
            if (Player?.Gear.IsOffHandDisabledByTwoHander == true) return false;
            if (item.TargetSlot == GearSlot.OffHand) return true;
            if (item is WeaponItemInstance weapon)
            {
                if (weapon.BaseDefinition.WeaponClass == WeaponClass.Quiver)
                {
                    return Player?.MainHand == null || Player.MainHand.BaseDefinition.WeaponClass == WeaponClass.Bow;
                }
                return weapon.BaseDefinition.HandRequirement == HandSlotRequirement.OneHanded;
            }
            if (item is ArmourItemInstance shield && shield.BaseDefinition.ArmourSlot == ArmourSlot.Shield)
            {
                return true;
            }
            return false;
        }
        return item.TargetSlot == slot;
    }

    // ─────────────────────────────────────────────
    //  Smart Tooltip — Sticky Drawer (Task 2.2, 140px)
    // ─────────────────────────────────────────────

    private void RefreshTooltip()
    {
        if (_tooltipTitleLabel == null) return;

        var equippedItem = Player?.Gear.GetItem(SelectedSlot);
        var activeItem = InspectedItem ?? equippedItem;

        string? renderedId = activeItem?.InstanceId ?? $"empty:{SelectedSlot}";
        bool itemChanged = renderedId != _lastRenderedItemId;
        _lastRenderedItemId = renderedId;
        if (itemChanged)
        {
            ResetAffixScroll();
            PulseDrawer();
        }

        if (activeItem == null)
        {
            RenderEmptyDrawer();
            UpdateActionButtons(null, isEquipped: false);
            return;
        }

        // 1. Header: Title + Verdict + compact single-line meta
        _tooltipTitleLabel.Text = activeItem.IsLocked ? $"🔒 {activeItem.Name}" : activeItem.Name;
        _tooltipTitleLabel.Modulate = GetRarityColor(activeItem.Rarity);

        if (_tooltipSubtitleLabel != null)
        {
            _tooltipSubtitleLabel.Text = activeItem is WeaponItemInstance weaponSub
                ? $"{weaponSub.WeaponClassDisplayName} • {weaponSub.HandRequirementDisplayName} • {weaponSub.Rarity} • ilvl {weaponSub.ItemLevel} • Cấp {weaponSub.ReqLevel}"
                : $"{activeItem.BaseName} • {activeItem.Rarity} • ilvl {activeItem.ItemLevel} • Cấp {activeItem.ReqLevel}";
        }

        // 2. Right column: base stats, implicit, affixes (smooth scroll / T1-T2 summary)
        RenderBaseStats(activeItem);
        RenderImplicit(activeItem);
        RenderAffixList(activeItem);

        // 3. Left column: the 2 decisive rows (▲/▼ EHP & ▲/▼ DPS)
        bool isEquipped = (equippedItem != null && equippedItem.InstanceId == activeItem.InstanceId);
        if (Player != null && !isEquipped)
        {
            var delta = EquipmentComparisonService.Compare(Player, SelectedSlot, activeItem);
            LastComparison = delta;
            RenderDeltaPreview(delta);
        }
        else
        {
            LastComparison = null;
            RenderEquippedBaseline();
        }

        // 4. Action Buttons
        UpdateActionButtons(activeItem, isEquipped);
    }

    private void RenderEmptyDrawer()
    {
        LastComparison = null;

        if (_tooltipTitleLabel != null)
        {
            _tooltipTitleLabel.Text = $"Ô {SelectedSlot} Trống";
            _tooltipTitleLabel.Modulate = new Color(0.6f, 0.6f, 0.65f);
        }

        if (_tooltipSubtitleLabel != null)
            _tooltipSubtitleLabel.Text = "Chưa trang bị. Chọn đồ dự phòng bên dưới để so sánh và mặc.";

        if (_tooltipBaseStatsLabel != null)
            _tooltipBaseStatsLabel.Visible = false;

        if (_tooltipImplicitLabel != null)
            _tooltipImplicitLabel.Visible = false;

        ClearAffixList();
        if (_affixHeaderLabel != null)
            _affixHeaderLabel.Text = "KHÔNG CÓ AFFIX";

        // Drawer stays pinned (sticky) — show the current baseline so the 140px slot never collapses
        float ehp = Player != null ? EquipmentComparisonService.CalculateEffectiveHealthPool(Player) : 0f;
        float dps = Player != null ? EquipmentComparisonService.CalculateTotalDps(Player) : 0f;
        var (ehpLine, dpsLine) = SmartDeltaFormatter.FormatEquippedBaseline(ehp, dps);

        ApplyVerdict(DeltaVerdict.EmptySlot);
        ApplyDeltaLine(_deltaEhpLabel, _deltaEhpDetailLabel, ehpLine with { Detail = "Ô trống • chưa có mốc" });
        ApplyDeltaLine(_deltaDpsLabel, _deltaDpsDetailLabel, dpsLine);

        if (_deltaSummaryLabel != null)
        {
            _deltaSummaryLabel.Text = "Res: —";
            _deltaSummaryLabel.Modulate = DeltaNeutralColor;
        }
    }

    private void RenderBaseStats(IGearItem item)
    {
        if (_tooltipBaseStatsLabel == null) return;

        if (item is WeaponItemInstance weapon)
        {
            _tooltipBaseStatsLabel.Text =
                $"Vật lý {weapon.ComputedMinDamage:F0}-{weapon.ComputedMaxDamage:F0} • {weapon.ComputedAps:F2} APS • " +
                $"Chí mạng {weapon.ComputedCritChance:F2}% • {weapon.TotalDps:F1} pDPS";
            _tooltipBaseStatsLabel.Visible = true;
        }
        else if (item is ArmourItemInstance armour)
        {
            var parts = new List<string>(4);
            if (armour.LocalArmour > 0f) parts.Add($"Giáp {armour.LocalArmour:F0}");
            if (armour.LocalEvasion > 0f) parts.Add($"Né {armour.LocalEvasion:F0}");
            if (armour.LocalEnergyShield > 0f) parts.Add($"ES {armour.LocalEnergyShield:F0}");
            if (armour.LocalBlockChance > 0f) parts.Add($"Block {armour.LocalBlockChance:F0}%");
            _tooltipBaseStatsLabel.Text = string.Join(" • ", parts) + (armour.Quality > 0 ? $" (Q +{armour.Quality}%)" : "");
            _tooltipBaseStatsLabel.Visible = parts.Count > 0;
        }
        else
        {
            _tooltipBaseStatsLabel.Visible = false;
        }
    }

    private void RenderImplicit(IGearItem item)
    {
        if (_tooltipImplicitLabel == null) return;

        if (!string.IsNullOrWhiteSpace(item.ImplicitDescription))
        {
            _tooltipImplicitLabel.Text = $"✦ (Implicit) {item.ImplicitDescription}";
            _tooltipImplicitLabel.Visible = true;
        }
        else
        {
            _tooltipImplicitLabel.Visible = false;
        }
    }

    private void RenderAffixList(IGearItem item)
    {
        ClearAffixList();

        var summary = SmartDeltaFormatter.SummarizeAffixes(item.Affixes, ShowHighTierAffixesOnly);

        if (_affixHeaderLabel != null)
            _affixHeaderLabel.Text = SmartDeltaFormatter.FormatAffixHeader(summary);

        if (_affixFilterToggle != null)
            _affixFilterToggle.Disabled = summary.HighTierCount == 0 && !ShowHighTierAffixesOnly;

        if (_tooltipAffixListVBox == null) return;

        if (summary.Lines.Count == 0 && ShowHighTierAffixesOnly && summary.PrefixCount + summary.SuffixCount > 0)
        {
            var none = new Label { Text = "Không có dòng T1/T2 trên món đồ này." };
            none.AddThemeFontSizeOverride("font_size", 8);
            none.AddThemeColorOverride("font_color", new Color(0.55f, 0.57f, 0.62f));
            _tooltipAffixListVBox.AddChild(none);
            return;
        }

        foreach (var line in summary.Lines)
        {
            var affixLabel = new Label
            {
                Text = SmartDeltaFormatter.FormatAffixRow(line),
                AutowrapMode = TextServer.AutowrapMode.WordSmart
            };
            affixLabel.AddThemeFontSizeOverride("font_size", 9);
            affixLabel.AddThemeColorOverride("font_color",
                line.IsHighTier ? HighTierAffixColor
                : line.Type == AffixType.Prefix ? PrefixAffixColor
                : SuffixAffixColor);
            _tooltipAffixListVBox.AddChild(affixLabel);
        }
    }

    private void RenderDeltaPreview(EquipmentComparisonResult delta)
    {
        ApplyVerdict(SmartDeltaFormatter.ClassifyVerdict(delta));
        ApplyDeltaLine(_deltaEhpLabel, _deltaEhpDetailLabel, SmartDeltaFormatter.FormatEhpDelta(delta));
        ApplyDeltaLine(_deltaDpsLabel, _deltaDpsDetailLabel, SmartDeltaFormatter.FormatDpsDelta(delta));

        if (_deltaSummaryLabel != null)
        {
            _deltaSummaryLabel.Text = SmartDeltaFormatter.FormatResistanceSummary(delta);
            _deltaSummaryLabel.TooltipText = delta.HighlightNotes.Count > 0
                ? string.Join("\n", delta.HighlightNotes)
                : string.Empty;

            bool anyResLoss = delta.DeltaFireRes < 0f || delta.DeltaColdRes < 0f ||
                              delta.DeltaLightningRes < 0f || delta.DeltaChaosRes < 0f;
            _deltaSummaryLabel.Modulate = anyResLoss ? DeltaTradeOffColor : DeltaNeutralColor;
        }
    }

    private void RenderEquippedBaseline()
    {
        float ehp = Player != null ? EquipmentComparisonService.CalculateEffectiveHealthPool(Player) : 0f;
        float dps = Player != null ? EquipmentComparisonService.CalculateTotalDps(Player) : 0f;
        var (ehpLine, dpsLine) = SmartDeltaFormatter.FormatEquippedBaseline(ehp, dps);

        ApplyVerdict(DeltaVerdict.Equipped);
        ApplyDeltaLine(_deltaEhpLabel, _deltaEhpDetailLabel, ehpLine);
        ApplyDeltaLine(_deltaDpsLabel, _deltaDpsDetailLabel, dpsLine);

        if (_deltaSummaryLabel != null)
        {
            _deltaSummaryLabel.Text = "Res: đang mặc";
            _deltaSummaryLabel.TooltipText = string.Empty;
            _deltaSummaryLabel.Modulate = DeltaNeutralColor;
        }
    }

    private void ApplyVerdict(DeltaVerdict verdict)
    {
        CurrentVerdict = verdict;
        Color color = GetVerdictColor(verdict);

        if (_deltaVerdictLabel != null)
        {
            _deltaVerdictLabel.Text = SmartDeltaFormatter.VerdictText(verdict);
            _deltaVerdictLabel.AddThemeColorOverride("font_color", color);
        }

        if (_deltaCardStyle != null)
        {
            _deltaCardStyle.BorderColor = new Color(color.R, color.G, color.B, 0.85f);
            _deltaCardStyle.BgColor = new Color(color.R * 0.12f, color.G * 0.12f, color.B * 0.12f, 0.95f);
        }
    }

    private static void ApplyDeltaLine(Label? headline, Label? detail, DeltaLine line)
    {
        if (headline != null)
        {
            headline.Text = line.Headline;
            headline.AddThemeColorOverride("font_color", GetDirectionColor(line.Direction));
        }

        if (detail != null)
        {
            detail.Text = line.Detail;
        }
    }

    private static Color GetDirectionColor(DeltaDirection direction) => direction switch
    {
        DeltaDirection.Up => DeltaUpColor,
        DeltaDirection.Down => DeltaDownColor,
        _ => DeltaNeutralColor
    };

    private static Color GetVerdictColor(DeltaVerdict verdict) => verdict switch
    {
        DeltaVerdict.Upgrade => DeltaUpColor,
        DeltaVerdict.Downgrade => DeltaDownColor,
        DeltaVerdict.TradeOff => DeltaTradeOffColor,
        DeltaVerdict.Equipped => new Color(0.88f, 0.72f, 0.25f),
        _ => new Color(0.55f, 0.57f, 0.62f)
    };

    private void ClearAffixList()
    {
        if (_tooltipAffixListVBox == null) return;
        foreach (Node child in _tooltipAffixListVBox.GetChildren())
        {
            // Detach immediately so child counts are accurate within the same frame
            _tooltipAffixListVBox.RemoveChild(child);
            child.QueueFree();
        }
    }

    // ─────────────────────────────────────────────
    //  Sticky Drawer Micro-Interactions
    // ─────────────────────────────────────────────

    /// <summary>
    /// Smooth wheel scrolling for the affix list: intercepts wheel events and tweens scroll_vertical
    /// instead of jumping, keeping the 140px drawer readable during quick 1.5s comparisons.
    /// </summary>
    private void OnAffixScrollGuiInput(InputEvent @event)
    {
        if (_affixScroll == null) return;
        if (@event is not InputEventMouseButton { Pressed: true } mb) return;
        if (mb.ButtonIndex != MouseButton.WheelUp && mb.ButtonIndex != MouseButton.WheelDown) return;

        float direction = mb.ButtonIndex == MouseButton.WheelDown ? 1f : -1f;
        float factor = mb.Factor > 0f ? mb.Factor : 1f;
        SmoothScrollAffixesBy(direction * AffixScrollStep * factor);
        _affixScroll.AcceptEvent();
    }

    /// <summary>
    /// Tweens the affix list scroll offset by <paramref name="deltaPixels"/>, clamped to the scrollable range.
    /// </summary>
    public void SmoothScrollAffixesBy(float deltaPixels)
    {
        if (_affixScroll == null) return;

        var vbar = _affixScroll.GetVScrollBar();
        float maxScroll = vbar != null ? MathF.Max(0f, (float)(vbar.MaxValue - vbar.Page)) : 0f;

        // Chain from the in-flight target so rapid wheel ticks accumulate smoothly
        bool tweenRunning = _affixScrollTween != null && _affixScrollTween.IsValid() && _affixScrollTween.IsRunning();
        float origin = tweenRunning ? _affixScrollTarget : _affixScroll.ScrollVertical;
        _affixScrollTarget = Math.Clamp(origin + deltaPixels, 0f, maxScroll);

        _affixScrollTween?.Kill();
        if (!IsInsideTree())
        {
            _affixScroll.ScrollVertical = (int)_affixScrollTarget;
            return;
        }

        _affixScrollTween = CreateTween();
        _affixScrollTween.SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        _affixScrollTween.TweenProperty(_affixScroll, "scroll_vertical", (int)_affixScrollTarget, AffixScrollDuration);
    }

    private void ResetAffixScroll()
    {
        _affixScrollTween?.Kill();
        _affixScrollTween = null;
        _affixScrollTarget = 0f;
        if (_affixScroll != null)
            _affixScroll.ScrollVertical = 0;
    }

    /// <summary>
    /// Brief brightness pulse on the drawer when the inspected item changes (dopamine cue, 0.18s).
    /// </summary>
    private void PulseDrawer()
    {
        if (_smartDeltaDrawer == null || !IsInsideTree()) return;

        _drawerPulseTween?.Kill();
        _smartDeltaDrawer.Modulate = new Color(1.25f, 1.22f, 1.12f, 1f);
        _drawerPulseTween = CreateTween();
        _drawerPulseTween.SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
        _drawerPulseTween.TweenProperty(_smartDeltaDrawer, "modulate", Colors.White, DrawerPulseDuration);
    }


    private void UpdateActionButtons(IGearItem? item, bool isEquipped)
    {
        if (_equipButton != null)
        {
            _equipButton.Disabled = (item == null || isEquipped);
            _equipButton.Text = isEquipped ? "Đã Mặc" : "Mặc Ngay";
        }

        if (_lockButton != null)
        {
            _lockButton.Disabled = (item == null);
            if (item != null)
            {
                _lockButton.Text = item.IsLocked ? "🔓 Mở Khóa" : "🔒 Khóa Đồ";
                _lockButton.Modulate = item.IsLocked ? new Color(0.95f, 0.75f, 0.35f) : Colors.White;
            }
            else
            {
                _lockButton.Text = "Khóa Đồ";
                _lockButton.Modulate = Colors.White;
            }
        }

        if (_stashButton != null)
        {
            _stashButton.Disabled = (item == null || item.IsLocked);
            _stashButton.Text = isEquipped ? "Tháo Đồ" : "Cất Rương";
        }
    }

    // ─────────────────────────────────────────────
    //  Action Handlers
    // ─────────────────────────────────────────────

    private void OnEquipButtonPressed()
    {
        if (Player == null || InspectedItem == null) return;

        bool success = Player.Gear.Equip(SelectedSlot, InspectedItem);
        if (success)
        {
            // Remove from candidate drawer if it was in there
            CandidateItems.RemoveAll(i => i.InstanceId == InspectedItem.InstanceId);
            OnItemEquipped?.Invoke(SelectedSlot, InspectedItem);
            RefreshView();
        }
    }

    private void OnLockButtonPressed()
    {
        if (InspectedItem == null) return;

        InspectedItem.IsLocked = !InspectedItem.IsLocked;
        OnItemLockToggled?.Invoke(InspectedItem);
        RefreshView();
    }

    private void OnStashButtonPressed()
    {
        if (Player == null || InspectedItem == null || InspectedItem.IsLocked) return;

        var equipped = Player.Gear.GetItem(SelectedSlot);
        if (equipped != null && equipped.InstanceId == InspectedItem.InstanceId)
        {
            // Unequipping active item
            var unequipped = Player.Gear.Unequip(SelectedSlot);
            if (unequipped != null)
            {
                CandidateItems.Add(unequipped);
                OnItemStashed?.Invoke(SelectedSlot, unequipped);
                InspectedItem = null;
                RefreshView();
            }
        }
        else
        {
            // Moving candidate item to stash
            CandidateItems.RemoveAll(i => i.InstanceId == InspectedItem.InstanceId);
            OnItemStashed?.Invoke(SelectedSlot, InspectedItem);
            InspectedItem = Player.Gear.GetItem(SelectedSlot);
            RefreshView();
        }
    }

    // ─────────────────────────────────────────────
    //  Default Candidates Generation (for Demo & FTUE)
    // ─────────────────────────────────────────────

    private void PopulateDefaultCandidatesIfEmpty()
    {
        if (CandidateItems.Count > 0) return;

        // Sample upgrade items matching GDD specifications for testing & immediate dopamine preview:
        // 1. Astral Plate of the Colossus (Rare Body Armour)
        var astralDef = new ArmourBaseDefinition("astral_plate", "Astral Plate", ArmourSlot.BodyArmour, 780, 0, 0, 0, 68, 180, 0, 0, "all_elemental_resistances", "+12% to all Elemental Resistances", 12, "Masterwork celestial armour");
        var astralItem = new ArmourItemInstance(astralDef, "Rare", quality: 20, itemLevel: 84)
        {
            CustomName = "Astral Plate of the Colossus"
        };
        astralItem.Affixes.Add(new AffixDefinition("flat_maximum_life_t2", "+{0} to Maximum Life", AffixType.Prefix, 2, 74, 110, 119, new[] { "life", "defenses" }));
        astralItem.Affixes.Add(new AffixDefinition("local_increased_armour_t2", "{0}% Increased Armour", AffixType.Prefix, 2, 72, 85, 92, new[] { "armour", "local" }));
        astralItem.Affixes.Add(new AffixDefinition("fire_resistance_t1", "+{0}% to Fire Resistance", AffixType.Suffix, 1, 84, 45, 48, new[] { "fire", "resistance" }));
        astralItem.Affixes.Add(new AffixDefinition("cold_resistance_t2", "+{0}% to Cold Resistance", AffixType.Suffix, 2, 72, 40, 44, new[] { "cold", "resistance" }));
        astralItem.RecalculateStats();
        CandidateItems.Add(astralItem);

        // 2. Jewelled Foil of Celebration (Rare MainHand Sword)
        var swordDef = new WeaponBaseDefinition("jewelled_foil", "Jewelled Foil", WeaponClass.OneHandedSword, HandSlotRequirement.OneHanded, 32, 60, 1.60f, 5.5f, ReqLevel: 68, ReqDex: 212, ImplicitAffixId: "foil_implicit_crit_multi", ImplicitValue: 25, Description: "Thrusting One-Handed Sword");
        var foilItem = new WeaponItemInstance(swordDef, "Rare", quality: 20, itemLevel: 84)
        {
            CustomName = "Jewelled Foil of Celebration"
        };
        foilItem.Affixes.Add(new AffixDefinition("percent_increased_physical_damage_t1", "{0}% Increased Physical Damage", AffixType.Prefix, 1, 83, 170, 179, new[] { "damage", "physical", "local" }));
        foilItem.Affixes.Add(new AffixDefinition("attack_speed_t1", "{0}% Increased Attack Speed", AffixType.Suffix, 1, 76, 26, 27, new[] { "speed", "attack", "local" }));
        foilItem.RecalculateLocalStats();
        CandidateItems.Add(foilItem);

        // 3. Crested Tower Shield (Rare OffHand Shield)
        var shieldDef = new ArmourBaseDefinition("crested_tower_shield", "Crested Tower Shield", ArmourSlot.Shield, 320, 0, 0, 24, 45, 110, 0, 0, "shield_implicit_flat_life", "+50 to Maximum Life", 50, "Heavy steel reinforced tower shield");
        var shieldItem = new ArmourItemInstance(shieldDef, "Rare", quality: 20, itemLevel: 75)
        {
            CustomName = "Crested Tower Shield of Sanctuary"
        };
        shieldItem.Affixes.Add(new AffixDefinition("flat_maximum_life_t2", "+{0} to Maximum Life", AffixType.Prefix, 2, 74, 110, 119, new[] { "life", "defenses" }));
        shieldItem.Affixes.Add(new AffixDefinition("fire_resistance_t1", "+{0}% to Fire Resistance", AffixType.Suffix, 1, 84, 45, 48, new[] { "fire", "resistance" }));
        shieldItem.RecalculateStats();
        CandidateItems.Add(shieldItem);

        // 4. Boarding Axe (1H OffHand Dual-Wield Candidate)
        var axeDef = new WeaponBaseDefinition("boarding_axe", "Boarding Axe", WeaponClass.OneHandedAxe, HandSlotRequirement.OneHanded, 15, 30, 1.35f, 5.0f, ReqLevel: 25, ReqStr: 40, Description: "A sturdy hand axe for brutal chopping");
        var axeItem = new WeaponItemInstance(axeDef, "Magic", quality: 10, itemLevel: 30)
        {
            CustomName = "Flaring Boarding Axe of Speed"
        };
        axeItem.Affixes.Add(new AffixDefinition("percent_increased_physical_damage_t4", "{0}% Increased Physical Damage", AffixType.Prefix, 4, 30, 60, 74, new[] { "damage", "physical", "local" }));
        axeItem.Affixes.Add(new AffixDefinition("attack_speed_t3", "{0}% Increased Attack Speed", AffixType.Suffix, 3, 30, 14, 16, new[] { "speed", "attack", "local" }));
        axeItem.RecalculateLocalStats();
        CandidateItems.Add(axeItem);

        // 3. Two-Stone Ring of the Elements (Magic Ring)
        var ringDef = new AccessoryBaseDefinition("two_stone_ring", "Two-Stone Ring", AccessorySlot.Ring, "+16% to Fire and Cold Resistances", "fire_cold_res", 16, 1, "Twin gemstones");
        var ringItem = new AccessoryItemInstance(ringDef, "Magic", itemLevel: 45)
        {
            CustomName = "Virile Two-Stone Ring of the Rainbow"
        };
        ringItem.Affixes.Add(new AffixDefinition("flat_maximum_life_t4", "+{0} to Maximum Life", AffixType.Prefix, 4, 44, 60, 69, new[] { "life" }));
        ringItem.Affixes.Add(new AffixDefinition("lightning_resistance_t2", "+{0}% to Lightning Resistance", AffixType.Suffix, 2, 72, 40, 44, new[] { "lightning", "resistance" }));
        ringItem.RecalculateStats();
        CandidateItems.Add(ringItem);

        // 4. Leather Belt of the Titan (Rare Belt)
        var beltDef = new AccessoryBaseDefinition("leather_belt", "Leather Belt", AccessorySlot.Belt, "+40 to Maximum Life", "flat_life", 40, 8, "Tough hide girdle");
        var beltItem = new AccessoryItemInstance(beltDef, "Rare", itemLevel: 75)
        {
            CustomName = "Leather Belt of the Titan"
        };
        beltItem.Affixes.Add(new AffixDefinition("flat_maximum_life_t2", "+{0} to Maximum Life", AffixType.Prefix, 2, 74, 110, 119, new[] { "life" }));
        beltItem.Affixes.Add(new AffixDefinition("strength_t2", "+{0} to Strength", AffixType.Suffix, 2, 74, 45, 50, new[] { "strength" }));
        beltItem.Affixes.Add(new AffixDefinition("fire_resistance_t2", "+{0}% to Fire Resistance", AffixType.Suffix, 2, 72, 40, 44, new[] { "fire", "resistance" }));
        beltItem.RecalculateStats();
        CandidateItems.Add(beltItem);
    }

    private static Color GetRarityColor(string rarity) => rarity switch
    {
        "Magic" => new Color(0.29f, 0.53f, 0.81f),    // Blue #4A88CF
        "Rare" => new Color(0.95f, 0.75f, 0.27f),     // Gold #F3C044
        "Unique" => new Color(0.69f, 0.38f, 0.15f),   // Orange #AF6025
        _ => new Color(0.78f, 0.80f, 0.85f)          // Normal Light Gray
    };
}
