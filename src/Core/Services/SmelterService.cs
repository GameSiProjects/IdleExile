using System;
using System.Collections.Generic;
using IdleExile.Core.Models;
using IdleExile.Core.State;

namespace IdleExile.Core.Services;

/// <summary>
/// Thaumaturgic Smelter service (Crucible Sanctuary module).
/// Pure C# domain logic — zero Godot dependencies!
///
/// Responsibilities:
/// 1. Evaluates dropped or stored items against the loot filter criteria.
/// 2. Disassembles filtered/junk equipment into Thaumaturgic Dust:
///    Dust = ItemLevel * RarityMultiplier * Max(1, AffixCount) * (1 + TierBonus).
/// 3. Manages Dust economy interactions:
///    - Infuse next maps (+10% IIQ for 5 maps).
///    - Transmute Dust into high-tier currencies (Orb of Unmaking, Chaos Orb).
/// </summary>
public sealed class SmelterService
{
    public const int DefaultInfuseCost = 2500;
    public const int DefaultInfuseMapCount = 5;
    public const float DefaultInfuseBonusQuantity = 0.10f;
    public const int DefaultTransmuteCost = 10000;

    public const string CurrencyOrbOfUnmaking = "orb_of_unmaking";
    public const string CurrencyChaosOrb = "chaos_orb";

    private readonly Dictionary<LootRarity, float> _rarityMultipliers = new()
    {
        [LootRarity.Normal] = 1.0f,
        [LootRarity.Magic] = 3.0f,
        [LootRarity.Rare] = 10.0f,
        [LootRarity.Unique] = 25.0f
    };

    /// <summary>
    /// Event fired when an item is broken down into Thaumaturgic Dust.
    /// Parameters: (int dustGained, string itemName).
    /// </summary>
    public event Action<int, string>? OnItemSmelted;

    /// <summary>
    /// Event fired when Thaumaturgic Dust is transmuted into currency.
    /// Parameters: (string currencyId, int quantity).
    /// </summary>
    public event Action<string, int>? OnCurrencyTransmuted;

    /// <summary>
    /// Event fired when maps are infused with Thaumaturgic mist.
    /// Parameters: (int mapCount, float bonusQuantity).
    /// </summary>
    public event Action<int, float>? OnMapsInfused;

    public SmelterService(SmelterRecipeConfig? config = null)
    {
        if (config?.RarityMultipliers != null)
        {
            foreach (var kvp in config.RarityMultipliers)
            {
                if (Enum.TryParse<LootRarity>(kvp.Key, true, out var rarity))
                {
                    _rarityMultipliers[rarity] = kvp.Value;
                }
            }
        }
    }

    // ─────────────────────────────────────────────
    //  Dust Yield Calculations
    // ─────────────────────────────────────────────

    /// <summary>
    /// Calculates the Thaumaturgic Dust yield from an EquipmentItem.
    /// Formula: ilvl * RarityMultiplier * Max(1, AffixCount) * (1 + speedOrTierBonus).
    /// </summary>
    public int CalculateDustYield(EquipmentItem item, float speedOrTierBonus = 0f)
    {
        if (item == null) return 0;
        return CalculateDustYield(item.ItemLevel, item.Rarity, item.AffixCount, speedOrTierBonus);
    }

    /// <summary>
    /// Calculates dust yield from a runtime IGearItem.
    /// </summary>
    public int CalculateDustYield(IGearItem gear, float speedOrTierBonus = 0f)
    {
        if (gear == null) return 0;
        var equip = EquipmentItem.FromGear(gear);
        return CalculateDustYield(equip, speedOrTierBonus);
    }

    /// <summary>
    /// Calculates dust yield from a dropped LootDrop.
    /// </summary>
    public int CalculateDustYield(LootDrop drop, int itemLevel = 1, float speedOrTierBonus = 0f)
    {
        if (drop == null || drop.Type != LootDropType.Equipment) return 0;
        var equip = EquipmentItem.FromLootDrop(drop, itemLevel);
        return CalculateDustYield(equip, speedOrTierBonus);
    }

    /// <summary>
    /// Core mathematical formula for dust yield.
    /// </summary>
    public int CalculateDustYield(int itemLevel, LootRarity rarity, int affixCount = 0, float speedOrTierBonus = 0f)
    {
        int safeIlvl = Math.Max(1, itemLevel);
        float rarityMult = _rarityMultipliers.TryGetValue(rarity, out float mult) ? mult : 1.0f;
        int affixFactor = Math.Max(1, affixCount);

        float rawYield = safeIlvl * rarityMult * affixFactor;
        if (speedOrTierBonus > 0f)
        {
            rawYield *= (1.0f + speedOrTierBonus);
        }

        return Math.Max(1, (int)MathF.Round(rawYield));
    }

    // ─────────────────────────────────────────────
    //  Automated Filtered Drop Processing
    // ─────────────────────────────────────────────

    /// <summary>
    /// Processes an equipment drop hidden by the loot filter.
    /// If AutoSmeltActive is enabled on the SanctuaryState, immediately adds the dust to the player's pool
    /// and fires the OnItemSmelted event.
    /// </summary>
    public int ProcessFilteredDrop(EquipmentItem droppedItem, SanctuaryState state, float speedOrTierBonus = 0f)
    {
        if (droppedItem == null || state == null) return 0;
        if (!state.AutoSmeltActive) return 0;

        int dust = CalculateDustYield(droppedItem, speedOrTierBonus);
        if (dust <= 0) return 0;

        state.AddDust(dust);
        OnItemSmelted?.Invoke(dust, droppedItem.Name);
        return dust;
    }

    /// <summary>
    /// Overload for processing a runtime IGearItem.
    /// </summary>
    public int ProcessFilteredDrop(IGearItem gear, SanctuaryState state, float speedOrTierBonus = 0f)
    {
        if (gear == null || state == null) return 0;
        var equip = EquipmentItem.FromGear(gear);
        return ProcessFilteredDrop(equip, state, speedOrTierBonus);
    }

    /// <summary>
    /// Overload for processing a dropped LootDrop.
    /// </summary>
    public int ProcessFilteredDrop(LootDrop drop, SanctuaryState state, int itemLevel = 1, float speedOrTierBonus = 0f)
    {
        if (drop == null || state == null || drop.Type != LootDropType.Equipment) return 0;
        var equip = EquipmentItem.FromLootDrop(drop, itemLevel);
        return ProcessFilteredDrop(equip, state, speedOrTierBonus);
    }

    // ─────────────────────────────────────────────
    //  Loot Filter Evaluation
    // ─────────────────────────────────────────────

    /// <summary>
    /// Evaluates whether a dropped loot item should be hidden by the loot filter and auto-smelted.
    /// White (Normal) and standard Blue (Magic) equipment are filtered out by default unless Tier 1 / Unique / 6-Link.
    /// </summary>
    public static bool IsFilteredOut(LootDrop? drop, bool filterRares = false)
    {
        if (drop == null) return false;
        if (drop.Type != LootDropType.Equipment) return false;

        // Seeded drops (FTUE, quest items) must always be shown to the player
        if (drop.IsSeeded) return false;

        // Tier 1 drops (Unique, 6-Link) are never filtered out
        if (LootDropEvaluationHelper.IsTier1Drop(drop)) return false;
        if (drop.Rarity == LootRarity.Unique) return false;

        // Rare equipment is shown unless filterRares is explicitly enabled
        if (drop.Rarity == LootRarity.Rare) return filterRares;

        // Normal and Magic non-Tier1 equipment are filtered out
        return true;
    }

    /// <summary>
    /// Evaluates whether an EquipmentItem should be hidden by the loot filter.
    /// </summary>
    public static bool IsFilteredOut(EquipmentItem? item, bool filterRares = false)
    {
        if (item == null) return false;
        if (item.Rarity == LootRarity.Unique) return false;
        if (item.Rarity == LootRarity.Rare) return filterRares;
        return true;
    }

    // ─────────────────────────────────────────────
    //  Dust Economy Actions
    // ─────────────────────────────────────────────

    /// <summary>
    /// Consumes Thaumaturgic Dust to infuse upcoming map runs with increased item quantity (+10% IIQ by default).
    /// </summary>
    public bool TryInfuseNextMaps(
        SanctuaryState state,
        int cost = DefaultInfuseCost,
        int mapCount = DefaultInfuseMapCount,
        float bonusQuantity = DefaultInfuseBonusQuantity)
    {
        if (state == null || cost <= 0 || mapCount <= 0) return false;
        if (state.ThaumaturgicDust < cost) return false;

        if (!state.TryConsumeDust(cost)) return false;

        state.InfuseMaps(mapCount, bonusQuantity);
        OnMapsInfused?.Invoke(mapCount, bonusQuantity);
        return true;
    }

    /// <summary>
    /// Transmutes a block of Thaumaturgic Dust into currency (e.g. 10,000 Dust -> 1x Orb of Unmaking or Chaos Orb).
    /// If an AffinityStashState is provided, deposits the currency directly into the stash.
    /// </summary>
    public bool TryTransmuteToCurrency(
        SanctuaryState state,
        string currencyId,
        int cost = DefaultTransmuteCost,
        AffinityStashState? stash = null,
        int quantity = 1)
    {
        if (state == null || string.IsNullOrWhiteSpace(currencyId) || cost <= 0 || quantity <= 0)
            return false;

        if (state.ThaumaturgicDust < cost) return false;

        if (!state.TryConsumeDust(cost)) return false;

        stash?.DepositCurrency(currencyId, quantity);
        OnCurrencyTransmuted?.Invoke(currencyId, quantity);
        return true;
    }

    /// <summary>
    /// Convenience helper: Transmutes 10,000 Dust into 1x Orb of Unmaking.
    /// </summary>
    public bool TryTransmuteToUnmaking(SanctuaryState state, AffinityStashState? stash = null, int cost = DefaultTransmuteCost)
    {
        return TryTransmuteToCurrency(state, CurrencyOrbOfUnmaking, cost, stash, 1);
    }

    /// <summary>
    /// Convenience helper: Transmutes 10,000 Dust into 1x Chaos Orb.
    /// </summary>
    public bool TryTransmuteToChaos(SanctuaryState state, AffinityStashState? stash = null, int cost = DefaultTransmuteCost)
    {
        return TryTransmuteToCurrency(state, CurrencyChaosOrb, cost, stash, 1);
    }
}
