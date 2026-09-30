namespace IdleExile.Core.Models;

/// <summary>
/// Immutable snapshot of a character's defensive configuration.
/// Captures all stats needed by <see cref="IdleExile.Core.Services.DefensiveLayers"/>
/// to evaluate incoming damage through the full PoE defensive pipeline.
/// </summary>
public sealed record DefensiveStats
{
    // --- Armour (Physical Damage Reduction) ---

    /// <summary>
    /// Total Armour rating after all Increased/More modifiers.
    /// Used in the non-linear formula: % Reduction = Armour / (Armour + 5 * Damage).
    /// </summary>
    public float Armour { get; init; } = 0f;

    // --- Evasion (Entropy-Based Avoidance) ---

    /// <summary>
    /// Chance to evade attacks as a percentage (0–100).
    /// Fed into the deterministic Evasion Entropy counter, not rolled per-hit via RNG.
    /// </summary>
    public float EvasionChance { get; init; } = 0f;

    // --- Block ---

    /// <summary>Attack Block chance in percent (0–75, hard-capped by PoE).</summary>
    public float AttackBlockChance { get; init; } = 0f;

    /// <summary>Spell Block chance in percent (0–75, hard-capped by PoE).</summary>
    public float SpellBlockChance { get; init; } = 0f;

    // --- Spell Suppression ---

    /// <summary>
    /// Chance to suppress spell damage (0–100%). Successful suppression halves spell damage.
    /// </summary>
    public float SpellSuppressionChance { get; init; } = 0f;

    // --- Elemental & Chaos Resistances ---

    /// <summary>Total Fire Resistance including overcap (e.g. 109% with 75% cap → 34% overcap).</summary>
    public float FireResistance { get; init; } = 0f;

    /// <summary>Total Cold Resistance including overcap.</summary>
    public float ColdResistance { get; init; } = 0f;

    /// <summary>Total Lightning Resistance including overcap.</summary>
    public float LightningResistance { get; init; } = 0f;

    /// <summary>Total Chaos Resistance including overcap.</summary>
    public float ChaosResistance { get; init; } = 0f;

    // --- Max Resistance Caps ---

    /// <summary>Maximum Fire Resistance cap (default 75%, up to 90% via gear/passives).</summary>
    public float MaxFireResistance { get; init; } = 75f;

    /// <summary>Maximum Cold Resistance cap (default 75%, up to 90%).</summary>
    public float MaxColdResistance { get; init; } = 75f;

    /// <summary>Maximum Lightning Resistance cap (default 75%, up to 90%).</summary>
    public float MaxLightningResistance { get; init; } = 75f;

    /// <summary>Maximum Chaos Resistance cap (default 75%, up to 90%).</summary>
    public float MaxChaosResistance { get; init; } = 75f;

    // --- Energy Shield Recharge ---

    /// <summary>
    /// Duration in seconds before ES begins recharging after taking damage.
    /// PoE default is 2.0 seconds.
    /// </summary>
    public float EnergyShieldRechargeDelay { get; init; } = 2.0f;

    /// <summary>
    /// Percentage of max ES recharged per second once recharge starts.
    /// PoE default is 33% per second (full recharge in ~3s).
    /// </summary>
    public float EnergyShieldRechargeRate { get; init; } = 33.0f;

    // --- Keystones & Special Mechanics ---

    /// <summary>
    /// Chaos Inoculation: Maximum Life locked to 1, but 100% immune to Chaos damage.
    /// </summary>
    public bool ChaosInoculation { get; init; } = false;

    /// <summary>
    /// If true, Chaos damage does NOT bypass Energy Shield (requires specific gear mod).
    /// Default: false (Chaos damage bypasses ES to hit Life directly).
    /// </summary>
    public bool ChaosDoesNotBypassES { get; init; } = false;

    /// <summary>
    /// Mind over Matter (MoM): Percentage of damage taken from Mana before Life.
    /// Default 0. Typical keystone value is 40%.
    /// </summary>
    public float MindOverMatterPercent { get; init; } = 0f;

    // --- Regeneration ---

    /// <summary>Life regeneration per second (flat value after all modifiers).</summary>
    public float LifeRegenPerSecond { get; init; } = 0f;

    /// <summary>Mana regeneration per second (flat value after all modifiers).</summary>
    public float ManaRegenPerSecond { get; init; } = 0f;

    // --- Convenience Methods ---

    /// <summary>
    /// Returns the effective (capped) resistance for a given damage type.
    /// Physical and Chaos have special handling (Physical uses Armour, not resistance).
    /// </summary>
    public float GetEffectiveResistance(DamageType type) => type switch
    {
        DamageType.Fire => System.MathF.Min(FireResistance, MaxFireResistance),
        DamageType.Cold => System.MathF.Min(ColdResistance, MaxColdResistance),
        DamageType.Lightning => System.MathF.Min(LightningResistance, MaxLightningResistance),
        DamageType.Chaos => ChaosInoculation ? 100f : System.MathF.Min(ChaosResistance, MaxChaosResistance),
        _ => 0f // Physical uses Armour, not resistance
    };

    /// <summary>
    /// Returns the overcap amount for a given elemental resistance type.
    /// Overcap = TotalRes - MaxRes (if positive). Useful for resist-stripping map mods.
    /// </summary>
    public float GetOvercap(DamageType type) => type switch
    {
        DamageType.Fire => System.MathF.Max(0f, FireResistance - MaxFireResistance),
        DamageType.Cold => System.MathF.Max(0f, ColdResistance - MaxColdResistance),
        DamageType.Lightning => System.MathF.Max(0f, LightningResistance - MaxLightningResistance),
        DamageType.Chaos => ChaosInoculation ? 0f : System.MathF.Max(0f, ChaosResistance - MaxChaosResistance),
        _ => 0f
    };
}
