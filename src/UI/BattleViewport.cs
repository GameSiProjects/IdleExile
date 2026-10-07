using System;
using System.Collections.Generic;
using Godot;
using IdleExile.Core.Models;
using IdleExile.Core.Simulation;
using IdleExile.Core.State;
using IdleExile.UI.Components;

namespace IdleExile.UI;

/// <summary>
/// Controller for the Battle Viewport (640 x 248 px), displaying map telemetry,
/// 2D arena combat presentation, player vitometer (Life/ES/Mana/Flasks), and loot toast notifications.
/// Designed according to docs/07_combat_ui_and_viewport.md.
/// </summary>
public partial class BattleViewport : Control
{
    public const float ViewportWidth = 640f;
    public const float ViewportHeight = 248f;
    public const float ArenaViewportHeight = 172f;
    public const float MapHeaderHeight = 32f;
    public const float VitometerHeight = 44f;
    // --- Container References (Task 2.1) ---
    public PanelContainer? MapHeaderContainer { get; private set; }
    public SubViewportContainer? ArenaViewportContainer { get; private set; }
    public PanelContainer? VitometerPanel { get; private set; }
    public VitometerView? Vitometer { get; private set; }
    public PanelContainer? LootToastOverlay { get; private set; }

    // --- Map Header Component (Task 2.2) ---
    public MapHeaderBar? HeaderBar { get; private set; }

    // --- Map Header Direct Control References (Task 2.1 & 2.2) ---
    private Label? _tierBadgeLabel;
    private Label? _mapNameLabel;
    private HBoxContainer? _threatBadgesHBox;
    private Label? _quantityBonusLabel;
    private ProgressBar? _mapProgressBar;

    // --- Arena 2D Component (Task 2.3) ---
    public Arena2D? Arena { get; private set; }

    // --- Floating Combat Text Manager (Task 2.5) ---
    public FloatingTextManager? FloatingText { get; private set; }

    // --- Arena Viewport Controls (Task 2.3) ---
    private SubViewport? _arenaSubViewport;
    private Node2D? _arena2D;
    private Node2D? _parallaxBackground;
    private Node2D? _monsterPool;
    private Node2D? _exileNode;
    private Node2D? _projectilePool;
    private Control? _floatingCombatTextLayer;
    private Node2D? _dropBeamLayer;

    // --- Vitometer Controls (Task 2.4 Prerequisite) ---
    private ProgressBar? _lifeEsBar;
    private Label? _defensesSummaryLabel;
    private HBoxContainer? _flasksContainer;
    private ProgressBar? _manaBar;
    private Label? _unreservedManaLabel;

    // --- Loot Toast Controls (Task 2.6 Prerequisite) ---
    private Label? _lootToastLabel;
    private double _toastRemainingDuration;

    // --- Dual-State Viewport Mode (Prompt 4) ---
    public Arena2D.ViewportMode CurrentMode
    {
        get => Arena?.CurrentMode ?? Arena2D.ViewportMode.SanctuaryIdle;
        set
        {
            if (Arena != null)
            {
                Arena.CurrentMode = value;
            }
            if (value == Arena2D.ViewportMode.SanctuaryIdle)
            {
                SetSanctuaryStatus();
            }
        }
    }

    public void SetViewportMode(Arena2D.ViewportMode mode) => CurrentMode = mode;

    // --- Quick Footer Telemetry Controls (Prompt 4) ---
    private Label? _dpsLabel;
    private Label? _statusLabel;

    public Label? DpsLabel
    {
        get => _dpsLabel;
        set => _dpsLabel = value;
    }

    public Label? StatusLabel
    {
        get => _statusLabel;
        set => _statusLabel = value;
    }

    public CombatDpsSnapshot? LatestDpsSnapshot { get; private set; }

    public override void _Ready()
    {
        BindNodes();
        ApplyInitialState();
#if DEBUG
        CreateDebugOverlay();
#endif
    }

    private void BindNodes()
    {
        // 1. Primary Containers & Components
        MapHeaderContainer = GetNodeOrNull<PanelContainer>("%MapHeaderContainer");
        HeaderBar = MapHeaderContainer as MapHeaderBar ?? GetNodeOrNull<MapHeaderBar>("%MapHeaderContainer");
        ArenaViewportContainer = GetNodeOrNull<SubViewportContainer>("%ArenaViewportContainer");
        VitometerPanel = GetNodeOrNull<PanelContainer>("%VitometerPanel");
        Vitometer = VitometerPanel as VitometerView ?? GetNodeOrNull<VitometerView>("%VitometerPanel");
        if (Vitometer != null && !Vitometer.IsNodeReady())
        {
            Vitometer._Ready();
        }
        LootToastOverlay = GetNodeOrNull<PanelContainer>("%LootToastOverlay");

        // 2. Map Header Elements
        _tierBadgeLabel = GetNodeOrNull<Label>("%TierBadgeLabel");
        _mapNameLabel = GetNodeOrNull<Label>("%MapNameLabel");
        _threatBadgesHBox = GetNodeOrNull<HBoxContainer>("%ThreatBadgesHBox");
        _quantityBonusLabel = GetNodeOrNull<Label>("%QuantityBonusLabel");
        _mapProgressBar = GetNodeOrNull<ProgressBar>("%MapProgressBar");

        // 3. Arena Viewport Elements
        _arenaSubViewport = GetNodeOrNull<SubViewport>("%ArenaSubViewport");
        Arena = GetNodeOrNull<Arena2D>("%Arena2D");
        _arena2D = Arena;
        _parallaxBackground = GetNodeOrNull<Node2D>("%ParallaxBackground");
        _monsterPool = GetNodeOrNull<Node2D>("%MonsterPool");
        _exileNode = GetNodeOrNull<Node2D>("%ExileNode");
        _projectilePool = GetNodeOrNull<Node2D>("%ProjectilePool");
        FloatingText = GetNodeOrNull<FloatingTextManager>("%FloatingCombatTextLayer");
        if (FloatingText != null && !FloatingText.IsNodeReady())
        {
            FloatingText._Ready();
        }
        if (Arena != null)
        {
            if (!Arena.IsNodeReady())
            {
                Arena._Ready();
            }
            Arena.FloatingText ??= FloatingText;
        }
        _floatingCombatTextLayer = FloatingText ?? GetNodeOrNull<Control>("%FloatingCombatTextLayer");
        _dropBeamLayer = GetNodeOrNull<Node2D>("%DropBeamLayer");

        // 4. Vitometer Elements
        _lifeEsBar = GetNodeOrNull<ProgressBar>("%LifeESBar");
        _defensesSummaryLabel = GetNodeOrNull<Label>("%DefensesSummaryLabel");
        _flasksContainer = GetNodeOrNull<HBoxContainer>("%FlasksContainer");
        _manaBar = GetNodeOrNull<ProgressBar>("%ManaBar");
        _unreservedManaLabel = GetNodeOrNull<Label>("%UnreservedManaLabel");

        // 5. Loot Toast Elements
        _lootToastLabel = GetNodeOrNull<Label>("%LootToastLabel");

        // 6. Quick Footer Telemetry Elements (Prompt 4)
        _dpsLabel ??= GetNodeOrNull<Label>("%DpsLabel")
                      ?? GetOwner<Control>()?.GetNodeOrNull<Label>("%DpsLabel")
                      ?? GetParent()?.GetNodeOrNull<Label>("%DpsLabel");
        _statusLabel ??= GetNodeOrNull<Label>("%StatusLabel")
                         ?? GetOwner<Control>()?.GetNodeOrNull<Label>("%StatusLabel")
                         ?? GetParent()?.GetNodeOrNull<Label>("%StatusLabel");
    }

    public override void _Process(double delta)
    {
        if (_toastRemainingDuration > 0)
        {
            _toastRemainingDuration -= delta;
            if (_toastRemainingDuration <= 0)
            {
                HideLootToast();
            }
        }
    }

    private void ApplyInitialState()
    {
        if (LootToastOverlay != null)
        {
            LootToastOverlay.Visible = false;
        }

        // Default initial display for UI preview
        UpdateMapInfo("Twilight Strand", 1, "Normal", 0, 0, 0, 0f);
        UpdateVitals(100, 100, 0, 0, 50, 50, 0.0, "Life: 100 / 100 | EHP: 100");
    }

    /// <summary>
    /// Updates the Map Header telemetry bar with current map metadata.
    /// </summary>
    public void UpdateMapInfo(string mapName, int tier, string rarity = "Normal", int iiq = 0, int iir = 0, int packSize = 0, float progressPercent = 0f)
    {
        if (HeaderBar != null)
        {
            HeaderBar.UpdateTelemetry(mapName, tier, rarity, iiq, iir, packSize, progressPercent);
        }
        else
        {
            if (_tierBadgeLabel != null)
            {
                _tierBadgeLabel.Text = $"T{tier}";
                _tierBadgeLabel.Modulate = MapHeaderBar.GetTierColor(tier);
            }

            if (_mapNameLabel != null)
            {
                _mapNameLabel.Text = string.Equals(rarity, "Normal", StringComparison.OrdinalIgnoreCase) ? mapName : $"{mapName} • {rarity}";
                _mapNameLabel.Modulate = MapHeaderBar.GetRarityColor(rarity);
            }

            if (_quantityBonusLabel != null)
            {
                _quantityBonusLabel.Text = $"IIQ: +{iiq}%";
            }

            if (_mapProgressBar != null)
            {
                _mapProgressBar.Value = Mathf.Clamp(progressPercent, 0f, 100f);
            }
        }
    }

    /// <summary>
    /// Configures the Map Header using a complete MapRunConfig (Task 2.2).
    /// </summary>
    public void SetMap(MapRunConfig config, float progressPercent = 0f, string rarity = "Normal", string? leagueEncounter = null)
    {
        HeaderBar?.SetMap(config, progressPercent, rarity, leagueEncounter);
    }

    /// <summary>
    /// Updates the Monster Pack Clear Progress Bar (0% to 100%).
    /// </summary>
    public void SetMapProgress(float progressPercent)
    {
        if (HeaderBar != null)
        {
            HeaderBar.SetProgress(progressPercent);
        }
        else if (_mapProgressBar != null)
        {
            _mapProgressBar.Value = Mathf.Clamp(progressPercent, 0f, 100f);
        }
    }

    /// <summary>
    /// Updates the Vitometer health, energy shield, mana, and defense telemetry.
    /// </summary>
    public void UpdateVitals(double currentLife, double maxLife, double currentEs, double maxEs, double currentMana, double maxMana, double reservedPercent, string defensesSummary = "")
    {
        if (_lifeEsBar != null && maxLife > 0)
        {
            _lifeEsBar.MaxValue = maxLife;
            _lifeEsBar.Value = Mathf.Clamp(currentLife, 0, maxLife);
        }

        if (_defensesSummaryLabel != null)
        {
            if (string.IsNullOrWhiteSpace(defensesSummary))
            {
                string esText = maxEs > 0 ? $" | ES: {Mathf.RoundToInt((float)currentEs)}/{Mathf.RoundToInt((float)maxEs)}" : "";
                _defensesSummaryLabel.Text = $"Life: {Mathf.RoundToInt((float)currentLife)}/{Mathf.RoundToInt((float)maxLife)}{esText}";
            }
            else
            {
                _defensesSummaryLabel.Text = defensesSummary;
            }
        }

        if (_manaBar != null && maxMana > 0)
        {
            _manaBar.MaxValue = maxMana;
            _manaBar.Value = Mathf.Clamp(currentMana, 0, maxMana);
        }

        if (_unreservedManaLabel != null)
        {
            string reservedText = reservedPercent > 0 ? $" ({Mathf.RoundToInt((float)reservedPercent)}% R)" : "";
            _unreservedManaLabel.Text = $"Mana: {Mathf.RoundToInt((float)currentMana)}/{Mathf.RoundToInt((float)maxMana)}{reservedText}";
        }

        // Delegate to VitometerView component (Task 2.4)
        if (Vitometer != null)
        {
            float reservedMana = (float)(reservedPercent > 0 ? (maxMana * (reservedPercent / 100.0)) : 0.0);
            Vitometer.UpdateVitals(
                (float)currentLife,
                (float)maxLife,
                (float)currentEs,
                (float)maxEs,
                (float)currentMana,
                (float)maxMana,
                reservedMana
            );

            if (!string.IsNullOrWhiteSpace(defensesSummary))
            {
                Vitometer.SetDefensesSummary(defensesSummary);
            }
        }
    }

    /// <summary>
    /// Binds player state and skill loadout directly to the Vitometer for reactive combat telemetry.
    /// </summary>
    public void BindPlayer(PlayerState player, SkillLoadout loadout) => Vitometer?.Bind(player, loadout);

    /// <summary>
    /// Configures the status of the Guard Skill golden layer.
    /// </summary>
    public void SetGuardSkill(bool isActive, float currentAbsorb = 0f, float maxAbsorb = 0f, string name = "Steelskin") =>
        Vitometer?.SetGuardSkill(isActive, currentAbsorb, maxAbsorb, name);

    /// <summary>
    /// Updates all 5 flask slots from a collection.
    /// </summary>
    public void UpdateFlasks(IReadOnlyList<FlaskSlot> flasks) => Vitometer?.UpdateAllFlasks(flasks);

    /// <summary>
    /// Attempts to drink the flask in the given slot index (0 to 4).
    /// </summary>
    public bool TryDrinkFlask(int slotIndex) => Vitometer?.TryDrinkFlask(slotIndex) ?? false;

    /// <summary>
    /// Displays a high-priority loot announcement banner over the arena.
    /// </summary>
    public void ShowLootToast(string message, Color? accentColor = null, double durationSeconds = 3.0)
    {
        if (LootToastOverlay == null || _lootToastLabel == null) return;

        _lootToastLabel.Text = message;
        _lootToastLabel.Modulate = accentColor ?? new Color(1.0f, 0.85f, 0.3f);
        LootToastOverlay.Visible = true;
        _toastRemainingDuration = durationSeconds;
    }

    /// <summary>
    /// Dismisses the loot announcement banner immediately.
    /// </summary>
    public void HideLootToast()
    {
        if (LootToastOverlay != null)
        {
            LootToastOverlay.Visible = false;
        }
        _toastRemainingDuration = 0;
    }

    // ─────────────────────────────────────────────
    //  Arena 2D Helpers (Task 2.3)
    // ─────────────────────────────────────────────
    /// <summary>
    /// Updates the Exile's equipped weapon visual configuration in the 2D arena (Task 2.3).
    /// </summary>
    public void UpdateEquippedWeapons(WeaponBaseDefinition? mainHand, IGearItem? offHand) =>
        Arena?.SetEquippedWeapons(mainHand, offHand);

    /// <summary>
    /// Launches an attack from Exile towards active monsters with the specified damage type projectile.
    /// </summary>
    public void FireExileAttack(DamageType type = DamageType.Physical, Vector2? targetPosition = null) =>
        Arena?.FireExileAttack(type, targetPosition);

    /// <summary>
    /// Unified visual pipeline for executing attack animations and VFX for ANY combatant (Exile or Monster).
    /// </summary>
    public void ExecuteAttackVisual(CombatAttackEventArgs args) => Arena?.ExecuteAttackVisual(args);

    /// <summary>
    /// Triggers Exile melee attack visual arc in the 2D arena.
    /// </summary>
    public void FireExileMeleeAttack(Vector2? targetPosition, float range, DamageType type, float? arcAngle = null, bool isCrit = false) =>
        Arena?.FireExileMeleeAttack(targetPosition, range, type, arcAngle, isCrit);

    /// <summary>
    /// Spawns a custom projectile streak in the arena.
    /// </summary>
    public bool SpawnProjectile(Vector2 start, Vector2 target, DamageType type, float speed = 520f) =>
        Arena?.SpawnProjectile(start, target, type, speed) ?? false;

    /// <summary>
    /// Triggers projectile impact VFX (AoE explosion ring, screen shake, hit flashes).
    /// </summary>
    public void TriggerProjectileImpact(Vector2 targetPos, DamageType type, float aoeRadius = 0f, bool isCrit = false) =>
        Arena?.TriggerProjectileImpact(targetPos, type, aoeRadius, isCrit);

    /// <summary>
    /// Rotates the Exile to face directly toward the target position in the arena.
    /// </summary>
    public void FaceTarget(Vector2 targetPos) => Arena?.FaceTarget(targetPos);

    /// <summary>
    /// Sets the Exile's facing angle directly in radians.
    /// </summary>
    public void SetFacingAngle(float angle) => Arena?.SetFacingAngle(angle);

    /// <summary>
    /// Triggers Exile attack animation recoil.
    /// </summary>
    public void TriggerExileAttackRecoil(float recoil = 1.0f) => Arena?.TriggerExileAttackRecoil(recoil);

    /// <summary>
    /// Sets the target monster or position for continuous auto-facing.
    /// </summary>
    public void SetTarget(long? instanceId, Vector2? targetPos = null) => Arena?.SetTarget(instanceId, targetPos);

    /// <summary>
    /// Returns the weapon/wand tip position in 2D arena coordinates.
    /// </summary>
    public Vector2 GetWeaponTipPosition() => Arena?.GetWeaponTipPosition() ?? Arena2D.CenterPosition;

    /// <summary>
    /// Triggers a melee strike AoE indicator arc in the 2D arena.
    /// </summary>
    public void TriggerMeleeStrikeIndicator(
        Vector2 origin,
        float directionAngle,
        float range,
        DamageType damageType,
        bool isCrit = false,
        float? arcAngle = null) =>
        Arena?.TriggerMeleeStrikeIndicator(origin, directionAngle, range, damageType, isCrit, arcAngle);

    /// <summary>
    /// Triggers a Boss Slam telegraph warning indicator for lethal attacks (>70% player EHP).
    /// </summary>
    public void TriggerBossSlam(Vector2 targetPosition, float duration = 1.35f, float radius = 50f, float threatEhpPercent = 85f) =>
        Arena?.TriggerBossSlam(targetPosition, duration, radius, threatEhpPercent);

    /// <summary>
    /// Updates monsters displayed in the 2D arena from the simulation wave spawner.
    /// </summary>
    public void SetArenaMonsters(IReadOnlyList<MonsterInstance> monsters) => Arena?.SetMonsters(monsters);

    /// <summary>
    /// Updates monster remaining life in the 2D arena.
    /// </summary>
    public void UpdateArenaMonsterLife(long instanceId, float currentLife) => Arena?.UpdateMonsterLife(instanceId, currentLife);

    /// <summary>
    /// Handles monster damage presentation (queues onto in-flight projectile or applies immediately for melee/DoT).
    /// </summary>
    public void HandleMonsterDamageDealt(long monsterId, float damage, float newLife, Vector2 pos, DamageType type, bool isCrit, bool isDoT) =>
        Arena?.HandleMonsterDamageDealt(monsterId, damage, newLife, pos, type, isCrit, isDoT);

    /// <summary>
    /// Attempts to find the 2D visual screen position for the specified monster instance.
    /// </summary>
    public bool TryGetMonsterPosition(long instanceId, out Vector2 position)
    {
        if (Arena != null)
        {
            return Arena.TryGetMonsterPosition(instanceId, out position);
        }
        position = Vector2.Zero;
        return false;
    }

    /// <summary>
    /// Updates Exile world position and movement telemetry in the 2D arena for relative corridor rendering (Task 2.7).
    /// </summary>
    public void UpdateExileWorldPosition(Vector2 worldPos, ExileMovementState state = ExileMovementState.Idle, float speed = 0f, float mapLength = 2400f) =>
        Arena?.UpdateExileWorldPosition(worldPos, state, speed, mapLength);

    /// <summary>
    /// Triggers visual shatter effect for awakened dormant monsters (e.g. Essence Monolith).
    /// </summary>
    public void WakeUpAllMonsters() => Arena?.WakeUpAllMonsters();

    /// <summary>
    /// 2D Camera managing corridor follow and Boss Arena Lock (Task 2.8).
    /// </summary>
    public ArenaCamera? Camera => Arena?.Camera;

    /// <summary>
    /// True when the Fog Gate at 85% map progress has sealed shut behind the Exile (Task 2.8).
    /// </summary>
    public bool IsFogGateSealed => Arena?.IsFogGateSealed ?? false;

    /// <summary>
    /// Updates the Fog Gate sealed state in the arena viewport (Task 2.8).
    /// </summary>
    public void SetFogGateSealed(bool isSealed) => Arena?.SetFogGateSealed(isSealed);

    /// <summary>
    /// Awakens the Map Boss with visual shockwave, roaring screen shake, and 96px Overhead Boss Bar (Task 2.8).
    /// </summary>
    public void AwakenBoss() => Arena?.AwakenBoss();

    // ─────────────────────────────────────────────
    //  Combat DPS Telemetry & Sanctuary State (Prompt 4)
    // ─────────────────────────────────────────────
    /// <summary>
    /// Updates the real-time combat DPS counter on the Quick Footer bar (Prompt 4).
    /// </summary>
    public void UpdateDps(CombatDpsSnapshot snapshot)
    {
        LatestDpsSnapshot = snapshot;
        if (_dpsLabel == null) return;

        _dpsLabel.Text = $"DPS: {snapshot.CurrentDps:N0} • Peak: {snapshot.PeakDps:N0}";

        // Golden glow when hitting or setting new peak DPS
        if (snapshot.CurrentDps > 0f && snapshot.CurrentDps >= snapshot.PeakDps - 0.5f)
        {
            _dpsLabel.Modulate = new Color(1.0f, 0.88f, 0.35f);
        }
        else
        {
            _dpsLabel.Modulate = new Color(0.88f, 0.90f, 0.96f);
        }

        // Diagnostic hover tooltip with 5-element breakdown and attack metrics
        _dpsLabel.TooltipText = FormatDpsDiagnosticTooltip(snapshot);
    }

    /// <summary>
    /// Updates the real-time combat DPS counter from a CombatDpsTracker instance.
    /// </summary>
    public void UpdateDps(CombatDpsTracker tracker)
    {
        if (tracker == null) return;
        UpdateDps(tracker.GetSnapshot());
    }

    /// <summary>
    /// Builds the diagnostic breakdown tooltip for the Quick Footer DPS badge.
    /// </summary>
    public static string FormatDpsDiagnosticTooltip(CombatDpsSnapshot snapshot)
    {
        return $"COMBAT TELEMETRY & DPS BREAKDOWN\n" +
               $"───────────────────────────────────\n" +
               $"• Current DPS:    {snapshot.CurrentDps:N0} /s\n" +
               $"• Peak Map DPS:   {snapshot.PeakDps:N0} /s\n" +
               $"• Average DPS:    {snapshot.AverageDps:N0} /s\n" +
               $"───────────────────────────────────\n" +
               $"DAMAGE BREAKDOWN:\n" +
               $"🗡 Physical:      {snapshot.PhysicalPercent:F1}%\n" +
               $"🔥 Fire:          {snapshot.FirePercent:F1}%\n" +
               $"❄ Cold:          {snapshot.ColdPercent:F1}%\n" +
               $"⚡ Lightning:     {snapshot.LightningPercent:F1}%\n" +
               $"🧪 Chaos:         {snapshot.ChaosPercent:F1}%\n" +
               $"───────────────────────────────────\n" +
               $"ATTACK METRICS:\n" +
               $"• Accuracy:       {snapshot.AccuracyPercent:F1}%\n" +
               $"• Hit Rate:       {snapshot.HitRateAps:F2} APS";
    }

    /// <summary>
    /// Sets Sanctuary safe haven status in the Quick Footer and vitals telemetry (Prompt 4).
    /// </summary>
    public void SetSanctuaryStatus()
    {
        if (_statusLabel != null)
        {
            _statusLabel.Text = "Crucible Online • Sanctuary Safe Haven (100% Recovered)";
        }
        if (_dpsLabel != null)
        {
            _dpsLabel.Text = "DPS: 0 • Peak: 0";
            _dpsLabel.Modulate = new Color(0.70f, 0.75f, 0.82f);
            _dpsLabel.TooltipText = "The Crucible Sanctuary • Safe Haven\nAll vitals fully restored (100%). Zero combat threat.";
        }
    }

#if DEBUG
    // ─────────────────────────────────────────────
    //  Developer Debug Telemetry Overlay (STRIPPED IN RELEASE BUILDS)
    // ─────────────────────────────────────────────
    private PanelContainer? _debugOverlayPanel;
    private Label? _debugCoordLabel;

    private void CreateDebugOverlay()
    {
        if (!OS.IsDebugBuild()) return;

        var style = new StyleBoxFlat
        {
            BgColor = new Color(0.04f, 0.05f, 0.07f, 0.88f),
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            BorderColor = new Color(0.20f, 0.45f, 0.75f, 0.90f),
            CornerRadiusTopLeft = 3,
            CornerRadiusTopRight = 3,
            CornerRadiusBottomRight = 3,
            CornerRadiusBottomLeft = 3,
            ContentMarginLeft = 6,
            ContentMarginRight = 6,
            ContentMarginTop = 2,
            ContentMarginBottom = 2
        };

        _debugCoordLabel = new Label
        {
            Name = "DebugCoordLabel",
            Text = "[DEBUG] P_E: (320.0, 86.0) | Idle | 0px/s | Dist: 0/2400px (0.0%)",
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center
        };
        _debugCoordLabel.AddThemeFontSizeOverride("font_size", 10);
        _debugCoordLabel.AddThemeColorOverride("font_color", new Color(0.40f, 0.85f, 1.0f, 0.95f));

        _debugOverlayPanel = new PanelContainer
        {
            Name = "DebugCoordinateOverlay",
            MouseFilter = MouseFilterEnum.Ignore,
            Position = new Vector2(8, 36),
            ZIndex = 50
        };
        _debugOverlayPanel.AddThemeStyleboxOverride("panel", style);
        _debugOverlayPanel.AddChild(_debugCoordLabel);

        AddChild(_debugOverlayPanel);
    }

    /// <summary>
    /// Live telemetry update for developer debug label. Automatically compiled out in Release builds.
    /// </summary>
    public void UpdateDebugCoordinates(Vector2 worldPos, ExileMovementState state, float speed, float distanceTraveled, float mapLength, float progressPct)
    {
        if (!OS.IsDebugBuild() || _debugCoordLabel == null) return;
        _debugCoordLabel.Text = $"[DEBUG] P_E: ({worldPos.X:F1}, {worldPos.Y:F1}) | {state} | {speed:F0}px/s | Dist: {distanceTraveled:F0}/{mapLength:F0}px ({progressPct:F1}%)";
    }
#endif

    // ─────────────────────────────────────────────
    //  Floating Combat Text API (Task 2.5)
    // ─────────────────────────────────────────────
    /// <summary>
    /// Spawns a floating combat text damage number in the arena.
    /// </summary>
    public void SpawnFloatingDamage(float damage, Vector2 position, DamageType type, bool isCrit = false) =>
        FloatingText?.SpawnDamageText(damage, position, type, isCrit);

    /// <summary>
    /// Spawns floating combat text from a calculated HitResult.
    /// </summary>
    public void SpawnFloatingHit(HitResult hit, Vector2 position) =>
        FloatingText?.SpawnHitResult(hit, position);

    /// <summary>
    /// Spawns custom floating text (e.g. "DODGED", "BLOCKED", "IMMUNE").
    /// </summary>
    public void SpawnFloatingText(string text, Vector2 position, Color color, bool isCrit = false) =>
        FloatingText?.SpawnCustomText(
            text,
            position,
            color,
            isCrit ? FloatingTextManager.ColorGoldOutline : FloatingTextManager.ColorBlackOutline,
            isCrit ? FloatingTextManager.CritFontSize : FloatingTextManager.NormalFontSize,
            isCrit ? FloatingTextManager.CritOutlineSize : FloatingTextManager.NormalOutlineSize,
            isCrit
        );
}
