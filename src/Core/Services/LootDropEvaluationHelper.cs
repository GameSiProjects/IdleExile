using System;
using System.Collections.Generic;
using IdleExile.Core.Models;

namespace IdleExile.Core.Services;

/// <summary>
/// Drop value tier classification for loot alerts, audio chimes, and visual effects.
/// Tier 1 corresponds to top-tier "holy grail" drops (Divine, Exalt, Mirror, 6-Link) that trigger
/// the golden edge glow alert and metallic chime.
/// </summary>
public enum DropTier
{
    Normal,
    Mid,
    High,
    Tier1
}

/// <summary>
/// Pure C# evaluation helper for classifying loot drops and evaluating high-tier alerts.
/// Conforms to Task 6.2 ("Khi rớt đồ Tier 1 (Divine/Exalt/6-Link)").
/// </summary>
public static class LootDropEvaluationHelper
{
    private static readonly HashSet<string> Tier1CurrencyIds = new(StringComparer.OrdinalIgnoreCase)
    {
        "divine_orb",
        "exalted_orb",
        "mirror_of_kalandra",
        "vaal_orb"
    };

    /// <summary>
    /// Determines whether the given loot drop qualifies as a Tier 1 drop
    /// (e.g. Divine Orb, Exalted Orb, Mirror, Vaal Orb, 6-Link equipment, or 5-Slot Active Gem).
    /// </summary>
    public static bool IsTier1Drop(LootDrop? drop)
    {
        if (drop == null) return false;

        // 1. High-value currencies
        if (drop.Type == LootDropType.Currency)
        {
            if (Tier1CurrencyIds.Contains(drop.Id))
                return true;

            if (drop.DisplayName.Contains("Divine", StringComparison.OrdinalIgnoreCase) ||
                drop.DisplayName.Contains("Exalted", StringComparison.OrdinalIgnoreCase) ||
                drop.DisplayName.Contains("Mirror", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        // 2. 6-Link Equipment or Unique Rarity
        if (drop.Type == LootDropType.Equipment)
        {
            if (drop.Rarity == LootRarity.Unique)
                return true;

            if (drop.DisplayName.Contains("6-Link", StringComparison.OrdinalIgnoreCase) ||
                drop.Description.Contains("6-Link", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        // 3. Fully socketed Active Gems (5 support slots = 6-Link equivalent in PoE 2)
        if (drop.Type == LootDropType.Gem)
        {
            if (drop.DisplayName.Contains("5-Slot", StringComparison.OrdinalIgnoreCase) ||
                drop.DisplayName.Contains("6-Link", StringComparison.OrdinalIgnoreCase) ||
                drop.Description.Contains("5-Slot", StringComparison.OrdinalIgnoreCase) ||
                drop.Description.Contains("6-Link", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Evaluates the DropTier classification of an item.
    /// </summary>
    public static DropTier EvaluateTier(LootDrop? drop)
    {
        if (drop == null) return DropTier.Normal;

        if (IsTier1Drop(drop))
            return DropTier.Tier1;

        if (drop.Type == LootDropType.Currency)
        {
            if (drop.Id is "chaos_orb" or "orb_of_alchemy" or "regal_orb" or "jewellers_orb" ||
                drop.Id.EndsWith("_fossil", StringComparison.OrdinalIgnoreCase))
            {
                return DropTier.High;
            }
            if (drop.Id is "orb_of_alteration" or "orb_of_augmentation" or "orb_of_transmutation")
            {
                return DropTier.Mid;
            }
        }

        if (drop.Type == LootDropType.Equipment)
        {
            return drop.Rarity switch
            {
                LootRarity.Unique => DropTier.Tier1,
                LootRarity.Rare => DropTier.High,
                LootRarity.Magic => DropTier.Mid,
                _ => DropTier.Normal
            };
        }

        if (drop.Type == LootDropType.Map)
        {
            return DropTier.High;
        }

        return DropTier.Normal;
    }
}
