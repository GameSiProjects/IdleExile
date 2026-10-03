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

    // Smart Tooltip
    private Label? _tooltipTitleLabel;
    private Label? _tooltipSubtitleLabel;
    private Label? _tooltipBaseStatsLabel;
    private Label? _tooltipImplicitLabel;
    private VBoxContainer? _tooltipAffixListVBox;
    private PanelContainer? _deltaPreviewCard;
    private Label? _deltaHeaderLabel;
    private HBoxContainer? _deltaBadgesHBox;
    private Label? _deltaSummaryLabel;

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

        // Tooltip
        _tooltipTitleLabel = GetNodeOrNull<Label>("%TooltipTitleLabel");
        _tooltipSubtitleLabel = GetNodeOrNull<Label>("%TooltipSubtitleLabel");
        _tooltipBaseStatsLabel = GetNodeOrNull<Label>("%TooltipBaseStatsLabel");
        _tooltipImplicitLabel = GetNodeOrNull<Label>("%TooltipImplicitLabel");
        _tooltipAffixListVBox = GetNodeOrNull<VBoxContainer>("%TooltipAffixListVBox");
        _deltaPreviewCard = GetNodeOrNull<PanelContainer>("%DeltaPreviewCard");
        _deltaHeaderLabel = GetNodeOrNull<Label>("%DeltaHeaderLabel");
        _deltaBadgesHBox = GetNodeOrNull<HBoxContainer>("%DeltaBadgesHBox");
        _deltaSummaryLabel = GetNodeOrNull<Label>("%DeltaSummaryLabel");

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
    //  Smart Tooltip & Delta Preview
    // ─────────────────────────────────────────────

    private void RefreshTooltip()
    {
        if (_tooltipTitleLabel == null) return;

        var equippedItem = Player?.Gear.GetItem(SelectedSlot);
        var activeItem = InspectedItem ?? equippedItem;

        if (activeItem == null)
        {
            // Empty slot state
            _tooltipTitleLabel.Text = $"Ô {SelectedSlot} Trống";
            _tooltipTitleLabel.Modulate = new Color(0.6f, 0.6f, 0.65f);

            if (_tooltipSubtitleLabel != null)
                _tooltipSubtitleLabel.Text = "Chưa trang bị vật phẩm. Nhấp vào đồ bên dưới để so sánh và mặc.";

            if (_tooltipBaseStatsLabel != null)
                _tooltipBaseStatsLabel.Text = "";

            if (_tooltipImplicitLabel != null)
                _tooltipImplicitLabel.Visible = false;

            ClearAffixList();

            if (_deltaPreviewCard != null)
                _deltaPreviewCard.Visible = false;

            UpdateActionButtons(null, isEquipped: false);
            return;
        }

        // 1. Title & Meta
        _tooltipTitleLabel.Text = activeItem.IsLocked ? $"🔒 {activeItem.Name}" : activeItem.Name;
        _tooltipTitleLabel.Modulate = GetRarityColor(activeItem.Rarity);

        if (_tooltipSubtitleLabel != null)
        {
            if (activeItem is WeaponItemInstance weaponSub)
            {
                _tooltipSubtitleLabel.Text = $"{weaponSub.WeaponClassDisplayName} • {weaponSub.HandRequirementDisplayName}\n{weaponSub.Rarity} • ilvl: {weaponSub.ItemLevel} • Cấp {weaponSub.ReqLevel}";
            }
            else
            {
                _tooltipSubtitleLabel.Text = $"{activeItem.BaseName} • {activeItem.Rarity} • ilvl: {activeItem.ItemLevel} • Cấp {activeItem.ReqLevel}";
            }
        }

        // 2. Base Stats (Armour / Weapon / Jewellery)
        if (_tooltipBaseStatsLabel != null)
        {
            if (activeItem is WeaponItemInstance weapon)
            {
                _tooltipBaseStatsLabel.Text =
                    $"Sát thương vật lý: {weapon.ComputedMinDamage:F0}-{weapon.ComputedMaxDamage:F0}\n" +
                    $"Tỷ lệ chí mạng: {weapon.ComputedCritChance:F2}%\n" +
                    $"Tốc độ tấn công: {weapon.ComputedAps:F2} APS\n" +
                    $"DPS cơ bản: {weapon.TotalDps:F1} pDPS";
                _tooltipBaseStatsLabel.Visible = true;
            }
            else if (activeItem is ArmourItemInstance armour)
            {
                var parts = new List<string>();
                if (armour.LocalArmour > 0f) parts.Add($"Giáp: {armour.LocalArmour:F0}");
                if (armour.LocalEvasion > 0f) parts.Add($"Né tránh: {armour.LocalEvasion:F0}");
                if (armour.LocalEnergyShield > 0f) parts.Add($"Khiên ES: {armour.LocalEnergyShield:F0}");
                if (armour.LocalBlockChance > 0f) parts.Add($"Đỡ đòn: {armour.LocalBlockChance:F0}%");
                _tooltipBaseStatsLabel.Text = string.Join(" | ", parts) + (armour.Quality > 0 ? $" (Quality +{armour.Quality}%)" : "");
                _tooltipBaseStatsLabel.Visible = parts.Count > 0;
            }
            else
            {
                _tooltipBaseStatsLabel.Visible = false;
            }
        }

        // 3. Implicit
        if (_tooltipImplicitLabel != null)
        {
            if (!string.IsNullOrWhiteSpace(activeItem.ImplicitDescription))
            {
                _tooltipImplicitLabel.Text = $"✦ (Implicit) {activeItem.ImplicitDescription}";
                _tooltipImplicitLabel.Visible = true;
                _tooltipImplicitLabel.Modulate = new Color(0.45f, 0.75f, 1.0f);
            }
            else
            {
                _tooltipImplicitLabel.Visible = false;
            }
        }

        // 4. Explicit Affixes
        ClearAffixList();
        if (_tooltipAffixListVBox != null && activeItem.Affixes.Count > 0)
        {
            foreach (var affix in activeItem.Affixes)
            {
                float avg = (affix.MinValue + affix.MaxValue) / 2f;
                string typePrefix = affix.Type == AffixType.Prefix ? "(P)" : "(S)";
                string text = string.Format(affix.NameTemplate, avg);

                var affixLabel = new Label
                {
                    Text = $"• {typePrefix} {text} (Tier {affix.Tier})",
                    AutowrapMode = TextServer.AutowrapMode.WordSmart
                };
                affixLabel.AddThemeFontSizeOverride("font_size", 10);
                affixLabel.AddThemeColorOverride("font_color", affix.Type == AffixType.Prefix ? new Color(0.55f, 0.75f, 0.95f) : new Color(0.9f, 0.75f, 0.45f));
                _tooltipAffixListVBox.AddChild(affixLabel);
            }
        }

        // 5. Delta Preview Card (Smart Tooltip)
        bool isEquipped = (equippedItem != null && equippedItem.InstanceId == activeItem.InstanceId);
        if (_deltaPreviewCard != null)
        {
            if (Player != null && !isEquipped)
            {
                var delta = EquipmentComparisonService.Compare(Player, SelectedSlot, activeItem);
                RenderDeltaPreview(delta);
                _deltaPreviewCard.Visible = true;
            }
            else
            {
                // Active item is already equipped
                if (_deltaHeaderLabel != null)
                    _deltaHeaderLabel.Text = "TRANG BỊ ĐANG MẶC TRÊN NGƯỜI (EQUIPPED)";
                if (_deltaSummaryLabel != null)
                    _deltaSummaryLabel.Text = "Chọn đồ khác bên dưới để so sánh chênh lệch Delta EHP / DPS.";
                ClearDeltaBadges();
                _deltaPreviewCard.Visible = true;
            }
        }

        // 6. Action Buttons
        UpdateActionButtons(activeItem, isEquipped);
    }

    private void RenderDeltaPreview(EquipmentComparisonResult delta)
    {
        if (_deltaHeaderLabel != null)
        {
            _deltaHeaderLabel.Text = delta.IsUpgrade
                ? "DELTA PREVIEW (SO SÁNH: CẢI THIỆN NÂNG CẤP ▲)"
                : "DELTA PREVIEW (SO SÁNH VỚI ĐỒ ĐANG MẶC)";
            _deltaHeaderLabel.Modulate = delta.IsUpgrade ? new Color(0.35f, 0.85f, 0.45f) : Colors.White;
        }

        ClearDeltaBadges();

        if (_deltaBadgesHBox != null)
        {
            // Delta EHP badge
            string ehpSign = delta.DeltaEhp >= 0 ? "+" : "";
            Color ehpColor = delta.DeltaEhp >= 0 ? new Color(0.35f, 0.85f, 0.45f) : new Color(0.95f, 0.4f, 0.35f);
            AddDeltaBadge($"{ehpSign}{delta.DeltaEhp:F0} EHP", ehpColor);

            // Delta DPS badge
            string dpsSign = delta.DeltaDpsPercent >= 0 ? "+" : "";
            Color dpsColor = delta.DeltaDpsPercent >= 0 ? new Color(0.88f, 0.72f, 0.25f) : new Color(0.85f, 0.35f, 0.35f);
            AddDeltaBadge($"{dpsSign}{delta.DeltaDpsPercent:F1}% DPS", dpsColor);

            // Delta Fire / Chaos Res badges if significant
            if (MathF.Abs(delta.DeltaFireRes) >= 1f)
            {
                string sign = delta.DeltaFireRes >= 0 ? "+" : "";
                Color col = delta.DeltaFireRes >= 0 ? new Color(0.35f, 0.85f, 0.45f) : new Color(0.95f, 0.4f, 0.35f);
                AddDeltaBadge($"{sign}{delta.DeltaFireRes:F0}% Fire", col);
            }

            if (delta.DeltaChaosRes < 0f)
            {
                AddDeltaBadge($"{delta.DeltaChaosRes:F0}% Chaos", new Color(0.95f, 0.4f, 0.35f));
            }
        }

        if (_deltaSummaryLabel != null)
        {
            _deltaSummaryLabel.Text = delta.HighlightNotes.Count > 0
                ? string.Join(" • ", delta.HighlightNotes)
                : (delta.IsUpgrade ? "Nâng cấp chỉ số toàn diện" : "Chỉ số cân bằng");
        }
    }

    private void AddDeltaBadge(string text, Color color)
    {
        if (_deltaBadgesHBox == null) return;
        var panel = new PanelContainer();
        var style = new StyleBoxFlat
        {
            BgColor = new Color(color.R, color.G, color.B, 0.18f),
            BorderColor = color,
            BorderWidthBottom = 1,
            BorderWidthTop = 1,
            BorderWidthLeft = 1,
            BorderWidthRight = 1,
            CornerRadiusBottomLeft = 3,
            CornerRadiusBottomRight = 3,
            CornerRadiusTopLeft = 3,
            CornerRadiusTopRight = 3
        };
        panel.AddThemeStyleboxOverride("panel", style);

        var lbl = new Label
        {
            Text = text,
            Modulate = color
        };
        lbl.AddThemeFontSizeOverride("font_size", 10);
        panel.AddChild(lbl);
        _deltaBadgesHBox.AddChild(panel);
    }

    private void ClearDeltaBadges()
    {
        if (_deltaBadgesHBox == null) return;
        foreach (Node child in _deltaBadgesHBox.GetChildren())
        {
            child.QueueFree();
        }
    }

    private void ClearAffixList()
    {
        if (_tooltipAffixListVBox == null) return;
        foreach (Node child in _tooltipAffixListVBox.GetChildren())
        {
            child.QueueFree();
        }
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
