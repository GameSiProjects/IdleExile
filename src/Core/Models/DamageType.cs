namespace IdleExile.Core.Models;

/// <summary>
/// The 5 core damage types in Path of Exile / Idle Exile.
/// </summary>
public enum DamageType
{
    Physical = 0,
    Fire = 1,
    Cold = 2,
    Lightning = 3,
    Chaos = 4
}

public static class DamageTypeExtensions
{
    /// <summary>
    /// Returns true if the damage type is Elemental (Fire, Cold, Lightning).
    /// </summary>
    public static bool IsElemental(this DamageType type) =>
        type is DamageType.Fire or DamageType.Cold or DamageType.Lightning;
}
