using System;
using System.Collections.Generic;
using IdleExile.Core.Simulation;

namespace IdleExile.Core.Models;

/// <summary>
/// Monster rarity tiers, matching PoE's classification:
/// Normal (white), Magic (blue), Rare (yellow), Boss (unique map boss).
/// </summary>
public enum MonsterRarity
{
    /// <summary>Trash mob — baseline stats, drops minimal loot.</summary>
    Normal = 0,

    /// <summary>Magic pack — blue name, 1 affix mod, boosted stats.</summary>
    Magic = 1,

    /// <summary>Rare monster — yellow name, 2-4 aura modifiers, significantly boosted stats.</summary>
    Rare = 2,

    /// <summary>Map Boss — unique name, multiple phases potential, massive HP pool.</summary>
    Boss = 3
}

/// <summary>
/// Aura modifier carried by Rare monsters that buff surrounding pack members.
/// In PoE, rare monsters project auras that grant bonuses to nearby allies.
/// </summary>
public sealed record MonsterAuraModifier(
    /// <summary>Machine-readable identifier (e.g. "extra_damage_fire", "haste").</summary>
    string Id,

    /// <summary>Human-readable display name (e.g. "Allies deal 30% extra Fire damage").</summary>
    string DisplayName,

    /// <summary>
    /// Flat bonus damage per second added to all nearby allies.
    /// Scaled at spawn time based on monster level.
    /// </summary>
    float BonusFlatDamage,

    /// <summary>
    /// Percentage increase to nearby allies' action speed (0-100).
    /// Haste aura example: 30 = +30% attack/cast speed.
    /// </summary>
    float HastePercent,

    /// <summary>
    /// Extra elemental resistance granted to nearby allies (0-100).
    /// Resistance aura example: 20 = +20% all elemental res.
    /// </summary>
    float BonusResistance
);

/// <summary>
/// Mutable runtime state of a single monster during combat.
///
/// Instances are created by <see cref="IdleExile.Core.Simulation.WaveSpawner"/>
/// and consumed by the CombatEngine tick loop.
///
/// All stats are pre-scaled at construction time based on monster level,
/// map modifiers, and rarity multipliers. Pure C# — no Godot dependency.
/// </summary>
public sealed class MonsterInstance : ICombatEntity
{
    // ─────────────────────────────────────────────
    //  Identity & ICombatEntity
    // ─────────────────────────────────────────────

    /// <summary>Unique per-instance ID for targeting and event correlation.</summary>
    public long InstanceId { get; }

    /// <summary>Unique entity ID conforming to ICombatEntity.</summary>
    public long Id => InstanceId;

    /// <summary>Current energy shield pool (0 for standard monsters).</summary>
    public float CurrentEnergyShield => 0f;

    /// <summary>Maximum energy shield pool (0 for standard monsters).</summary>
    public float MaxEnergyShield => 0f;

    /// <summary>Active status ailments tracker on this monster.</summary>
    public AilmentTracker Ailments { get; } = new();

    /// <summary>Evasion entropy counter for deterministic evasion checks.</summary>
    public int EvasionEntropy { get; private set; } = 0;

    /// <summary>Display name (e.g. "Tidal Behemoth", "Skeletal Warrior", "Burning Archer").</summary>
    public string Name { get; }

    /// <summary>Monster rarity tier — determines stat multipliers and loot bonus.</summary>
    public MonsterRarity Rarity { get; }

    /// <summary>Effective monster level used for all scaling formulas.</summary>
    public int MonsterLevel { get; }

    // ─────────────────────────────────────────────
    //  Spatial Geometry & 2D Position
    // ─────────────────────────────────────────────

    /// <summary>2D coordinate (X, Y) within the 440 x 208 arena.</summary>
    public Vector2D Position { get; set; } = Vector2D.Zero;

    /// <summary>Effective collision/hitbox radius in pixels for AoE and range tests.</summary>
    public float Radius { get; set; } = 8.5f;

    /// <summary>
    /// Maximum distance in pixels at which this monster can strike the Exile.
    /// Melee mobs: 35px (contact range), Bosses: 45px (larger reach).
    /// Monsters must close distance before attacking.
    /// </summary>
    public float AttackRange { get; }

    /// <summary>
    /// Movement speed in pixels per second towards the Exile.
    /// Trash: 60px/s, Magic: 55px/s, Rare: 45px/s (slower, bulkier), Boss: 35px/s (lumbering).
    /// Affected by CC multipliers (Chill/Freeze) in CombatEngine Phase 4.
    /// </summary>
    public float MovementSpeed { get; }

    /// <summary>
    /// Perception/awareness detection radius in pixels.
    /// Monsters stay idle in their formation until the Exile enters this radius or until damaged.
    /// Normal: 150px, Magic: 180px, Rare: 200px, Boss: 260px.
    /// </summary>
    public float AggroRange { get; set; }

    /// <summary>
    /// Whether this monster has aggroed onto the Exile.
    /// Monsters only advance and attack when aggroed.
    /// </summary>
    public bool IsAggroed { get; set; } = false;

    /// <summary>Original spawn position within the corridor or arena.</summary>
    public Vector2D SpawnPosition { get; set; } = Vector2D.Zero;

    // ─────────────────────────────────────────────
    //  Health
    // ─────────────────────────────────────────────

    /// <summary>Maximum life after all scaling (level, rarity, map mods).</summary>
    public float MaxLife { get; }

    /// <summary>Current life. Monster dies when this reaches 0.</summary>
    public float CurrentLife { get; private set; }

    /// <summary>True while CurrentLife > 0.</summary>
    public bool IsAlive => CurrentLife > 0f;

    /// <summary>Current life as fraction (0.0 to 1.0) — used for Boss health bars.</summary>
    public float LifePercent => MaxLife > 0f ? CurrentLife / MaxLife : 0f;

    // ─────────────────────────────────────────────
    //  Offensive Stats (pre-scaled)
    // ─────────────────────────────────────────────

    /// <summary>
    /// Primary damage type this monster deals.
    /// Trash mobs typically deal Physical; elemental mobs specified by map/data.
    /// </summary>
    public DamageType PrimaryDamageType { get; }

    /// <summary>Base damage per hit (min) after level and rarity scaling.</summary>
    public float DamageMin { get; }

    /// <summary>Base damage per hit (max) after level and rarity scaling.</summary>
    public float DamageMax { get; }

    /// <summary>Attacks per second (action speed). Determines ticks between attacks.</summary>
    public float AttacksPerSecond { get; }

    /// <summary>Critical strike chance in percent (0-100). Most trash mobs have 5%.</summary>
    public float CritChance { get; }

    /// <summary>Critical strike multiplier in percent (e.g. 130 = 1.3x).</summary>
    public float CritMultiplier { get; }

    /// <summary>
    /// Accuracy rating — fed into the player's Evasion Entropy system.
    /// Higher accuracy = higher chance to hit evasion-based characters.
    /// </summary>
    public float Accuracy { get; }

    // ─────────────────────────────────────────────
    //  Defensive Stats (ICombatEntity.Defenses)
    // ─────────────────────────────────────────────

    /// <summary>Defensive configuration snapshot (resistances, armour, block, evasion).</summary>
    public DefensiveStats Defenses { get; set; } = new();

    /// <summary>Monster's fire resistance (0-75 typically, bosses up to 40%).</summary>
    public float FireResistance
    {
        get => Defenses.FireResistance;
        init => Defenses = Defenses with { FireResistance = value };
    }

    /// <summary>Monster's cold resistance.</summary>
    public float ColdResistance
    {
        get => Defenses.ColdResistance;
        init => Defenses = Defenses with { ColdResistance = value };
    }

    /// <summary>Monster's lightning resistance.</summary>
    public float LightningResistance
    {
        get => Defenses.LightningResistance;
        init => Defenses = Defenses with { LightningResistance = value };
    }

    /// <summary>Monster's chaos resistance (most monsters have 0, some bosses up to 30%).</summary>
    public float ChaosResistance
    {
        get => Defenses.ChaosResistance;
        init => Defenses = Defenses with { ChaosResistance = value };
    }

    // ─────────────────────────────────────────────
    //  Reward Stats
    // ─────────────────────────────────────────────

    /// <summary>Base experience granted on kill, scaled by monster level and rarity.</summary>
    public float BaseExperience { get; }

    /// <summary>
    /// Item quantity bonus multiplier from this specific monster (1.0 = normal).
    /// Rare = ~3x, Boss = ~5x baseline.
    /// </summary>
    public float ItemQuantityMultiplier { get; }

    /// <summary>
    /// Item rarity bonus multiplier from this specific monster (1.0 = normal).
    /// Rare = ~4x, Boss = ~8x baseline.
    /// </summary>
    public float ItemRarityMultiplier { get; }

    // ─────────────────────────────────────────────
    //  Rare Monster Aura Modifiers
    // ─────────────────────────────────────────────

    /// <summary>
    /// Aura modifiers projected by Rare monsters onto nearby allies.
    /// Empty for Normal and Boss rarity. Typically 2-4 auras for Rare.
    /// </summary>
    public IReadOnlyList<MonsterAuraModifier> AuraModifiers { get; }

    // ─────────────────────────────────────────────
    //  Events
    // ─────────────────────────────────────────────

    /// <summary>Fired when this monster takes damage. Args: (damageDealt, remainingLife).</summary>
    public event Action<float, float>? OnDamageTaken;

    /// <summary>Fired when this monster dies.</summary>
    public event Action<MonsterInstance>? OnDied;

    /// <summary>
    /// Clears all event subscribers to prevent memory leaks.
    /// Called by CombatEngine after death processing is complete.
    /// </summary>
    public void ClearEventSubscribers()
    {
        OnDamageTaken = null;
        OnDied = null;
    }

    // ─────────────────────────────────────────────
    //  Constructor
    // ─────────────────────────────────────────────

    public MonsterInstance(
        long instanceId,
        string name,
        MonsterRarity rarity,
        int monsterLevel,
        float maxLife,
        DamageType primaryDamageType,
        float damageMin,
        float damageMax,
        float attacksPerSecond,
        float critChance,
        float critMultiplier,
        float accuracy,
        float baseExperience,
        float itemQuantityMultiplier,
        float itemRarityMultiplier,
        IReadOnlyList<MonsterAuraModifier>? auraModifiers = null,
        Vector2D? initialPosition = null)
    {
        InstanceId = instanceId;
        Name = name;
        Rarity = rarity;
        MonsterLevel = monsterLevel;

        Radius = rarity switch
        {
            MonsterRarity.Boss => 16f,
            MonsterRarity.Rare => 12f,
            MonsterRarity.Magic => 10f,
            _ => 8.5f
        };

        AttackRange = rarity switch
        {
            MonsterRarity.Boss => 45f,   // Larger reach
            MonsterRarity.Rare => 40f,   // Slightly extended
            MonsterRarity.Magic => 35f,  // Standard melee
            _ => 35f                     // Standard melee contact range
        };

        MovementSpeed = rarity switch
        {
            MonsterRarity.Boss => 35f,   // Slow, lumbering
            MonsterRarity.Rare => 45f,   // Bulky, deliberate
            MonsterRarity.Magic => 55f,  // Slightly faster than trash
            _ => 60f                     // Standard trash mob speed
        };

        AggroRange = rarity switch
        {
            MonsterRarity.Boss => 50f,
            MonsterRarity.Rare => 47.5f,
            MonsterRarity.Magic => 42.5f,
            _ => 37.5f
        };

        if (initialPosition.HasValue)
        {
            Position = initialPosition.Value;
            SpawnPosition = initialPosition.Value;
        }

        MaxLife = MathF.Max(1f, maxLife);
        CurrentLife = MaxLife;

        PrimaryDamageType = primaryDamageType;
        DamageMin = MathF.Max(0f, damageMin);
        DamageMax = MathF.Max(DamageMin, damageMax);
        AttacksPerSecond = MathF.Max(0.1f, attacksPerSecond);
        CritChance = Math.Clamp(critChance, 0f, 100f);
        CritMultiplier = MathF.Max(100f, critMultiplier);
        Accuracy = MathF.Max(0f, accuracy);

        BaseExperience = MathF.Max(0f, baseExperience);
        ItemQuantityMultiplier = MathF.Max(0f, itemQuantityMultiplier);
        ItemRarityMultiplier = MathF.Max(0f, itemRarityMultiplier);

        AuraModifiers = auraModifiers ?? Array.Empty<MonsterAuraModifier>();
    }

    // ─────────────────────────────────────────────
    //  Damage Application
    // ─────────────────────────────────────────────

    /// <summary>
    /// Aggros this monster, enabling it to advance towards and attack the Exile.
    /// </summary>
    public void Aggro()
    {
        IsAggroed = true;
    }

    /// <summary>
    /// Applies damage to this monster, accounting for its resistances.
    /// Returns the actual damage dealt after resistance mitigation.
    /// </summary>
    /// <param name="amount">Raw damage amount (post-player modifiers).</param>
    /// <param name="damageType">Type of damage for resistance lookup.</param>
    public float TakeDamage(float amount, DamageType damageType)
    {
        if (amount <= 0f || !IsAlive)
            return 0f;

        IsAggroed = true;

        // Apply monster resistance
        float resistance = GetResistance(damageType);
        float mitigated = amount * (1f - resistance / 100f);
        mitigated = MathF.Max(0f, mitigated);

        CurrentLife -= mitigated;
        if (CurrentLife <= 0f)
        {
            CurrentLife = 0f;
            OnDamageTaken?.Invoke(mitigated, 0f);
            OnDied?.Invoke(this);
            return mitigated;
        }

        OnDamageTaken?.Invoke(mitigated, CurrentLife);
        return mitigated;
    }

    /// <summary>
    /// Applies raw unmitigated damage directly (used for DoT effects that have
    /// already been calculated with resistances factored in).
    /// </summary>
    public float TakeRawDamage(float amount)
    {
        if (amount <= 0f || !IsAlive)
            return 0f;

        IsAggroed = true;

        CurrentLife -= amount;
        if (CurrentLife <= 0f)
        {
            CurrentLife = 0f;
            OnDamageTaken?.Invoke(amount, 0f);
            OnDied?.Invoke(this);
            return amount;
        }

        OnDamageTaken?.Invoke(amount, CurrentLife);
        return amount;
    }

    /// <summary>
    /// Checks whether an incoming attack lands on this monster using deterministic Evasion Entropy.
    /// Returns true if hit lands, false if evaded.
    /// </summary>
    public bool DoesAttackHit(float evasionChance)
    {
        if (evasionChance <= 0f)
            return true;

        float chanceToHit = Math.Clamp(100f - evasionChance, 5f, 100f);
        EvasionEntropy += (int)chanceToHit;
        if (EvasionEntropy >= 100)
        {
            EvasionEntropy -= 100;
            return true;
        }
        return false;
    }

    /// <summary>
    /// Deducts damage directly from monster vitals, satisfying ICombatEntity.
    /// </summary>
    public float ApplyDamage(float amount, bool isChaos, bool chaosDoesNotBypassEs = false, float mindOverMatterPercent = 0f)
    {
        return TakeRawDamage(amount);
    }

    /// <summary>
    /// Returns the effective resistance for a given damage type.
    /// Physical damage uses Armour in PoE, but monsters have flat physical resistance of 0% unless specified.
    /// </summary>
    public float GetResistance(DamageType type) => type switch
    {
        DamageType.Physical => Defenses.Armour > 0f ? 0f : 0f,
        _ => Defenses.GetEffectiveResistance(type)
    };

    /// <summary>
    /// Returns the base DamagePool for this monster's attack across all damage types.
    /// </summary>
    public DamagePool GetBaseDamagePool()
    {
        var pool = new DamagePool();
        pool[PrimaryDamageType] = new DamageRange(DamageMin, DamageMax);
        return pool;
    }

    /// <summary>
    /// Builds the DamageModifierContext for this monster, applying crit, damage multiplier, and map modifier extra elemental damage.
    /// </summary>
    public DamageModifierContext BuildDamageContext(MapModifierRuntime? mapModifiers = null)
    {
        var context = new DamageModifierContext
        {
            BaseCritChance = CritChance,
            BaseCritMultiplier = CritMultiplier,
            IsAttack = true
        };

        if (mapModifiers != null)
        {
            if (mapModifiers.MonsterDamageMultiplier != 1.0f)
            {
                float morePercent = (mapModifiers.MonsterDamageMultiplier - 1.0f) * 100f;
                context.Global.AddMore(morePercent);
            }

            if (mapModifiers.ExtraColdDamagePercent > 0f)
                context.AddGainAsExtra(PrimaryDamageType, DamageType.Cold, mapModifiers.ExtraColdDamagePercent);
            if (mapModifiers.ExtraFireDamagePercent > 0f)
                context.AddGainAsExtra(PrimaryDamageType, DamageType.Fire, mapModifiers.ExtraFireDamagePercent);
            if (mapModifiers.ExtraLightningDamagePercent > 0f)
                context.AddGainAsExtra(PrimaryDamageType, DamageType.Lightning, mapModifiers.ExtraLightningDamagePercent);
            if (mapModifiers.ExtraChaosDamagePercent > 0f)
                context.AddGainAsExtra(PrimaryDamageType, DamageType.Chaos, mapModifiers.ExtraChaosDamagePercent);
        }

        return context;
    }

    /// <summary>
    /// Rolls a random damage value for this monster's hit using its DamageMin/DamageMax range.
    /// </summary>
    public float RollDamage(Random random)
    {
        if (DamageMin >= DamageMax)
            return DamageMin;
        return DamageMin + (float)random.NextDouble() * (DamageMax - DamageMin);
    }
}
