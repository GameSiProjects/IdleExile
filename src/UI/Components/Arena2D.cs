using System;
using System.Collections.Generic;
using Godot;
using IdleExile.Core.Models;
using IdleExile.Core.Services;
using IdleExile.Core.Simulation;
using IdleExile.Core.State;

namespace IdleExile.UI.Components;

/// <summary>
/// Pending visual damage and life update queued to detonate upon projectile impact.
/// </summary>
public struct PendingDamageEntry
{
    public long MonsterId;
    public float Damage;
    public float NewLife;
    public bool IsCrit;
    public Vector2 Position;
}

/// <summary>
/// Data structure representing an active in-flight projectile streak in 2D space.
/// Pre-allocated in an object pool to guarantee zero runtime GC allocations.
/// </summary>
public struct ProjectileData
{
    public bool IsActive;
    public Vector2 StartPosition;
    public Vector2 TargetPosition;
    public Vector2 CurrentPosition;
    public float Progress;
    public float Speed;
    public DamageType DamageType;
    public float StreakLength;
    public Color BaseColor;
    public Color GlowColor;
    public float AoeRadius;
    public bool IsCrit;
    public long TargetMonsterId;
}

/// <summary>
/// Data structure representing a monster's 2D top-down visual token.
/// Maintained in a fixed-size pool for seamless rendering without node churning.
/// </summary>
public sealed class MonsterVisualToken
{
    public long InstanceId;
    public string Name = string.Empty;
    public MonsterRarity Rarity;
    public float MaxLife;
    public float CurrentLife;
    public bool IsAlive;
    public Vector2 Position;
    public Vector2 BasePosition;
    public float Scale = 1.0f;
    public float HitFlashTimer;
    public float DeathFadeTimer;
    public float BobbingPhase;
    public Color CustomColor;
    public bool IsDormant;
    public bool IsAggroed;
    public LeagueEncounterType EncounterType;
    public bool IsAwakened = true;
    public float AwakeningTimer;
    public float AwakeningShockwaveRadius;
    public float AttackLungeTimer;
    public Vector2 AttackLungeOffset;
}

/// <summary>
/// Boss Slam Telegraph warning state.
/// Implements the 2D circular shrinking indicator ring (1.2s - 1.5s wind-up) for lethal attacks (>70% EHP).
/// </summary>
public struct BossSlamTelegraph
{
    public bool IsActive;
    public Vector2 Position;
    public float Radius;
    public float TotalDuration;
    public float ElapsedDuration;
    public float ThreatEhpPercent;
    public float ScreenShakeIntensity;

    public readonly float Progress => TotalDuration > 0f ? Mathf.Clamp(ElapsedDuration / TotalDuration, 0f, 1f) : 1f;
    public readonly bool IsReadyToDetonate => ElapsedDuration >= TotalDuration;
}

/// <summary>
/// 2D Top-Down Combat Arena renderer component for the Battle Viewport (440 x 208 px).
/// Senior Game Design Shift:
/// 1. 2D Top-Down / Angled Bird's-Eye (~60° Tilt) arena with stone paver flagstone floor & vignette.
/// 2. Exile Character positioned at the Focal Center (220, 104) with 360° circular aura and dynamic auto-facing.
/// 3. Monster packs arranged in 2D formations (Swarm surround, flanks, elite with minions, Boss with overhead bar).
/// 4. 360° Projectile multi-vector flight, PoE 5-elemental color coding, and visual clamping (max 12).
/// 5. True 2D circular Boss Slam Telegraph indicator (shrinking red ring) with screen shake impact.
/// </summary>
public partial class Arena2D : Node2D
{
    // ─────────────────────────────────────────────
    //  Constants & Dimensions
    // ─────────────────────────────────────────────
    public const float ArenaWidth = 440f;
    public const float ArenaHeight = 208f;
    public static readonly Vector2 CenterPosition = new(ArenaWidth * 0.5f, ArenaHeight * 0.5f); // (220f, 104f)
    public const float GroundBaselineY = 104f; // Legacy alias for CenterY in Top-Down
    public const int MaxProjectiles = 12;
    public const int MaxMonsters = 16;

    // ─────────────────────────────────────────────
    //  PoE 5 Elemental & Ailment Color Tokens
    // ─────────────────────────────────────────────
    public static readonly Color ColorPhysical = new(0.88f, 0.88f, 0.88f, 1.0f);
    public static readonly Color ColorFire = new(1.0f, 0.34f, 0.13f, 1.0f);
    public static readonly Color ColorCold = new(0.0f, 0.90f, 1.0f, 1.0f);
    public static readonly Color ColorLightning = new(1.0f, 0.92f, 0.0f, 1.0f);
    public static readonly Color ColorChaos = new(0.83f, 0.0f, 0.97f, 1.0f);

    public static readonly Color ColorRareAura = new(1.0f, 0.84f, 0.0f, 0.45f);
    public static readonly Color ColorMagicAura = new(0.25f, 0.65f, 1.0f, 0.40f);
    public static readonly Color ColorBossAura = new(0.9f, 0.15f, 0.15f, 0.55f);

    // ─────────────────────────────────────────────
    //  Top-Down Floor & Atmospheric State
    // ─────────────────────────────────────────────
    private float _floorTime;
    [Export] public float ParallaxSpeedFar = 8.0f; // Preserved for backwards compat
    [Export] public float ParallaxSpeedMid = 22.0f;
    [Export] public float ParallaxSpeedNear = 45.0f;

    // ─────────────────────────────────────────────
    //  Exile Character State (Top-Down)
    // ─────────────────────────────────────────────
    public Vector2 ExilePosition { get; set; } = CenterPosition;
    public Vector2 ExileWorldPosition { get; set; } = new(40f, 104f);
    public ExileMovementState MovementState { get; set; } = ExileMovementState.Idle;
    public float MovementSpeed { get; set; } = 0f;
    public float MapLength { get; set; } = 2400f;
    private float _exileFacingAngle; // In radians
    public float ExileFacingAngle => _exileFacingAngle;
    private float _exileAttackRecoil;
    private float _exileAuraPulsePhase;

    /// <summary>
    /// Camera2D controller managing corridor follow, look-ahead offset, and Boss Arena lock (Task 2.6 & 2.8).
    /// </summary>
    public ArenaCamera Camera { get; } = new();

    /// <summary>
    /// True when the Fog Gate threshold at 85% map progress has been sealed shut behind the Exile (Task 2.8).
    /// </summary>
    public bool IsFogGateSealed { get; set; } = false;

    /// <summary>
    /// Active ground slam impact craters fading out over time.
    /// </summary>
    private readonly List<(Vector2 Position, float Timer)> _slamCraters = new();

    /// <summary>
    /// Updates the Fog Gate sealed status and triggers visual redraw (Task 2.8).
    /// </summary>
    public void SetFogGateSealed(bool sealedState)
    {
        IsFogGateSealed = sealedState;
        QueueRedraw();
    }

    /// <summary>
    /// Awaken Map Boss with shockwave ring, roaring screen shake, and Overhead Boss Bar charge-up (Task 2.8).
    /// </summary>
    public void AwakenBoss()
    {
        for (int i = 0; i < _activeMonsterCount; i++)
        {
            var m = _monsters[i];
            if (m.Rarity == MonsterRarity.Boss && !m.IsAwakened)
            {
                m.IsAwakened = true;
                m.AwakeningTimer = 1.0f;
                ScreenShake(3.5f, 0.25f);
            }
        }
    }

    /// <summary>
    /// Explicit target position currently tracked by the Exile (e.g. from CombatEngine.CurrentTarget).
    /// </summary>
    public Vector2? TargetPosition { get; set; }

    /// <summary>
    /// Instance ID of the current target monster for priority Auto-Facing.
    /// </summary>
    public long? TargetMonsterInstanceId { get; set; }

    // ─────────────────────────────────────────────
    //  Equipped Weapons State (for accurate 2D rendering)
    // ─────────────────────────────────────────────
    public WeaponBaseDefinition? MainHandWeapon { get; private set; }
    public IGearItem? OffHandItem { get; private set; }

    /// <summary>
    /// Updates the Exile's equipped weapon set and triggers a visual redraw.
    /// </summary>
    public void SetEquippedWeapons(WeaponBaseDefinition? mainHand, IGearItem? offHand)
    {
        MainHandWeapon = mainHand;
        OffHandItem = offHand;
        QueueRedraw();
    }

    // ─────────────────────────────────────────────
    //  Entity & Projectile Pools (Zero GC per frame)
    // ─────────────────────────────────────────────
    private readonly ProjectileData[] _projectiles = new ProjectileData[MaxProjectiles];
    private readonly List<PendingDamageEntry>[] _projectilePendingHits = new List<PendingDamageEntry>[MaxProjectiles];
    private readonly MonsterVisualToken[] _monsters = new MonsterVisualToken[MaxMonsters];
    private int _activeMonsterCount;

    // ─────────────────────────────────────────────
    //  Boss Slam Telegraph & Screen Shake State
    // ─────────────────────────────────────────────
    private BossSlamTelegraph _telegraph;
    private float _screenShakeTimer;
    private float _screenShakeMagnitude;
    private Vector2 _shakeOffset = Vector2.Zero;

    // ─────────────────────────────────────────────
    //  Melee Strike AoE Indicators (Object Pool)
    // ─────────────────────────────────────────────
    public const int MaxMeleeIndicators = 8;
    private readonly MeleeStrikeIndicator[] _meleeIndicators = new MeleeStrikeIndicator[MaxMeleeIndicators];

    /// <summary>
    /// Pooled data for a melee attack AoE indicator.
    /// Renders as an expanding directional arc (120° sweep) from the monster's position toward the Exile,
    /// colored by damage type, with crit attacks getting a wider/brighter indicator.
    /// Lifetime is 0.3s — fast and punchy to match the 30Hz tick rhythm.
    /// </summary>
    public struct MeleeStrikeIndicator
    {
        public bool IsActive;
        /// <summary>World position of the attacking monster.</summary>
        public Vector2 Origin;
        /// <summary>Angle in radians from monster toward Exile.</summary>
        public float DirectionAngle;
        /// <summary>Attack reach radius in pixels.</summary>
        public float Range;
        /// <summary>Total lifespan in seconds (0.3s default).</summary>
        public float TotalDuration;
        /// <summary>Elapsed time since trigger.</summary>
        public float Elapsed;
        /// <summary>Base color for the arc (damage type).</summary>
        public Color ArcColor;
        /// <summary>Arc aperture in radians (normal: 2.1 rad ≈ 120°, crit: 2.6 rad ≈ 150°).</summary>
        public float ArcAngle;
        /// <summary>True for crit strikes (wider, brighter).</summary>
        public bool IsCrit;

        public readonly float Progress => TotalDuration > 0f ? Mathf.Clamp(Elapsed / TotalDuration, 0f, 1f) : 1f;
    }

    // ─────────────────────────────────────────────
    //  Events & Callbacks
    // ─────────────────────────────────────────────
    public event Action<Vector2, float>? OnBossSlamDetonated;
    public event Action<Vector2, DamageType>? OnProjectileImpact;

    // ─────────────────────────────────────────────
    //  Subcomponents & Layers (Task 2.5)
    // ─────────────────────────────────────────────
    public FloatingTextManager? FloatingText { get; set; }

    // ─────────────────────────────────────────────
    //  Public Query Properties
    // ─────────────────────────────────────────────
    public bool IsTelegraphActive => _telegraph.IsActive;
    public float TelegraphProgress => _telegraph.Progress;
    public int ActiveProjectileCount
    {
        get
        {
            int count = 0;
            for (int i = 0; i < _projectiles.Length; i++)
            {
                if (_projectiles[i].IsActive) count++;
            }
            return count;
        }
    }
    public int ActiveMeleeIndicatorCount
    {
        get
        {
            int count = 0;
            for (int i = 0; i < _meleeIndicators.Length; i++)
            {
                if (_meleeIndicators[i].IsActive) count++;
            }
            return count;
        }
    }
    public int ActiveMonsterCount => _activeMonsterCount;

    public Arena2D()
    {
        for (int i = 0; i < MaxProjectiles; i++)
        {
            _projectilePendingHits[i] = new List<PendingDamageEntry>(4);
        }
    }

    public override void _Ready()
    {
        // Pre-initialize monster tokens to avoid allocations during combat
        for (int i = 0; i < MaxMonsters; i++)
        {
            _monsters[i] = new MonsterVisualToken();
        }

        for (int i = 0; i < MaxProjectiles; i++)
        {
            _projectilePendingHits[i] ??= new List<PendingDamageEntry>(4);
        }

        // Initialize default mock monsters in 2D top-down arrangement
        SpawnDefaultPackPreview();

        // Connect floating text layer if present in scene tree
        FloatingText ??= GetNodeOrNull<FloatingTextManager>("%FloatingCombatTextLayer")
                         ?? GetNodeOrNull<FloatingTextManager>("FloatingCombatTextLayer");
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        _floorTime += dt;

        // 0. Update Camera Tracking & Arena Lock (Task 2.6 & Task 2.8)
        Vector2 bossCenterWorld = new(MapLength * ExileMovementController.BossArenaRatio, CenterPosition.Y);
        bool inBossPhase = IsFogGateSealed || MovementState == ExileMovementState.EnteringBossRoom || MovementState == ExileMovementState.InBossArena;
        Camera.Update(dt, ExileWorldPosition, bossCenterWorld, inBossPhase);

        if (inBossPhase)
        {
            AwakenBoss();
        }

        // Update visual ExilePosition relative to camera
        if (MovementState == ExileMovementState.Idle && !inBossPhase)
        {
            ExilePosition = CenterPosition;
        }
        else
        {
            ExilePosition = Camera.WorldToScreen(ExileWorldPosition, CenterPosition);
        }

        // Update ground slam craters
        for (int i = _slamCraters.Count - 1; i >= 0; i--)
        {
            var (cPos, cTime) = _slamCraters[i];
            cTime -= dt;
            if (cTime <= 0f)
            {
                _slamCraters.RemoveAt(i);
            }
            else
            {
                _slamCraters[i] = (cPos, cTime);
            }
        }

        // 1. Update Exile Dynamic Facing towards nearest active monster
        UpdateExileFacing(dt);

        // 2. Update Exile Aura & Recoil
        _exileAuraPulsePhase += dt * 2.5f;
        if (_exileAttackRecoil > 0f)
        {
            _exileAttackRecoil = Mathf.Max(0f, _exileAttackRecoil - dt * 6.0f);
        }

        // 3. Update Monsters Animation States
        for (int i = 0; i < _activeMonsterCount; i++)
        {
            var m = _monsters[i];
            m.BobbingPhase += dt * 2.8f;
            if (m.AttackLungeTimer > 0f)
            {
                m.AttackLungeTimer = Mathf.Max(0f, m.AttackLungeTimer - dt);
            }
            if (m.HitFlashTimer > 0f)
            {
                m.HitFlashTimer = Mathf.Max(0f, m.HitFlashTimer - dt * 7f);
            }
            if (!m.IsAlive && m.DeathFadeTimer > 0f)
            {
                m.DeathFadeTimer = Mathf.Max(0f, m.DeathFadeTimer - dt * 2.5f);
            }
            if (m.AwakeningTimer > 0f)
            {
                m.AwakeningTimer = Mathf.Max(0f, m.AwakeningTimer - dt);
                m.AwakeningShockwaveRadius = Mathf.Lerp(130f, 0f, m.AwakeningTimer / 1.0f);
            }
        }

        // 4. Update In-Flight 2D Projectiles
        for (int i = 0; i < MaxProjectiles; i++)
        {
            if (!_projectiles[i].IsActive) continue;

            ref var p = ref _projectiles[i];
            float dist = p.StartPosition.DistanceTo(p.TargetPosition);
            if (dist <= 0.001f)
            {
                p.IsActive = false;
                continue;
            }

            p.Progress += (p.Speed * dt) / dist;
            p.CurrentPosition = p.StartPosition.Lerp(p.TargetPosition, Mathf.Clamp(p.Progress, 0f, 1f));

            if (p.Progress >= 1.0f)
            {
                p.IsActive = false;
                TriggerProjectileImpact(p.TargetPosition, p.DamageType, p.AoeRadius, p.IsCrit);

                var hits = _projectilePendingHits[i];
                for (int h = 0; h < hits.Count; h++)
                {
                    var entry = hits[h];
                    UpdateMonsterLife(entry.MonsterId, entry.NewLife);
                    FloatingText?.SpawnDamageText(entry.Damage, entry.Position, p.DamageType, entry.IsCrit);
                }
                hits.Clear();
            }
        }

        // 5. Update Boss Slam Telegraph Indicator
        if (_telegraph.IsActive)
        {
            _telegraph.ElapsedDuration += dt;
            if (_telegraph.IsReadyToDetonate)
            {
                DetonateBossSlam();
            }
        }

        // 6. Update Melee Strike AoE Indicators
        for (int i = 0; i < MaxMeleeIndicators; i++)
        {
            if (!_meleeIndicators[i].IsActive) continue;
            _meleeIndicators[i].Elapsed += dt;
            if (_meleeIndicators[i].Elapsed >= _meleeIndicators[i].TotalDuration)
            {
                _meleeIndicators[i].IsActive = false;
            }
        }

        // 7. Update Screen Shake
        if (_screenShakeTimer > 0f)
        {
            _screenShakeTimer -= dt;
            if (_screenShakeTimer <= 0f)
            {
                _shakeOffset = Vector2.Zero;
            }
            else
            {
                float factor = _screenShakeTimer / 0.15f;
                _shakeOffset = new Vector2(
                    (float)(GD.Randf() * 2.0 - 1.0) * _screenShakeMagnitude * factor,
                    (float)(GD.Randf() * 2.0 - 1.0) * _screenShakeMagnitude * factor
                );
            }
            Position = _shakeOffset;
        }

        // 8. Request redraw
        QueueRedraw();
    }

    private void UpdateExileFacing(float dt)
    {
        Vector2? target = GetCurrentTargetPosition();
        if (target.HasValue)
        {
            Vector2 toTarget = target.Value - ExilePosition;
            if (toTarget.LengthSquared() > 0.01f)
            {
                float desiredAngle = toTarget.Angle();
                // Smooth angle interpolation
                _exileFacingAngle = Mathf.LerpAngle(_exileFacingAngle, desiredAngle, dt * 10f);
                _exileFacingAngle = Mathf.Wrap(_exileFacingAngle, -Mathf.Pi, Mathf.Pi);
            }
        }
        else
        {
            // Out of combat: smoothly face forward marching direction (+X rightward in corridor)
            float desiredAngle = 0f;
            _exileFacingAngle = Mathf.LerpAngle(_exileFacingAngle, desiredAngle, dt * 6f);
            _exileFacingAngle = Mathf.Wrap(_exileFacingAngle, -Mathf.Pi, Mathf.Pi);
        }
    }

    public override void _Draw()
    {
        // 1. Draw Top-Down Flagstone Floor & Dungeon Tiles
        DrawTopDownFloor();

        // 2. Draw Ground Slam Impact Craters (underneath characters and indicators)
        DrawSlamCraters();

        // 3. Draw Boss Slam Telegraph Indicator (Underneath characters)
        if (_telegraph.IsActive)
        {
            DrawBossSlamTelegraph();
        }

        // 4. Draw Melee Strike AoE Indicators (Underneath characters, above floor)
        DrawMeleeStrikeIndicators();

        // 5. Draw Monster Tokens, 360° Auras & Overhead Health Bars
        DrawMonsters();

        // 6. Draw Exile Character with 360° Aura & Facing Vector
        DrawExileCharacter();

        // 7. Draw 360° In-Flight Projectiles
        DrawProjectiles();
    }

    // ─────────────────────────────────────────────
    //  Rendering: Top-Down Dungeon Stone Floor
    // ─────────────────────────────────────────────
    private void DrawTopDownFloor()
    {
        // 1. Base dark stone foundation slab
        DrawRect(new Rect2(0, 0, ArenaWidth, ArenaHeight), new Color(0.038f, 0.041f, 0.055f, 1.0f));

        // Use Camera position for smooth relative scrolling in corridor and fixed arena lock (Task 2.6 & 2.8)
        float camX = Camera.CurrentPosition.X;

        // 2. Playable Corridor Flagstone Tiles (between MinCorridorY=32 and MaxCorridorY=176)
        float tileW = 36f;
        float tileH = 28f;
        float topY = ExileMovementController.MinCorridorY;
        float bottomY = ExileMovementController.MaxCorridorY;
        int cols = Mathf.CeilToInt(ArenaWidth / tileW) + 2;
        int rows = Mathf.CeilToInt((bottomY - topY) / tileH) + 1;

        float scrollX = -Mathf.PosMod(camX, tileW);
        int tileWorldColBase = Mathf.FloorToInt(camX / tileW);

        Color groutColor = new(0.022f, 0.025f, 0.035f, 0.95f);
        Color tileColorA = new(0.058f, 0.064f, 0.082f, 0.85f);
        Color tileColorB = new(0.048f, 0.054f, 0.070f, 0.85f);

        for (int c = -1; c <= cols; c++)
        {
            int worldC = tileWorldColBase + c;
            float colOffset = ((worldC % 2 + 2) % 2 == 1) ? (tileH * 0.5f) : 0f;
            float x = c * tileW + scrollX;

            for (int r = -1; r <= rows + 1; r++)
            {
                float y = topY + r * tileH + colOffset;
                if (y + tileH < topY || y > bottomY)
                    continue;

                float drawY = Mathf.Max(y, topY);
                float drawH = Mathf.Min(y + tileH, bottomY) - drawY;
                if (drawH <= 0.5f) continue;

                Rect2 tileRect = new(x + 0.5f, drawY + 0.5f, tileW - 1f, drawH - 1f);

                // Deterministic tile shading based on world coordinates (worldC, r)
                int shadeIndex = ((worldC + r) % 2 + 2) % 2;
                Color col = (shadeIndex == 0) ? tileColorA : tileColorB;
                DrawRect(tileRect, col);
            }

            // Vertical grout seams
            DrawLine(new Vector2(x, topY), new Vector2(x, bottomY), groutColor, 1.0f);
        }

        // 3. Top & Bottom Walls (Dungeon corridor: Top 0..32, Bottom 176..208)
        Color wallBase = new(0.020f, 0.022f, 0.030f, 1.0f);
        Color curbBorder = new(0.18f, 0.20f, 0.26f, 0.95f);
        Color curbHighlight = new(0.28f, 0.32f, 0.42f, 0.70f);

        // Top wall slab & Bottom wall slab
        DrawRect(new Rect2(0, 0, ArenaWidth, topY), wallBase);
        DrawRect(new Rect2(0, bottomY, ArenaWidth, ArenaHeight - bottomY), wallBase);

        // Wall stone seams scrolling with camera X
        for (int c = -1; c <= cols; c++)
        {
            float wx = c * tileW + scrollX;
            DrawLine(new Vector2(wx, 0), new Vector2(wx, topY), groutColor, 1.2f);
            DrawLine(new Vector2(wx, bottomY), new Vector2(wx, ArenaHeight), groutColor, 1.2f);
        }

        // Curb edges (Horizontal lines at topY and bottomY)
        DrawLine(new Vector2(0, topY), new Vector2(ArenaWidth, topY), curbBorder, 2.0f);
        DrawLine(new Vector2(0, topY + 1f), new Vector2(ArenaWidth, topY + 1f), curbHighlight, 1.0f);

        DrawLine(new Vector2(0, bottomY), new Vector2(ArenaWidth, bottomY), curbBorder, 2.0f);
        DrawLine(new Vector2(0, bottomY - 1f), new Vector2(ArenaWidth, bottomY - 1f), curbHighlight, 1.0f);

        // 4. Boss Room Chamber Dais (Task 2.8) - rendered if visible on screen
        float bossCenterWorldX = MapLength * ExileMovementController.BossArenaRatio;
        Vector2 bossCenterScreen = Camera.WorldToScreen(new Vector2(bossCenterWorldX, CenterPosition.Y), CenterPosition);
        if (bossCenterScreen.X >= -140f && bossCenterScreen.X <= ArenaWidth + 140f)
        {
            DrawBossRoomChamber(bossCenterScreen);
        }

        // 5. Roadside Sconces / Torches (spaced every 140px along corridor in world X)
        float torchSpacing = 140f;
        int torchBase = Mathf.FloorToInt((camX - 140f) / torchSpacing);
        for (int t = torchBase; t <= torchBase + 5; t++)
        {
            float worldTorchX = t * torchSpacing;
            float screenX = Camera.WorldToScreen(new Vector2(worldTorchX, 0), CenterPosition).X;
            if (screenX >= -25f && screenX <= ArenaWidth + 25f)
            {
                DrawCorridorTorch(new Vector2(screenX, topY - 3f), isTop: true);
                DrawCorridorTorch(new Vector2(screenX, bottomY + 3f), isTop: false);
            }
        }

        // 6. Start Portal Dais (Milestone Altar at World X = 40f)
        float startDaisScreenX = Camera.WorldToScreen(new Vector2(40f, 0), CenterPosition).X;
        if (startDaisScreenX >= -60f && startDaisScreenX <= ArenaWidth + 60f)
        {
            Vector2 daisCenter = new(startDaisScreenX, CenterPosition.Y);
            Color runicOuter = new(0.20f, 0.45f, 0.85f, 0.45f);
            Color runicInner = new(0.12f, 0.25f, 0.45f, 0.25f);

            DrawCircle(daisCenter, 44f, runicInner);
            DrawArc(daisCenter, 44f, 0, Mathf.Tau, 36, runicOuter, 1.8f);
            DrawArc(daisCenter, 28f, 0, Mathf.Tau, 24, runicOuter, 1.2f);

            for (int i = 0; i < 8; i++)
            {
                float angle = i * (Mathf.Tau / 8f);
                Vector2 dir = new(Mathf.Cos(angle), Mathf.Sin(angle));
                DrawLine(daisCenter + dir * 28f, daisCenter + dir * 44f, runicOuter, 1.0f);
            }
        }

        // 7. Fog Gate Threshold (at 85% of MapLength) (Task 2.8)
        float fogGateWorldX = MapLength * ExileMovementController.FogGateRatio;
        float fogGateScreenX = Camera.WorldToScreen(new Vector2(fogGateWorldX, 0), CenterPosition).X;
        if (fogGateScreenX >= -30f && fogGateScreenX <= ArenaWidth + 30f)
        {
            DrawFogGate(fogGateScreenX, IsFogGateSealed);
        }

        // 8. Ambient drifting atmospheric dust motes
        Color moteColor = new(0.42f, 0.50f, 0.68f, 0.30f);
        for (int i = 0; i < 8; i++)
        {
            float mx = (i * 55f + _floorTime * (10f + (i % 3) * 5f)) % ArenaWidth;
            float my = (25f + (i * 24f) + Mathf.Sin(_floorTime * 1.5f + i) * 8f) % ArenaHeight;
            DrawCircle(new Vector2(mx, my), 1.2f, moteColor);
        }

        // 9. Vignette edge shadows
        Color vignetteEdge = new(0.015f, 0.018f, 0.025f, 0.65f);
        DrawRect(new Rect2(0, 0, ArenaWidth, 6), vignetteEdge);
        DrawRect(new Rect2(0, ArenaHeight - 6, ArenaWidth, 6), vignetteEdge);
    }

    private void DrawSlamCraters()
    {
        for (int c = 0; c < _slamCraters.Count; c++)
        {
            var (pos, timer) = _slamCraters[c];
            float alpha = Mathf.Clamp(timer / 3.0f, 0f, 1f);

            // Cracked stone circle scorch
            DrawCircle(pos, 52f, new Color(0.08f, 0.02f, 0.02f, 0.70f * alpha));
            DrawArc(pos, 52f, 0, Mathf.Tau, 24, new Color(0.45f, 0.12f, 0.06f, 0.60f * alpha), 2f);

            // Radial crack fissures
            for (int i = 0; i < 6; i++)
            {
                float angle = i * (Mathf.Tau / 6f) + 0.35f;
                Vector2 dir = new(Mathf.Cos(angle), Mathf.Sin(angle));
                DrawLine(pos, pos + dir * 48f, new Color(0.95f, 0.32f, 0.06f, 0.75f * alpha), 1.5f);
            }

            // Glowing ember core
            DrawCircle(pos, 14f, new Color(1.0f, 0.38f, 0.08f, 0.40f * alpha));
        }
    }

    private void DrawFogGate(float screenX, bool isSealed)
    {
        float topY = ExileMovementController.MinCorridorY;
        float bottomY = ExileMovementController.MaxCorridorY;
        float shimmer = 0.7f + Mathf.Sin(_floorTime * 4.5f) * 0.3f;

        if (!isSealed)
        {
            // Open / Approaching Fog Gate: Ethereal golden mist shimmer (Task 2.8)
            Color mistGlow = new(1.0f, 0.85f, 0.35f, 0.35f * shimmer);
            Color mistCore = new(0.98f, 0.82f, 0.28f, 0.75f * shimmer);
            Color mistLine = new(1.0f, 0.95f, 0.70f, 0.90f);

            // Broad mist field
            DrawRect(new Rect2(screenX - 10f, topY, 20f, bottomY - topY), new Color(1.0f, 0.80f, 0.20f, 0.12f * shimmer));
            // Thick luminous glow
            DrawLine(new Vector2(screenX, topY), new Vector2(screenX, bottomY), mistGlow, 12.0f);
            DrawLine(new Vector2(screenX, topY), new Vector2(screenX, bottomY), mistCore, 4.0f);
            DrawLine(new Vector2(screenX, topY), new Vector2(screenX, bottomY), mistLine, 1.5f);

            // Shimmering horizontal wisps
            for (int w = 0; w < 10; w++)
            {
                float wy = topY + (w * ((bottomY - topY) / 10f)) + Mathf.Sin(_floorTime * 3f + w) * 5f;
                float ww = 12f + Mathf.Sin(_floorTime * 5f + w * 2f) * 6f;
                DrawLine(new Vector2(screenX - ww, wy), new Vector2(screenX + ww, wy), new Color(1.0f, 0.88f, 0.4f, 0.25f * shimmer), 1.5f);
            }
        }
        else
        {
            // Sealed Fog Gate: Impenetrable dark crimson / fiery runic barrier with iron spikes (Task 2.8)
            float pulse = 0.8f + Mathf.Sin(_floorTime * 6f) * 0.2f;
            Color barrierGlow = new(0.85f, 0.08f, 0.08f, 0.45f * pulse);
            Color barrierCore = new(1.0f, 0.18f, 0.12f, 0.95f);
            Color ironBar = new(0.18f, 0.18f, 0.22f, 0.95f);

            // Pulsing crimson danger wash
            DrawRect(new Rect2(screenX - 14f, topY, 28f, bottomY - topY), new Color(0.6f, 0.02f, 0.02f, 0.25f * pulse));
            DrawLine(new Vector2(screenX, topY), new Vector2(screenX, bottomY), barrierGlow, 16.0f);
            DrawLine(new Vector2(screenX, topY), new Vector2(screenX, bottomY), barrierCore, 3.5f);

            // Iron barrier lattice & runic spikes (preventing backtracking to the left)
            for (int s = 0; s < 10; s++)
            {
                float sy = topY + s * ((bottomY - topY) / 9f);
                DrawLine(new Vector2(screenX - 12f, sy), new Vector2(screenX + 12f, sy), ironBar, 2.0f);
                // Spike head pointing toward corridor (west / left)
                DrawLine(new Vector2(screenX - 6f, sy - 3f), new Vector2(screenX - 13f, sy), ironBar, 1.5f);
                DrawLine(new Vector2(screenX - 6f, sy + 3f), new Vector2(screenX - 13f, sy), ironBar, 1.5f);
            }

            // Warning runes on the barrier
            for (int r = 0; r < 4; r++)
            {
                float ry = topY + 25f + r * 35f;
                DrawCircle(new Vector2(screenX, ry), 4f, new Color(1.0f, 0.25f, 0.1f, 0.85f * pulse));
            }
        }
    }

    private void DrawBossRoomChamber(Vector2 center)
    {
        // 1. Dark obsidian arena circular dais (Radius = 115f) (Task 2.8)
        float chamberRadius = 115f;
        Color obsidianBase = new(0.045f, 0.020f, 0.028f, 0.95f);
        Color runeRingOuter = new(0.85f, 0.18f, 0.18f, 0.40f);
        Color runeRingInner = new(0.95f, 0.45f, 0.15f, 0.35f);
        Color goldTrim = new(0.72f, 0.58f, 0.26f, 0.50f);

        DrawCircle(center, chamberRadius, obsidianBase);

        // Concentric ritual rings
        DrawArc(center, chamberRadius, 0, Mathf.Tau, 48, goldTrim, 2.5f);
        DrawArc(center, chamberRadius - 6f, 0, Mathf.Tau, 48, runeRingOuter, 1.2f);
        DrawArc(center, 75f, 0, Mathf.Tau, 36, runeRingOuter, 1.5f);
        DrawArc(center, 40f, 0, Mathf.Tau, 24, runeRingInner, 1.2f);

        // Rotating demonic inscribed runes in the ring
        float rotAngle = _floorTime * 0.25f;
        for (int i = 0; i < 8; i++)
        {
            float a = rotAngle + i * (Mathf.Tau / 8f);
            Vector2 p1 = center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 40f;
            Vector2 p2 = center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 75f;
            DrawLine(p1, p2, new Color(0.90f, 0.25f, 0.15f, 0.35f), 1.0f);
        }

        // 4 Flanking Sacrificial Braziers / Pillars at corners of the dais
        Vector2[] brazierOffsets =
        [
            new(-88f, -70f),
            new(88f, -70f),
            new(-88f, 70f),
            new(88f, 70f)
        ];

        foreach (var offset in brazierOffsets)
        {
            Vector2 bPos = center + offset;
            DrawSacrificialBrazier(bPos);
        }
    }

    private void DrawSacrificialBrazier(Vector2 pos)
    {
        // Stone pillar base
        DrawCircle(pos, 9f, new Color(0.12f, 0.12f, 0.16f, 1.0f));
        DrawArc(pos, 9f, 0, Mathf.Tau, 16, new Color(0.45f, 0.38f, 0.22f, 0.8f), 1.5f);

        // Demonic fire flame flicker
        float flicker = 0.85f + Mathf.Sin(_floorTime * 12f + pos.X) * 0.15f;
        Color ambientAura = new(0.95f, 0.12f, 0.18f, 0.15f * flicker);
        Color flameCore = new(1.0f, 0.35f, 0.12f, 0.90f * flicker);
        Color flameHot = new(1.0f, 0.85f, 0.35f, 0.95f * flicker);

        DrawCircle(pos, 22f * flicker, ambientAura);
        DrawCircle(pos, 4.5f * flicker, flameCore);
        DrawCircle(pos + new Vector2(0, -2.5f), 2.5f * flicker, flameHot);
    }

    private void DrawCorridorTorch(Vector2 pos, bool isTop)
    {
        // Iron bracket anchored to wall
        Vector2 bracketEnd = pos + new Vector2(0f, isTop ? 5f : -5f);
        DrawLine(pos, bracketEnd, new Color(0.28f, 0.30f, 0.36f), 2.0f);

        // Torch flame flicker
        float flicker = 0.85f + Mathf.Sin(_floorTime * 14f + pos.X) * 0.15f;
        // Warm ambient light pool on ground
        DrawCircle(bracketEnd, 16f * flicker, new Color(1.0f, 0.55f, 0.15f, 0.09f));
        // Flame core
        DrawCircle(bracketEnd, 3.2f * flicker, new Color(1.0f, 0.60f, 0.15f, 0.85f));
        DrawCircle(bracketEnd + new Vector2(0, -1.8f), 1.8f, new Color(1.0f, 0.95f, 0.65f, 0.95f));
    }

    // ─────────────────────────────────────────────
    //  Rendering: Exile Character (Top-Down)
    // ─────────────────────────────────────────────
    private void DrawExileCharacter()
    {
        Vector2 facingDir = new(Mathf.Cos(_exileFacingAngle), Mathf.Sin(_exileFacingAngle));
        Vector2 normalDir = new(-facingDir.Y, facingDir.X);

        // Micro-recoil backwards on attack
        Vector2 recoil = -facingDir * _exileAttackRecoil * 5f;

        // Walking stride bobbing and footstep dust
        bool isWalking = MovementState == ExileMovementState.Marching || MovementState == ExileMovementState.Advancing;
        Vector2 walkOffset = Vector2.Zero;
        if (isWalking)
        {
            float bobPhase = _floorTime * 14f;
            walkOffset = new Vector2(
                Mathf.Cos(bobPhase * 0.5f) * 0.9f,
                Mathf.Sin(bobPhase) * 1.5f
            );

            // Trailing subtle dust puff behind Exile
            float dustAlpha = 0.25f + Mathf.Sin(bobPhase * 2f) * 0.15f;
            Vector2 dustPos = ExilePosition - facingDir * 10f;
            DrawCircle(dustPos, 3.5f, new Color(0.32f, 0.30f, 0.28f, dustAlpha));
        }

        Vector2 pos = ExilePosition + recoil + walkOffset;

        // 1. Pulsing 360° Aura on the ground
        float auraPulse = 0.35f + Mathf.Sin(_exileAuraPulsePhase) * 0.15f;
        Color auraInner = new(0.12f, 0.42f, 0.85f, auraPulse * 0.35f);
        Color auraRing = new(0.35f, 0.75f, 1.0f, auraPulse + 0.25f);

        DrawCircle(pos, 22f, auraInner);
        DrawArc(pos, 22f, 0, Mathf.Tau, 32, auraRing, 1.5f);

        // 2. Ground Shadow (Top-down soft oval)
        DrawCircle(pos + new Vector2(0, 3), 11f, new Color(0, 0, 0, 0.45f));

        // 3. Top-Down Hooded Cloak & Shoulders
        Color cloakDark = new(0.10f, 0.12f, 0.17f, 1.0f);
        Color cloakHighlight = new(0.24f, 0.30f, 0.42f, 1.0f);

        // Shoulder / Mantle polygon oriented in facing direction
        Vector2[] mantlePoints =
        [
            pos - facingDir * 8f - normalDir * 9f,
            pos - facingDir * 8f + normalDir * 9f,
            pos + facingDir * 5f + normalDir * 7f,
            pos + facingDir * 5f - normalDir * 7f
        ];
        DrawColoredPolygon(mantlePoints, cloakDark);

        // Hood / Head token at center
        DrawCircle(pos, 6.5f, cloakDark);
        DrawCircle(pos + facingDir * 1.5f, 4.5f, cloakHighlight);

        // Glowing Exile Eyes / Visor slit in facing direction (visual counterpart to monster eyes)
        Vector2 exileEyeOffset = facingDir * 3.8f;
        Color exileEyeAura = new(0.2f, 0.7f, 1.0f, 0.4f);
        Color exileEyeGlow = new(0.45f, 0.92f, 1.0f, 0.95f); // PoE Arcane Cyan
        Color exileEyeCore = Colors.White;

        // Eye glow aura
        DrawCircle(pos + exileEyeOffset + normalDir * 2.0f, 2.0f, exileEyeAura);
        DrawCircle(pos + exileEyeOffset - normalDir * 2.0f, 2.0f, exileEyeAura);

        // Sharp glowing eyes with inner core highlights
        DrawCircle(pos + exileEyeOffset + normalDir * 2.0f, 1.3f, exileEyeGlow);
        DrawCircle(pos + exileEyeOffset + normalDir * 2.0f + facingDir * 0.3f, 0.6f, exileEyeCore);

        DrawCircle(pos + exileEyeOffset - normalDir * 2.0f, 1.3f, exileEyeGlow);
        DrawCircle(pos + exileEyeOffset - normalDir * 2.0f + facingDir * 0.3f, 0.6f, exileEyeCore);

        // 4. Equipped Weapons (MainHand & OffHand) rendered with procedural top-down geometry
        DrawEquippedWeapons(pos, facingDir, normalDir, cloakDark, cloakHighlight);
    }

    private void DrawEquippedWeapons(Vector2 pos, Vector2 facingDir, Vector2 normalDir, Color cloakDark, Color cloakHighlight)
    {
        Vector2 rightHand = pos + normalDir * 8.5f + facingDir * 2f;
        Vector2 leftHand = pos - normalDir * 8.5f + facingDir * 2f;

        float swingPhase = Mathf.Sin(_exileAttackRecoil * Mathf.Pi);
        float swingAngle = swingPhase * 0.55f;

        var mainWClass = MainHandWeapon?.WeaponClass;

        if (mainWClass == WeaponClass.Bow)
        {
            DrawBowAndQuiver(pos, facingDir, normalDir, swingPhase);
        }
        else if (mainWClass == WeaponClass.TwoHandedSword)
        {
            DrawTwoHandedSword(pos, facingDir, normalDir, swingAngle);
        }
        else if (mainWClass == WeaponClass.TwoHandedAxe)
        {
            DrawTwoHandedAxe(pos, facingDir, normalDir, swingAngle);
        }
        else if (mainWClass == WeaponClass.TwoHandedMace)
        {
            DrawTwoHandedMace(pos, facingDir, normalDir, swingAngle);
        }
        else if (mainWClass == WeaponClass.Staff)
        {
            DrawStaff(rightHand, facingDir, normalDir, swingPhase);
        }
        else
        {
            // 1-Handed or Unarmed in Main Hand
            DrawMainHandWeapon(rightHand, facingDir, normalDir, mainWClass, swingAngle, swingPhase);

            // Off-Hand: Shield, Dual-Wield Weapon, or Quiver
            DrawOffHandItem(leftHand, facingDir, normalDir, swingAngle, swingPhase);
        }

        // Draw Hands (Grip Tokens)
        DrawCircle(rightHand, 2.8f, cloakDark);
        DrawCircle(rightHand, 1.8f, cloakHighlight);

        DrawCircle(leftHand, 2.8f, cloakDark);
        DrawCircle(leftHand, 1.8f, cloakHighlight);
    }

    private void DrawBowAndQuiver(Vector2 pos, Vector2 facingDir, Vector2 normalDir, float swingPhase)
    {
        // 1. Quiver worn diagonally on Exile's back mantle
        Vector2 quiverStart = pos - facingDir * 7f - normalDir * 5f;
        Vector2 quiverEnd = pos - facingDir * 2f + normalDir * 4f;
        DrawLine(quiverStart, quiverEnd, new Color(0.35f, 0.22f, 0.14f), 3.5f, antialiased: true);
        DrawCircle(quiverStart, 1.8f, Colors.White);
        DrawCircle(quiverStart + new Vector2(0.5f, -1.2f), 1.5f, new Color(0.4f, 0.85f, 0.55f));

        // 2. Bow held in front
        Vector2 bowCenter = pos + facingDir * 7f;
        Vector2 leftTip = bowCenter - normalDir * 12f - facingDir * 3f;
        Vector2 rightTip = bowCenter + normalDir * 12f - facingDir * 3f;
        Vector2 midPoint = bowCenter + facingDir * 3.5f;

        Color bowWood = new(0.55f, 0.38f, 0.22f);
        Color bowHighlight = new(0.75f, 0.55f, 0.35f);
        DrawLine(leftTip, midPoint, bowWood, 2.4f, antialiased: true);
        DrawLine(midPoint, rightTip, bowWood, 2.4f, antialiased: true);
        DrawLine(leftTip, midPoint, bowHighlight, 1.2f, antialiased: true);
        DrawLine(midPoint, rightTip, bowHighlight, 1.2f, antialiased: true);

        DrawCircle(leftTip, 1.8f, new Color(0.85f, 0.80f, 0.70f));
        DrawCircle(rightTip, 1.8f, new Color(0.85f, 0.80f, 0.70f));

        Color stringColor = new(0.92f, 0.92f, 0.96f, 0.85f);
        Color arrowShaft = new(0.48f, 0.38f, 0.26f);

        if (_exileAttackRecoil > 0.05f)
        {
            Vector2 pullPos = pos + normalDir * 2f - facingDir * (1f + swingPhase * 4.5f);
            DrawLine(leftTip, pullPos, stringColor, 1.2f, antialiased: true);
            DrawLine(pullPos, rightTip, stringColor, 1.2f, antialiased: true);

            Vector2 arrowHead = midPoint + facingDir * 10f;
            DrawLine(pullPos, arrowHead, arrowShaft, 1.8f, antialiased: true);
            DrawCircle(arrowHead, 2.2f, Colors.White);
        }
        else
        {
            DrawLine(leftTip, midPoint - facingDir * 0.5f, stringColor, 1.2f, antialiased: true);
            DrawLine(midPoint - facingDir * 0.5f, rightTip, stringColor, 1.2f, antialiased: true);

            Vector2 arrowHead = midPoint + facingDir * 8f;
            DrawLine(midPoint - facingDir * 2f, arrowHead, arrowShaft, 1.5f, antialiased: true);
            DrawCircle(arrowHead, 1.8f, new Color(0.88f, 0.88f, 0.92f));
        }
    }

    private void DrawTwoHandedSword(Vector2 pos, Vector2 facingDir, Vector2 normalDir, float swingAngle)
    {
        Vector2 grip = pos + normalDir * 3.5f + facingDir * 4f;
        Vector2 bladeDir = facingDir.Rotated(swingAngle * 1.2f);
        Vector2 bladeNormal = new(-bladeDir.Y, bladeDir.X);

        // Wide crossguard
        DrawLine(grip - bladeNormal * 5.5f, grip + bladeNormal * 5.5f, new Color(0.45f, 0.48f, 0.55f), 2.8f, antialiased: true);
        // Pommel
        DrawCircle(grip - bladeDir * 4f, 2.2f, new Color(0.45f, 0.48f, 0.55f));

        // Greatsword blade
        Vector2 bladeTip = grip + bladeDir * 24f;
        DrawLine(grip, bladeTip, new Color(0.80f, 0.84f, 0.90f), 3.6f, antialiased: true);
        DrawLine(grip + bladeDir * 3f, bladeTip - bladeDir * 3f, new Color(0.35f, 0.38f, 0.44f), 1.4f, antialiased: true);
        DrawCircle(bladeTip, 2.0f, Colors.White);
    }

    private void DrawTwoHandedAxe(Vector2 pos, Vector2 facingDir, Vector2 normalDir, float swingAngle)
    {
        Vector2 grip = pos + normalDir * 3.5f + facingDir * 4f;
        Vector2 bladeDir = facingDir.Rotated(swingAngle * 1.2f);
        Vector2 bladeNormal = new(-bladeDir.Y, bladeDir.X);

        Vector2 haftTip = grip + bladeDir * 21f;
        DrawLine(grip - bladeDir * 5f, haftTip, new Color(0.42f, 0.30f, 0.18f), 2.8f, antialiased: true);

        Vector2 headCenter = grip + bladeDir * 17f;
        Vector2[] rightBlade =
        [
            headCenter - bladeDir * 3.5f,
            headCenter + bladeNormal * 8f - bladeDir * 5f,
            headCenter + bladeNormal * 9f + bladeDir * 2f,
            headCenter + bladeDir * 3.5f
        ];
        Vector2[] leftBlade =
        [
            headCenter - bladeDir * 3.5f,
            headCenter - bladeNormal * 8f - bladeDir * 5f,
            headCenter - bladeNormal * 9f + bladeDir * 2f,
            headCenter + bladeDir * 3.5f
        ];

        DrawColoredPolygon(rightBlade, new Color(0.70f, 0.74f, 0.80f));
        DrawColoredPolygon(leftBlade, new Color(0.70f, 0.74f, 0.80f));
        DrawLine(rightBlade[1], rightBlade[2], Colors.White, 1.5f, antialiased: true);
        DrawLine(leftBlade[1], leftBlade[2], Colors.White, 1.5f, antialiased: true);
    }

    private void DrawTwoHandedMace(Vector2 pos, Vector2 facingDir, Vector2 normalDir, float swingAngle)
    {
        Vector2 grip = pos + normalDir * 3.5f + facingDir * 4f;
        Vector2 bladeDir = facingDir.Rotated(swingAngle * 1.2f);
        Vector2 hammerTip = grip + bladeDir * 19f;

        DrawLine(grip - bladeDir * 4f, hammerTip, new Color(0.35f, 0.36f, 0.40f), 3.0f, antialiased: true);

        Vector2 hNormal = new(-bladeDir.Y, bladeDir.X);
        DrawLine(hammerTip - hNormal * 5.5f, hammerTip + hNormal * 5.5f, new Color(0.70f, 0.72f, 0.78f), 5.5f, antialiased: true);
        DrawCircle(hammerTip, 2.2f, Colors.White);
    }

    private void DrawStaff(Vector2 handPos, Vector2 facingDir, Vector2 normalDir, float swingPhase)
    {
        Vector2 staffTip = handPos + facingDir * 18f;
        Vector2 staffTail = handPos - facingDir * 9f;

        DrawLine(staffTail, staffTip, new Color(0.48f, 0.35f, 0.20f), 2.4f, antialiased: true);
        DrawCircle(staffTail, 2.0f, new Color(0.78f, 0.65f, 0.30f));

        float orbRadius = 3.8f + swingPhase * 1.5f;
        DrawCircle(staffTip, orbRadius, new Color(0.20f, 0.85f, 1.0f, 0.90f));
        DrawArc(staffTip, orbRadius + 1.5f, 0, Mathf.Tau, 16, new Color(0.4f, 0.9f, 1.0f, 0.6f), 1.2f);
        DrawCircle(staffTip, 1.8f, Colors.White);
    }

    private void DrawMainHandWeapon(Vector2 handPos, Vector2 facingDir, Vector2 normalDir, WeaponClass? weaponClass, float swingAngle, float swingPhase)
    {
        Vector2 bladeDir = facingDir.Rotated(swingAngle);
        Vector2 bladeNormal = new(-bladeDir.Y, bladeDir.X);

        switch (weaponClass)
        {
            case WeaponClass.OneHandedAxe:
                Vector2 axeHaftTip = handPos + bladeDir * 14f;
                DrawLine(handPos - bladeDir * 3f, axeHaftTip, new Color(0.48f, 0.35f, 0.22f), 2.2f, antialiased: true);
                Vector2[] axeHead =
                [
                    axeHaftTip - bladeDir * 3f,
                    axeHaftTip + bladeNormal * 6f - bladeDir * 4f,
                    axeHaftTip + bladeNormal * 7.5f + bladeDir * 1.5f,
                    axeHaftTip + bladeDir * 1.5f
                ];
                DrawColoredPolygon(axeHead, new Color(0.72f, 0.75f, 0.80f));
                DrawLine(axeHead[1], axeHead[2], Colors.White, 1.4f, antialiased: true);
                break;

            case WeaponClass.OneHandedMace:
                Vector2 maceTip = handPos + bladeDir * 13f;
                DrawLine(handPos - bladeDir * 2f, maceTip, new Color(0.35f, 0.36f, 0.40f), 2.4f, antialiased: true);
                DrawCircle(maceTip, 3.6f, new Color(0.68f, 0.70f, 0.75f));
                DrawCircle(maceTip, 1.8f, Colors.White);
                break;

            case WeaponClass.Wand:
            case WeaponClass.Sceptre:
                Vector2 wandTip = handPos + facingDir * 13f;
                DrawLine(handPos - facingDir * 2f, wandTip, new Color(0.60f, 0.48f, 0.35f), 1.8f, antialiased: true);
                Color wandGemColor = (weaponClass == WeaponClass.Sceptre || MainHandWeapon?.Id.Contains("fire") == true)
                    ? new Color(1.0f, 0.45f, 0.20f, 0.95f)
                    : new Color(0.25f, 0.75f, 1.0f, 0.95f);
                float wandPulse = 3.2f + swingPhase * 2.0f;
                DrawCircle(wandTip, wandPulse, wandGemColor);
                DrawArc(wandTip, wandPulse + 1.5f, 0, Mathf.Tau, 16, new Color(wandGemColor.R, wandGemColor.G, wandGemColor.B, 0.5f), 1.2f);
                DrawCircle(wandTip, 1.5f, Colors.White);
                break;

            case WeaponClass.Dagger:
                Vector2 dagTip = handPos + bladeDir * 11f;
                DrawLine(handPos, dagTip, new Color(0.78f, 0.82f, 0.88f), 2.2f, antialiased: true);
                DrawCircle(dagTip, 1.3f, Colors.White);
                break;

            case WeaponClass.Claw:
                Vector2 clawBase = handPos + bladeDir * 2f;
                for (int t = -1; t <= 1; t++)
                {
                    Vector2 cDir = bladeDir.Rotated(t * 0.28f);
                    DrawLine(clawBase, clawBase + cDir * 9f, new Color(0.85f, 0.88f, 0.92f), 1.5f, antialiased: true);
                }
                break;

            case WeaponClass.OneHandedSword:
            default:
                // Standard Sword / Foil
                float bladeLen = (MainHandWeapon?.Id.Contains("foil") == true) ? 19f : 15f;
                DrawLine(handPos - bladeNormal * 3f, handPos + bladeNormal * 3f, new Color(0.78f, 0.65f, 0.32f), 2.0f, antialiased: true);
                DrawCircle(handPos - bladeDir * 3f, 1.8f, new Color(0.78f, 0.65f, 0.32f));
                Vector2 tip = handPos + bladeDir * bladeLen;
                DrawLine(handPos, tip, new Color(0.78f, 0.82f, 0.88f), 2.4f, antialiased: true);
                DrawLine(handPos + bladeDir * 2f, tip - bladeDir * 2f, Colors.White, 1.0f, antialiased: true);
                DrawCircle(tip, 1.4f, Colors.White);
                break;
        }
    }

    private void DrawOffHandItem(Vector2 handPos, Vector2 facingDir, Vector2 normalDir, float swingAngle, float swingPhase)
    {
        if (OffHandItem is ArmourItemInstance shield && shield.BaseDefinition.ArmourSlot == ArmourSlot.Shield)
        {
            // Shield on left forearm
            Vector2 shieldCenter = handPos - normalDir * 2f;
            DrawArc(shieldCenter, 6.5f, 0, Mathf.Tau, 20, new Color(0.72f, 0.74f, 0.80f), 2.2f, antialiased: true);
            DrawCircle(shieldCenter, 5.5f, new Color(0.18f, 0.20f, 0.28f));
            DrawCircle(shieldCenter, 2.2f, new Color(0.85f, 0.70f, 0.25f));
            return;
        }

        if (OffHandItem is WeaponItemInstance offWeapon)
        {
            if (offWeapon.BaseDefinition.WeaponClass == WeaponClass.Shield)
            {
                Vector2 shieldCenter = handPos - normalDir * 2f;
                DrawArc(shieldCenter, 6.5f, 0, Mathf.Tau, 20, new Color(0.72f, 0.74f, 0.80f), 2.2f, antialiased: true);
                DrawCircle(shieldCenter, 5.5f, new Color(0.18f, 0.20f, 0.28f));
                DrawCircle(shieldCenter, 2.2f, new Color(0.85f, 0.70f, 0.25f));
                return;
            }

            // Dual Wielding (e.g. 2nd Sword / Axe / Wand)
            float offSwing = Mathf.Sin(_exileAttackRecoil * Mathf.Pi) * 0.45f;
            Vector2 offBladeDir = facingDir.Rotated(-0.25f - offSwing);
            Vector2 offBladeNormal = new(-offBladeDir.Y, offBladeDir.X);
            float offLen = 14f;

            DrawLine(handPos - offBladeNormal * 2.5f, handPos + offBladeNormal * 2.5f, new Color(0.78f, 0.65f, 0.32f), 1.8f, antialiased: true);
            DrawCircle(handPos - offBladeDir * 2.5f, 1.5f, new Color(0.78f, 0.65f, 0.32f));
            Vector2 offTip = handPos + offBladeDir * offLen;
            DrawLine(handPos, offTip, new Color(0.78f, 0.82f, 0.88f), 2.2f, antialiased: true);
            DrawLine(handPos + offBladeDir * 2f, offTip - offBladeDir * 2f, Colors.White, 0.9f, antialiased: true);
            DrawCircle(offTip, 1.2f, Colors.White);
        }
    }

    // ─────────────────────────────────────────────
    //  Rendering: Monsters & Overhead Bars
    // ─────────────────────────────────────────────
    private void DrawMonsters()
    {
        for (int i = 0; i < _activeMonsterCount; i++)
        {
            var m = _monsters[i];
            if (!m.IsAlive && m.DeathFadeTimer <= 0f) continue;

            float alpha = m.IsAlive ? 1.0f : Mathf.Clamp(m.DeathFadeTimer / 0.4f, 0f, 1f);
            float bob = Mathf.Sin(m.BobbingPhase) * 1.2f;
            float lunge = m.AttackLungeTimer > 0f ? Mathf.Sin((m.AttackLungeTimer / 0.16f) * Mathf.Pi) : 0f;
            Vector2 pos = new(
                m.Position.X + m.AttackLungeOffset.X * lunge,
                m.Position.Y + bob + m.AttackLungeOffset.Y * lunge
            );

            // Ground shadow
            float shadowRadius = 9f * m.Scale;
            DrawCircle(pos + new Vector2(0, 3 * m.Scale), shadowRadius, new Color(0, 0, 0, 0.42f * alpha));

            // Rare Monster Aura Ring
            if (m.Rarity == MonsterRarity.Rare)
            {
                float auraPulse = 0.35f + Mathf.Sin(m.BobbingPhase * 1.5f) * 0.15f;
                DrawArc(pos, 18f * m.Scale, 0, Mathf.Tau, 24,
                    new Color(ColorRareAura.R, ColorRareAura.G, ColorRareAura.B, auraPulse * alpha), 2.0f);
            }
            // Magic Monster Outline Aura
            else if (m.Rarity == MonsterRarity.Magic)
            {
                DrawArc(pos, 14f * m.Scale, 0, Mathf.Tau, 16,
                    new Color(ColorMagicAura.R, ColorMagicAura.G, ColorMagicAura.B, 0.35f * alpha), 1.5f);
            }
            // Boss Imposing Dark Red Ring
            else if (m.Rarity == MonsterRarity.Boss)
            {
                float bossPulse = 0.45f + Mathf.Sin(m.BobbingPhase * 2.0f) * 0.2f;
                DrawArc(pos, 28f * m.Scale, 0, Mathf.Tau, 32,
                    new Color(ColorBossAura.R, ColorBossAura.G, ColorBossAura.B, bossPulse * alpha), 2.5f);

                // Task 2.8: Rotating Demonic Rune Ring under the Boss
                float runeRot = _floorTime * 1.5f;
                DrawArc(pos, 20f * m.Scale, 0, Mathf.Tau, 24, new Color(0.95f, 0.20f, 0.15f, 0.45f * alpha), 1.5f);
                for (int runeIdx = 0; runeIdx < 6; runeIdx++)
                {
                    float ra = runeRot + runeIdx * (Mathf.Tau / 6f);
                    Vector2 rp = pos + new Vector2(Mathf.Cos(ra), Mathf.Sin(ra)) * (20f * m.Scale);
                    DrawCircle(rp, 2.0f, new Color(1.0f, 0.35f, 0.12f, 0.85f * alpha));
                }

                // Task 2.8: Boss Awakening Fiery Shockwave Ring
                if (m.AwakeningShockwaveRadius > 0f)
                {
                    float shockAlpha = Mathf.Clamp(m.AwakeningTimer / 1.0f, 0f, 1f);
                    Color waveColor = new(1.0f, 0.40f, 0.10f, 0.85f * shockAlpha);
                    Color waveInner = new(1.0f, 0.85f, 0.25f, 0.45f * shockAlpha);
                    DrawArc(pos, m.AwakeningShockwaveRadius, 0, Mathf.Tau, 36, waveColor, 3.0f);
                    DrawArc(pos, m.AwakeningShockwaveRadius * 0.75f, 0, Mathf.Tau, 24, waveInner, 1.5f);
                }
            }

            // Task 2.7: League Encounter Visual Indicators
            if (m.EncounterType == LeagueEncounterType.BreachCrevice)
            {
                DrawArc(pos, 16f * m.Scale, 0, Mathf.Tau, 16, new Color(0.65f, 0.15f, 0.85f, 0.40f * alpha), 1.8f);
            }
            else if (m.EncounterType == LeagueEncounterType.SulphiteVein)
            {
                DrawArc(pos, 15f * m.Scale, 0, Mathf.Tau, 16, new Color(0.95f, 0.75f, 0.15f, 0.45f * alpha), 1.5f);
            }

            // Monster Body Silhouette (Top-down token)
            Color bodyColor = m.HitFlashTimer > 0f ? Colors.White : m.CustomColor;
            bodyColor.A = alpha;

            float r = 8.5f * m.Scale;
            DrawCircle(pos, r, bodyColor);

            // Task 2.7: Dormant Essence Monolith Crystal Encasement
            if (m.IsDormant)
            {
                Color crystalFill = new(0.15f, 0.75f, 0.95f, 0.45f * alpha);
                Color crystalBorder = new(0.50f, 0.90f, 1.0f, 0.85f * alpha);

                Vector2[] crystalPoly =
                [
                    pos + new Vector2(0f, -14f * m.Scale),
                    pos + new Vector2(12f * m.Scale, -4f * m.Scale),
                    pos + new Vector2(10f * m.Scale, 11f * m.Scale),
                    pos + new Vector2(0f, 16f * m.Scale),
                    pos + new Vector2(-10f * m.Scale, 11f * m.Scale),
                    pos + new Vector2(-12f * m.Scale, -4f * m.Scale)
                ];
                DrawColoredPolygon(crystalPoly, crystalFill);
                for (int c = 0; c < crystalPoly.Length; c++)
                {
                    DrawLine(crystalPoly[c], crystalPoly[(c + 1) % crystalPoly.Length], crystalBorder, 1.5f);
                }
            }

            // Monster Eyes facing towards Exile
            Vector2 toExile = (ExilePosition - pos).Normalized();
            Vector2 eyeOffset = toExile * (3.5f * m.Scale);
            Vector2 eyeNormal = new(-toExile.Y, toExile.X);

            Color eyeColor = m.Rarity switch
            {
                MonsterRarity.Boss => new Color(1.0f, 0.2f, 0.2f, alpha),
                MonsterRarity.Rare => new Color(1.0f, 0.85f, 0.1f, alpha),
                MonsterRarity.Magic => new Color(0.2f, 0.8f, 1.0f, alpha),
                _ => new Color(0.85f, 0.3f, 0.3f, alpha)
            };

            DrawCircle(pos + eyeOffset + eyeNormal * (2.5f * m.Scale), 1.4f * m.Scale, eyeColor);
            DrawCircle(pos + eyeOffset - eyeNormal * (2.5f * m.Scale), 1.4f * m.Scale, eyeColor);

            // Overhead Health Bar
            if (m.Rarity == MonsterRarity.Boss)
            {
                DrawBossOverheadBar(pos, m, alpha);
            }
            else if (m.CurrentLife < m.MaxLife && m.IsAlive)
            {
                DrawRegularOverheadBar(pos, m, alpha);
            }
        }
    }

    private void DrawRegularOverheadBar(Vector2 pos, MonsterVisualToken m, float alpha)
    {
        float barW = 24f * m.Scale;
        float barH = 3f;
        float barX = pos.X - barW / 2;
        float barY = pos.Y - 18f * m.Scale;

        // Background
        DrawRect(new Rect2(barX, barY, barW, barH), new Color(0.1f, 0.1f, 0.1f, 0.8f * alpha));

        // Fill
        float fillW = barW * Mathf.Clamp(m.CurrentLife / m.MaxLife, 0f, 1f);
        DrawRect(new Rect2(barX, barY, fillW, barH), new Color(0.85f, 0.2f, 0.2f, 0.9f * alpha));
    }

    private void DrawBossOverheadBar(Vector2 pos, MonsterVisualToken m, float alpha)
    {
        // Task 2.8: Senior 96px Overhead Boss Bar with Gold Filigree and Skull Badge
        float barW = 96f;
        float barH = 6f;
        float barX = pos.X - barW * 0.5f;
        float barY = pos.Y - 38f;

        Color goldBorder = new(0.85f, 0.70f, 0.28f, alpha);
        Color goldGlow = new(1.0f, 0.88f, 0.40f, 0.35f * alpha);
        Color barBg = new(0.06f, 0.06f, 0.08f, 0.95f * alpha);

        // Outer glow & border
        DrawRect(new Rect2(barX - 2, barY - 2, barW + 4, barH + 4), goldGlow);
        DrawRect(new Rect2(barX - 1, barY - 1, barW + 2, barH + 2), goldBorder);
        DrawRect(new Rect2(barX, barY, barW, barH), barBg);

        // Filigree corner accents
        DrawLine(new Vector2(barX - 3, barY - 3), new Vector2(barX, barY - 3), goldBorder, 1.5f);
        DrawLine(new Vector2(barX - 3, barY - 3), new Vector2(barX - 3, barY), goldBorder, 1.5f);
        DrawLine(new Vector2(barX + barW + 3, barY - 3), new Vector2(barX + barW, barY - 3), goldBorder, 1.5f);
        DrawLine(new Vector2(barX + barW + 3, barY - 3), new Vector2(barX + barW + 3, barY), goldBorder, 1.5f);

        // Life fill ratio (accounting for awakening animation)
        float lifeRatio = Mathf.Clamp(m.CurrentLife / m.MaxLife, 0f, 1f);
        if (m.AwakeningTimer > 0f)
        {
            float awakeProg = 1f - (m.AwakeningTimer / 1.0f);
            lifeRatio *= awakeProg;
        }

        float fillW = barW * lifeRatio;
        if (fillW > 0.5f)
        {
            // Crimson health fill with top specular line
            DrawRect(new Rect2(barX, barY, fillW, barH), new Color(0.85f, 0.12f, 0.12f, alpha));
            DrawLine(new Vector2(barX, barY + 1f), new Vector2(barX + fillW, barY + 1f), new Color(1.0f, 0.45f, 0.35f, 0.90f * alpha), 1.0f);
        }

        // Boss Skull / Crest Badge on the left
        Vector2 skullPos = new(barX - 8f, barY + barH * 0.5f);
        DrawCircle(skullPos, 5f, new Color(0.12f, 0.12f, 0.15f, alpha));
        DrawArc(skullPos, 5f, 0, Mathf.Tau, 12, goldBorder, 1.2f);
        DrawCircle(skullPos, 2.5f, new Color(0.95f, 0.25f, 0.25f, alpha));
    }

    // ─────────────────────────────────────────────
    //  Rendering: Projectiles (PoE 5 Elements)
    // ─────────────────────────────────────────────
    private void DrawProjectiles()
    {
        for (int i = 0; i < MaxProjectiles; i++)
        {
            if (!_projectiles[i].IsActive) continue;

            var p = _projectiles[i];
            Vector2 dir = (p.TargetPosition - p.StartPosition).Normalized();
            Vector2 tailPos = p.CurrentPosition - dir * p.StreakLength;

            // Outer glow line
            DrawLine(tailPos, p.CurrentPosition, p.GlowColor, 4.0f, antialiased: true);
            // Inner core bright streak
            DrawLine(tailPos + dir * 2f, p.CurrentPosition, p.BaseColor, 2.0f, antialiased: true);

            // Projectile head tip / Fireball orb
            if (p.DamageType == DamageType.Fire && p.AoeRadius > 0f)
            {
                // Fireball: Outer fiery orange aura + inner flame core + white hot center
                DrawCircle(p.CurrentPosition, 5.0f, p.GlowColor);
                DrawCircle(p.CurrentPosition, 3.2f, p.BaseColor);
                DrawCircle(p.CurrentPosition, 1.8f, Colors.White);
            }
            else
            {
                DrawCircle(p.CurrentPosition, 2.2f, Colors.White);
            }
        }
    }

    // ─────────────────────────────────────────────
    //  Rendering: Boss Slam Telegraph Indicator (2D Circular AoE)
    // ─────────────────────────────────────────────
    private void DrawBossSlamTelegraph()
    {
        Vector2 pos = _telegraph.Position;
        float baseRadius = _telegraph.Radius;
        float progress = _telegraph.Progress;

        // 1. Static outer lethal danger zone (Alpha = 0.22 as specified in Task 2.8)
        Color zoneFill = new(0.95f, 0.12f, 0.12f, 0.22f);
        Color zoneBorder = new(1.0f, 0.18f, 0.18f, 0.70f + Mathf.Sin(_floorTime * 10f) * 0.25f);
        DrawCircle(pos, baseRadius, zoneFill);
        DrawArc(pos, baseRadius, 0, Mathf.Tau, 36, zoneBorder, 1.8f);

        // 2. Shrinking Ring indicator (from 1.6x down to 0.05x over 1.4s wind-up)
        float currentShrinkRadius = Mathf.Lerp(baseRadius * 1.6f, baseRadius * 0.05f, progress);
        Color shrinkRingColor = new(1.0f, 0.85f, 0.20f, 0.95f);
        DrawArc(pos, currentShrinkRadius, 0, Mathf.Tau, 32, shrinkRingColor, 2.5f);

        // 3. Danger warning crosshair in center (pulses faster as progress nears 1.0)
        float pulseSpeed = Mathf.Lerp(8f, 25f, progress);
        float crossAlpha = 0.6f + Mathf.Sin(_floorTime * pulseSpeed) * 0.35f;
        Color crosshairColor = new(1.0f, 0.2f, 0.2f, crossAlpha);
        DrawLine(pos + new Vector2(-7, 0), pos + new Vector2(7, 0), crosshairColor, 1.8f);
        DrawLine(pos + new Vector2(0, -7), pos + new Vector2(0, 7), crosshairColor, 1.8f);
        DrawCircle(pos, 3f, crosshairColor);
    }

    // ─────────────────────────────────────────────
    //  Rendering: Melee Strike AoE Indicators
    // ─────────────────────────────────────────────
    /// <summary>
    /// Renders all active melee strike indicators as expanding directional arcs.
    /// Each indicator is a semi-transparent arc that expands from 0 to full range
    /// while fading out, creating a fast "slash sweep" visual.
    /// </summary>
    private void DrawMeleeStrikeIndicators()
    {
        for (int i = 0; i < MaxMeleeIndicators; i++)
        {
            if (!_meleeIndicators[i].IsActive) continue;
            ref var ind = ref _meleeIndicators[i];

            float progress = ind.Progress;

            // Expand radius from 30% to 100% of range
            float currentRadius = Mathf.Lerp(ind.Range * 0.3f, ind.Range, progress);

            // Fade alpha: peak at 0.15 progress, then fade out
            float alpha;
            if (progress < 0.15f)
            {
                alpha = Mathf.Lerp(0f, ind.IsCrit ? 0.55f : 0.38f, progress / 0.15f);
            }
            else
            {
                alpha = Mathf.Lerp(ind.IsCrit ? 0.55f : 0.38f, 0f, (progress - 0.15f) / 0.85f);
            }

            if (alpha < 0.01f) continue;

            // Arc sweep: centered on direction angle, spanning ArcAngle
            float halfArc = ind.ArcAngle * 0.5f;
            float startAngle = ind.DirectionAngle - halfArc;
            float endAngle = ind.DirectionAngle + halfArc;

            // 1. Filled arc (semi-transparent fan)
            Color fillColor = new(ind.ArcColor.R, ind.ArcColor.G, ind.ArcColor.B, alpha * 0.5f);
            int segments = 16;
            var points = new Vector2[segments + 2];
            points[0] = ind.Origin;
            for (int s = 0; s <= segments; s++)
            {
                float angle = Mathf.Lerp(startAngle, endAngle, (float)s / segments);
                points[s + 1] = ind.Origin + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * currentRadius;
            }

            // Draw filled polygon (the fan)
            var fillPoints = new Vector2[segments + 2];
            var fillColors = new Color[segments + 2];
            for (int s = 0; s < points.Length; s++)
            {
                fillPoints[s] = points[s];
                fillColors[s] = fillColor;
            }
            DrawPolygon(fillPoints, fillColors);

            // 2. Arc border (brighter edge)
            Color edgeColor = new(ind.ArcColor.R, ind.ArcColor.G, ind.ArcColor.B, alpha * 0.9f);
            float lineWidth = ind.IsCrit ? 2.5f : 1.5f;
            DrawArc(ind.Origin, currentRadius, startAngle, endAngle, segments, edgeColor, lineWidth);

            // 3. Crit indicator: radial slash lines from origin
            if (ind.IsCrit)
            {
                Color slashColor = new(1.0f, 1.0f, 1.0f, alpha * 0.7f);
                for (int s = 0; s < 3; s++)
                {
                    float slashAngle = Mathf.Lerp(startAngle + halfArc * 0.3f, endAngle - halfArc * 0.3f, (float)s / 2f);
                    Vector2 slashEnd = ind.Origin + new Vector2(Mathf.Cos(slashAngle), Mathf.Sin(slashAngle)) * (currentRadius * 1.15f);
                    DrawLine(ind.Origin, slashEnd, slashColor, 1.0f);
                }
            }
        }
    }

    // ─────────────────────────────────────────────
    //  Public Control API: Projectiles & Attacks
    // ─────────────────────────────────────────────
    /// <summary>
    /// Spawns an in-flight projectile from start to target in 2D space.
    /// Clamps visual rendering to MaxProjectiles to protect frame rate and avoid visual clutter.
    /// </summary>
    public bool SpawnProjectile(
        Vector2 start,
        Vector2 target,
        DamageType type,
        float speed = 520f,
        float aoeRadius = 0f,
        bool isCrit = false,
        long targetMonsterId = -1)
    {
        // Find available slot in object pool
        int freeIndex = -1;
        for (int i = 0; i < MaxProjectiles; i++)
        {
            if (!_projectiles[i].IsActive)
            {
                freeIndex = i;
                break;
            }
        }

        if (freeIndex == -1)
        {
            // Visual clamping limit reached; drop rendering gracefully while core handles damage
            return false;
        }

        _projectilePendingHits[freeIndex].Clear();

        (Color baseColor, Color glowColor) = GetDamageTypeColors(type);

        _projectiles[freeIndex] = new ProjectileData
        {
            IsActive = true,
            StartPosition = start,
            TargetPosition = target,
            CurrentPosition = start,
            Progress = 0f,
            Speed = speed,
            DamageType = type,
            StreakLength = 16f,
            BaseColor = baseColor,
            GlowColor = glowColor,
            AoeRadius = aoeRadius,
            IsCrit = isCrit,
            TargetMonsterId = targetMonsterId
        };

        return true;
    }

    /// <summary>
    /// Sets the target monster or position for auto-facing.
    /// </summary>
    public void SetTarget(long? instanceId, Vector2? targetPos = null)
    {
        TargetMonsterInstanceId = instanceId;
        TargetPosition = targetPos;
    }

    /// <summary>
    /// Instantly turns the Exile to face toward the specified target position.
    /// </summary>
    public void FaceTarget(Vector2 targetPos)
    {
        TargetPosition = targetPos;
        Vector2 toTarget = targetPos - ExilePosition;
        if (toTarget.LengthSquared() > 0.001f)
        {
            _exileFacingAngle = Mathf.Wrap(toTarget.Angle(), -Mathf.Pi, Mathf.Pi);
        }
    }

    /// <summary>
    /// Directly sets the Exile's facing angle in radians.
    /// </summary>
    public void SetFacingAngle(float angle)
    {
        _exileFacingAngle = Mathf.Wrap(angle, -Mathf.Pi, Mathf.Pi);
    }

    /// <summary>
    /// Triggers the Exile's attack recoil and weapon animation phase.
    /// </summary>
    public void TriggerExileAttackRecoil(float recoil = 1.0f)
    {
        _exileAttackRecoil = recoil;
    }

    /// <summary>
    /// Calculates the 2D world position of the weapon / wand tip based on the Exile's current facing angle.
    /// </summary>
    public Vector2 GetWeaponTipPosition()
    {
        Vector2 facingDir = new(Mathf.Cos(_exileFacingAngle), Mathf.Sin(_exileFacingAngle));
        Vector2 normalDir = new(-facingDir.Y, facingDir.X);
        return ExilePosition + normalDir * 8.5f + facingDir * 18f;
    }

    /// <summary>
    /// Resolves the current target position for auto-facing:
    /// 1. If TargetMonsterInstanceId corresponds to an alive monster, returns its token position.
    /// 2. Else if TargetPosition is set, returns TargetPosition.
    /// 3. Otherwise, falls back to the closest alive monster in the arena.
    /// </summary>
    public Vector2? GetCurrentTargetPosition()
    {
        if (TargetMonsterInstanceId.HasValue)
        {
            for (int i = 0; i < _activeMonsterCount; i++)
            {
                if (_monsters[i].InstanceId == TargetMonsterInstanceId.Value && _monsters[i].IsAlive)
                {
                    return _monsters[i].Position;
                }
            }
        }

        if (TargetPosition.HasValue)
        {
            return TargetPosition.Value;
        }

        return GetClosestAliveMonsterPosition();
    }

    /// <summary>
    /// Triggers Exile attack animation: visual recoil, turns to face target, and launches projectile towards target.
    /// </summary>
    public void FireExileAttack(DamageType type = DamageType.Physical, Vector2? targetPosition = null)
    {
        _exileAttackRecoil = 1.0f;

        Vector2 target = targetPosition ?? GetCurrentTargetPosition() ?? new Vector2(ExilePosition.X + 90f, ExilePosition.Y);
        Vector2 toTarget = target - ExilePosition;
        if (toTarget.LengthSquared() > 0.001f)
        {
            _exileFacingAngle = Mathf.Wrap(toTarget.Angle(), -Mathf.Pi, Mathf.Pi);
        }

        Vector2 wandTip = GetWeaponTipPosition();
        SpawnProjectile(wandTip, target, type);
    }

    private Vector2? GetClosestAliveMonsterPosition()
    {
        Vector2? closest = null;
        float minDistSq = float.MaxValue;

        for (int i = 0; i < _activeMonsterCount; i++)
        {
            if (_monsters[i].IsAlive)
            {
                float dSq = ExilePosition.DistanceSquaredTo(_monsters[i].Position);
                if (dSq < minDistSq)
                {
                    minDistSq = dSq;
                    closest = _monsters[i].Position;
                }
            }
        }
        return closest;
    }

    public void TriggerProjectileImpact(Vector2 targetPos, DamageType type, float aoeRadius = 0f, bool isCrit = false)
    {
        // 1. If this projectile has an AoE explosion (e.g. Fireball), trigger the explosion ring at impact!
        if (aoeRadius > 0f)
        {
            TriggerMeleeStrikeIndicator(targetPos, 0f, aoeRadius, type, isCrit, Mathf.Tau);
            ScreenShake(isCrit ? 1.4f : 0.8f, 0.12f);
        }

        // 2. Trigger hit flash on monsters near the target in 2D Euclidean distance
        float flashRadius = aoeRadius > 0f ? aoeRadius : 28f;
        for (int i = 0; i < _activeMonsterCount; i++)
        {
            if (_monsters[i].IsAlive && _monsters[i].Position.DistanceTo(targetPos) <= flashRadius)
            {
                _monsters[i].HitFlashTimer = 0.12f;
            }
        }

        OnProjectileImpact?.Invoke(targetPos, type);
    }

    // ─────────────────────────────────────────────
    //  Public Control API: Boss Slam Telegraph
    // ─────────────────────────────────────────────
    /// <summary>
    /// Triggers a Boss Slam telegraph warning indicator anywhere in the 2D arena.
    /// Only used for lethal attacks (>70% player EHP) to give a window for Emergency Portal (Space).
    /// </summary>
    public void TriggerBossSlam(Vector2 targetPosition, float duration = 1.4f, float radius = 55f, float threatEhpPercent = 85f)
    {
        _telegraph = new BossSlamTelegraph
        {
            IsActive = true,
            Position = targetPosition,
            Radius = radius,
            TotalDuration = Mathf.Max(0.5f, duration),
            ElapsedDuration = 0f,
            ThreatEhpPercent = threatEhpPercent,
            ScreenShakeIntensity = 6.0f
        };
    }

    /// <summary>
    /// Cancels or clears the current telegraph indicator.
    /// </summary>
    public void CancelBossSlam()
    {
        _telegraph.IsActive = false;
    }

    private void DetonateBossSlam()
    {
        Vector2 pos = _telegraph.Position;
        float threat = _telegraph.ThreatEhpPercent;
        float intensity = _telegraph.ScreenShakeIntensity;

        _telegraph.IsActive = false;

        // Add ground scorch impact crater (lasts 3.0s) (Task 2.8)
        _slamCraters.Add((pos, 3.0f));

        // Apply intense screen shake
        ScreenShake(intensity > 0 ? intensity : 6.0f, 0.25f);

        // Flash recoil on exile if caught in the 2D blast radius
        if (pos.DistanceTo(ExilePosition) <= _telegraph.Radius)
        {
            _exileAttackRecoil = 0.9f;
        }

        OnBossSlamDetonated?.Invoke(pos, threat);
    }

    // ─────────────────────────────────────────────
    //  Public Control API: Melee Strike AoE Indicators
    // ─────────────────────────────────────────────
    /// <summary>
    /// Triggers a melee attack AoE indicator arc at the given position.
    /// The arc expands from the monster's origin toward the Exile, colored by damage type.
    /// Crit strikes produce a wider, brighter indicator with radial slash lines.
    /// </summary>
    public void TriggerMeleeStrikeIndicator(
        Vector2 origin,
        float directionAngle,
        float range,
        DamageType damageType,
        bool isCrit = false,
        float? arcAngle = null)
    {
        // Find available slot in pool (oldest-overwrite if full)
        int slot = -1;
        for (int i = 0; i < MaxMeleeIndicators; i++)
        {
            if (!_meleeIndicators[i].IsActive)
            {
                slot = i;
                break;
            }
        }

        // Overwrite the oldest (highest elapsed) if all slots full
        if (slot == -1)
        {
            float maxElapsed = -1f;
            slot = 0;
            for (int i = 0; i < MaxMeleeIndicators; i++)
            {
                if (_meleeIndicators[i].Elapsed > maxElapsed)
                {
                    maxElapsed = _meleeIndicators[i].Elapsed;
                    slot = i;
                }
            }
        }

        (Color baseColor, _) = GetDamageTypeColors(damageType);

        _meleeIndicators[slot] = new MeleeStrikeIndicator
        {
            IsActive = true,
            Origin = origin,
            DirectionAngle = directionAngle,
            Range = Mathf.Max(15f, range),
            TotalDuration = isCrit ? 0.38f : 0.28f,
            Elapsed = 0f,
            ArcColor = baseColor,
            ArcAngle = arcAngle ?? (isCrit ? 2.6f : 2.1f),   // Default: Crit: ~150°, Normal: ~120°
            IsCrit = isCrit
        };
    }

    /// <summary>
    /// Triggers Exile melee attack animation: visual recoil, turns to face target, and directional slash arc sweep towards target.
    /// </summary>
    public void FireExileMeleeAttack(Vector2? targetPosition, float range, DamageType type, float? arcAngle = null, bool isCrit = false)
    {
        _exileAttackRecoil = 0.8f;
        float dirAngle;
        if (targetPosition.HasValue)
        {
            Vector2 toTarget = targetPosition.Value - ExilePosition;
            dirAngle = toTarget.LengthSquared() > 0.001f ? Mathf.Atan2(toTarget.Y, toTarget.X) : _exileFacingAngle;
            TargetPosition = targetPosition.Value;
        }
        else
        {
            dirAngle = _exileFacingAngle;
        }

        _exileFacingAngle = Mathf.Wrap(dirAngle, -Mathf.Pi, Mathf.Pi);

        TriggerMeleeStrikeIndicator(ExilePosition, dirAngle, range, type, isCrit, arcAngle);
    }

    /// <summary>
    /// Triggers an attack lunge animation for a monster token towards its target.
    /// </summary>
    public void TriggerMonsterAttackLunge(long instanceId, Vector2 direction)
    {
        for (int i = 0; i < _activeMonsterCount; i++)
        {
            if (_monsters[i].InstanceId == instanceId)
            {
                _monsters[i].AttackLungeTimer = 0.16f;
                _monsters[i].AttackLungeOffset = direction * 7f;
                break;
            }
        }
    }

    /// <summary>
    /// Unified visual attack pipeline for ANY combatant (Exile, Monster, Minion, Boss).
    /// Guarantees symmetrical handling of screen coordinates, attacker recoil/lunge, facing angle,
    /// melee slash sweep indicators, and in-flight projectiles.
    /// </summary>
    public void ExecuteAttackVisual(CombatAttackEventArgs args)
    {
        bool isExileAttacker = args.Attacker is PlayerState || args.Attacker.Id == 0;

        // 1. Resolve Attacker Screen Coordinate
        Vector2 attackerScreen;
        if (isExileAttacker)
        {
            attackerScreen = ExilePosition;
        }
        else if (!TryGetMonsterPosition(args.Attacker.Id, out attackerScreen))
        {
            attackerScreen = Camera.WorldToScreen(new Vector2(args.Origin.X, args.Origin.Y), CenterPosition);
        }

        // 2. Resolve Target Screen Coordinate
        Vector2 targetScreen;
        if (args.Target is PlayerState || (args.Target != null && args.Target.Id == 0))
        {
            targetScreen = ExilePosition;
        }
        else if (args.Target != null && TryGetMonsterPosition(args.Target.Id, out targetScreen))
        {
            // Successfully resolved monster token position
        }
        else if (args.TargetPosition.HasValue)
        {
            targetScreen = Camera.WorldToScreen(new Vector2(args.TargetPosition.Value.X, args.TargetPosition.Value.Y), CenterPosition);
        }
        else
        {
            targetScreen = isExileAttacker
                ? attackerScreen + new Vector2(90f, 0f)
                : ExilePosition;
        }

        // 3. Directional Vector & Angle in Screen Space
        Vector2 toTarget = targetScreen - attackerScreen;
        float dirAngle = toTarget.LengthSquared() > 0.001f
            ? Mathf.Atan2(toTarget.Y, toTarget.X)
            : (isExileAttacker ? _exileFacingAngle : Mathf.Pi);

        // 4. Attacker Animation: Recoil / Lunge
        if (isExileAttacker)
        {
            _exileAttackRecoil = 0.85f;
            _exileFacingAngle = Mathf.Wrap(dirAngle, -Mathf.Pi, Mathf.Pi);
            TargetPosition = targetScreen;
        }
        else
        {
            TriggerMonsterAttackLunge(args.Attacker.Id, toTarget.Normalized());
        }

        // 5. Visual Effects (VFX) Dispatch based on GeometryType & IsMelee
        if (!args.IsMelee)
        {
            Vector2 launchOrigin = isExileAttacker ? GetWeaponTipPosition() : attackerScreen;
            float aoeRadius = args.GeometryType == SkillGeometryType.RadialAoE ? Mathf.Max(30f, args.Range) : 0f;
            long targetMonsterId = args.Target?.Id ?? -1;

            if (args.Projectiles != null && args.Projectiles.Count > 0)
            {
                if (args.Projectiles.Count == 1)
                {
                    // Single projectile directed at targetScreen
                    float speed = (float)args.Projectiles[0].Velocity.Length;
                    SpawnProjectile(launchOrigin, targetScreen, args.DamageType, speed > 0f ? speed : 520f, aoeRadius, args.IsCrit, targetMonsterId);
                }
                else
                {
                    // Multi-vector projectile spread
                    foreach (var p in args.Projectiles)
                    {
                        Vector2 pTarget = launchOrigin + new Vector2(p.Direction.X, p.Direction.Y) * 220f;
                        float speed = (float)p.Velocity.Length;
                        SpawnProjectile(launchOrigin, pTarget, args.DamageType, speed > 0f ? speed : 520f, aoeRadius, args.IsCrit, targetMonsterId);
                    }
                }
            }
            else
            {
                SpawnProjectile(launchOrigin, targetScreen, args.DamageType, aoeRadius: aoeRadius, isCrit: args.IsCrit, targetMonsterId: targetMonsterId);
            }
        }
        else if (args.GeometryType == SkillGeometryType.RadialAoE)
        {
            // Point-blank melee radial nova/slam centered on attacker
            float radius = Mathf.Max(30f, args.Range);
            TriggerMeleeStrikeIndicator(attackerScreen, 0f, radius, args.DamageType, args.IsCrit, Mathf.Tau);
        }
        else // Melee strike, Cone slash sweep, or close-quarters Strike
        {
            float arcSpan = args.ConeAngle > 0f ? Mathf.DegToRad(args.ConeAngle) : Mathf.DegToRad(70f);
            float reach = Mathf.Max(25f, args.Range);
            TriggerMeleeStrikeIndicator(attackerScreen, dirAngle, reach, args.DamageType, args.IsCrit, arcSpan);
        }
    }

    /// <summary>
    /// Triggers a subtle screen shake for lethal impacts and critical slams.
    /// </summary>
    public void ScreenShake(float magnitude = 3.0f, float duration = 0.15f)
    {
        _screenShakeMagnitude = magnitude;
        _screenShakeTimer = duration;
    }

    /// <summary>
    /// Spawns a floating combat text number in the arena.
    /// </summary>
    public void SpawnFloatingDamage(float damage, Vector2 position, DamageType type, bool isCrit = false) =>
        FloatingText?.SpawnDamageText(damage, position, type, isCrit);

    /// <summary>
    /// Spawns floating combat text from a calculated HitResult.
    /// </summary>
    public void SpawnFloatingHit(HitResult hit, Vector2 position) =>
        FloatingText?.SpawnHitResult(hit, position);

    // ─────────────────────────────────────────────
    //  Public Control API: Monsters Management (2D Formations)
    // ─────────────────────────────────────────────
    /// <summary>
    /// Populates the visual arena with monsters from the simulation WaveSpawner.
    /// Uses 2D radial / surrounding distribution around the central Exile.
    /// Reuses internal tokens to achieve zero GC pressure.
    /// </summary>
    public void SetMonsters(IReadOnlyList<MonsterInstance> monsterInstances)
    {
        _activeMonsterCount = Mathf.Min(monsterInstances.Count, MaxMonsters);

        for (int i = 0; i < _activeMonsterCount; i++)
        {
            var inst = monsterInstances[i];
            var token = _monsters[i];

            token.InstanceId = inst.InstanceId;
            token.Name = inst.Name;
            token.Rarity = inst.Rarity;
            token.MaxLife = inst.MaxLife;
            token.CurrentLife = inst.CurrentLife;
            token.IsAlive = inst.IsAlive;
            token.IsAggroed = inst.IsAggroed;
            token.HitFlashTimer = 0f;
            token.DeathFadeTimer = inst.IsAlive ? 1.0f : 0f;
            token.BobbingPhase = i * 0.75f;

            // 2D Positioning around Exile (supports world coordinates & screen space via Camera)
            Vector2 spawnPos;
            if (inst.Position != Vector2D.Zero)
            {
                spawnPos = Camera.WorldToScreen(new Vector2(inst.Position.X, inst.Position.Y), CenterPosition);
            }
            else if (inst.Rarity == MonsterRarity.Boss)
            {
                // Boss stands in commanding North-East or East position
                spawnPos = new Vector2(320f, 95f);
            }
            else
            {
                // Radial surrounding distribution around Exile
                float angle = (i * Mathf.Tau / _activeMonsterCount) + 0.35f;
                float radiusX = 110f + (i % 3) * 22f;
                float radiusY = 55f + (i % 2) * 18f;

                float x = Mathf.Clamp(CenterPosition.X + Mathf.Cos(angle) * radiusX, 40f, ArenaWidth - 40f);
                float y = Mathf.Clamp(CenterPosition.Y + Mathf.Sin(angle) * radiusY, 28f, ArenaHeight - 28f);
                spawnPos = new Vector2(x, y);
            }

            token.BasePosition = spawnPos;
            token.Position = spawnPos;

            // Rarity scaling & color configuration
            token.Scale = inst.Rarity switch
            {
                MonsterRarity.Boss => 1.8f,
                MonsterRarity.Rare => 1.3f,
                MonsterRarity.Magic => 1.1f,
                _ => 0.95f
            };

            token.CustomColor = inst.Rarity switch
            {
                MonsterRarity.Boss => new Color(0.24f, 0.08f, 0.12f),
                MonsterRarity.Rare => new Color(0.28f, 0.24f, 0.10f),
                MonsterRarity.Magic => new Color(0.12f, 0.20f, 0.32f),
                _ => new Color(0.18f, 0.19f, 0.22f)
            };

            // Task 2.7: Detect Encounter metadata from name
            token.IsDormant = inst.Name.Contains("Imprisoned") || inst.Name.Contains("Crystalline");
            if (inst.Name.Contains("Essence") || inst.Name.Contains("Imprisoned") || inst.Name.Contains("Crystalline"))
            {
                token.EncounterType = LeagueEncounterType.EssenceMonolith;
            }
            else if (inst.Name.Contains("Breachling") || inst.Name.Contains("Xoph") || inst.Name.Contains("Breach"))
            {
                token.EncounterType = LeagueEncounterType.BreachCrevice;
            }
            else if (inst.Name.Contains("Subterranean") || inst.Name.Contains("Burrower"))
            {
                token.EncounterType = LeagueEncounterType.SulphiteVein;
            }
            else
            {
                token.EncounterType = LeagueEncounterType.None;
            }
        }
    }

    /// <summary>
    /// Handles monster damage presentation. If there is an active in-flight projectile
    /// targeting this monster (or within AoE blast radius), the visual damage and life bar
    /// drop are queued to detonate at the exact moment of projectile impact.
    /// Otherwise (melee, DoT, or instant effects), damage and floating text appear immediately.
    /// </summary>
    public void HandleMonsterDamageDealt(long monsterId, float damage, float newLife, Vector2 pos, DamageType type, bool isCrit, bool isDoT)
    {
        if (!isDoT)
        {
            for (int i = 0; i < MaxProjectiles; i++)
            {
                if (_projectiles[i].IsActive && _projectiles[i].Progress < 0.95f)
                {
                    bool isTargetMatch = _projectiles[i].TargetMonsterId == monsterId;
                    bool isAoEMatch = _projectiles[i].AoeRadius > 0f && _projectiles[i].TargetPosition.DistanceTo(pos) <= (_projectiles[i].AoeRadius + 15f);

                    if (isTargetMatch || isAoEMatch)
                    {
                        _projectilePendingHits[i].Add(new PendingDamageEntry
                        {
                            MonsterId = monsterId,
                            Damage = damage,
                            NewLife = newLife,
                            IsCrit = isCrit,
                            Position = pos
                        });
                        return; // Queued to pop on impact!
                    }
                }
            }
        }

        // Fallback for melee, DoT, or untracked projectile: immediate visual feedback
        UpdateMonsterLife(monsterId, newLife);
        FloatingText?.SpawnDamageText(damage, pos, type, isCrit);
    }

    /// <summary>
    /// Updates an active monster's remaining health and triggers death fade-out if killed.
    /// </summary>
    public void UpdateMonsterLife(long instanceId, float currentLife)
    {
        for (int i = 0; i < _activeMonsterCount; i++)
        {
            if (_monsters[i].InstanceId == instanceId)
            {
                _monsters[i].CurrentLife = Mathf.Max(0f, currentLife);
                if (currentLife <= 0f && _monsters[i].IsAlive)
                {
                    _monsters[i].IsAlive = false;
                    _monsters[i].DeathFadeTimer = 0.4f;
                }
                break;
            }
        }
    }

    /// <summary>
    /// Synchronizes visual token positions from core MonsterInstance positions.
    /// Called each tick after CombatEngine Phase 4 moves monsters toward the Exile.
    /// Smoothly interpolates visual position to prevent jitter at 30Hz tick rate.
    /// </summary>
    public void SyncMonsterPositions(IReadOnlyList<MonsterInstance> coreMonsters)
    {
        if (coreMonsters == null) return;

        for (int i = 0; i < _activeMonsterCount && i < coreMonsters.Count; i++)
        {
            var core = coreMonsters[i];
            var token = _monsters[i];

            if (token.InstanceId == core.InstanceId)
            {
                if (token.IsAlive && !core.IsAlive)
                {
                    token.IsAlive = false;
                    token.DeathFadeTimer = 0.4f;
                }
                else if (token.IsAlive)
                {
                    token.IsAggroed = core.IsAggroed;
                    // Update base position from core simulation via Camera (Task 2.8)
                    Vector2 targetPos;
                    if (core.Position != Vector2D.Zero)
                    {
                        targetPos = Camera.WorldToScreen(new Vector2(core.Position.X, core.Position.Y), CenterPosition);
                    }
                    else
                    {
                        targetPos = token.BasePosition;
                    }

                    token.BasePosition = targetPos;

                    // Smooth lerp toward target position for visual fluidity
                    token.Position = token.Position.Lerp(targetPos, 0.25f);
                }
            }
        }
    }

    /// <summary>
    /// Updates Exile real-world coordinates and movement telemetry for relative corridor rendering (Task 2.7 & 2.8).
    /// </summary>
    public void UpdateExileWorldPosition(Vector2 worldPos, ExileMovementState state = ExileMovementState.Idle, float speed = 0f, float mapLength = 2400f)
    {
        ExileWorldPosition = worldPos;
        MovementState = state;
        MovementSpeed = speed;
        if (mapLength > 0f) MapLength = mapLength;

        // Auto-seal Fog Gate if Exile advances past the 85% threshold (Task 2.8)
        float fogGateWorldX = MapLength * ExileMovementController.FogGateRatio;
        if (worldPos.X >= fogGateWorldX && !IsFogGateSealed)
        {
            IsFogGateSealed = true;
        }

        QueueRedraw();
    }

    /// <summary>
    /// Awaken dormant monsters in the arena (e.g. Essence Monolith shattered).
    /// </summary>
    public void WakeUpAllMonsters()
    {
        for (int i = 0; i < _activeMonsterCount; i++)
        {
            if (_monsters[i].IsDormant)
            {
                _monsters[i].IsDormant = false;
                _monsters[i].HitFlashTimer = 0.22f; // Crystal shatter flash
            }
        }
    }

    /// <summary>
    /// Attempts to find the 2D visual screen position for the specified monster instance.
    /// Returns true and the token's screen position if found, false otherwise.
    /// </summary>
    public bool TryGetMonsterPosition(long instanceId, out Vector2 position)
    {
        for (int i = 0; i < _activeMonsterCount; i++)
        {
            if (_monsters[i].InstanceId == instanceId)
            {
                position = _monsters[i].Position;
                return true;
            }
        }
        position = Vector2.Zero;
        return false;
    }

    /// <summary>
    /// Returns the 2D arena position for the specified monster instance.
    /// Falls back to default upper arena if not found.
    /// </summary>
    public Vector2 GetMonsterPosition(long instanceId)
    {
        if (TryGetMonsterPosition(instanceId, out var pos))
        {
            return pos;
        }
        return new Vector2(220f, 60f);
    }

    private void SpawnDefaultPackPreview()
    {
        _activeMonsterCount = 4;

        // 1. Rare Goliath (North-East quadrant)
        _monsters[0].InstanceId = 101;
        _monsters[0].Name = "Strand Goliath";
        _monsters[0].Rarity = MonsterRarity.Rare;
        _monsters[0].MaxLife = 450f;
        _monsters[0].CurrentLife = 450f;
        _monsters[0].IsAlive = true;
        _monsters[0].Position = new Vector2(330f, 65f);
        _monsters[0].Scale = 1.35f;
        _monsters[0].CustomColor = new Color(0.28f, 0.24f, 0.10f);

        // 2. Trash mob (North-West quadrant)
        _monsters[1].InstanceId = 102;
        _monsters[1].Name = "Drowned Peon";
        _monsters[1].Rarity = MonsterRarity.Normal;
        _monsters[1].MaxLife = 120f;
        _monsters[1].CurrentLife = 120f;
        _monsters[1].IsAlive = true;
        _monsters[1].Position = new Vector2(110f, 65f);
        _monsters[1].Scale = 0.95f;
        _monsters[1].CustomColor = new Color(0.18f, 0.19f, 0.22f);

        // 3. Trash mob (South-West quadrant)
        _monsters[2].InstanceId = 103;
        _monsters[2].Name = "Drowned Peon";
        _monsters[2].Rarity = MonsterRarity.Normal;
        _monsters[2].MaxLife = 120f;
        _monsters[2].CurrentLife = 120f;
        _monsters[2].IsAlive = true;
        _monsters[2].Position = new Vector2(100f, 145f);
        _monsters[2].Scale = 0.95f;
        _monsters[2].CustomColor = new Color(0.18f, 0.19f, 0.22f);

        // 4. Magic mob (South-East quadrant)
        _monsters[3].InstanceId = 104;
        _monsters[3].Name = "Shore Stalker";
        _monsters[3].Rarity = MonsterRarity.Magic;
        _monsters[3].MaxLife = 220f;
        _monsters[3].CurrentLife = 220f;
        _monsters[3].IsAlive = true;
        _monsters[3].Position = new Vector2(335f, 145f);
        _monsters[3].Scale = 1.1f;
        _monsters[3].CustomColor = new Color(0.12f, 0.20f, 0.32f);
    }

    public static (Color baseColor, Color glowColor) GetDamageTypeColors(DamageType type) => type switch
    {
        DamageType.Physical => (ColorPhysical, new Color(1.0f, 1.0f, 1.0f, 0.35f)),
        DamageType.Fire => (ColorFire, new Color(1.0f, 0.5f, 0.2f, 0.45f)),
        DamageType.Cold => (ColorCold, new Color(0.4f, 0.9f, 1.0f, 0.45f)),
        DamageType.Lightning => (ColorLightning, new Color(1.0f, 1.0f, 0.4f, 0.5f)),
        DamageType.Chaos => (ColorChaos, new Color(0.9f, 0.3f, 1.0f, 0.5f)),
        _ => (Colors.White, Colors.Transparent)
    };
}
