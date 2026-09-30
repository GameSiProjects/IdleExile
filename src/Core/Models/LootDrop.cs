using System;

namespace IdleExile.Core.Models;

/// <summary>
/// Categorization of dropped loot items for filter evaluation and rendering.
/// </summary>
public enum LootDropType
{
    Currency,
    Equipment,
    Gem,
    Map
}

/// <summary>
/// Rarity tier of dropped loot items (mirrors PoE item rarities).
/// </summary>
public enum LootRarity
{
    Normal,
    Magic,
    Rare,
    Unique
}

/// <summary>
/// Immutable representation of an item dropped in combat.
/// Used by loot generators, seeded drop rules, loot filter, and UI toast overlays.
/// Pure C# model — no Godot dependency.
/// </summary>
public sealed record LootDrop
{
    /// <summary>Unique item identifier (e.g. "orb_of_transmutation", "strand", "added_fire_damage").</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>Human-readable display name (e.g. "Orb of Transmutation").</summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>Item category (Currency, Equipment, Gem, Map).</summary>
    public LootDropType Type { get; init; }

    /// <summary>Item rarity tier (Normal, Magic, Rare, Unique).</summary>
    public LootRarity Rarity { get; init; } = LootRarity.Normal;

    /// <summary>Stack size or item count.</summary>
    public int Quantity { get; init; } = 1;

    /// <summary>Visual hex color token for loot beams, text colors, and toast banners.</summary>
    public string ColorHex { get; init; } = "#FFFFFF";

    /// <summary>Whether this drop was deterministically seeded (FTUE or fail-safe rule).</summary>
    public bool IsSeeded { get; init; } = false;

    /// <summary>Short descriptive label or tooltip text.</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>Factory helper for standard currency drops.</summary>
    public static LootDrop Currency(string id, string displayName, int quantity = 1, string colorHex = "#AA9E82", bool isSeeded = false) => new()
    {
        Id = id,
        DisplayName = displayName,
        Type = LootDropType.Currency,
        Rarity = LootRarity.Normal,
        Quantity = quantity,
        ColorHex = colorHex,
        IsSeeded = isSeeded,
        Description = $"Currency: {displayName}"
    };

    /// <summary>Factory helper for equipment drops.</summary>
    public static LootDrop Equipment(string id, string displayName, LootRarity rarity, string colorHex = "#8888FF", bool isSeeded = false) => new()
    {
        Id = id,
        DisplayName = displayName,
        Type = LootDropType.Equipment,
        Rarity = rarity,
        Quantity = 1,
        ColorHex = colorHex,
        IsSeeded = isSeeded,
        Description = $"{rarity} Equipment"
    };

    /// <summary>Factory helper for gem drops.</summary>
    public static LootDrop Gem(string id, string displayName, string colorHex = "#1BA29B", bool isSeeded = false) => new()
    {
        Id = id,
        DisplayName = displayName,
        Type = LootDropType.Gem,
        Rarity = LootRarity.Normal,
        Quantity = 1,
        ColorHex = colorHex,
        IsSeeded = isSeeded,
        Description = $"Skill Gem: {displayName}"
    };

    /// <summary>Factory helper for map slate drops.</summary>
    public static LootDrop Map(string id, string displayName, int tier, string colorHex = "#C4C4C4", bool isSeeded = false) => new()
    {
        Id = id,
        DisplayName = displayName,
        Type = LootDropType.Map,
        Rarity = LootRarity.Normal,
        Quantity = 1,
        ColorHex = colorHex,
        IsSeeded = isSeeded,
        Description = $"Tier {tier} Map Slate"
    };
}
