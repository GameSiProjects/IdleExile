using System;
using System.Linq;
using IdleExile.Core.Models;
using IdleExile.Core.Simulation;
using IdleExile.Core.State;

namespace IdleExile.Core.Services;

/// <summary>
/// Unified Combat Attack Service for Idle Exile.
/// Executes attacks and spells using a single, symmetrical PoE mathematical pipeline
/// for both Player attacking Monster and Monster attacking Player.
///
/// Ensures:
/// 1. Same damage calculation (Base DamagePool + DamageModifierContext + RollHit).
/// 2. Same status ailment evaluation (Ignite, Chill, Freeze, Shock, Bleed, Poison).
/// 3. Same defensive layer evaluation (Evasion, Block, Suppression, Armour, Resistances, EHP).
/// 4. Symmetric debuff amplification (Shock debuff on target increases damage taken).
///
/// Pure C# — zero Godot Node dependencies.
/// </summary>
public static class CombatAttackService
{
    /// <summary>
    /// Executes a single combat hit between an attacker and target entity.
    /// Used symmetrically by Player attacks and Monster attacks.
    /// </summary>
    /// <param name="attacker">The entity executing the attack/spell.</param>
    /// <param name="target">The entity receiving the hit.</param>
    /// <param name="attack">The attack payload containing damage pools, modifiers, and tags.</param>
    /// <param name="targetDefenses">Effective defensive stats of the target entity.</param>
    /// <param name="random">RNG source for hit rolls, crits, block, and suppression.</param>
    /// <param name="guardSkill">Optional active guard skill protecting the target (e.g. Steelskin, Molten Shell).</param>
    /// <returns>Comprehensive result containing hit rolls, defense outcomes, and actual damage dealt.</returns>
    public static CombatAttackResult ExecuteHit(
        ICombatEntity attacker,
        ICombatEntity target,
        AttackPayload attack,
        DefensiveStats targetDefenses,
        Random? random = null,
        GuardSkillState? guardSkill = null)
    {
        var rng = random ?? Random.Shared;

        if (!target.IsAlive)
        {
            return CombatAttackResult.Empty(attacker, target);
        }

        // 1. Shock Debuff Amplifier: Shocked targets take increased/more damage from hits
        DamageModifierContext hitContext = attack.Context;
        var shock = target.Ailments.ActiveAilments.FirstOrDefault(a => a.Type == AilmentType.Shock);
        if (shock != null && shock.EffectPercent > 0f)
        {
            hitContext = attack.Context.Clone();
            hitContext.AddGlobalMore(shock.EffectPercent);
        }

        // 2. Roll Hit & Status Ailments via unified PoE DamageCalculator
        var hit = DamageCalculator.RollHit(attack.BaseDamage, hitContext, target.MaxLife, rng);

        // 3. Guard Skill Damage Absorption (if target has an active guard skill)
        HitDamageBreakdown incomingDamage = hit.Damage;
        if (guardSkill != null && guardSkill.IsActive && incomingDamage.Total > 0f)
        {
            float totalBefore = incomingDamage.Total;
            float remaining = guardSkill.AbsorbDamage(totalBefore);
            if (totalBefore > 0f)
            {
                incomingDamage = incomingDamage.Multiply(remaining / totalBefore);
            }
        }

        // 4. Process through Defender's PoE Defensive Layers (Evasion, Block, Suppression, Armour, Resistances, EHP)
        var defense = DefensiveLayers.ProcessHit(
            incomingDamage,
            isAttack: attack.IsAttack,
            isSpell: attack.IsSpell,
            target,
            targetDefenses,
            rng
        );

        // 5. Apply Inflicted Status Ailments if hit connected and dealt damage
        if (!defense.FullyAvoided && defense.TotalDamageApplied > 0f)
        {
            foreach (var ailment in hit.InflictedAilments)
            {
                target.Ailments.ApplyAilment(ailment);
            }
        }

        return new CombatAttackResult(
            Attacker: attacker,
            Target: target,
            Hit: hit,
            Defense: defense,
            DamageDealt: defense.TotalDamageApplied,
            TargetDied: defense.TargetDied || !target.IsAlive
        );
    }

    /// <summary>
    /// Constructs an AttackPayload for the player character using their equipped gear,
    /// primary active skill, and aggregated passive/gear stat modifiers.
    /// </summary>
    public static AttackPayload CreatePlayerAttack(
        PlayerState player,
        SkillLoadout loadout,
        StatModifierBucket? passiveStats,
        StatModifierBucket? gearStats)
    {
        var basePool = loadout.PrimarySkill.GetBaseDamagePool(player.Gear);
        if (basePool.TotalMax <= 0f)
        {
            // Default unarmed fallback: 8-14 Physical
            basePool.Physical = new DamageRange(8f, 14f);
        }

        var effectiveGear = gearStats?.Clone() ?? new StatModifierBucket();
        if (player.MainHand != null && player.MainHand.BaseDefinition.ImplicitAffixId == "wand_implicit_spell_damage")
        {
            effectiveGear.AddIncreased(player.MainHand.BaseDefinition.ImplicitValue);
        }

        var damageContext = loadout.PrimarySkill.BuildDamageContext(passiveStats, effectiveGear);
        var geometry = loadout.PrimarySkill.GetGeometryType();
        float range = loadout.PrimarySkill.GetBaseRadiusOrRange();

        DamageType primaryType = basePool.Physical.Max > 0f ? DamageType.Physical
            : basePool.Fire.Max > 0f ? DamageType.Fire
            : basePool.Cold.Max > 0f ? DamageType.Cold
            : basePool.Lightning.Max > 0f ? DamageType.Lightning
            : DamageType.Chaos;

        return new AttackPayload(player, basePool, damageContext)
        {
            IsAttack = loadout.PrimarySkill.IsAttack,
            IsSpell = loadout.PrimarySkill.IsSpell,
            IsMelee = loadout.PrimarySkill.IsMelee,
            Accuracy = 1000f,
            Geometry = geometry,
            EffectiveRange = range,
            PrimaryDamageType = primaryType
        };
    }

    /// <summary>
    /// Constructs a fallback Default Attack payload (PoE basic weapon/unarmed strike, 0 mana cost).
    /// Used when the character has insufficient mana to cast the active gem setup,
    /// preventing combat stalling and ensuring the idle loop never stops.
    /// </summary>
    public static AttackPayload CreatePlayerDefaultAttack(
        PlayerState player,
        StatModifierBucket? passiveStats,
        StatModifierBucket? gearStats)
    {
        var basePool = new DamagePool();
        if (player.Gear.MainHand != null)
        {
            basePool.Physical = player.Gear.GetBasePhysicalDamageRange(isCleave: false);
        }
        else
        {
            basePool.Physical = new DamageRange(8f, 14f);
        }

        var context = new DamageModifierContext
        {
            IsAttack = true
        };

        bool isRanged = player.Gear.MainHand?.BaseDefinition.WeaponClass == WeaponClass.Bow ||
                        player.Gear.MainHand?.BaseDefinition.WeaponClass == WeaponClass.Wand;
        float range = isRanged ? 220f : SpatialCombatService.MeleeRange;

        return new AttackPayload(player, basePool, context)
        {
            IsAttack = true,
            IsSpell = false,
            IsMelee = !isRanged,
            Accuracy = 1000f,
            Geometry = SkillGeometryType.SingleTarget,
            EffectiveRange = range,
            PrimaryDamageType = DamageType.Physical
        };
    }

    /// <summary>
    /// Constructs an AttackPayload for a monster using its pre-scaled base damage,
    /// critical strike properties, primary damage type, and active map modifiers.
    /// </summary>
    public static AttackPayload CreateMonsterAttack(
        MonsterInstance monster,
        MapModifierRuntime? mapModifiers)
    {
        var basePool = monster.GetBaseDamagePool();
        var context = monster.BuildDamageContext(mapModifiers);

        return new AttackPayload(monster, basePool, context)
        {
            IsAttack = true,
            IsSpell = false,
            IsMelee = true,
            Accuracy = monster.Accuracy,
            Geometry = SkillGeometryType.SingleTarget,
            EffectiveRange = monster.AttackRange + monster.Radius,
            PrimaryDamageType = monster.PrimaryDamageType
        };
    }
}
