using System;
using System.Collections.Generic;
using IdleExile.Core.Models;

namespace IdleExile.Core.Simulation;

/// <summary>
/// Coordinates deterministic seeded drop rules for the First Time User Experience (FTUE)
/// and the Endless Hollow (Tier 0) fail-safe map sustain mechanism.
///
/// Rules:
/// 1. 12th Monster Kill in Map 1: Guarantees 1x Magic Equipment + 1x Orb of Transmutation ("Ting!").
/// 2. First Magic Pack Kill: Guarantees 1x Support Gem matching the player's chosen Starter Archetype.
/// 3. Map 1 Boss Kill: Guarantees 2x Map Slates (1x Strand T1, 1x Arid Lake T2), 1x Chaos Orb, and 1x Rare Equipment.
/// 4. Fail-Safe Map (T0): Every 50 monster kills guarantees dropping at least 1x Tier 1 Map Slate.
///
/// Pure C# — no Godot dependency.
/// </summary>
public sealed class SeededDropCoordinator
{
    private readonly FtueManager? _ftue;

    /// <summary>Number of monsters killed in the current active map run.</summary>
    public int KillCountInMap { get; private set; } = 0;

    /// <summary>Cumulative monster kills in the Tier 0 fail-safe map.</summary>
    public int FailSafeT0KillCount { get; private set; } = 0;

    /// <summary>Whether the guaranteed 12th-kill loot has already dropped in this FTUE session.</summary>
    public bool HasDroppedTwelfthKillLoot { get; private set; } = false;

    /// <summary>Whether the guaranteed archetype Support Gem has already dropped.</summary>
    public bool HasDroppedMagicSupportGem { get; private set; } = false;

    /// <summary>Whether the guaranteed Map 1 Boss reward batch has already dropped.</summary>
    public bool HasDroppedBossLoot { get; private set; } = false;

    /// <summary>Whether the current active map run is the initial FTUE Map (Map 1).</summary>
    public bool IsFirstMapRun { get; set; } = true;

    /// <summary>Fallback archetype definition if FtueManager reference is not provided.</summary>
    public StarterArchetypeDefinition? FallbackArchetype { get; set; }

    // ─────────────────────────────────────────────
    //  Events (Subscribed by UI Toast & Audio)
    // ─────────────────────────────────────────────

    /// <summary>Fired for each individual loot item dropped.</summary>
    public event Action<LootDrop>? OnLootDropped;

    /// <summary>Fired when a batch of items drops together (e.g. Boss loot explosion).</summary>
    public event Action<IReadOnlyList<LootDrop>>? OnLootBatchDropped;

    public SeededDropCoordinator(FtueManager? ftue = null)
    {
        _ftue = ftue;
    }

    /// <summary>
    /// Processes a monster kill, evaluating all deterministic drop rules.
    /// Returns the list of seeded items generated (if any).
    /// </summary>
    public IReadOnlyList<LootDrop> ProcessMonsterKill(MonsterInstance monster, bool isFailSafeMap = false)
    {
        if (monster == null) throw new ArgumentNullException(nameof(monster));

        KillCountInMap++;
        var drops = new List<LootDrop>();

        // ── Rule 4: Fail-Safe Mechanism (Tier 0: The Endless Hollow) ──
        if (isFailSafeMap)
        {
            FailSafeT0KillCount++;
            if (FailSafeT0KillCount % 50 == 0)
            {
                drops.Add(LootDrop.Map("strand", "Strand Map", tier: 1, colorHex: "#C4C4C4", isSeeded: true));
            }
        }

        // ── FTUE Deterministic Rules (Only apply in Map 1 during FTUE) ──
        if (IsFirstMapRun)
        {
            // Rule 1: 12th Monster Kill in Map 1
            if (KillCountInMap == 12 && !HasDroppedTwelfthKillLoot)
            {
                HasDroppedTwelfthKillLoot = true;

                var archetype = GetActiveArchetype();
                string equipId = archetype?.Id switch
                {
                    StarterArchetype.Elementalist => "driftwood_wand",
                    StarterArchetype.Deadeye => "crude_bow",
                    _ => "rusted_sword"
                };

                string equipName = archetype?.Id switch
                {
                    StarterArchetype.Elementalist => "Gleaming Wand",
                    StarterArchetype.Deadeye => "Swift Composite Bow",
                    _ => "Tempered Iron Blade"
                };

                drops.Add(LootDrop.Equipment(equipId, equipName, LootRarity.Magic, colorHex: "#8888FF", isSeeded: true));
                drops.Add(LootDrop.Currency("orb_of_transmutation", "Orb of Transmutation", quantity: 1, colorHex: "#4169E1", isSeeded: true));

                _ftue?.NotifyFirstLootDropped();
            }

            // Rule 2: First Magic Pack Leader / Magic Monster Kill
            if (monster.Rarity == MonsterRarity.Magic && !HasDroppedMagicSupportGem)
            {
                HasDroppedMagicSupportGem = true;

                var archetype = GetActiveArchetype();
                string gemId = archetype?.GuaranteedSupportGemId ?? "added_fire_damage";

                (string gemName, string colorHex) = gemId switch
                {
                    "ignite_proliferation" => ("Ignite Proliferation Support", "#3498DB"),
                    "multiple_projectiles" => ("Lesser Multiple Projectiles Support", "#2ECC71"),
                    _ => ("Added Fire Damage Support", "#E74C3C")
                };

                drops.Add(LootDrop.Gem(gemId, gemName, colorHex: colorHex, isSeeded: true));

                _ftue?.NotifySupportGemAcquired(gemId);
            }

            // Rule 3: Map 1 Boss Slain (Loot Explosion & Map Sustain Confirmation)
            if (monster.Rarity == MonsterRarity.Boss && !HasDroppedBossLoot)
            {
                HasDroppedBossLoot = true;

                drops.Add(LootDrop.Map("strand", "Strand Map", tier: 1, colorHex: "#C4C4C4", isSeeded: true));
                drops.Add(LootDrop.Map("arid_lake", "Arid Lake", tier: 2, colorHex: "#FFD700", isSeeded: true));
                drops.Add(LootDrop.Currency("chaos_orb", "Chaos Orb", quantity: 1, colorHex: "#FFD700", isSeeded: true));
                drops.Add(LootDrop.Equipment("gilded_cuirass", "Gilded Sentinel Cuirass", LootRarity.Rare, colorHex: "#FFFF77", isSeeded: true));

                IsFirstMapRun = false;
                _ftue?.NotifyFirstMapCompleted();
            }
        }

        // Emit events for audio & UI listeners
        if (drops.Count > 0)
        {
            foreach (var item in drops)
            {
                OnLootDropped?.Invoke(item);
            }

            OnLootBatchDropped?.Invoke(drops);
        }

        return drops;
    }

    /// <summary>
    /// Resets per-map kill counters when entering a new map instance.
    /// </summary>
    public void ResetForNewMap(bool isFirstMap = false)
    {
        KillCountInMap = 0;
        IsFirstMapRun = isFirstMap;
    }

    /// <summary>
    /// Resets all seeded state (e.g. on profile reset or tests).
    /// </summary>
    public void ResetAll()
    {
        KillCountInMap = 0;
        FailSafeT0KillCount = 0;
        HasDroppedTwelfthKillLoot = false;
        HasDroppedMagicSupportGem = false;
        HasDroppedBossLoot = false;
        IsFirstMapRun = true;
    }

    private StarterArchetypeDefinition? GetActiveArchetype()
    {
        return _ftue?.SelectedArchetype ?? FallbackArchetype;
    }
}
