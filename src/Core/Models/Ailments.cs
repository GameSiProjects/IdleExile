namespace IdleExile.Core.Models;

/// <summary>
/// Status ailments supported by the combat engine.
/// </summary>
public enum AilmentType
{
    Ignite,
    Chill,
    Freeze,
    Shock,
    Bleed,
    Poison
}

/// <summary>
/// An applied ailment instance resulting from a damaging hit.
/// </summary>
public sealed record AilmentInstance(
    AilmentType Type,
    DamageType SourceDamageType,
    float DamagePerSecond,
    float EffectPercent,
    float DurationSeconds,
    bool IsStacking,
    bool PreventsAction = false
);

public static class AilmentConstants
{
    // Ignite (Fire DoT)
    public const float IgniteBaseDuration = 4.0f;
    public const float IgniteBaseDamageRatio = 0.90f; // 90% of base fire damage per second

    // Bleed (Physical DoT, Attacks)
    public const float BleedBaseDuration = 5.0f;
    public const float BleedBaseDamageRatio = 0.70f;   // 70% of base physical damage per second
    public const float BleedMovingDamageRatio = 2.10f; // 210% while target is moving

    // Poison (Chaos DoT, Stacking)
    public const float PoisonBaseDuration = 2.0f;
    public const float PoisonBaseDamageRatio = 0.30f; // 30% of combined base physical + chaos damage per second

    // Chill (Cold Slow)
    public const float ChillBaseDuration = 2.0f;
    public const float ChillMinThreshold = 5.0f;  // Minimum 5% slow required to apply
    public const float ChillMaxCap = 30.0f;       // Maximum 30% slow cap

    // Freeze (Cold Incapacitate)
    public const float FreezeMinDuration = 0.3f; // Below 0.3s (300ms), freeze is discarded
    public const float FreezeMaxDuration = 3.0f; // Cap at 3.0s

    // Shock (Lightning +% Damage Taken)
    public const float ShockBaseDuration = 2.0f;
    public const float ShockMinThreshold = 5.0f;  // Minimum 5% increased damage taken
    public const float ShockMaxCap = 50.0f;       // Maximum 50% increased damage taken cap
}
