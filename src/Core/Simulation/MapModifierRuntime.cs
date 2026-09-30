using System;
using System.Collections.Generic;
using IdleExile.Core.Models;

namespace IdleExile.Core.Simulation;

/// <summary>
/// Breakdown of damage reflected from monsters back onto the player.
/// In PoE, reflected damage is treated as a hit from the monster and mitigated by player defenses.
/// </summary>
public sealed record ReflectedDamageResult(
    float ReflectedPhysical,
    float ReflectedFire,
    float ReflectedCold,
    float ReflectedLightning,
    float TotalReflectedDamage
)
{
    public bool HasReflectedDamage => TotalReflectedDamage > 0f;
}

/// <summary>
/// Evaluates and applies runtime combat effects of rolled map modifiers:
/// 1. Player defensive debuffs (e.g. -Max Elemental Resistances).
/// 2. Monster damage conversions & gain-as-extra (e.g. "Monsters deal 40% extra damage as Cold").
/// 3. Monster status ailment immunities (e.g. "Cannot be Chilled").
/// 4. Damage reflect pipeline hook (Physical & Elemental reflection).
///
/// Pure C# — no Godot Node dependencies.
/// </summary>
public sealed class MapModifierRuntime
{
    // --- Player Defensive Modifiers ---
    public float ReducedMaxElementalResistances { get; private set; } = 0f;
    public float ReducedArmourPercent { get; private set; } = 0f;
    public float LessLifeRecoveryPercent { get; private set; } = 0f;

    // --- Monster Offensive Conversions & Extra Damage ---
    public float ExtraColdDamagePercent { get; private set; } = 0f;
    public float ExtraFireDamagePercent { get; private set; } = 0f;
    public float ExtraLightningDamagePercent { get; private set; } = 0f;
    public float ExtraChaosDamagePercent { get; private set; } = 0f;

    // --- Reflect Modifiers ---
    public float ElementalReflectPercent { get; private set; } = 0f;
    public float PhysicalReflectPercent { get; private set; } = 0f;

    // --- Monster Ailment Immunities ---
    public bool MonstersCannotBeChilled { get; private set; } = false;
    public bool MonstersCannotBeFrozen { get; private set; } = false;
    public bool MonstersCannotBeIgnited { get; private set; } = false;
    public bool MonstersCannotBeShocked { get; private set; } = false;
    public bool MonstersCannotBePoisoned { get; private set; } = false;
    public bool MonstersCannotBeBled { get; private set; } = false;
    public bool MonstersImmuneToAllAilments { get; private set; } = false;

    // --- Monster Stat Scaling ---
    public float MonsterLifeMultiplier { get; private set; } = 1.0f;
    public float MonsterDamageMultiplier { get; private set; } = 1.0f;
    public float MonsterSpeedMultiplier { get; private set; } = 1.0f;

    public MapModifierRuntime(IReadOnlyList<RolledMapModifier>? modifiers)
    {
        if (modifiers == null)
            return;

        foreach (var mod in modifiers)
        {
            ParseModifier(mod);
        }
    }

    private void ParseModifier(RolledMapModifier mod)
    {
        string id = mod.Definition.Id.ToLowerInvariant();
        float val = mod.RolledValue;

        switch (id)
        {
            case "player_reduced_max_res":
                ReducedMaxElementalResistances += val;
                break;

            case "extra_damage_cold":
                ExtraColdDamagePercent += val;
                break;

            case "extra_damage_fire":
                ExtraFireDamagePercent += val;
                break;

            case "extra_damage_lightning":
                ExtraLightningDamagePercent += val;
                break;

            case "extra_damage_chaos":
                ExtraChaosDamagePercent += val;
                break;

            case "monster_reflect_elemental":
                ElementalReflectPercent += val;
                break;

            case "monster_reflect_physical":
                PhysicalReflectPercent += val;
                break;

            case "cannot_be_chilled":
                MonstersCannotBeChilled = true;
                break;

            case "cannot_be_frozen":
                MonstersCannotBeFrozen = true;
                break;

            case "cannot_be_ignited":
                MonstersCannotBeIgnited = true;
                break;

            case "cannot_be_shocked":
                MonstersCannotBeShocked = true;
                break;

            case "cannot_be_poisoned":
                MonstersCannotBePoisoned = true;
                break;

            case "cannot_be_bled":
                MonstersCannotBeBled = true;
                break;

            case "immune_to_ailments":
                MonstersImmuneToAllAilments = true;
                break;

            case "monster_increased_life":
                MonsterLifeMultiplier *= (1f + val / 100f);
                break;

            case "monster_increased_damage":
                MonsterDamageMultiplier *= (1f + val / 100f);
                break;

            case "monster_increased_speed":
                MonsterSpeedMultiplier *= (1f + val / 100f);
                break;
        }
    }

    /// <summary>
    /// Modifies a snapshot of <see cref="DefensiveStats"/> by applying map-level penalties
    /// such as reduced maximum elemental resistances.
    /// </summary>
    public DefensiveStats ApplyToDefensiveStats(DefensiveStats baseStats)
    {
        if (ReducedMaxElementalResistances <= 0f && ReducedArmourPercent <= 0f)
            return baseStats;

        float newMaxFire = MathF.Max(0f, baseStats.MaxFireResistance - ReducedMaxElementalResistances);
        float newMaxCold = MathF.Max(0f, baseStats.MaxColdResistance - ReducedMaxElementalResistances);
        float newMaxLight = MathF.Max(0f, baseStats.MaxLightningResistance - ReducedMaxElementalResistances);

        float newArmour = baseStats.Armour * MathF.Max(0f, 1f - ReducedArmourPercent / 100f);

        return baseStats with
        {
            MaxFireResistance = newMaxFire,
            MaxColdResistance = newMaxCold,
            MaxLightningResistance = newMaxLight,
            Armour = newArmour
        };
    }

    /// <summary>
    /// Applies extra elemental/chaos damage to a monster's base hit based on rolled map modifiers.
    /// E.g. "Monsters deal 40% extra damage as Cold" adds 40% of the base damage as Cold.
    /// </summary>
    public DamagePool ModifyMonsterDamage(float baseDamage, DamageType primaryType)
    {
        var pool = new DamagePool();
        float scaledBase = baseDamage * MonsterDamageMultiplier;

        // Base primary damage
        pool[primaryType] = new DamageRange(scaledBase, scaledBase);

        // Extra damage as Cold
        if (ExtraColdDamagePercent > 0f)
        {
            float extra = scaledBase * (ExtraColdDamagePercent / 100f);
            pool.Cold = new DamageRange(pool.Cold.Min + extra, pool.Cold.Max + extra);
        }

        // Extra damage as Fire
        if (ExtraFireDamagePercent > 0f)
        {
            float extra = scaledBase * (ExtraFireDamagePercent / 100f);
            pool.Fire = new DamageRange(pool.Fire.Min + extra, pool.Fire.Max + extra);
        }

        // Extra damage as Lightning
        if (ExtraLightningDamagePercent > 0f)
        {
            float extra = scaledBase * (ExtraLightningDamagePercent / 100f);
            pool.Lightning = new DamageRange(pool.Lightning.Min + extra, pool.Lightning.Max + extra);
        }

        // Extra damage as Chaos
        if (ExtraChaosDamagePercent > 0f)
        {
            float extra = scaledBase * (ExtraChaosDamagePercent / 100f);
            pool.Chaos = new DamageRange(pool.Chaos.Min + extra, pool.Chaos.Max + extra);
        }

        return pool;
    }

    /// <summary>
    /// Checks whether an ailment can be applied to monsters in this map.
    /// Returns false if the map has immunity flags preventing the ailment.
    /// </summary>
    public bool CanInflictAilmentOnMonster(AilmentType type)
    {
        if (MonstersImmuneToAllAilments)
            return false;

        return type switch
        {
            AilmentType.Chill => !MonstersCannotBeChilled,
            AilmentType.Freeze => !MonstersCannotBeFrozen,
            AilmentType.Ignite => !MonstersCannotBeIgnited,
            AilmentType.Shock => !MonstersCannotBeShocked,
            AilmentType.Bleed => !MonstersCannotBeBled,
            AilmentType.Poison => !MonstersCannotBePoisoned,
            _ => true
        };
    }

    /// <summary>
    /// Calculates damage reflected back to the player from an outgoing damaging hit.
    /// Supports Elemental Reflect and Physical Reflect.
    /// </summary>
    public ReflectedDamageResult CalculateReflectedDamage(HitDamageBreakdown playerDamageDealt)
    {
        float reflectedPhysical = 0f;
        float reflectedFire = 0f;
        float reflectedCold = 0f;
        float reflectedLight = 0f;

        if (PhysicalReflectPercent > 0f && playerDamageDealt.Physical > 0f)
        {
            reflectedPhysical = playerDamageDealt.Physical * (PhysicalReflectPercent / 100f);
        }

        if (ElementalReflectPercent > 0f)
        {
            float ratio = ElementalReflectPercent / 100f;
            reflectedFire = playerDamageDealt.Fire * ratio;
            reflectedCold = playerDamageDealt.Cold * ratio;
            reflectedLight = playerDamageDealt.Lightning * ratio;
        }

        float total = reflectedPhysical + reflectedFire + reflectedCold + reflectedLight;

        return new ReflectedDamageResult(
            ReflectedPhysical: reflectedPhysical,
            ReflectedFire: reflectedFire,
            ReflectedCold: reflectedCold,
            ReflectedLightning: reflectedLight,
            TotalReflectedDamage: total
        );
    }
}
