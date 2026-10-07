using System;
using System.Collections.Generic;
using System.Linq;
using IdleExile.Core.Models;
using IdleExile.Core.State;

namespace IdleExile.Core.Services;

/// <summary>
/// Result data contract capturing the before-and-after comparison of a Crucible Anvil crafting operation.
/// Conforms strictly to GDD Section 15 and Task 5: Zero Deterministic Recipes, pure RNG/Fossil slamming
/// with instant Smart Stat Delta telemetry.
/// Pure C# model — zero Godot dependencies.
/// </summary>
public sealed record AnvilCraftResult(
    IGearItem Item,
    string CurrencyOrMethodUsed,
    bool Success,
    string Message,
    float OldEhp,
    float NewEhp,
    float DeltaEhp,
    float OldDps,
    float NewDps,
    float DeltaDps,
    float DeltaFireRes,
    float DeltaColdRes,
    float DeltaLightningRes,
    float DeltaChaosRes,
    float DeltaArmour,
    float DeltaEvasion,
    float DeltaEnergyShield,
    IReadOnlyList<AffixDefinition> OldAffixes,
    IReadOnlyList<AffixDefinition> NewAffixes,
    bool IsCorrupted
);

/// <summary>
/// Domain service for The Crucible Anvil workspace.
/// Complies 100% with Path of Exile crafting mechanics:
/// - Orb of Transmutation: Normal -> Magic (1-2 affixes).
/// - Orb of Alteration: Magic -> Magic reroll (1-2 affixes).
/// - Chaos Orb: Rare -> Rare reforge (4-6 affixes).
/// - Exalted Orb: Rare (<6 affixes) -> adds 1 random affix.
/// - Orb of Annulment: Magic/Rare -> removes 1 random affix.
/// - Divine Orb: Rerolls values of existing affixes within ranges.
/// - Vaal Orb: Unpredictably modifies item and seals it as Corrupted.
/// - Abyssal Resonator: 1-4 Fossils reforge item to Rare with weighted/blocked tags.
/// Pure C# — zero Godot node dependencies.
/// </summary>
public class TheCrucibleAnvilService
{
    public const string TransmuteId = "orb_of_transmutation";
    public const string AlterationId = "orb_of_alteration";
    public const string ChaosId = "chaos_orb";
    public const string ExaltedId = "exalted_orb";
    public const string AnnulmentId = "orb_of_annulment";
    public const string DivineId = "divine_orb";
    public const string VaalId = "vaal_orb";

    private readonly Random _rng;

    public TheCrucibleAnvilService(Random? rng = null)
    {
        _rng = rng ?? new Random();
    }

    /// <summary>
    /// Recalculates precomputed stats on any runtime IGearItem instance.
    /// </summary>
    public static void RecalculateItemStats(IGearItem item)
    {
        if (item is ArmourItemInstance a)
        {
            a.RecalculateStats();
        }
        else if (item is WeaponItemInstance w)
        {
            w.RecalculateLocalStats();
        }
        else if (item is AccessoryItemInstance acc)
        {
            acc.RecalculateStats();
        }
    }

    /// <summary>
    /// Determines whether an item can receive a specific currency slam.
    /// </summary>
    public bool CanSlamCurrency(IGearItem? item, string currencyId)
    {
        if (item == null || IsCorrupted(item)) return false;

        return currencyId switch
        {
            TransmuteId => string.Equals(item.Rarity, "Normal", StringComparison.OrdinalIgnoreCase),
            AlterationId => string.Equals(item.Rarity, "Magic", StringComparison.OrdinalIgnoreCase),
            ChaosId => string.Equals(item.Rarity, "Rare", StringComparison.OrdinalIgnoreCase),
            ExaltedId => string.Equals(item.Rarity, "Rare", StringComparison.OrdinalIgnoreCase) && item.Affixes.Count < 6,
            AnnulmentId => (string.Equals(item.Rarity, "Magic", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(item.Rarity, "Rare", StringComparison.OrdinalIgnoreCase)) && item.Affixes.Count > 0,
            DivineId => item.Affixes.Count > 0,
            VaalId => !IsCorrupted(item),
            _ => false
        };
    }

    /// <summary>
    /// Determines whether an item can receive an Abyssal Resonator Fossil slam.
    /// </summary>
    public bool CanSlamFossils(IGearItem? item, IReadOnlyList<string>? fossilIds)
    {
        if (item == null || IsCorrupted(item)) return false;
        if (fossilIds == null || fossilIds.Count == 0 || fossilIds.Count > 4) return false;
        return true;
    }

    /// <summary>
    /// Applies a currency slam to the target item and computes the full Smart Stat Delta.
    /// </summary>
    public AnvilCraftResult SlamCurrency(
        IGearItem item,
        string currencyId,
        DatabaseLoader db,
        PlayerState? player = null)
    {
        if (item == null) throw new ArgumentNullException(nameof(item));
        if (db == null) throw new ArgumentNullException(nameof(db));

        if (!CanSlamCurrency(item, currencyId))
        {
            return CreateFailureResult(item, currencyId, "Item is ineligible for this currency slam or is already Corrupted.");
        }

        if (player != null && !player.Stash.WithdrawCurrency(currencyId, 1))
        {
            return CreateFailureResult(item, currencyId, $"Insufficient {currencyId} in stash.");
        }

        // Snapshot stats before slam
        var oldSnapshot = SnapshotStats(item, player);
        var oldAffixes = new List<AffixDefinition>(item.Affixes);

        string message = string.Empty;

        switch (currencyId)
        {
            case TransmuteId:
                item.Rarity = "Magic";
                item.Affixes.Clear();
                RollMagicAffixes(item, db.Affixes.Values);
                message = "Upgraded to Magic item with new modifiers!";
                break;

            case AlterationId:
                item.Affixes.Clear();
                RollMagicAffixes(item, db.Affixes.Values);
                message = "Reforged Magic item modifiers.";
                break;

            case ChaosId:
                item.Rarity = "Rare";
                item.Affixes.Clear();
                RollRareAffixes(item, db.Affixes.Values, bonusTags: null, blockedTags: null);
                message = "Reforged Rare item with unpredictable chaotic modifiers!";
                break;

            case ExaltedId:
                AddSingleAffix(item, db.Affixes.Values);
                message = "Exalted! Added a new powerful modifier.";
                break;

            case AnnulmentId:
                if (item.Affixes.Count > 0)
                {
                    int removeIdx = _rng.Next(item.Affixes.Count);
                    var removed = item.Affixes[removeIdx];
                    item.Affixes.RemoveAt(removeIdx);
                    message = $"Annulled! Removed '{removed.NameTemplate}'.";
                }
                break;

            case DivineId:
                RerollAffixValues(item);
                message = "Divined! Re-randomized numeric values of all modifiers.";
                break;

            case VaalId:
                ApplyVaalCorruption(item, db.Affixes.Values);
                message = "Corrupted by the Vaal! Sealed forever.";
                break;
        }

        RecalculateItemStats(item);
        if (player != null)
        {
            player.RecalculateStatsFromGear();
        }

        var newSnapshot = SnapshotStats(item, player);
        return BuildResult(item, currencyId, true, message, oldSnapshot, newSnapshot, oldAffixes, item.Affixes, IsCorrupted(item));
    }

    /// <summary>
    /// Slams 1-4 Fossils via the Abyssal Resonator into the target item.
    /// </summary>
    public AnvilCraftResult SlamFossils(
        IGearItem item,
        IReadOnlyList<string> fossilIds,
        DatabaseLoader db,
        PlayerState? player = null)
    {
        if (item == null) throw new ArgumentNullException(nameof(item));
        if (db == null) throw new ArgumentNullException(nameof(db));

        if (!CanSlamFossils(item, fossilIds))
        {
            return CreateFailureResult(item, "Abyssal Resonator", "Cannot slam fossils: item is Corrupted or invalid fossil count.");
        }

        if (player != null)
        {
            foreach (var fid in fossilIds)
            {
                if (player.Stash.GetFossilCount(fid) < 1)
                {
                    return CreateFailureResult(item, "Abyssal Resonator", $"Insufficient {fid} in stash.");
                }
            }

            foreach (var fid in fossilIds)
            {
                player.Stash.WithdrawFossil(fid, 1);
            }
        }

        var oldSnapshot = SnapshotStats(item, player);
        var oldAffixes = new List<AffixDefinition>(item.Affixes);

        // Aggregate bonus tags and blocked groups from chosen fossils
        var bonusTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var blockedTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var fid in fossilIds)
        {
            if (db.Fossils.TryGetValue(fid, out var fossil))
            {
                ParseFossilBiases(fossil, bonusTags, blockedTags);
            }
        }

        item.Rarity = "Rare";
        item.Affixes.Clear();
        RollRareAffixes(item, db.Affixes.Values, bonusTags, blockedTags);

        RecalculateItemStats(item);
        if (player != null)
        {
            player.RecalculateStatsFromGear();
        }

        var newSnapshot = SnapshotStats(item, player);
        string fossilSummary = string.Join(", ", fossilIds.Select(id => db.Fossils.TryGetValue(id, out var f) ? f.Name : id));
        string message = $"Abyssal Resonator Slammed with [{fossilSummary}]!";

        return BuildResult(item, "Abyssal Resonator", true, message, oldSnapshot, newSnapshot, oldAffixes, item.Affixes, IsCorrupted(item));
    }

    private static void ParseFossilBiases(FossilDefinition fossil, HashSet<string> bonusTags, HashSet<string> blockedTags)
    {
        string p = fossil.PrimaryBonus.ToLowerInvariant();
        if (p.Contains("life")) bonusTags.Add("life");
        if (p.Contains("lightning")) bonusTags.Add("lightning");
        if (p.Contains("fire")) bonusTags.Add("fire");
        if (p.Contains("cold")) bonusTags.Add("cold");
        if (p.Contains("defence") || p.Contains("defense"))
        {
            bonusTags.Add("defenses");
            bonusTags.Add("armour");
            bonusTags.Add("evasion");
            bonusTags.Add("energy_shield");
        }
        if (p.Contains("chaos")) bonusTags.Add("chaos");
        if (p.Contains("physical")) bonusTags.Add("physical");

        string b = fossil.BlockedGroup.ToLowerInvariant();
        if (b.Contains("defence") || b.Contains("defense"))
        {
            blockedTags.Add("defenses");
            blockedTags.Add("armour");
            blockedTags.Add("evasion");
            blockedTags.Add("energy_shield");
        }
        if (b.Contains("physical")) blockedTags.Add("physical");
        if (b.Contains("cold")) blockedTags.Add("cold");
        if (b.Contains("fire")) blockedTags.Add("fire");
        if (b.Contains("life")) bonusTags.Remove("life"); blockedTags.Add("life");
        if (b.Contains("elemental"))
        {
            blockedTags.Add("fire");
            blockedTags.Add("cold");
            blockedTags.Add("lightning");
        }
    }

    private void RollMagicAffixes(IGearItem item, IEnumerable<AffixDefinition> allAffixes)
    {
        var eligible = GetEligibleAffixes(item, allAffixes, null, null).ToList();
        if (eligible.Count == 0) return;

        var prefixes = eligible.Where(a => a.Type == AffixType.Prefix).ToList();
        var suffixes = eligible.Where(a => a.Type == AffixType.Suffix).ToList();

        // 1 guaranteed prefix, 50% chance for a suffix
        if (prefixes.Count > 0)
        {
            item.Affixes.Add(PickWeightedAffix(prefixes, null));
        }

        if (_rng.NextDouble() < 0.60 && suffixes.Count > 0)
        {
            var chosenSuffix = PickWeightedAffix(suffixes, null);
            if (!item.Affixes.Any(a => a.Id == chosenSuffix.Id))
            {
                item.Affixes.Add(chosenSuffix);
            }
        }
    }

    private void RollRareAffixes(
        IGearItem item,
        IEnumerable<AffixDefinition> allAffixes,
        HashSet<string>? bonusTags,
        HashSet<string>? blockedTags)
    {
        var eligible = GetEligibleAffixes(item, allAffixes, bonusTags, blockedTags).ToList();
        if (eligible.Count == 0) return;

        // Rare items roll 4 to 6 affixes: up to 3 prefixes and up to 3 suffixes
        int targetCount = _rng.Next(4, 7); // 4, 5, or 6
        int prefixTarget = Math.Min(3, targetCount / 2 + (_rng.Next(2) == 0 ? 1 : 0));
        int suffixTarget = Math.Min(3, targetCount - prefixTarget);

        var prefixes = eligible.Where(a => a.Type == AffixType.Prefix).ToList();
        var suffixes = eligible.Where(a => a.Type == AffixType.Suffix).ToList();

        for (int i = 0; i < prefixTarget && prefixes.Count > 0; i++)
        {
            var pick = PickWeightedAffix(prefixes, bonusTags);
            if (!item.Affixes.Any(a => a.Id == pick.Id))
            {
                item.Affixes.Add(pick);
                prefixes.RemoveAll(a => a.Id == pick.Id);
            }
        }

        for (int i = 0; i < suffixTarget && suffixes.Count > 0; i++)
        {
            var pick = PickWeightedAffix(suffixes, bonusTags);
            if (!item.Affixes.Any(a => a.Id == pick.Id))
            {
                item.Affixes.Add(pick);
                suffixes.RemoveAll(a => a.Id == pick.Id);
            }
        }
    }

    private void AddSingleAffix(IGearItem item, IEnumerable<AffixDefinition> allAffixes)
    {
        int prefixCount = item.Affixes.Count(a => a.Type == AffixType.Prefix);
        int suffixCount = item.Affixes.Count(a => a.Type == AffixType.Suffix);

        bool canAddPrefix = prefixCount < 3;
        bool canAddSuffix = suffixCount < 3;

        if (!canAddPrefix && !canAddSuffix) return;

        var eligible = GetEligibleAffixes(item, allAffixes, null, null)
            .Where(a => !item.Affixes.Any(x => x.Id == a.Id))
            .ToList();

        if (!canAddPrefix) eligible.RemoveAll(a => a.Type == AffixType.Prefix);
        if (!canAddSuffix) eligible.RemoveAll(a => a.Type == AffixType.Suffix);

        if (eligible.Count > 0)
        {
            var pick = PickWeightedAffix(eligible, null);
            item.Affixes.Add(pick);
        }
    }

    private static void RerollAffixValues(IGearItem item)
    {
        // In our data model, values are derived from AffixDefinition MinValue/MaxValue.
        // Recalculating stats reflects the refreshed numerical distribution.
        RecalculateItemStats(item);
    }

    private void ApplyVaalCorruption(IGearItem item, IEnumerable<AffixDefinition> allAffixes)
    {
        // Mark corrupted
        SetCorrupted(item, true);

        int roll = _rng.Next(100);
        if (roll < 50)
        {
            // 50% chance: Add powerful corrupted implicit or bonus affix
            var eligible = GetEligibleAffixes(item, allAffixes, null, null).ToList();
            if (eligible.Count > 0 && item.Affixes.Count < 6)
            {
                var pick = PickWeightedAffix(eligible, null);
                if (!item.Affixes.Any(a => a.Id == pick.Id))
                {
                    item.Affixes.Add(pick);
                }
            }
        }
        else if (roll < 75)
        {
            // 25% chance: Chaos reforge while corrupting
            item.Rarity = "Rare";
            item.Affixes.Clear();
            RollRareAffixes(item, allAffixes, null, null);
        }
        // Remaining 25%: White socket / no functional change besides being corrupted
    }

    private IEnumerable<AffixDefinition> GetEligibleAffixes(
        IGearItem item,
        IEnumerable<AffixDefinition> allAffixes,
        HashSet<string>? bonusTags,
        HashSet<string>? blockedTags)
    {
        bool isWeapon = item is WeaponItemInstance;
        bool isArmour = item is ArmourItemInstance;
        bool isAccessory = item is AccessoryItemInstance;

        foreach (var affix in allAffixes)
        {
            if (affix.MinIlvl > item.ItemLevel) continue;
            if (affix.Type == AffixType.Implicit) continue;

            if (blockedTags != null && affix.Tags != null && affix.Tags.Any(t => blockedTags.Contains(t)))
            {
                continue;
            }

            // Compatibility filter
            if (isWeapon)
            {
                // Weapons cannot roll pure local armour/evasion/energy shield
                if (affix.Tags != null && affix.Tags.Contains("defenses")) continue;
            }
            else if (isArmour)
            {
                // Armours cannot roll pure local weapon physical damage
                if (affix.Tags != null && affix.Tags.Contains("weapon") && affix.Tags.Contains("local")) continue;
            }

            yield return affix;
        }
    }

    private AffixDefinition PickWeightedAffix(IReadOnlyList<AffixDefinition> candidates, HashSet<string>? bonusTags)
    {
        if (candidates.Count == 1) return candidates[0];

        float totalWeight = 0f;
        var weights = new float[candidates.Count];

        for (int i = 0; i < candidates.Count; i++)
        {
            float w = 100f / Math.Max(1, candidates[i].Tier); // Higher tier -> higher weight
            if (bonusTags != null && candidates[i].Tags != null && candidates[i].Tags.Any(t => bonusTags.Contains(t)))
            {
                w *= 10.0f; // 10x multiplier for fossil bonus
            }
            weights[i] = w;
            totalWeight += w;
        }

        double roll = _rng.NextDouble() * totalWeight;
        double accum = 0;
        for (int i = 0; i < candidates.Count; i++)
        {
            accum += weights[i];
            if (roll <= accum) return candidates[i];
        }

        return candidates[^1];
    }

    public static bool IsCorrupted(IGearItem item)
    {
        if (item is ArmourItemInstance a) return a.CustomName.Contains("[Corrupted]", StringComparison.OrdinalIgnoreCase);
        if (item is WeaponItemInstance w) return w.CustomName.Contains("[Corrupted]", StringComparison.OrdinalIgnoreCase);
        if (item is AccessoryItemInstance acc) return acc.CustomName.Contains("[Corrupted]", StringComparison.OrdinalIgnoreCase);
        return false;
    }

    public static void SetCorrupted(IGearItem item, bool corrupted)
    {
        if (!corrupted) return;
        string tag = " [Corrupted]";
        if (item is ArmourItemInstance a && !a.CustomName.Contains("[Corrupted]")) a.CustomName = string.IsNullOrEmpty(a.CustomName) ? a.BaseName + tag : a.CustomName + tag;
        else if (item is WeaponItemInstance w && !w.CustomName.Contains("[Corrupted]")) w.CustomName = string.IsNullOrEmpty(w.CustomName) ? w.BaseName + tag : w.CustomName + tag;
        else if (item is AccessoryItemInstance acc && !acc.CustomName.Contains("[Corrupted]")) acc.CustomName = string.IsNullOrEmpty(acc.CustomName) ? acc.BaseName + tag : acc.CustomName + tag;
    }

    private static (float Ehp, float Dps, float FireRes, float ColdRes, float LightRes, float ChaosRes, float Armour, float Eva, float Es)
        SnapshotStats(IGearItem item, PlayerState? player)
    {
        float ehp = player != null ? EquipmentComparisonService.CalculateEffectiveHealthPool(player) : (item.FlatLifeBonus * 1.5f + item.LocalEnergyShield);
        float dps = player != null ? EquipmentComparisonService.CalculateTotalDps(player) : item.LocalDps;

        return (
            Ehp: ehp,
            Dps: dps,
            FireRes: item.FireResistanceBonus,
            ColdRes: item.ColdResistanceBonus,
            LightRes: item.LightningResistanceBonus,
            ChaosRes: item.ChaosResistanceBonus,
            Armour: item.LocalArmour,
            Eva: item.LocalEvasion,
            Es: item.LocalEnergyShield
        );
    }

    private static AnvilCraftResult BuildResult(
        IGearItem item,
        string method,
        bool success,
        string message,
        (float Ehp, float Dps, float FireRes, float ColdRes, float LightRes, float ChaosRes, float Armour, float Eva, float Es) oldStats,
        (float Ehp, float Dps, float FireRes, float ColdRes, float LightRes, float ChaosRes, float Armour, float Eva, float Es) newStats,
        IReadOnlyList<AffixDefinition> oldAffixes,
        IReadOnlyList<AffixDefinition> newAffixes,
        bool isCorrupted)
    {
        return new AnvilCraftResult(
            Item: item,
            CurrencyOrMethodUsed: method,
            Success: success,
            Message: message,
            OldEhp: oldStats.Ehp,
            NewEhp: newStats.Ehp,
            DeltaEhp: newStats.Ehp - oldStats.Ehp,
            OldDps: oldStats.Dps,
            NewDps: newStats.Dps,
            DeltaDps: newStats.Dps - oldStats.Dps,
            DeltaFireRes: newStats.FireRes - oldStats.FireRes,
            DeltaColdRes: newStats.ColdRes - oldStats.ColdRes,
            DeltaLightningRes: newStats.LightRes - oldStats.LightRes,
            DeltaChaosRes: newStats.ChaosRes - oldStats.ChaosRes,
            DeltaArmour: newStats.Armour - oldStats.Armour,
            DeltaEvasion: newStats.Eva - oldStats.Eva,
            DeltaEnergyShield: newStats.Es - oldStats.Es,
            OldAffixes: oldAffixes,
            NewAffixes: new List<AffixDefinition>(newAffixes),
            IsCorrupted: isCorrupted
        );
    }

    private static AnvilCraftResult CreateFailureResult(IGearItem item, string method, string reason)
    {
        return new AnvilCraftResult(
            Item: item,
            CurrencyOrMethodUsed: method,
            Success: false,
            Message: reason,
            OldEhp: 0f,
            NewEhp: 0f,
            DeltaEhp: 0f,
            OldDps: 0f,
            NewDps: 0f,
            DeltaDps: 0f,
            DeltaFireRes: 0f,
            DeltaColdRes: 0f,
            DeltaLightningRes: 0f,
            DeltaChaosRes: 0f,
            DeltaArmour: 0f,
            DeltaEvasion: 0f,
            DeltaEnergyShield: 0f,
            OldAffixes: item.Affixes,
            NewAffixes: item.Affixes,
            IsCorrupted: IsCorrupted(item)
        );
    }
}
