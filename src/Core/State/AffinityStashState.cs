using System;
using System.Collections.Generic;
using System.Linq;
using IdleExile.Core.Models;

namespace IdleExile.Core.State;

/// <summary>
/// Pure C# state engine for the 4-tab Affinity Stash (Currency, Gear, Maps, Fossils).
/// Complies strictly with GDD Section 5.2 and AGENTS.md:
/// - Pure C# logic with zero Godot dependencies.
/// - Automatic stacking (up to 5,000+ items per stack).
/// - 1-Click Affinity Dump support for Zero-APM loot cleanup.
/// - 100% headless unit testable.
/// </summary>
public sealed class AffinityStashState
{
    public const int DefaultMaxGearSlots = 60;
    public const int DefaultMaxStackSize = 5000;

    /// <summary>Currency stacks: CurrencyId -> Count.</summary>
    private readonly Dictionary<string, int> _currencies = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Stored unequipped gear items.</summary>
    private readonly List<IGearItem> _gearItems = new();

    /// <summary>Stored itemized map slates.</summary>
    private readonly List<StashedMapSlate> _maps = new();

    /// <summary>Fossil stacks: FossilId -> Count.</summary>
    private readonly Dictionary<string, int> _fossils = new(StringComparer.OrdinalIgnoreCase);

    public int MaxGearSlots { get; set; } = DefaultMaxGearSlots;

    public IReadOnlyDictionary<string, int> Currencies => _currencies;
    public IReadOnlyList<IGearItem> GearItems => _gearItems;
    public IReadOnlyList<StashedMapSlate> Maps => _maps;
    public IReadOnlyDictionary<string, int> Fossils => _fossils;

    public int TotalCurrencyCount => _currencies.Values.Sum();
    public int TotalGearCount => _gearItems.Count;
    public int TotalMapCount => _maps.Sum(m => m.Quantity);
    public int TotalFossilCount => _fossils.Values.Sum();

    // Events for reactive UI binding
    public event Action? OnStashChanged;
    public event Action<string, int>? OnCurrencyChanged;
    public event Action<IGearItem>? OnGearDeposited;
    public event Action<string>? OnGearWithdrawn;
    public event Action<StashedMapSlate>? OnMapDeposited;
    public event Action<string, int>? OnFossilChanged;

    public AffinityStashState(int maxGearSlots = DefaultMaxGearSlots)
    {
        MaxGearSlots = maxGearSlots;
    }

    #region Currency Management

    public int GetCurrencyCount(string currencyId)
    {
        return _currencies.TryGetValue(currencyId, out int count) ? count : 0;
    }

    public bool HasCurrency(string currencyId, int count = 1)
    {
        if (count <= 0) return true;
        return GetCurrencyCount(currencyId) >= count;
    }

    public void DepositCurrency(string currencyId, int count = 1)
    {
        if (string.IsNullOrWhiteSpace(currencyId) || count <= 0) return;

        if (_currencies.TryGetValue(currencyId, out int existing))
        {
            _currencies[currencyId] = existing + count;
        }
        else
        {
            _currencies[currencyId] = count;
        }

        OnCurrencyChanged?.Invoke(currencyId, _currencies[currencyId]);
        OnStashChanged?.Invoke();
    }

    public bool WithdrawCurrency(string currencyId, int count = 1)
    {
        if (string.IsNullOrWhiteSpace(currencyId) || count <= 0) return false;
        if (!_currencies.TryGetValue(currencyId, out int existing) || existing < count) return false;

        int remaining = existing - count;
        if (remaining <= 0)
        {
            _currencies.Remove(currencyId);
            OnCurrencyChanged?.Invoke(currencyId, 0);
        }
        else
        {
            _currencies[currencyId] = remaining;
            OnCurrencyChanged?.Invoke(currencyId, remaining);
        }

        OnStashChanged?.Invoke();
        return true;
    }

    #endregion

    #region Gear Management

    public bool DepositGear(IGearItem item)
    {
        if (item == null) return false;
        if (_gearItems.Count >= MaxGearSlots) return false;

        // Ensure no duplicate instance ID
        if (_gearItems.Any(g => g.InstanceId == item.InstanceId)) return false;

        _gearItems.Add(item);
        OnGearDeposited?.Invoke(item);
        OnStashChanged?.Invoke();
        return true;
    }

    public bool WithdrawGear(string instanceId, out IGearItem? item)
    {
        item = null;
        if (string.IsNullOrWhiteSpace(instanceId)) return false;

        int index = _gearItems.FindIndex(g => g.InstanceId == instanceId);
        if (index < 0) return false;

        item = _gearItems[index];
        _gearItems.RemoveAt(index);
        OnGearWithdrawn?.Invoke(instanceId);
        OnStashChanged?.Invoke();
        return true;
    }

    public bool RemoveGear(string instanceId)
    {
        return WithdrawGear(instanceId, out _);
    }

    public IGearItem? FindGear(string instanceId)
    {
        return _gearItems.FirstOrDefault(g => g.InstanceId == instanceId);
    }

    public IReadOnlyList<IGearItem> GetFilteredGear(string slotCategory = "All", string rarityFilter = "All", string search = "")
    {
        IEnumerable<IGearItem> query = _gearItems;

        if (!string.Equals(slotCategory, "All", StringComparison.OrdinalIgnoreCase))
        {
            query = slotCategory.ToLowerInvariant() switch
            {
                "weapon" or "weapons" => query.Where(g => g.TargetSlot is GearSlot.MainHand or GearSlot.OffHand),
                "armour" or "armor" => query.Where(g => g.TargetSlot is GearSlot.Helmet or GearSlot.BodyArmour or GearSlot.Gloves or GearSlot.Boots),
                "jewellery" or "accessory" or "accessories" => query.Where(g => g.TargetSlot is GearSlot.Amulet or GearSlot.Ring1 or GearSlot.Ring2 or GearSlot.Belt),
                "locked" => query.Where(g => g.IsLocked),
                _ => query
            };
        }

        if (!string.Equals(rarityFilter, "All", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(g => string.Equals(g.Rarity, rarityFilter, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            string trimmed = search.Trim();
            query = query.Where(g =>
                g.Name.Contains(trimmed, StringComparison.OrdinalIgnoreCase) ||
                g.BaseName.Contains(trimmed, StringComparison.OrdinalIgnoreCase) ||
                g.TargetSlot.ToString().Contains(trimmed, StringComparison.OrdinalIgnoreCase));
        }

        return query.ToList();
    }

    #endregion

    #region Map Management

    public void DepositMap(StashedMapSlate slate)
    {
        if (slate == null || string.IsNullOrWhiteSpace(slate.MapId) || slate.Quantity <= 0) return;

        var existing = _maps.FirstOrDefault(m =>
            m.MapId == slate.MapId &&
            m.Tier == slate.Tier &&
            string.Equals(m.Rarity, slate.Rarity, StringComparison.OrdinalIgnoreCase));

        if (existing != null)
        {
            existing.Quantity += slate.Quantity;
        }
        else
        {
            _maps.Add(slate);
        }

        OnMapDeposited?.Invoke(slate);
        OnStashChanged?.Invoke();
    }

    public void DepositMap(MapDefinition mapDef, int quantity = 1, string rarity = "Normal")
    {
        if (mapDef == null || quantity <= 0) return;
        DepositMap(new StashedMapSlate(mapDef, quantity, rarity));
    }

    public bool WithdrawMap(string mapId, int tier, string rarity = "Normal", int quantity = 1)
    {
        if (string.IsNullOrWhiteSpace(mapId) || quantity <= 0) return false;

        var slate = _maps.FirstOrDefault(m =>
            m.MapId == mapId &&
            m.Tier == tier &&
            string.Equals(m.Rarity, rarity, StringComparison.OrdinalIgnoreCase));

        if (slate == null || slate.Quantity < quantity) return false;

        slate.Quantity -= quantity;
        if (slate.Quantity <= 0)
        {
            _maps.Remove(slate);
        }

        OnStashChanged?.Invoke();
        return true;
    }

    public StashedMapSlate? FindMap(string mapId, int tier, string rarity = "Normal")
    {
        return _maps.FirstOrDefault(m =>
            m.MapId == mapId &&
            m.Tier == tier &&
            string.Equals(m.Rarity, rarity, StringComparison.OrdinalIgnoreCase));
    }

    public IReadOnlyList<StashedMapSlate> GetFilteredMaps(int minTier = 0, int maxTier = 20, string search = "")
    {
        IEnumerable<StashedMapSlate> query = _maps.Where(m => m.Tier >= minTier && m.Tier <= maxTier);

        if (!string.IsNullOrWhiteSpace(search))
        {
            string trimmed = search.Trim();
            query = query.Where(m =>
                m.MapName.Contains(trimmed, StringComparison.OrdinalIgnoreCase) ||
                m.MapId.Contains(trimmed, StringComparison.OrdinalIgnoreCase) ||
                (m.BossName != null && m.BossName.Contains(trimmed, StringComparison.OrdinalIgnoreCase)));
        }

        return query.OrderBy(m => m.Tier).ThenBy(m => m.MapName).ToList();
    }

    #endregion

    #region Fossil Management

    public int GetFossilCount(string fossilId)
    {
        return _fossils.TryGetValue(fossilId, out int count) ? count : 0;
    }

    public bool HasFossil(string fossilId, int count = 1)
    {
        if (count <= 0) return true;
        return GetFossilCount(fossilId) >= count;
    }

    public void DepositFossil(string fossilId, int count = 1)
    {
        if (string.IsNullOrWhiteSpace(fossilId) || count <= 0) return;

        if (_fossils.TryGetValue(fossilId, out int existing))
        {
            _fossils[fossilId] = existing + count;
        }
        else
        {
            _fossils[fossilId] = count;
        }

        OnFossilChanged?.Invoke(fossilId, _fossils[fossilId]);
        OnStashChanged?.Invoke();
    }

    public bool WithdrawFossil(string fossilId, int count = 1)
    {
        if (string.IsNullOrWhiteSpace(fossilId) || count <= 0) return false;
        if (!_fossils.TryGetValue(fossilId, out int existing) || existing < count) return false;

        int remaining = existing - count;
        if (remaining <= 0)
        {
            _fossils.Remove(fossilId);
            OnFossilChanged?.Invoke(fossilId, 0);
        }
        else
        {
            _fossils[fossilId] = remaining;
            OnFossilChanged?.Invoke(fossilId, remaining);
        }

        OnStashChanged?.Invoke();
        return true;
    }

    #endregion

    #region 1-Click Affinity Dump

    /// <summary>
    /// Executes a 1-Click Affinity Dump, sweeping player backpack currencies, map inventory slates,
    /// and unequipped candidate gear into their respective affinity tabs.
    /// </summary>
    public AffinityDumpResult DumpAll(
        PlayerState player,
        IReadOnlyDictionary<string, MapDefinition>? mapCatalog = null,
        IEnumerable<IGearItem>? candidateGear = null)
    {
        if (player == null) return new AffinityDumpResult();

        int currenciesDumped = 0;
        int mapsDumped = 0;
        int gearDumped = 0;
        int fossilsDumped = 0;

        // 1. Dump player backpack currencies
        if (player.Currencies.Count > 0)
        {
            foreach (var kvp in player.Currencies.ToList())
            {
                if (kvp.Value > 0)
                {
                    // Check if it's a fossil or standard currency
                    if (kvp.Key.EndsWith("_fossil", StringComparison.OrdinalIgnoreCase))
                    {
                        DepositFossil(kvp.Key, kvp.Value);
                        fossilsDumped += kvp.Value;
                    }
                    else
                    {
                        DepositCurrency(kvp.Key, kvp.Value);
                        currenciesDumped += kvp.Value;
                    }
                }
            }
            player.Currencies.Clear();
        }

        // 2. Dump player backpack map slates
        if (player.MapInventory.Count > 0)
        {
            foreach (var mapId in player.MapInventory.ToList())
            {
                if (string.IsNullOrWhiteSpace(mapId)) continue;

                if (mapCatalog != null && mapCatalog.TryGetValue(mapId, out var mapDef))
                {
                    DepositMap(mapDef, 1, "Normal");
                }
                else
                {
                    var fallbackDef = new MapDefinition(mapId, mapId.Replace("_", " "), 1, 1, false, 10, 100, null, string.Empty);
                    DepositMap(new StashedMapSlate(fallbackDef, 1, "Normal"));
                }
                mapsDumped++;
            }
            player.MapInventory.Clear();
        }

        // 3. Dump unequipped, unlocked candidate gear
        if (candidateGear != null)
        {
            foreach (var gear in candidateGear)
            {
                if (gear != null && !gear.IsLocked)
                {
                    if (DepositGear(gear))
                    {
                        gearDumped++;
                    }
                }
            }
        }

        OnStashChanged?.Invoke();

        return new AffinityDumpResult
        {
            CurrenciesDumped = currenciesDumped,
            MapsDumped = mapsDumped,
            GearDumped = gearDumped,
            FossilsDumped = fossilsDumped
        };
    }

    #endregion
}
