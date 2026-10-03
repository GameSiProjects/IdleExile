using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using IdleExile.Core.Models;
using IdleExile.Core.Services;
using IdleExile.Core.State;
using IdleExile.UI;

namespace IdleExile.UI.Components;

/// <summary>
/// PoE 2 Decoupled Gem View Controller (Task 3.1 & Phase 3 UI).
/// Equipment has NO sockets; Active Gem contains up to 5 Auto-Linked Support sockets.
/// Features:
/// - Skill slot switcher (Primary, Secondary, Trigger, Aura I, Aura II).
/// - Central Active Gem Socket with elemental themes (Fire, Cold, Lightning, Physical, Chaos).
/// - Orbital arc of 5 Auto-Linked Support Gem sockets with glowing energy link conduits.
/// - Live metrics bar: compounded Mana cost, Action Speed, Damage Multiplier, and Projectiles.
/// - Smart Inspector Drawer: Tag badges, stat modifiers, and Tag Matching compatibility analysis.
/// - Candidate Drawer: Filterable inventory/stash gems with 1-click inspect and double-click socketing.
/// </summary>
public partial class GemView : Control
{
    // ─────────────────────────────────────────────
    //  Signals & Events
    // ─────────────────────────────────────────────

    public event Action<int, SkillSocketSetup>? OnSkillSlotChanged;
    public event Action<SkillSocketSetup>? OnGemsModified;
    public event Action<GemDefinition?>? OnGemInspected;

    // ─────────────────────────────────────────────
    //  State
    // ─────────────────────────────────────────────

    public SkillLoadout? Loadout { get; private set; }
    public DatabaseLoader? Database { get; private set; }
    public PlayerState? Player { get; private set; }

    public int SelectedSkillSlotIndex { get; private set; } = 0;
    public int SelectedSocketIndex { get; private set; } = -1; // -1: Active Gem, 0..4: Support Gems
    public GemDefinition? InspectedGem { get; private set; }
    public GemDefinition? SelectedCandidateGem { get; private set; }
    public string CurrentCandidateFilter { get; private set; } = "All";

    private GemDefinition? _hoveredGem;
    public GemDefinition? HoveredGem => _hoveredGem;

    public GemDefinition? EffectiveFocusedGem =>
        _hoveredGem ?? (SelectedCandidateGem ?? (SelectedSocketIndex == -1 ? CurrentSetup.ActiveGem : (SelectedSocketIndex >= 0 && SelectedSocketIndex < CurrentSetup.SocketedSupports.Count ? CurrentSetup.SocketedSupports[SelectedSocketIndex] : null)));

    public bool IsActiveSocketHighlighted { get; private set; }
    public bool IsActiveSocketDimmed { get; private set; }
    private readonly bool[] _slotBtnHighlighted = new bool[5];
    public bool IsSlotButtonHighlighted(int slotIndex) => slotIndex >= 0 && slotIndex < 5 && _slotBtnHighlighted[slotIndex];

    public MultiplierPreviewResult? CurrentPreviewResult { get; private set; }
    private readonly Dictionary<Button, GemDefinition> _candidateCards = new();

    public SkillSocketSetup CurrentSetup => Loadout?.GetSetupAt(SelectedSkillSlotIndex) ?? new SkillSocketSetup();

    // ─────────────────────────────────────────────
    //  Constants & Color Palette
    // ─────────────────────────────────────────────

    private static readonly Color ColorFire = new(0.92f, 0.28f, 0.22f);
    private static readonly Color ColorCold = new(0.24f, 0.60f, 0.95f);
    private static readonly Color ColorLightning = new(0.95f, 0.78f, 0.24f);
    private static readonly Color ColorPhysical = new(0.26f, 0.85f, 0.44f);
    private static readonly Color ColorChaos = new(0.72f, 0.28f, 0.92f);
    private static readonly Color ColorGold = new(1.0f, 0.86f, 0.42f);
    private static readonly Color ColorEmptyBorder = new(0.22f, 0.24f, 0.30f, 0.8f);
    private static readonly Color ColorEmptyBg = new(0.07f, 0.075f, 0.10f, 0.95f);

    // ─────────────────────────────────────────────
    //  Node References
    // ─────────────────────────────────────────────

    // Slot Switcher Buttons
    private readonly Button[] _slotBtns = new Button[5];

    // Sockets & Hub
    private PanelContainer? _socketHubContainer;
    private SocketRingCanvas? _socketRingCanvas;
    private Button? _activeSocketBtn;
    private Label? _activeGemIconLabel;
    private Label? _activeGemNameLabel;
    private Label? _activeGemTypeLabel;
    private Label? _activeGemElementPill;

    private readonly Button[] _supportSocketBtns = new Button[5];
    private readonly Label[] _supportIndexLabels = new Label[5];
    private readonly Label[] _supportNameLabels = new Label[5];
    private readonly ColorRect[] _supportStatusPips = new ColorRect[5];

    // Live Metrics Bar
    private Label? _metricManaLabel;
    private Label? _metricSpeedLabel;
    private Label? _metricDamageMultLabel;
    private Label? _metricProjectilesLabel;
    private Label? _metricLinksLabel;

    // Inspector Drawer
    private PanelContainer? _inspectorDrawer;
    private Label? _inspectorGemName;
    private Label? _inspectorCategoryBadge;
    private Label? _inspectorAttributeBadge;
    private HBoxContainer? _inspectorTagsHBox;
    private Label? _inspectorModifiersLabel;
    private Label? _inspectorStatusLabel;

    // Candidate Drawer
    private Button? _filterAllBtn;
    private Button? _filterActiveBtn;
    private Button? _filterSupportBtn;
    private Button? _socketActionBtn;
    private Button? _unsocketActionBtn;
    private ScrollContainer? _candidateScroll;
    private HBoxContainer? _candidateHBox;

    public override void _Ready()
    {
        BindNodes();
        HookEvents();
    }

    public void Initialize(SkillLoadout loadout, DatabaseLoader db, PlayerState? player = null)
    {
        Loadout = loadout;
        Database = db;
        Player = player;

        // Hook reactive updates on all 5 skill setups
        for (int i = 0; i < 5; i++)
        {
            var setup = Loadout.GetSetupAt(i);
            setup.OnSetupChanged += () =>
            {
                if (SelectedSkillSlotIndex == i)
                {
                    RefreshView();
                }
            };
        }

        RefreshView();
    }

    private void BindNodes()
    {
        // 1. Skill Slot Switcher (Top Bar)
        _slotBtns[0] = GetNodeOrNull<Button>("%SlotPrimaryBtn")!;
        _slotBtns[1] = GetNodeOrNull<Button>("%SlotSecondaryBtn")!;
        _slotBtns[2] = GetNodeOrNull<Button>("%SlotTriggerBtn")!;
        _slotBtns[3] = GetNodeOrNull<Button>("%SlotAura1Btn")!;
        _slotBtns[4] = GetNodeOrNull<Button>("%SlotAura2Btn")!;

        var slotGroup = new ButtonGroup();
        for (int i = 0; i < _slotBtns.Length; i++)
        {
            if (_slotBtns[i] != null)
            {
                _slotBtns[i].ButtonGroup = slotGroup;
                int slotIdx = i;
                _slotBtns[i].Pressed += () => SelectSkillSlot(slotIdx);
            }
        }

        // 2. Sockets & Hub
        _socketHubContainer = GetNodeOrNull<PanelContainer>("%SocketHubContainer");
        _socketRingCanvas = GetNodeOrNull<SocketRingCanvas>("%SocketRingCanvas");
        if (_socketRingCanvas != null)
        {
            _socketRingCanvas.ParentView = this;
        }

        _activeSocketBtn = GetNodeOrNull<Button>("%ActiveGemSocket");
        _activeGemIconLabel = GetNodeOrNull<Label>("%ActiveGemIcon");
        _activeGemNameLabel = GetNodeOrNull<Label>("%ActiveGemName");
        _activeGemTypeLabel = GetNodeOrNull<Label>("%ActiveGemType");
        _activeGemElementPill = GetNodeOrNull<Label>("%ActiveGemElementPill");

        if (_activeSocketBtn != null)
        {
            _activeSocketBtn.Pressed += () => SelectSocket(-1);
            _activeSocketBtn.MouseEntered += () =>
            {
                if (CurrentSetup.ActiveGem != null)
                {
                    SetHoveredGem(CurrentSetup.ActiveGem);
                }
            };
            _activeSocketBtn.MouseExited += () =>
            {
                if (_hoveredGem == CurrentSetup.ActiveGem)
                {
                    SetHoveredGem(null);
                }
            };
        }

        for (int i = 0; i < 5; i++)
        {
            _supportSocketBtns[i] = GetNodeOrNull<Button>($"%SupportSocket{i}")!;
            _supportIndexLabels[i] = GetNodeOrNull<Label>($"%SupportIndex{i}")!;
            _supportNameLabels[i] = GetNodeOrNull<Label>($"%SupportName{i}")!;
            _supportStatusPips[i] = GetNodeOrNull<ColorRect>($"%SupportPip{i}")!;

            if (_supportSocketBtns[i] != null)
            {
                int socketIdx = i;
                _supportSocketBtns[i].Pressed += () => SelectSocket(socketIdx);
                _supportSocketBtns[i].MouseEntered += () =>
                {
                    if (socketIdx < CurrentSetup.SocketedSupports.Count)
                    {
                        SetHoveredGem(CurrentSetup.SocketedSupports[socketIdx]);
                    }
                };
                _supportSocketBtns[i].MouseExited += () =>
                {
                    if (socketIdx < CurrentSetup.SocketedSupports.Count && _hoveredGem == CurrentSetup.SocketedSupports[socketIdx])
                    {
                        SetHoveredGem(null);
                    }
                };
            }
        }

        // 3. Live Metrics Bar
        _metricManaLabel = GetNodeOrNull<Label>("%MetricManaLabel");
        _metricSpeedLabel = GetNodeOrNull<Label>("%MetricSpeedLabel");
        _metricDamageMultLabel = GetNodeOrNull<Label>("%MetricDamageMultLabel");
        _metricProjectilesLabel = GetNodeOrNull<Label>("%MetricProjectilesLabel");
        _metricLinksLabel = GetNodeOrNull<Label>("%MetricLinksLabel");

        // 4. Inspector Drawer
        _inspectorDrawer = GetNodeOrNull<PanelContainer>("%GemInspectorDrawer");
        _inspectorGemName = GetNodeOrNull<Label>("%InspectorGemName");
        _inspectorCategoryBadge = GetNodeOrNull<Label>("%InspectorCategoryBadge");
        _inspectorAttributeBadge = GetNodeOrNull<Label>("%InspectorAttributeBadge");
        _inspectorTagsHBox = GetNodeOrNull<HBoxContainer>("%InspectorTagsHBox");
        _inspectorModifiersLabel = GetNodeOrNull<Label>("%InspectorModifiersLabel");
        _inspectorStatusLabel = GetNodeOrNull<Label>("%InspectorStatusLabel");

        // 5. Candidate Drawer
        _filterAllBtn = GetNodeOrNull<Button>("%FilterAllBtn");
        _filterActiveBtn = GetNodeOrNull<Button>("%FilterActiveBtn");
        _filterSupportBtn = GetNodeOrNull<Button>("%FilterSupportBtn");
        _socketActionBtn = GetNodeOrNull<Button>("%SocketActionBtn");
        _unsocketActionBtn = GetNodeOrNull<Button>("%UnsocketActionBtn");
        _candidateScroll = GetNodeOrNull<ScrollContainer>("%CandidateScroll");
        _candidateHBox = GetNodeOrNull<HBoxContainer>("%CandidateHBox");

        var filterGroup = new ButtonGroup();
        if (_filterAllBtn != null) _filterAllBtn.ButtonGroup = filterGroup;
        if (_filterActiveBtn != null) _filterActiveBtn.ButtonGroup = filterGroup;
        if (_filterSupportBtn != null) _filterSupportBtn.ButtonGroup = filterGroup;
    }

    private void HookEvents()
    {
        if (_filterAllBtn != null) _filterAllBtn.Pressed += () => SetCandidateFilter("All");
        if (_filterActiveBtn != null) _filterActiveBtn.Pressed += () => SetCandidateFilter("Active");
        if (_filterSupportBtn != null) _filterSupportBtn.Pressed += () => SetCandidateFilter("Support");

        if (_socketActionBtn != null)
        {
            _socketActionBtn.Pressed += OnSocketActionClicked;
        }

        if (_unsocketActionBtn != null)
        {
            _unsocketActionBtn.Pressed += OnUnsocketActionClicked;
        }
    }

    // ─────────────────────────────────────────────
    //  Public Interaction API
    // ─────────────────────────────────────────────

    public void SelectSkillSlot(int index)
    {
        if (index < 0 || index >= 5) return;
        SelectedSkillSlotIndex = index;
        SelectedSocketIndex = -1;

        for (int i = 0; i < _slotBtns.Length; i++)
        {
            if (_slotBtns[i] != null)
            {
                _slotBtns[i].ButtonPressed = (i == index);
            }
        }

        RefreshView();
        OnSkillSlotChanged?.Invoke(index, CurrentSetup);
    }

    public void SelectSocket(int socketIndex)
    {
        SelectedSocketIndex = socketIndex;

        if (socketIndex == -1)
        {
            InspectedGem = CurrentSetup.ActiveGem;
        }
        else if (socketIndex >= 0 && socketIndex < CurrentSetup.SocketedSupports.Count)
        {
            InspectedGem = CurrentSetup.SocketedSupports[socketIndex];
        }
        else
        {
            InspectedGem = null;
        }

        UpdateHighlightState();
        UpdateMetricsPreview();
        UpdateInspectorView();
        UpdateActionButtons();
        OnGemInspected?.Invoke(InspectedGem);
    }

    public void SelectCandidateGem(GemDefinition gem)
    {
        SelectedCandidateGem = gem;
        InspectedGem = gem;
        UpdateHighlightState();
        UpdateMetricsPreview();
        UpdateInspectorView();
        UpdateActionButtons();
        OnGemInspected?.Invoke(gem);
    }

    public bool SocketGem(GemDefinition gem, int? targetSocketIndex = null)
    {
        var setup = CurrentSetup;
        bool success = false;

        if (gem.Category == "Active")
        {
            success = setup.SetActiveGem(gem);
            SelectedSocketIndex = -1;
            InspectedGem = gem;
        }
        else if (gem.Category == "Support")
        {
            int targetIdx = targetSocketIndex ?? SelectedSocketIndex;
            if (targetIdx >= 0 && targetIdx < SkillSocketSetup.MaxSupportSlots)
            {
                success = setup.SetSupportGemAt(targetIdx, gem);
                SelectedSocketIndex = targetIdx;
            }
            else
            {
                success = setup.AddSupportGem(gem);
                SelectedSocketIndex = setup.SocketedSupports.Count - 1;
            }
            InspectedGem = gem;
        }

        if (success)
        {
            RefreshView();
            OnGemsModified?.Invoke(setup);
        }

        return success;
    }

    public bool UnsocketGem(int targetSocketIndex)
    {
        var setup = CurrentSetup;
        bool success = false;

        if (targetSocketIndex == -1)
        {
            if (setup.ActiveGem != null)
            {
                setup.ClearActiveGem();
                InspectedGem = null;
                success = true;
            }
        }
        else if (targetSocketIndex >= 0 && targetSocketIndex < setup.SocketedSupports.Count)
        {
            success = setup.RemoveSupportGemAt(targetSocketIndex);
            InspectedGem = null;
        }

        if (success)
        {
            RefreshView();
            OnGemsModified?.Invoke(setup);
        }

        return success;
    }

    // ─────────────────────────────────────────────
    //  View Refresh & Data Binding
    // ─────────────────────────────────────────────

    public void RefreshView()
    {
        var setup = CurrentSetup;

        // 1. Position Sockets in Hub
        LayoutSocketsInHub();

        // 2. Render Active Gem Socket
        RenderActiveSocket(setup.ActiveGem);

        // 3. Render 5 Support Gem Sockets
        for (int i = 0; i < 5; i++)
        {
            GemDefinition? support = i < setup.SocketedSupports.Count ? setup.SocketedSupports[i] : null;
            bool isCompatible = support != null && setup.IsSupportCompatible(support);
            RenderSupportSocket(i, support, isCompatible);
        }

        // 4. Render Live Metrics Bar
        RenderMetrics(setup);

        // 5. Redraw Conduit Link Lines
        _socketRingCanvas?.QueueRedraw();

        // 6. Update Inspector Drawer
        if (InspectedGem == null)
        {
            InspectedGem = setup.ActiveGem;
        }
        UpdateInspectorView();

        // 7. Update Candidate Drawer
        PopulateCandidates();

        // 8. Update Action Buttons
        UpdateActionButtons();

        // 9. Update Highlight & Validator State
        UpdateHighlightState();
        UpdateMetricsPreview();
    }

    private void LayoutSocketsInHub()
    {
        if (_socketRingCanvas == null) return;

        Vector2 canvasSize = _socketRingCanvas.Size;
        float centerX = canvasSize.X > 0 ? canvasSize.X * 0.5f : 204f;
        float centerY = 116f;

        // Active Gem Socket (68 x 68 px, centered at centerX, centerY)
        if (_activeSocketBtn != null)
        {
            _activeSocketBtn.CustomMinimumSize = new Vector2(68, 68);
            _activeSocketBtn.Size = new Vector2(68, 68);
            _activeSocketBtn.Position = new Vector2(centerX - 34f, centerY - 34f);
        }

        // 5 Support Sockets arranged along upper arc (radius 76px, angles -160 to -20)
        float radius = 76f;
        float startAngle = -160f * (MathF.PI / 180f);
        float endAngle = -20f * (MathF.PI / 180f);
        float step = (endAngle - startAngle) / 4f;

        for (int i = 0; i < 5; i++)
        {
            if (_supportSocketBtns[i] == null) continue;

            float angle = startAngle + step * i;
            float sx = centerX + radius * MathF.Cos(angle);
            float sy = centerY + radius * MathF.Sin(angle);

            _supportSocketBtns[i].CustomMinimumSize = new Vector2(46, 46);
            _supportSocketBtns[i].Size = new Vector2(46, 46);
            _supportSocketBtns[i].Position = new Vector2(sx - 23f, sy - 23f);
        }
    }

    private void RenderActiveSocket(GemDefinition? gem)
    {
        if (_activeSocketBtn == null) return;

        bool isSelected = SelectedSocketIndex == -1;

        if (gem != null)
        {
            DamageType element = GetGemDamageType(gem);
            Color themeColor = GetDamageTypeColor(element);
            string icon = GetDamageTypeIcon(element);

            if (_activeGemIconLabel != null) _activeGemIconLabel.Text = icon;
            if (_activeGemNameLabel != null)
            {
                _activeGemNameLabel.Text = gem.Name.ToUpperInvariant();
                _activeGemNameLabel.Modulate = IsActiveSocketDimmed
                    ? new Color(0.45f, 0.48f, 0.55f, 0.6f)
                    : (IsActiveSocketHighlighted ? Colors.White : themeColor);
            }
            if (_activeGemTypeLabel != null) _activeGemTypeLabel.Text = "Active Skill";
            if (_activeGemElementPill != null)
            {
                _activeGemElementPill.Visible = true;
                if (IsActiveSocketHighlighted)
                {
                    _activeGemElementPill.Text = "✓ MATCH";
                    _activeGemElementPill.Modulate = ColorGold;
                }
                else if (IsActiveSocketDimmed)
                {
                    _activeGemElementPill.Text = "MISMATCH";
                    _activeGemElementPill.Modulate = new Color(0.92f, 0.42f, 0.35f, 0.8f);
                }
                else
                {
                    _activeGemElementPill.Text = element.ToString().ToUpperInvariant();
                    _activeGemElementPill.Modulate = themeColor;
                }
            }

            Color activeBorderColor = IsActiveSocketHighlighted
                ? ColorGold
                : (IsActiveSocketDimmed ? new Color(0.24f, 0.26f, 0.32f, 0.45f) : (isSelected ? Colors.White : themeColor));

            int activeBorderWidth = IsActiveSocketHighlighted ? 3 : (isSelected ? 2 : 2);
            Color activeShadowColor = IsActiveSocketHighlighted
                ? ColorGold with { A = 0.65f }
                : (IsActiveSocketDimmed ? Colors.Transparent : themeColor with { A = 0.35f });
            int activeShadowSize = IsActiveSocketHighlighted ? 12 : (IsActiveSocketDimmed ? 0 : 6);

            var style = CreateSocketStyle(
                bgColor: IsActiveSocketDimmed ? new Color(0.06f, 0.065f, 0.08f, 0.85f) : new Color(0.09f, 0.095f, 0.13f, 0.98f),
                borderColor: activeBorderColor,
                borderWidth: activeBorderWidth,
                cornerRadius: 8,
                shadowColor: activeShadowColor,
                shadowSize: activeShadowSize
            );
            _activeSocketBtn.AddThemeStyleboxOverride("normal", style);
            _activeSocketBtn.AddThemeStyleboxOverride("hover", CreateHoverSocketStyle(style, activeBorderColor));
            _activeSocketBtn.TooltipText = $"{gem.Name} (Active Gem)\nElement: {element}\n" +
                $"{(IsActiveSocketHighlighted ? Loc.Tr("UI_GEM_ACTIVE_COMPAT_SELECTED") : (IsActiveSocketDimmed ? Loc.Tr("UI_GEM_ACTIVE_INCOMPAT_SELECTED") : gem.Description))}";
        }
        else
        {
            if (_activeGemIconLabel != null) _activeGemIconLabel.Text = "+";
            if (_activeGemNameLabel != null)
            {
                _activeGemNameLabel.Text = Loc.Tr("UI_GEM_SOCKET_BTN");
                _activeGemNameLabel.Modulate = new Color(0.55f, 0.58f, 0.65f);
            }
            if (_activeGemTypeLabel != null) _activeGemTypeLabel.Text = "Active Socket";
            if (_activeGemElementPill != null) _activeGemElementPill.Visible = false;

            var style = CreateSocketStyle(
                bgColor: ColorEmptyBg,
                borderColor: isSelected ? ColorGold : ColorEmptyBorder,
                borderWidth: isSelected ? 2 : 1,
                cornerRadius: 8
            );
            _activeSocketBtn.AddThemeStyleboxOverride("normal", style);
            _activeSocketBtn.AddThemeStyleboxOverride("hover", CreateHoverSocketStyle(style, ColorGold));
            _activeSocketBtn.TooltipText = Loc.Tr("UI_GEM_CLICK_ACTIVE_PROMPT");
        }
    }

    private void RenderSupportSocket(int index, GemDefinition? gem, bool isCompatible)
    {
        if (index < 0 || index >= _supportSocketBtns.Length || _supportSocketBtns[index] == null)
            return;

        var btn = _supportSocketBtns[index];
        var idxLabel = _supportIndexLabels[index];
        var nameLabel = _supportNameLabels[index];
        var pip = _supportStatusPips[index];

        bool isSelected = SelectedSocketIndex == index;

        if (idxLabel != null) idxLabel.Text = $"#{index + 1}";

        if (gem != null)
        {
            Color attrColor = GetAttributeColor(gem.Color);
            string shortName = GetSupportShortName(gem.Name);

            if (nameLabel != null)
            {
                nameLabel.Text = shortName;
                nameLabel.Modulate = isCompatible ? attrColor : new Color(0.6f, 0.6f, 0.6f);
            }

            if (pip != null)
            {
                pip.Visible = true;
                pip.Color = isCompatible ? new Color(0.28f, 0.88f, 0.42f) : new Color(0.95f, 0.45f, 0.25f);
            }

            var style = CreateSocketStyle(
                bgColor: new Color(0.08f, 0.085f, 0.11f, 0.95f),
                borderColor: isSelected ? Colors.White : (isCompatible ? attrColor : new Color(0.65f, 0.35f, 0.25f)),
                borderWidth: isSelected ? 2 : 1,
                cornerRadius: 23, // Circular socket
                shadowColor: isCompatible ? attrColor with { A = 0.25f } : Colors.Transparent,
                shadowSize: isCompatible ? 4 : 0
            );
            btn.AddThemeStyleboxOverride("normal", style);
            btn.AddThemeStyleboxOverride("hover", CreateHoverSocketStyle(style, attrColor));
            btn.TooltipText = $"{gem.Name} (Slot #{index + 1})\n" +
                $"{(isCompatible ? Loc.Tr("UI_GEM_COMPATIBLE") : Loc.Tr("UI_GEM_INCOMPATIBLE"))}\n" +
                $"{gem.Description}";
        }
        else
        {
            if (nameLabel != null)
            {
                nameLabel.Text = "+";
                nameLabel.Modulate = new Color(0.42f, 0.45f, 0.52f);
            }

            if (pip != null) pip.Visible = false;

            var style = CreateSocketStyle(
                bgColor: ColorEmptyBg,
                borderColor: isSelected ? ColorGold : ColorEmptyBorder,
                borderWidth: isSelected ? 2 : 1,
                cornerRadius: 23
            );
            btn.AddThemeStyleboxOverride("normal", style);
            btn.AddThemeStyleboxOverride("hover", CreateHoverSocketStyle(style, ColorGold));
            btn.TooltipText = Loc.Format("UI_GEM_SUPPORT_SOCKET_EMPTY", index + 1);
        }
    }

    private void RenderMetrics(SkillSocketSetup setup)
    {
        if (setup.ActiveGem != null)
        {
            float manaCost = setup.CalculateEffectiveManaCost();
            float actionSpeed = setup.CalculateBaseActionSpeed(Player?.Gear);
            var breakdown = setup.GetDamageMultiplierBreakdown();
            float dmgMultiplier = breakdown.TotalMultiplier;
            int projectiles = setup.GetProjectileCount();
            int validCount = setup.ValidLinkedSupports.Count();
            int totalSupports = setup.SocketedSupports.Count;

            if (_metricManaLabel != null)
            {
                _metricManaLabel.Text = $"💧 Mana: {manaCost:F0}";
                _metricManaLabel.TooltipText = $"Effective Mana Cost: {manaCost:F1} (compounded with linked supports)";
            }

            if (_metricSpeedLabel != null)
            {
                bool isAttack = setup.ActiveGem.Tags?.Contains("attack", StringComparer.OrdinalIgnoreCase) ?? true;
                string speedText = isAttack ? $"{actionSpeed:F2} APS" : $"{1f / MathF.Max(0.01f, actionSpeed):F2}s Cast";
                _metricSpeedLabel.Text = $"⚡ {speedText}";
                _metricSpeedLabel.TooltipText = $"Effective Action Speed: {actionSpeed:F2} attacks/casts per second";
            }

            if (_metricDamageMultLabel != null)
            {
                float morePercent = breakdown.NetMorePercent;
                string sign = morePercent >= 0f ? "+" : "";
                _metricDamageMultLabel.Text = $"💥 ∏: {dmgMultiplier:F2}x ({sign}{morePercent:F0}%)";
                _metricDamageMultLabel.Modulate = Colors.White;
                _metricDamageMultLabel.TooltipText = Loc.Format("UI_GEM_COMPOUND_DMG_TOOLTIP", breakdown.MathRepresentation, breakdown.FormulaString);
            }

            if (_metricProjectilesLabel != null)
            {
                if (setup.IsProjectile)
                {
                    _metricProjectilesLabel.Text = $"🏹 {projectiles} Proj";
                    _metricProjectilesLabel.TooltipText = $"Fires {projectiles} projectiles simultaneously";
                }
                else if (string.Equals(setup.ActiveGem.Id, "cleave", StringComparison.OrdinalIgnoreCase))
                {
                    _metricProjectilesLabel.Text = $"🗡 180° Sweep";
                    _metricProjectilesLabel.TooltipText = "Cleaves enemies in a 180-degree frontal arc";
                }
                else
                {
                    _metricProjectilesLabel.Text = $"🎯 Single Target";
                    _metricProjectilesLabel.TooltipText = "Direct single-target or contact strike";
                }
            }

            if (_metricLinksLabel != null)
            {
                _metricLinksLabel.Text = $"🔗 {validCount}/5 Linked";
                _metricLinksLabel.TooltipText = $"{validCount} of {totalSupports} socketed supports are active & compatible";
                _metricLinksLabel.Modulate = validCount > 0 ? ColorGold : Colors.White;
            }
        }
        else
        {
            if (_metricManaLabel != null) _metricManaLabel.Text = "💧 Mana: -";
            if (_metricSpeedLabel != null) _metricSpeedLabel.Text = "⚡ Speed: -";
            if (_metricDamageMultLabel != null)
            {
                _metricDamageMultLabel.Text = "💥 ∏: 1.00x";
                _metricDamageMultLabel.Modulate = Colors.White;
                _metricDamageMultLabel.TooltipText = "∏(1 + More Damage) = 1.00x (Baseline)";
            }
            if (_metricProjectilesLabel != null) _metricProjectilesLabel.Text = "🏹 Proj: -";
            if (_metricLinksLabel != null)
            {
                _metricLinksLabel.Text = "🔗 0/5 Linked";
                _metricLinksLabel.Modulate = new Color(0.6f, 0.6f, 0.6f);
            }
        }
    }

    public void DrawSocketLinks(Control canvas)
    {
        if (canvas == null) return;

        var setup = CurrentSetup;
        var activeGem = setup?.ActiveGem;

        Vector2 activePos = _activeSocketBtn != null
            ? _activeSocketBtn.Position + _activeSocketBtn.Size * 0.5f
            : new Vector2(204f, 116f);

        // 1. Celestial Arc connecting support socket cluster
        float arcRadius = 76f;
        float startAngle = -165f * (MathF.PI / 180f);
        float endAngle = -15f * (MathF.PI / 180f);
        Color arcColor = activeGem != null
            ? new Color(0.28f, 0.32f, 0.42f, 0.65f)
            : new Color(0.18f, 0.20f, 0.26f, 0.45f);

        canvas.DrawArc(activePos, arcRadius, startAngle, endAngle, 32, arcColor, 1.5f, antialiased: true);

        // 2. Conduits from active gem to support sockets
        for (int i = 0; i < 5; i++)
        {
            if (i >= _supportSocketBtns.Length || _supportSocketBtns[i] == null) continue;

            Vector2 supportPos = _supportSocketBtns[i].Position + _supportSocketBtns[i].Size * 0.5f;
            Vector2 dir = (supportPos - activePos).Normalized();
            Vector2 startPoint = activePos + dir * 34f;
            Vector2 endPoint = supportPos - dir * 23f;

            bool hasSupport = setup != null && i < setup.SocketedSupports.Count;
            GemDefinition? supportGem = hasSupport ? setup!.SocketedSupports[i] : null;
            bool isCompatible = supportGem != null && setup!.IsSupportCompatible(supportGem);

            if (hasSupport && isCompatible)
            {
                Color supportColor = GetAttributeColor(supportGem?.Color);
                bool isFocused = supportGem == EffectiveFocusedGem;
                float glowWidth = isFocused ? 6.5f : 4.5f;
                float coreWidth = isFocused ? 3.0f : 2.0f;
                Color glowColor = isFocused ? ColorGold with { A = 0.6f } : supportColor with { A = 0.35f };

                // Conduit glow
                canvas.DrawLine(startPoint, endPoint, glowColor, glowWidth, antialiased: true);
                // Core golden energy stream
                canvas.DrawLine(startPoint, endPoint, ColorGold, coreWidth, antialiased: true);
                // Junction node
                canvas.DrawCircle(endPoint, isFocused ? 4.5f : 3.0f, ColorGold);
            }
            else if (hasSupport && !isCompatible)
            {
                // Incompatible warning conduit
                Color warnColor = new Color(0.92f, 0.40f, 0.25f, 0.75f);
                canvas.DrawLine(startPoint, endPoint, warnColor, 1.8f, antialiased: true);
            }
            else
            {
                // Empty conduit slot
                Color emptyColor = activeGem != null
                    ? new Color(0.24f, 0.27f, 0.35f, 0.55f)
                    : new Color(0.16f, 0.18f, 0.24f, 0.35f);
                canvas.DrawLine(startPoint, endPoint, emptyColor, 1.5f, antialiased: true);
            }
        }
    }

    // ─────────────────────────────────────────────
    //  Inspector Drawer & Smart Tooltip
    // ─────────────────────────────────────────────

    private void UpdateInspectorView()
    {
        var gem = _hoveredGem ?? InspectedGem;

        if (gem != null)
        {
            bool isActive = string.Equals(gem.Category, "Active", StringComparison.OrdinalIgnoreCase);
            DamageType element = GetGemDamageType(gem);
            Color themeColor = isActive ? GetDamageTypeColor(element) : GetAttributeColor(gem.Color);

            if (_inspectorGemName != null)
            {
                _inspectorGemName.Text = gem.Name;
                _inspectorGemName.Modulate = themeColor;
            }

            if (_inspectorCategoryBadge != null)
            {
                _inspectorCategoryBadge.Text = isActive ? "ACTIVE SKILL GEM" : "SUPPORT GEM";
                _inspectorCategoryBadge.Modulate = isActive ? new Color(0.35f, 0.85f, 0.95f) : ColorGold;
            }

            if (_inspectorAttributeBadge != null)
            {
                string attrName = gem.Color switch
                {
                    "Red" => "STRENGTH",
                    "Green" => "DEXTERITY",
                    "Blue" => "INTELLIGENCE",
                    _ => "PRISMATIC"
                };
                _inspectorAttributeBadge.Text = attrName;
                _inspectorAttributeBadge.Modulate = GetAttributeColor(gem.Color);
            }

            // Populate Tags HBox
            PopulateInspectorTags(gem);

            // Modifiers text
            if (_inspectorModifiersLabel != null)
            {
                _inspectorModifiersLabel.Text = FormatGemModifiers(gem);
            }

            // Compatibility status & Compounded More damage formula
            if (_inspectorStatusLabel != null)
            {
                if (isActive)
                {
                    var breakdown = CurrentSetup.GetDamageMultiplierBreakdown();
                    _inspectorStatusLabel.Text = Loc.Format("UI_GEM_INSPECTOR_ACTIVE", breakdown.MathRepresentation, breakdown.FormulaString);
                    _inspectorStatusLabel.Modulate = new Color(0.45f, 0.85f, 0.55f);
                }
                else
                {
                    var activeGem = CurrentSetup.ActiveGem;
                    if (activeGem != null)
                    {
                        var compat = GemCompatibilityValidator.ValidateCompatibility(activeGem, gem);
                        if (compat.IsCompatible)
                        {
                            var preview = GemCompatibilityValidator.PreviewSupportImpact(CurrentSetup, gem);
                            string deltaStr = preview.DeltaMultiplier >= 0 ? $"+{preview.DeltaMultiplier:F2}x" : $"{preview.DeltaMultiplier:F2}x";
                            string deltaPctStr = preview.DeltaPercent >= 0 ? $"+{preview.DeltaPercent:F0}%" : $"{preview.DeltaPercent:F0}%";
                            _inspectorStatusLabel.Text = $"{compat.Reason}\n" +
                                $"∏(1 + More): {preview.CurrentBreakdown.TotalMultiplier:F2}x ➔ {preview.ProjectedBreakdown.TotalMultiplier:F2}x (▲ {deltaStr} | {deltaPctStr} More)";
                            _inspectorStatusLabel.Modulate = new Color(0.35f, 0.92f, 0.45f);
                        }
                        else
                        {
                            var currentBreakdown = CurrentSetup.GetDamageMultiplierBreakdown();
                            _inspectorStatusLabel.Text = Loc.Format("UI_GEM_INSPECTOR_INCOMPAT", compat.Reason, currentBreakdown.TotalMultiplier);
                            _inspectorStatusLabel.Modulate = new Color(0.95f, 0.42f, 0.35f);
                        }
                    }
                    else
                    {
                        _inspectorStatusLabel.Text = Loc.Tr("UI_GEM_NEED_ACTIVE");
                        _inspectorStatusLabel.Modulate = new Color(0.85f, 0.72f, 0.35f);
                    }
                }
            }
        }
        else
        {
            if (_inspectorGemName != null)
            {
                _inspectorGemName.Text = Loc.Tr("UI_GEM_INVENTORY_EMPTY");
                _inspectorGemName.Modulate = new Color(0.55f, 0.58f, 0.65f);
            }

            if (_inspectorCategoryBadge != null) _inspectorCategoryBadge.Text = "PoE 2 Gem Sockets";
            if (_inspectorAttributeBadge != null) _inspectorAttributeBadge.Text = "SLOT";

            ClearInspectorTags();

            if (_inspectorModifiersLabel != null)
            {
                _inspectorModifiersLabel.Text = Loc.Tr("UI_GEM_PO2_DECOUPLED_HINT");
            }

            if (_inspectorStatusLabel != null)
            {
                _inspectorStatusLabel.Text = Loc.Tr("UI_GEM_DOUBLECLICK_HINT");
                _inspectorStatusLabel.Modulate = new Color(0.6f, 0.65f, 0.75f);
            }
        }
    }

    private void PopulateInspectorTags(GemDefinition gem)
    {
        if (_inspectorTagsHBox == null) return;
        ClearInspectorTags();

        string[]? tags = gem.Category == "Active" ? gem.Tags : gem.RequiredTags;
        if (tags == null || tags.Length == 0) return;

        foreach (var tag in tags)
        {
            var pill = new PanelContainer();
            var pillStyle = new StyleBoxFlat
            {
                BgColor = new Color(0.12f, 0.13f, 0.17f, 0.95f),
                BorderWidthLeft = 1,
                BorderWidthTop = 1,
                BorderWidthRight = 1,
                BorderWidthBottom = 1,
                BorderColor = new Color(0.28f, 0.32f, 0.42f, 0.7f),
                CornerRadiusTopLeft = 2,
                CornerRadiusTopRight = 2,
                CornerRadiusBottomRight = 2,
                CornerRadiusBottomLeft = 2,
                ContentMarginLeft = 4f,
                ContentMarginTop = 1f,
                ContentMarginRight = 4f,
                ContentMarginBottom = 1f
            };
            pill.AddThemeStyleboxOverride("panel", pillStyle);

            var tagLabel = new Label
            {
                Text = tag.ToUpperInvariant(),
                Modulate = new Color(0.78f, 0.82f, 0.92f)
            };
            tagLabel.AddThemeFontSizeOverride("font_size", 9);
            pill.AddChild(tagLabel);

            _inspectorTagsHBox.AddChild(pill);
        }
    }

    private void ClearInspectorTags()
    {
        if (_inspectorTagsHBox == null) return;
        foreach (var child in _inspectorTagsHBox.GetChildren())
        {
            child.QueueFree();
        }
    }

    private static string FormatGemModifiers(GemDefinition gem)
    {
        var lines = new List<string>();

        if (gem.BaseStats != null)
        {
            if (gem.BaseStats.TryGetValue("manaCost", out var mc)) lines.Add($"Mana Cost: {mc}");
            if (gem.BaseStats.TryGetValue("castTime", out var ct)) lines.Add($"Cast Time: {ct}s");
            if (gem.BaseStats.TryGetValue("damageMultiplier", out var dm)) lines.Add($"Damage Multiplier: {dm}%");
            if (gem.BaseStats.TryGetValue("projectileCount", out var pc)) lines.Add($"Projectiles: {pc}");
            if (gem.BaseStats.TryGetValue("aoeRadius", out var ar)) lines.Add($"AoE Radius: {ar}px");
        }

        if (gem.Modifiers != null)
        {
            if (gem.Modifiers.TryGetValue("manaMultiplier", out var mm)) lines.Add($"Mana Multiplier: {mm}%");
            if (gem.Modifiers.TryGetValue("damageMultiplier", out var dm)) lines.Add($"Damage: {dm}% (More/Less)");
            if (gem.Modifiers.TryGetValue("extraProjectiles", out var ep)) lines.Add($"Fires {ep} additional projectiles");
            if (gem.Modifiers.TryGetValue("extraPhysicalAsFirePercent", out var epf)) lines.Add($"Gain {epf}% Physical as Extra Fire");
            if (gem.Modifiers.TryGetValue("morePhysicalDamagePercent", out var mpd)) lines.Add($"35% More Melee Physical Damage");
            if (gem.Modifiers.TryGetValue("moreFireDamagePercent", out var mfd)) lines.Add($"20% More Fire Damage");
            if (gem.Modifiers.TryGetValue("moreAoeDamagePercent", out var mad)) lines.Add($"30% More Area Damage");
        }

        if (lines.Count == 0 && !string.IsNullOrWhiteSpace(gem.Description))
        {
            return gem.Description;
        }

        return string.Join(" • ", lines);
    }

    // ─────────────────────────────────────────────
    //  Candidate Drawer (Bottom Stash / Inventory)
    // ─────────────────────────────────────────────

    private void SetCandidateFilter(string filter)
    {
        CurrentCandidateFilter = filter;
        PopulateCandidates();
    }

    private void PopulateCandidates()
    {
        if (_candidateHBox == null || Database == null) return;

        _candidateCards.Clear();
        foreach (var child in _candidateHBox.GetChildren())
        {
            child.QueueFree();
        }

        var gems = Database.Gems.Values
            .Where(g => CurrentCandidateFilter == "All" || string.Equals(g.Category, CurrentCandidateFilter, StringComparison.OrdinalIgnoreCase))
            .ToList();

        foreach (var gem in gems)
        {
            var card = CreateCandidateCard(gem);
            _candidateCards[card] = gem;
            _candidateHBox.AddChild(card);
        }

        UpdateHighlightState();
    }

    private Button CreateCandidateCard(GemDefinition gem)
    {
        bool isActive = gem.Category == "Active";
        DamageType element = GetGemDamageType(gem);
        Color attrColor = isActive ? GetDamageTypeColor(element) : GetAttributeColor(gem.Color);
        bool isSelected = SelectedCandidateGem == gem;

        var btn = new Button
        {
            CustomMinimumSize = new Vector2(82, 40),
            TooltipText = $"{gem.Name} ({gem.Category} Gem)\n{gem.Description}"
        };

        var style = CreateSocketStyle(
            bgColor: new Color(0.08f, 0.085f, 0.11f, 0.95f),
            borderColor: isSelected ? ColorGold : attrColor with { A = 0.65f },
            borderWidth: isSelected ? 2 : 1,
            cornerRadius: 3
        );
        btn.AddThemeStyleboxOverride("normal", style);
        btn.AddThemeStyleboxOverride("hover", CreateHoverSocketStyle(style, attrColor));

        var vbox = new VBoxContainer
        {
            MouseFilter = MouseFilterEnum.Ignore,
            Alignment = BoxContainer.AlignmentMode.Center
        };
        vbox.SetAnchorsPreset(LayoutPreset.FullRect);

        var topHBox = new HBoxContainer
        {
            MouseFilter = MouseFilterEnum.Ignore,
            Alignment = BoxContainer.AlignmentMode.Center
        };
        var badge = new Label
        {
            Text = isActive ? "ACT" : "SUP",
            Modulate = isActive ? new Color(0.35f, 0.85f, 0.95f) : ColorGold
        };
        badge.AddThemeFontSizeOverride("font_size", 9);
        topHBox.AddChild(badge);

        var dot = new ColorRect
        {
            CustomMinimumSize = new Vector2(4, 4),
            Color = attrColor,
            MouseFilter = MouseFilterEnum.Ignore
        };
        topHBox.AddChild(dot);
        vbox.AddChild(topHBox);

        var nameLabel = new Label
        {
            Text = GetSupportShortName(gem.Name),
            Modulate = attrColor,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        nameLabel.AddThemeFontSizeOverride("font_size", 9);
        vbox.AddChild(nameLabel);

        btn.AddChild(vbox);

        // Hover tracking
        btn.MouseEntered += () => OnCandidateMouseEntered(gem);
        btn.MouseExited += () => OnCandidateMouseExited(gem);

        // Click to inspect, double-click to equip
        btn.Pressed += () => SelectCandidateGem(gem);
        btn.GuiInput += @ev =>
        {
            if (@ev is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left && mb.DoubleClick)
            {
                SocketGem(gem);
            }
        };

        return btn;
    }

    private void OnCandidateMouseEntered(GemDefinition gem)
    {
        SetHoveredGem(gem);
    }

    private void OnCandidateMouseExited(GemDefinition gem)
    {
        if (_hoveredGem == gem)
        {
            SetHoveredGem(null);
        }
    }

    public void SetHoveredGem(GemDefinition? gem)
    {
        _hoveredGem = gem;
        UpdateHighlightState();
        UpdateMetricsPreview();
        UpdateInspectorView();
    }

    public void SetHoveredGemForTesting(GemDefinition? gem) => SetHoveredGem(gem);

    public void UpdateHighlightState()
    {
        var focusGem = EffectiveFocusedGem;
        bool isSupportFocused = focusGem != null && string.Equals(focusGem.Category, "Support", StringComparison.OrdinalIgnoreCase);
        bool isActiveFocused = focusGem != null && string.Equals(focusGem.Category, "Active", StringComparison.OrdinalIgnoreCase);

        // 1. Central Active Socket Highlight
        if (isSupportFocused)
        {
            if (CurrentSetup.ActiveGem != null)
            {
                bool compat = GemCompatibilityValidator.IsCompatible(CurrentSetup.ActiveGem, focusGem);
                IsActiveSocketHighlighted = compat;
                IsActiveSocketDimmed = !compat;
            }
            else
            {
                IsActiveSocketHighlighted = false;
                IsActiveSocketDimmed = false;
            }
        }
        else if (isActiveFocused)
        {
            IsActiveSocketHighlighted = focusGem == CurrentSetup.ActiveGem;
            IsActiveSocketDimmed = false;
        }
        else
        {
            IsActiveSocketHighlighted = false;
            IsActiveSocketDimmed = false;
        }

        RenderActiveSocket(CurrentSetup.ActiveGem);

        // 2. Skill Slot Switcher Tabs (Top Bar)
        for (int i = 0; i < 5; i++)
        {
            var btn = _slotBtns[i];
            if (btn == null) continue;

            if (isSupportFocused)
            {
                var slotSetup = Loadout?.GetSetupAt(i);
                if (slotSetup?.ActiveGem != null)
                {
                    bool compat = GemCompatibilityValidator.IsCompatible(slotSetup.ActiveGem, focusGem);
                    _slotBtnHighlighted[i] = compat;
                    btn.Modulate = compat ? new Color(1.0f, 0.92f, 0.45f, 1.0f) : new Color(0.5f, 0.52f, 0.6f, 0.45f);
                }
                else
                {
                    _slotBtnHighlighted[i] = false;
                    btn.Modulate = new Color(0.5f, 0.52f, 0.6f, 0.45f);
                }
            }
            else
            {
                _slotBtnHighlighted[i] = false;
                btn.Modulate = Colors.White;
            }
        }

        // 3. Support Sockets in Hub
        for (int i = 0; i < 5; i++)
        {
            GemDefinition? support = i < CurrentSetup.SocketedSupports.Count ? CurrentSetup.SocketedSupports[i] : null;
            bool isCompatible = support != null && CurrentSetup.IsSupportCompatible(support);
            RenderSupportSocket(i, support, isCompatible);
        }

        // 4. Candidate Cards in Drawer
        foreach (var (btn, gem) in _candidateCards)
        {
            if (btn == null || !IsInstanceValid(btn)) continue;

            if (isSupportFocused)
            {
                if (string.Equals(gem.Category, "Active", StringComparison.OrdinalIgnoreCase))
                {
                    bool compat = GemCompatibilityValidator.IsCompatible(gem, focusGem);
                    btn.Modulate = compat ? Colors.White : new Color(0.48f, 0.5f, 0.6f, 0.4f);
                }
                else
                {
                    btn.Modulate = Colors.White;
                }
            }
            else if (isActiveFocused)
            {
                if (string.Equals(gem.Category, "Support", StringComparison.OrdinalIgnoreCase))
                {
                    bool compat = GemCompatibilityValidator.IsCompatible(focusGem, gem);
                    btn.Modulate = compat ? Colors.White : new Color(0.48f, 0.5f, 0.6f, 0.4f);
                }
                else
                {
                    btn.Modulate = Colors.White;
                }
            }
            else
            {
                btn.Modulate = Colors.White;
            }
        }

        // 5. Conduit Links
        _socketRingCanvas?.QueueRedraw();
    }

    private void UpdateMetricsPreview()
    {
        var setup = CurrentSetup;
        var focusGem = EffectiveFocusedGem;

        if (setup.ActiveGem != null && focusGem != null && string.Equals(focusGem.Category, "Support", StringComparison.OrdinalIgnoreCase))
        {
            bool isCompat = GemCompatibilityValidator.IsCompatible(setup.ActiveGem, focusGem);
            if (isCompat)
            {
                var preview = GemCompatibilityValidator.PreviewSupportImpact(setup, focusGem, SelectedSocketIndex >= 0 ? SelectedSocketIndex : null);
                CurrentPreviewResult = preview;

                if (_metricDamageMultLabel != null)
                {
                    string sign = preview.DeltaMultiplier >= 0 ? "+" : "";
                    string pctSign = preview.DeltaPercent >= 0 ? "+" : "";
                    _metricDamageMultLabel.Text = $"💥 ∏: {preview.CurrentBreakdown.TotalMultiplier:F2}x ➔ {preview.ProjectedBreakdown.TotalMultiplier:F2}x";
                    _metricDamageMultLabel.Modulate = ColorGold;
                    _metricDamageMultLabel.TooltipText = Loc.Format(
                        "UI_GEM_DAMAGE_FORECAST_TOOLTIP",
                        preview.CurrentBreakdown.FormulaString,
                        preview.ProjectedBreakdown.FormulaString,
                        sign,
                        preview.DeltaMultiplier,
                        pctSign,
                        preview.DeltaPercent
                    );
                }
                return;
            }
            else
            {
                CurrentPreviewResult = null;
                if (_metricDamageMultLabel != null)
                {
                    var breakdown = setup.GetDamageMultiplierBreakdown();
                    _metricDamageMultLabel.Text = Loc.Format("UI_GEM_UNCHANGED", breakdown.TotalMultiplier);
                    _metricDamageMultLabel.Modulate = new Color(0.95f, 0.45f, 0.35f);
                    _metricDamageMultLabel.TooltipText = Loc.Format("UI_GEM_INCOMPAT_PREVIEW_TOOLTIP", breakdown.FormulaString);
                }
                return;
            }
        }

        CurrentPreviewResult = null;
        RenderMetrics(setup);
    }

    private void UpdateActionButtons()
    {
        var setup = CurrentSetup;

        // Equip / Socket Button
        if (_socketActionBtn != null)
        {
            bool canSocket = SelectedCandidateGem != null;
            _socketActionBtn.Disabled = !canSocket;
            if (SelectedCandidateGem != null)
            {
                string targetDesc = SelectedCandidateGem.Category == "Active"
                    ? "Active Slot"
                    : (SelectedSocketIndex >= 0 ? $"Slot #{SelectedSocketIndex + 1}" : "Next Slot");
                _socketActionBtn.Text = Loc.Format("UI_GEM_SOCKET_TARGET", targetDesc);
            }
            else
            {
                _socketActionBtn.Text = Loc.Tr("UI_GEM_SOCKET_ACTION");
            }
        }

        // Unsocket Button
        if (_unsocketActionBtn != null)
        {
            bool canUnsocket = false;
            if (SelectedSocketIndex == -1 && setup.ActiveGem != null)
            {
                canUnsocket = true;
                _unsocketActionBtn.Text = Loc.Tr("UI_GEM_UNSOCKET_ACTIVE");
            }
            else if (SelectedSocketIndex >= 0 && SelectedSocketIndex < setup.SocketedSupports.Count)
            {
                canUnsocket = true;
                _unsocketActionBtn.Text = Loc.Format("UI_GEM_UNSOCKET_SLOT", SelectedSocketIndex + 1);
            }
            else
            {
                _unsocketActionBtn.Text = Loc.Tr("UI_GEM_UNSOCKET_ACTION");
            }
            _unsocketActionBtn.Disabled = !canUnsocket;
        }
    }

    private void OnSocketActionClicked()
    {
        if (SelectedCandidateGem != null)
        {
            SocketGem(SelectedCandidateGem);
        }
    }

    private void OnUnsocketActionClicked()
    {
        UnsocketGem(SelectedSocketIndex);
    }

    // ─────────────────────────────────────────────
    //  Helpers
    // ─────────────────────────────────────────────

    public static DamageType GetGemDamageType(GemDefinition gem)
    {
        if (gem.BaseStats != null && gem.BaseStats.TryGetValue("damageType", out var dtObj))
        {
            if (Enum.TryParse<DamageType>(dtObj?.ToString(), ignoreCase: true, out var dt))
                return dt;
        }

        if (gem.Tags != null)
        {
            if (gem.Tags.Contains("fire", StringComparer.OrdinalIgnoreCase)) return DamageType.Fire;
            if (gem.Tags.Contains("cold", StringComparer.OrdinalIgnoreCase)) return DamageType.Cold;
            if (gem.Tags.Contains("lightning", StringComparer.OrdinalIgnoreCase)) return DamageType.Lightning;
            if (gem.Tags.Contains("chaos", StringComparer.OrdinalIgnoreCase)) return DamageType.Chaos;
            if (gem.Tags.Contains("physical", StringComparer.OrdinalIgnoreCase)) return DamageType.Physical;
        }

        return gem.Color?.ToLowerInvariant() switch
        {
            "red" => DamageType.Fire,
            "blue" => DamageType.Cold,
            "green" => DamageType.Physical,
            _ => DamageType.Physical
        };
    }

    public static Color GetDamageTypeColor(DamageType type) => type switch
    {
        DamageType.Fire => ColorFire,
        DamageType.Cold => ColorCold,
        DamageType.Lightning => ColorLightning,
        DamageType.Chaos => ColorChaos,
        _ => ColorPhysical
    };

    public static string GetDamageTypeIcon(DamageType type) => type switch
    {
        DamageType.Fire => "🔥",
        DamageType.Cold => "❄",
        DamageType.Lightning => "⚡",
        DamageType.Chaos => "☠",
        _ => "🗡"
    };

    public static Color GetAttributeColor(string? colorName) => colorName?.ToLowerInvariant() switch
    {
        "red" => new Color(0.85f, 0.28f, 0.24f),
        "green" => new Color(0.26f, 0.82f, 0.42f),
        "blue" => new Color(0.24f, 0.58f, 0.92f),
        "white" => new Color(0.88f, 0.90f, 0.95f),
        _ => new Color(0.65f, 0.68f, 0.75f)
    };

    private static string GetSupportShortName(string fullName)
    {
        string trimmed = fullName.Replace(" Support", "", StringComparison.OrdinalIgnoreCase);
        if (trimmed.Length <= 10) return trimmed;

        return trimmed switch
        {
            "Greater Multiple Projectiles" => "GMP",
            "Lesser Multiple Projectiles" => "LMP",
            "Added Fire Damage" => "Add Fire",
            "Ignite Proliferation" => "Ignite Prolif",
            "Melee Physical Damage" => "Melee Phys",
            "Concentrated Effect" => "Conc Effect",
            "Faster Attacks" => "Faster Atk",
            _ => trimmed.Length > 9 ? trimmed.Substring(0, 8) + ".." : trimmed
        };
    }

    private static StyleBoxFlat CreateSocketStyle(
        Color bgColor,
        Color borderColor,
        int borderWidth,
        int cornerRadius,
        Color? shadowColor = null,
        int shadowSize = 0)
    {
        return new StyleBoxFlat
        {
            BgColor = bgColor,
            BorderWidthLeft = borderWidth,
            BorderWidthTop = borderWidth,
            BorderWidthRight = borderWidth,
            BorderWidthBottom = borderWidth,
            BorderColor = borderColor,
            CornerRadiusTopLeft = cornerRadius,
            CornerRadiusTopRight = cornerRadius,
            CornerRadiusBottomRight = cornerRadius,
            CornerRadiusBottomLeft = cornerRadius,
            ShadowColor = shadowColor ?? Colors.Transparent,
            ShadowSize = shadowSize,
            ContentMarginLeft = 2f,
            ContentMarginTop = 2f,
            ContentMarginRight = 2f,
            ContentMarginBottom = 2f
        };
    }

    private static StyleBoxFlat CreateHoverSocketStyle(StyleBoxFlat baseStyle, Color hoverBorderColor)
    {
        var hover = (StyleBoxFlat)baseStyle.Duplicate();
        hover.BorderColor = hoverBorderColor;
        hover.ShadowColor = hoverBorderColor with { A = 0.4f };
        hover.ShadowSize = Math.Max(baseStyle.ShadowSize, 4);
        return hover;
    }
}
