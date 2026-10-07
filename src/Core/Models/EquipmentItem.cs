using System;
using System.Collections.Generic;

namespace IdleExile.Core.Models;

/// <summary>
/// Immutable data representation of an equippable item instance or drop.
/// Used by inventory, drop coordinator, loot filters, and the Thaumaturgic Smelter system.
/// Pure C# model — zero Godot dependencies.
/// </summary>
public sealed record EquipmentItem(
    string Id,
    string Name,
    LootRarity Rarity,
    int ItemLevel,
    int AffixCount = 0,
    GearSlot Slot = GearSlot.BodyArmour,
    int Quality = 0,
    bool IsIdentified = true,
    IReadOnlyList<string>? AffixNames = null
)
{
    /// <summary>
    /// Creates an EquipmentItem representation from a runtime IGearItem.
    /// </summary>
    public static EquipmentItem FromGear(IGearItem gear)
    {
        if (gear == null) throw new ArgumentNullException(nameof(gear));

        return new EquipmentItem(
            Id: gear.InstanceId,
            Name: gear.Name,
            Rarity: ParseRarity(gear.Rarity),
            ItemLevel: Math.Max(1, gear.ItemLevel),
            AffixCount: gear.Affixes.Count,
            Slot: gear.TargetSlot,
            Quality: gear.Quality,
            IsIdentified: true
        );
    }

    /// <summary>
    /// Creates an EquipmentItem representation from a dropped loot item.
    /// </summary>
    public static EquipmentItem FromLootDrop(LootDrop drop, int itemLevel = 1, int? affixCount = null)
    {
        if (drop == null) throw new ArgumentNullException(nameof(drop));

        int resolvedAffixes = affixCount ?? EstimateAffixCount(drop.Rarity);
        return new EquipmentItem(
            Id: drop.Id,
            Name: drop.DisplayName,
            Rarity: drop.Rarity,
            ItemLevel: Math.Max(1, itemLevel),
            AffixCount: resolvedAffixes,
            Slot: GuessSlot(drop.Id),
            Quality: 0,
            IsIdentified: true
        );
    }

    /// <summary>
    /// Parses string rarity into a strongly-typed LootRarity enum.
    /// </summary>
    public static LootRarity ParseRarity(string? rarityStr)
    {
        if (string.IsNullOrWhiteSpace(rarityStr)) return LootRarity.Normal;
        return Enum.TryParse<LootRarity>(rarityStr, true, out var r) ? r : LootRarity.Normal;
    }

    /// <summary>
    /// Estimates a standard affix count for an item based on its rarity tier.
    /// Normal: 0, Magic: 1, Rare: 4, Unique: 4.
    /// </summary>
    public static int EstimateAffixCount(LootRarity rarity) => rarity switch
    {
        LootRarity.Normal => 0,
        LootRarity.Magic => 1,
        LootRarity.Rare => 4,
        LootRarity.Unique => 4,
        _ => 0
    };

    /// <summary>
    /// Infers equipment slot from item identifier conventions.
    /// </summary>
    public static GearSlot GuessSlot(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return GearSlot.BodyArmour;

        string lower = id.ToLowerInvariant();
        if (lower.Contains("sword") || lower.Contains("bow") || lower.Contains("wand") || lower.Contains("axe") || lower.Contains("mace") || lower.Contains("dagger") || lower.Contains("staff"))
            return GearSlot.MainHand;
        if (lower.Contains("shield") || lower.Contains("quiver"))
            return GearSlot.OffHand;
        if (lower.Contains("helmet") || lower.Contains("circlet") || lower.Contains("coif") || lower.Contains("crown") || lower.Contains("helm"))
            return GearSlot.Helmet;
        if (lower.Contains("glove") || lower.Contains("gauntlet") || lower.Contains("mitt"))
            return GearSlot.Gloves;
        if (lower.Contains("boot") || lower.Contains("greave") || lower.Contains("shoe"))
            return GearSlot.Boots;
        if (lower.Contains("belt") || lower.Contains("sash"))
            return GearSlot.Belt;
        if (lower.Contains("amulet") || lower.Contains("talisman"))
            return GearSlot.Amulet;
        if (lower.Contains("ring"))
            return GearSlot.Ring1;

        return GearSlot.BodyArmour;
    }
}
