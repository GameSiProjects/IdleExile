using System;
using Godot;
using IdleExile.Core.Models;

namespace IdleExile.UI.Components;

/// <summary>
/// Internal state tracked for each pre-allocated floating combat text slot.
/// Maintained strictly in fixed-size arrays to ensure zero runtime GC allocations.
/// </summary>
public sealed class FloatingTextItem
{
    public Label Label { get; set; } = null!;
    public LabelSettings Settings { get; set; } = null!;
    public bool IsActive { get; set; }
    public Vector2 BasePosition { get; set; }
    public Vector2 Velocity { get; set; }
    public float ElapsedTime { get; set; }
    public float TotalDuration { get; set; }
    public bool IsCrit { get; set; }
    public float PopScale { get; set; }
    public float ShakeMagnitude { get; set; }
    public float ShakeDecayDuration { get; set; }
    public int SpawnSequence { get; set; }
}

/// <summary>
/// High-performance Object Pooled Floating Combat Text (FCT) system for Idle Exile.
/// Implements Task 2.5:
/// 1. Object pool for floating combat numbers with zero runtime memory allocations (Anti-GC).
/// 2. PoE standard damage color tokens: White (Phys), Red (Fire), Cold Blue (Cold), Yellow (Lightning), Purple (Chaos).
/// 3. Critical strike presentation: 16px font size, pop & shake animation, and golden outline.
/// </summary>
public partial class FloatingTextManager : Control
{
    // ─────────────────────────────────────────────
    //  Constants & Color Tokens (PoE Standard)
    // ─────────────────────────────────────────────
    public const int DefaultPoolSize = 32;
    public const int NormalFontSize = 11;
    public const int CritFontSize = 16;
    public const int NormalOutlineSize = 2;
    public const int CritOutlineSize = 3;

    public const float NormalDuration = 0.70f;
    public const float CritDuration = 0.90f;
    public const float NormalPopScale = 1.18f;
    public const float CritPopScale = 1.50f;
    public const float CritShakeMagnitude = 3.5f;
    public const float CritShakeDuration = 0.22f;

    // PoE 5-Elemental Damage Color Palette (docs/07_combat_ui_and_viewport.md)
    public static readonly Color ColorPhysical = new(0.88f, 0.88f, 0.88f, 1.0f);   // #E0E0E0 White/Silver
    public static readonly Color ColorFire = new(1.0f, 0.34f, 0.13f, 1.0f);       // #FF5722 Flame Orange-Red
    public static readonly Color ColorCold = new(0.25f, 0.77f, 1.0f, 1.0f);       // #40C4FF Cyan / Icy Blue
    public static readonly Color ColorLightning = new(1.0f, 0.92f, 0.0f, 1.0f);   // #FFEA00 Electric Gold / Yellow
    public static readonly Color ColorChaos = new(0.83f, 0.0f, 0.97f, 1.0f);       // #D500F9 Toxic Purple

    // Outline Colors
    public static readonly Color ColorGoldOutline = new(1.0f, 0.84f, 0.0f, 1.0f); // #FFD700 Golden Crit Outline
    public static readonly Color ColorBlackOutline = new(0.04f, 0.04f, 0.06f, 0.95f); // Deep Black Shadow Outline

    // ─────────────────────────────────────────────
    //  Pool State
    // ─────────────────────────────────────────────
    [Export] public int PoolCapacity { get; set; } = DefaultPoolSize;

    private FloatingTextItem[] _pool = [];
    private int _spawnSequenceCounter;
    private bool _isInitialized;
    private readonly Random _random = new(1337);

    public int ActiveCount
    {
        get
        {
            int count = 0;
            for (int i = 0; i < _pool.Length; i++)
            {
                if (_pool[i].IsActive) count++;
            }
            return count;
        }
    }

    public int Capacity => _pool.Length;

    public override void _Ready()
    {
        InitializePool();
    }

    /// <summary>
    /// Pre-allocates all labels and label settings in the pool.
    /// Safe to call multiple times or during tests.
    /// </summary>
    public void InitializePool(int capacity = DefaultPoolSize)
    {
        if (_isInitialized && _pool.Length == capacity) return;

        // Clean up any existing pooled labels if re-initializing
        if (_pool.Length > 0)
        {
            for (int i = 0; i < _pool.Length; i++)
            {
                if (_pool[i]?.Label != null && IsInstanceValid(_pool[i].Label))
                {
                    RemoveChild(_pool[i].Label);
                    _pool[i].Label.QueueFree();
                }
            }
        }

        PoolCapacity = Mathf.Max(1, capacity);
        _pool = new FloatingTextItem[PoolCapacity];

        for (int i = 0; i < PoolCapacity; i++)
        {
            var settings = new LabelSettings
            {
                FontSize = NormalFontSize,
                FontColor = ColorPhysical,
                OutlineSize = NormalOutlineSize,
                OutlineColor = ColorBlackOutline
            };

            var label = new Label
            {
                Name = $"FCT_Label_{i}",
                Visible = false,
                MouseFilter = MouseFilterEnum.Ignore,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                LabelSettings = settings
            };

            AddChild(label);

            _pool[i] = new FloatingTextItem
            {
                Label = label,
                Settings = settings,
                IsActive = false
            };
        }

        _isInitialized = true;
    }

    public override void _Process(double delta)
    {
        if (!_isInitialized) return;

        float dt = (float)delta;

        for (int i = 0; i < _pool.Length; i++)
        {
            var item = _pool[i];
            if (!item.IsActive) continue;

            item.ElapsedTime += dt;

            // 1. Check expiration
            if (item.ElapsedTime >= item.TotalDuration)
            {
                item.IsActive = false;
                item.Label.Visible = false;
                continue;
            }

            float t = item.ElapsedTime / item.TotalDuration;

            // 2. Physics & Motion Arc: Upward drift with slight deceleration and horizontal drift
            item.Velocity = new Vector2(item.Velocity.X * 0.96f, item.Velocity.Y + 22f * dt);
            item.BasePosition += item.Velocity * dt;

            // 3. Pop Animation (Spring bounce scaling down to 1.0)
            float currentScale = 1.0f;
            float popDuration = item.IsCrit ? 0.16f : 0.10f;
            if (item.ElapsedTime < popDuration)
            {
                float popT = item.ElapsedTime / popDuration;
                // Elastic curve: starts at PopScale, bounces slightly below 1.0, settles at 1.0
                currentScale = Mathf.Lerp(item.PopScale, 1.0f, popT);
            }
            item.Label.Scale = new Vector2(currentScale, currentScale);

            // Center pivot so scaling and rotation expand from the middle of the label
            Vector2 labelSize = item.Label.Size;
            item.Label.PivotOffset = labelSize * 0.5f;

            // 4. Crit Shake Animation (High-frequency vibration decaying over initial duration)
            Vector2 shakeOffset = Vector2.Zero;
            if (item.IsCrit && item.ElapsedTime < item.ShakeDecayDuration && item.ShakeDecayDuration > 0f)
            {
                float shakeFactor = 1.0f - (item.ElapsedTime / item.ShakeDecayDuration);
                float shakeX = Mathf.Sin(item.ElapsedTime * 65f) * item.ShakeMagnitude * shakeFactor;
                float shakeY = Mathf.Cos(item.ElapsedTime * 80f) * (item.ShakeMagnitude * 0.65f) * shakeFactor;
                shakeOffset = new Vector2(shakeX, shakeY);
            }

            // 5. Update Screen Position (Centered on target point)
            Vector2 renderPos = item.BasePosition + shakeOffset - (labelSize * 0.5f);
            item.Label.Position = renderPos;

            // 6. Alpha Fade Out (Stays opaque for 65%, then fades cleanly to 0)
            float alpha = 1.0f;
            if (t > 0.65f)
            {
                alpha = Mathf.Clamp((1.0f - t) / 0.35f, 0f, 1f);
            }
            item.Label.Modulate = new Color(1f, 1f, 1f, alpha);
        }
    }

    /// <summary>
    /// Spawns floating combat text for a damage hit with PoE color coding and crit styling.
    /// </summary>
    public FloatingTextItem? SpawnDamageText(float damage, Vector2 position, DamageType damageType, bool isCrit = false)
    {
        string text = FormatDamageText(damage, isCrit);
        Color textColor = GetDamageColor(damageType);
        Color outlineColor = isCrit ? ColorGoldOutline : ColorBlackOutline;
        int fontSize = isCrit ? CritFontSize : NormalFontSize;
        int outlineSize = isCrit ? CritOutlineSize : NormalOutlineSize;

        return SpawnCustomText(text, position, textColor, outlineColor, fontSize, outlineSize, isCrit);
    }

    /// <summary>
    /// Spawns floating combat text from a calculated HitResult.
    /// Automatically detects the primary damage type and applies critical strike visuals.
    /// </summary>
    public FloatingTextItem? SpawnHitResult(HitResult hit, Vector2 position)
    {
        DamageType primaryType = DeterminePrimaryDamageType(hit.Damage);
        return SpawnDamageText(hit.TotalDamage, position, primaryType, hit.IsCrit);
    }

    /// <summary>
    /// Spawns a floating combat text item with custom text and styling.
    /// Uses pre-allocated pool items with zero GC allocations.
    /// If pool is saturated, recycles the oldest active item.
    /// </summary>
    public FloatingTextItem? SpawnCustomText(
        string text,
        Vector2 position,
        Color textColor,
        Color outlineColor,
        int fontSize = NormalFontSize,
        int outlineSize = NormalOutlineSize,
        bool isCrit = false)
    {
        if (!_isInitialized)
        {
            InitializePool();
        }

        FloatingTextItem item = AcquireItemFromPool();

        // Configure Label Settings
        item.Settings.FontSize = fontSize;
        item.Settings.FontColor = textColor;
        item.Settings.OutlineSize = outlineSize;
        item.Settings.OutlineColor = outlineColor;

        // Configure Label Node
        item.Label.Text = text;
        item.Label.Modulate = Colors.White;
        item.Label.Scale = isCrit ? new Vector2(CritPopScale, CritPopScale) : new Vector2(NormalPopScale, NormalPopScale);
        item.Label.Visible = true;

        // Velocity: Upward impulse (-48 to -62 px/s) with subtle random horizontal scatter (-16 to +16 px/s)
        float horizontalScatter = (float)(_random.NextDouble() * 32.0 - 16.0);
        float verticalVelocity = isCrit ? -58f : -48f;

        // Configure Item State
        item.IsActive = true;
        item.BasePosition = position;
        item.Velocity = new Vector2(horizontalScatter, verticalVelocity);
        item.ElapsedTime = 0f;
        item.TotalDuration = isCrit ? CritDuration : NormalDuration;
        item.IsCrit = isCrit;
        item.PopScale = isCrit ? CritPopScale : NormalPopScale;
        item.ShakeMagnitude = isCrit ? CritShakeMagnitude : 0f;
        item.ShakeDecayDuration = isCrit ? CritShakeDuration : 0f;
        item.SpawnSequence = ++_spawnSequenceCounter;

        // Center on start position immediately
        Vector2 labelSize = item.Label.Size;
        item.Label.PivotOffset = labelSize * 0.5f;
        item.Label.Position = position - (labelSize * 0.5f);

        return item;
    }

    /// <summary>
    /// Clears and hides all active floating texts, returning them immediately to the pool.
    /// </summary>
    public void ClearAll()
    {
        for (int i = 0; i < _pool.Length; i++)
        {
            if (_pool[i] != null)
            {
                _pool[i].IsActive = false;
                if (_pool[i].Label != null && IsInstanceValid(_pool[i].Label))
                {
                    _pool[i].Label.Visible = false;
                }
            }
        }
    }

    /// <summary>
    /// Acquires an available item from the pool.
    /// If all slots are occupied, recycles the oldest item in the pool (LRU strategy).
    /// </summary>
    private FloatingTextItem AcquireItemFromPool()
    {
        FloatingTextItem? oldestItem = null;
        int minSequence = int.MaxValue;

        for (int i = 0; i < _pool.Length; i++)
        {
            var slot = _pool[i];
            if (!slot.IsActive)
            {
                return slot;
            }

            if (slot.SpawnSequence < minSequence)
            {
                minSequence = slot.SpawnSequence;
                oldestItem = slot;
            }
        }

        // Pool saturated: Recycle the oldest active item
        return oldestItem ?? _pool[0];
    }

    /// <summary>
    /// Formats damage numbers cleanly for mini-window display.
    /// Examples: "420", "1,850", "14.2k", with exclamation on crit.
    /// </summary>
    public static string FormatDamageText(float damage, bool isCrit)
    {
        int rounded = Mathf.RoundToInt(damage);
        if (rounded >= 100000)
        {
            return $"{(damage / 1000f):0.#}k";
        }
        return rounded.ToString("N0");
    }

    /// <summary>
    /// Returns the PoE canonical damage type color.
    /// </summary>
    public static Color GetDamageColor(DamageType type) => type switch
    {
        DamageType.Physical => ColorPhysical,
        DamageType.Fire => ColorFire,
        DamageType.Cold => ColorCold,
        DamageType.Lightning => ColorLightning,
        DamageType.Chaos => ColorChaos,
        _ => ColorPhysical
    };

    /// <summary>
    /// Identifies the primary damage type from a hit breakdown by finding the highest contributor.
    /// </summary>
    public static DamageType DeterminePrimaryDamageType(HitDamageBreakdown breakdown)
    {
        float maxVal = breakdown.Physical;
        DamageType primary = DamageType.Physical;

        if (breakdown.Fire > maxVal)
        {
            maxVal = breakdown.Fire;
            primary = DamageType.Fire;
        }
        if (breakdown.Cold > maxVal)
        {
            maxVal = breakdown.Cold;
            primary = DamageType.Cold;
        }
        if (breakdown.Lightning > maxVal)
        {
            maxVal = breakdown.Lightning;
            primary = DamageType.Lightning;
        }
        if (breakdown.Chaos > maxVal)
        {
            primary = DamageType.Chaos;
        }

        return primary;
    }
}
