using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using IdleExile.Core.Models;

namespace IdleExile.Core.State;

/// <summary>
/// A single socketed skill setup representing an Active Gem and its linked Support Gems.
/// Follows the PoE 2 decoupled gem model:
/// - Equipment has NO sockets.
/// - Active Gems contain up to 5 support sockets.
/// - Support Gems are automatically linked if they share at least one tag with the Active Gem.
/// </summary>
public sealed class SkillSocketSetup
{
    public const int MaxSupportSlots = 5;

    public GemDefinition? ActiveGem { get; private set; }
    private readonly List<GemDefinition> _supportGems = new();

    /// <summary>
    /// Event fired whenever the active gem or support gems within this setup change.
    /// </summary>
    public event Action? OnSetupChanged;

    /// <summary>
    /// All support gems currently socketed into this active gem.
    /// </summary>
    public IReadOnlyList<GemDefinition> SocketedSupports => _supportGems;

    /// <summary>
    /// Support gems that are actively linked and valid (share compatible tags with the Active Gem).
    /// </summary>
    public IEnumerable<GemDefinition> ValidLinkedSupports =>
        _supportGems.Where(IsSupportCompatible);

    /// <summary>
    /// Indicates whether the active skill has the melee tag.
    /// </summary>
    public bool IsMelee => ActiveGem?.Tags?.Contains("melee", StringComparer.OrdinalIgnoreCase) ?? false;

    /// <summary>
    /// Indicates whether the active skill has the projectile tag.
    /// </summary>
    public bool IsProjectile => ActiveGem?.Tags?.Contains("projectile", StringComparer.OrdinalIgnoreCase) ?? false;

    /// <summary>
    /// Indicates whether the active skill has the attack tag. Defaults to true if no active gem (unarmed attack).
    /// </summary>
    public bool IsAttack => ActiveGem?.Tags?.Contains("attack", StringComparer.OrdinalIgnoreCase) ?? true;

    /// <summary>
    /// Indicates whether the active skill has the spell tag.
    /// </summary>
    public bool IsSpell => ActiveGem?.Tags?.Contains("spell", StringComparer.OrdinalIgnoreCase) ?? false;

    public SkillSocketSetup(GemDefinition? activeGem = null)
    {
        if (activeGem != null)
        {
            SetActiveGem(activeGem);
        }
    }

    /// <summary>
    /// Sockets or replaces the Active Gem for this skill slot.
    /// </summary>
    public bool SetActiveGem(GemDefinition? gem)
    {
        if (gem != null && gem.Category != "Active")
            return false;

        ActiveGem = gem;
        OnSetupChanged?.Invoke();
        return true;
    }

    /// <summary>
    /// Removes the current active gem from this socket setup.
    /// </summary>
    public void ClearActiveGem()
    {
        ActiveGem = null;
        OnSetupChanged?.Invoke();
    }

    /// <summary>
    /// Sockets a Support Gem into the next available slot on this Active Gem (up to 5 max).
    /// </summary>
    public bool AddSupportGem(GemDefinition gem)
    {
        if (gem.Category != "Support")
            return false;

        if (_supportGems.Count >= MaxSupportSlots)
            return false;

        _supportGems.Add(gem);
        OnSetupChanged?.Invoke();
        return true;
    }

    /// <summary>
    /// Sockets or replaces a Support Gem at a specific slot index (0 to 4).
    /// </summary>
    public bool SetSupportGemAt(int index, GemDefinition gem)
    {
        if (gem.Category != "Support")
            return false;

        if (index < 0 || index >= MaxSupportSlots)
            return false;

        if (index < _supportGems.Count)
        {
            _supportGems[index] = gem;
            OnSetupChanged?.Invoke();
            return true;
        }

        if (index == _supportGems.Count)
        {
            _supportGems.Add(gem);
            OnSetupChanged?.Invoke();
            return true;
        }

        return false;
    }

    /// <summary>
    /// Removes a Support Gem at the given slot index.
    /// </summary>
    public bool RemoveSupportGemAt(int index)
    {
        if (index < 0 || index >= _supportGems.Count)
            return false;

        _supportGems.RemoveAt(index);
        OnSetupChanged?.Invoke();
        return true;
    }

    /// <summary>
    /// Clears all socketed support gems.
    /// </summary>
    public void ClearSupports()
    {
        _supportGems.Clear();
        OnSetupChanged?.Invoke();
    }

    /// <summary>
    /// Validates if a support gem is compatible with the current active gem based on tags.
    /// In PoE, a support is linked if it has no required tags OR at least one required tag matches the active gem tags.
    /// </summary>
    public bool IsSupportCompatible(GemDefinition support)
    {
        if (ActiveGem == null)
            return false;

        if (support.RequiredTags == null || support.RequiredTags.Length == 0)
            return true;

        if (ActiveGem.Tags == null || ActiveGem.Tags.Length == 0)
            return false;

        // Matches if any required tag is present in the active gem's tags
        return support.RequiredTags.Any(req => ActiveGem.Tags.Contains(req, StringComparer.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Extracts the base damage pool (Min/Max) for this skill setup.
    /// <summary>
    /// Extracts the base damage pool (Min/Max) for this skill setup.
    /// If the skill is an attack and equipped weapons are provided, draws base damage from weapon(s).
    /// </summary>
    public DamagePool GetBaseDamagePool(EquippedGearState? gear = null)
    {
        var pool = new DamagePool();
        if (ActiveGem?.BaseStats == null)
            return pool;

        bool isAttack = ActiveGem.Tags?.Contains("attack", StringComparer.OrdinalIgnoreCase) ?? false;
        if (isAttack && gear?.MainHand != null)
        {
            bool isCleave = string.Equals(ActiveGem.Id, "cleave", StringComparison.OrdinalIgnoreCase);
            var physRange = gear.GetBasePhysicalDamageRange(isCleave);
            pool.Physical = physRange;
            return pool;
        }

        float min = GetFloat(ActiveGem.BaseStats, "baseDamageMin", 0f);
        float max = GetFloat(ActiveGem.BaseStats, "baseDamageMax", 0f);

        // If min > max, swap
        if (min > max) (min, max) = (max, min);

        string damageTypeStr = GetString(ActiveGem.BaseStats, "damageType", "Physical");
        if (Enum.TryParse<DamageType>(damageTypeStr, ignoreCase: true, out var damageType))
        {
            pool[damageType] = new DamageRange(min, max);
        }
        else
        {
            pool.Physical = new DamageRange(min, max);
        }

        return pool;
    }

    /// <summary>
    /// Calculates the effective mana cost after compounding all linked support gem mana multipliers.
    /// Formula: BaseManaCost * prod(SupportManaMultiplier / 100).
    /// </summary>
    public float CalculateEffectiveManaCost()
    {
        if (ActiveGem?.BaseStats == null)
            return 0f;

        float baseCost = GetFloat(ActiveGem.BaseStats, "manaCost", 0f);
        float multiplierProduct = 1f;

        foreach (var support in ValidLinkedSupports)
        {
            if (support.Modifiers != null)
            {
                float manaMult = GetFloat(support.Modifiers, "manaMultiplier", 100f);
                multiplierProduct *= (manaMult / 100f);
            }
        }

        return MathF.Max(0f, baseCost * multiplierProduct);
    }

    /// <summary>
    /// Calculates the compounded damage multiplier for all valid linked supports:
    /// prod(1 + more/100) * prod(1 - less/100).
    /// </summary>
    public float CalculateTotalDamageMultiplier()
    {
        float multiplier = 1f;

        if (ActiveGem?.BaseStats != null)
        {
            float activeMult = GetFloat(ActiveGem.BaseStats, "damageMultiplier", 100f);
            if (activeMult > 0f)
            {
                multiplier *= (activeMult / 100f);
            }
        }

        foreach (var support in ValidLinkedSupports)
        {
            if (support.Modifiers == null)
                continue;

            if (support.Modifiers.TryGetValue("damageMultiplier", out _))
            {
                float dmgMult = GetFloat(support.Modifiers, "damageMultiplier", 0f);
                multiplier *= (1f + dmgMult / 100f);
            }

            if (support.Modifiers.TryGetValue("morePhysicalDamagePercent", out _))
            {
                float morePhys = GetFloat(support.Modifiers, "morePhysicalDamagePercent", 0f);
                multiplier *= (1f + morePhys / 100f);
            }

            if (support.Modifiers.TryGetValue("moreFireDamagePercent", out _))
            {
                float moreFire = GetFloat(support.Modifiers, "moreFireDamagePercent", 0f);
                multiplier *= (1f + moreFire / 100f);
            }

            if (support.Modifiers.TryGetValue("moreAoeDamagePercent", out _))
            {
                float moreAoe = GetFloat(support.Modifiers, "moreAoeDamagePercent", 0f);
                multiplier *= (1f + moreAoe / 100f);
            }
        }

        return MathF.Max(0.01f, multiplier);
    }

    /// <summary>
    /// Computes the base actions per second (APS or casts per second) for this skill.
    /// For attack skills, uses the equipped weapon's effective base APS (including dual-wielding bonuses).
    /// </summary>
    public float CalculateBaseActionSpeed(EquippedGearState? gear = null)
    {
        if (ActiveGem?.BaseStats == null)
            return 1.0f;

        // If castTime is defined (Spells), APS = 1.0 / castTime
        float castTime = GetFloat(ActiveGem.BaseStats, "castTime", 0f);
        if (castTime > 0f)
        {
            return 1.0f / castTime;
        }

        // If attack skill, base APS comes from equipped weapon(s)
        float weaponAps = gear?.GetEffectiveBaseAps() ?? 1.20f;
        float attackSpeedMult = GetFloat(ActiveGem.BaseStats, "attackSpeedMultiplier", 100f);
        return weaponAps * (attackSpeedMult / 100f);
    }

    /// <summary>
    /// Constructs a fully aggregated <see cref="DamageModifierContext"/> from this skill's active gem,
    /// linked support gems, and optional external gear/passive modifier buckets.
    /// </summary>
    public DamageModifierContext BuildDamageContext(
        StatModifierBucket? passiveStats = null,
        StatModifierBucket? gearStats = null)
    {
        var context = new DamageModifierContext();

        if (ActiveGem == null)
            return context;

        // 1. Determine Attack vs Spell from Active Gem tags
        bool isAttack = ActiveGem.Tags?.Contains("attack", StringComparer.OrdinalIgnoreCase) ?? false;
        context.IsAttack = isAttack;

        // 2. Active Gem Base Stats
        if (ActiveGem.BaseStats != null)
        {
            float baseCrit = GetFloat(ActiveGem.BaseStats, "criticalStrikeChance", 5.0f);
            context.BaseCritChance = baseCrit;

            float damageMultiplier = GetFloat(ActiveGem.BaseStats, "damageMultiplier", 0f);
            if (damageMultiplier > 0f)
            {
                // Active gem damage effectiveness / multiplier
                float morePercent = damageMultiplier - 100f;
                if (morePercent > 0f)
                {
                    context.AddGlobalMore(morePercent);
                }
                else if (morePercent < 0f)
                {
                    context.AddGlobalLess(MathF.Abs(morePercent));
                }
            }
        }

        // 3. Process Linked Support Gems Modifiers
        foreach (var support in ValidLinkedSupports)
        {
            if (support.Modifiers == null)
                continue;

            // Damage Multiplier (e.g. -25% Less for GMP, or +30% More)
            if (support.Modifiers.TryGetValue("damageMultiplier", out _))
            {
                float dmgMult = GetFloat(support.Modifiers, "damageMultiplier", 0f);
                if (dmgMult > 0f)
                {
                    context.AddGlobalMore(dmgMult);
                }
                else if (dmgMult < 0f)
                {
                    context.AddGlobalLess(MathF.Abs(dmgMult));
                }
            }

            if (support.Modifiers.TryGetValue("morePhysicalDamagePercent", out _))
            {
                float morePhys = GetFloat(support.Modifiers, "morePhysicalDamagePercent", 0f);
                context.AddGlobalMore(morePhys);
            }

            if (support.Modifiers.TryGetValue("moreFireDamagePercent", out _))
            {
                float moreFire = GetFloat(support.Modifiers, "moreFireDamagePercent", 0f);
                context.AddGlobalMore(moreFire);
            }

            if (support.Modifiers.TryGetValue("moreAoeDamagePercent", out _))
            {
                float moreAoe = GetFloat(support.Modifiers, "moreAoeDamagePercent", 0f);
                context.AddGlobalMore(moreAoe);
            }

            // Extra Physical as Fire (e.g. 35% for Added Fire Damage)
            if (support.Modifiers.TryGetValue("extraPhysicalAsFirePercent", out _))
            {
                float extraFire = GetFloat(support.Modifiers, "extraPhysicalAsFirePercent", 0f);
                context.AddGainAsExtra(DamageType.Physical, DamageType.Fire, extraFire);
            }

            // Extra Flat Added Damages
            if (support.Modifiers.TryGetValue("addedPhysicalDamage", out _))
            {
                float flat = GetFloat(support.Modifiers, "addedPhysicalDamage", 0f);
                context.AddedFlatDamage.Physical = new DamageRange(flat, flat);
            }
            if (support.Modifiers.TryGetValue("addedFireDamage", out _))
            {
                float flat = GetFloat(support.Modifiers, "addedFireDamage", 0f);
                context.AddedFlatDamage.Fire = new DamageRange(flat, flat);
            }
            if (support.Modifiers.TryGetValue("addedColdDamage", out _))
            {
                float flat = GetFloat(support.Modifiers, "addedColdDamage", 0f);
                context.AddedFlatDamage.Cold = new DamageRange(flat, flat);
            }
            if (support.Modifiers.TryGetValue("addedLightningDamage", out _))
            {
                float flat = GetFloat(support.Modifiers, "addedLightningDamage", 0f);
                context.AddedFlatDamage.Lightning = new DamageRange(flat, flat);
            }
            if (support.Modifiers.TryGetValue("addedChaosDamage", out _))
            {
                float flat = GetFloat(support.Modifiers, "addedChaosDamage", 0f);
                context.AddedFlatDamage.Chaos = new DamageRange(flat, flat);
            }
        }

        // 4. Incorporate External Passive & Gear Stats
        if (passiveStats != null)
        {
            context.Global.Combine(passiveStats);
        }

        if (gearStats != null)
        {
            context.Global.Combine(gearStats);
        }

        return context;
    }

    /// <summary>
    /// Identifies the 2D spatial geometry type for this active skill setup.
    /// </summary>
    public IdleExile.Core.Services.SkillGeometryType GetGeometryType()
    {
        if (ActiveGem == null)
            return IdleExile.Core.Services.SkillGeometryType.SingleTarget;

        if (ActiveGem.BaseStats != null && (ActiveGem.BaseStats.ContainsKey("sweepAngle") || ActiveGem.BaseStats.ContainsKey("coneAngle")))
            return IdleExile.Core.Services.SkillGeometryType.Cone;

        if (string.Equals(ActiveGem.Id, "cleave", StringComparison.OrdinalIgnoreCase))
            return IdleExile.Core.Services.SkillGeometryType.Cone;

        int projCount = GetProjectileCount();
        if (projCount > 1)
            return IdleExile.Core.Services.SkillGeometryType.ProjectileSpread;

        if (ActiveGem.Tags?.Contains("aoe", StringComparer.OrdinalIgnoreCase) ?? false)
            return IdleExile.Core.Services.SkillGeometryType.RadialAoE;

        return IdleExile.Core.Services.SkillGeometryType.SingleTarget;
    }

    /// <summary>
    /// Gets base radius or range for AoE/Cone hit testing.
    /// </summary>
    public float GetBaseRadiusOrRange()
    {
        if (ActiveGem?.BaseStats == null)
            return 35f;

        float aoeRadius = GetFloat(ActiveGem.BaseStats, "aoeRadius", 0f);
        if (aoeRadius > 0f) return aoeRadius;

        float range = GetFloat(ActiveGem.BaseStats, "range", 0f);
        if (range > 0f) return range;

        return 35f;
    }

    /// <summary>
    /// Gets cone aperture angle in degrees.
    /// </summary>
    public float GetConeAngle()
    {
        if (ActiveGem?.BaseStats == null)
            return 75f;

        float sweep = GetFloat(ActiveGem.BaseStats, "sweepAngle", 0f);
        if (sweep > 0f) return sweep;

        return GetFloat(ActiveGem.BaseStats, "coneAngle", 75f);
    }

    /// <summary>
    /// Calculates total projectile count (base + extra from linked supports like GMP/LMP).
    /// </summary>
    public int GetProjectileCount()
    {
        int count = 1;
        if (ActiveGem?.BaseStats != null)
        {
            count = (int)GetFloat(ActiveGem.BaseStats, "projectileCount", 1f);
        }

        foreach (var support in ValidLinkedSupports)
        {
            if (support.Modifiers != null)
            {
                count += (int)GetFloat(support.Modifiers, "extraProjectiles", 0f);
            }
        }

        return Math.Max(1, count);
    }

    /// <summary>
    /// Gets angular spread angle in degrees for multi-vector volleys.
    /// </summary>
    public float GetSpreadAngle()
    {
        if (ActiveGem?.BaseStats != null)
        {
            float spread = GetFloat(ActiveGem.BaseStats, "spreadAngle", 0f);
            if (spread > 0f) return spread;
        }

        int count = GetProjectileCount();
        if (count >= 5) return 45f;
        if (count >= 3) return 30f;
        return 20f;
    }

    /// <summary>
    /// Gets number of chains supported by linked gems.
    /// </summary>
    public int GetChainCount()
    {
        int chains = 0;
        foreach (var support in ValidLinkedSupports)
        {
            if (support.Modifiers != null)
            {
                chains += (int)GetFloat(support.Modifiers, "extraChains", 0f);
            }
        }
        return chains;
    }

    /// <summary>
    /// Gets number of pierces supported by active gem and linked support gems.
    /// </summary>
    public int GetPierceCount()
    {
        int pierce = 0;
        if (ActiveGem?.BaseStats != null)
        {
            pierce = (int)GetFloat(ActiveGem.BaseStats, "pierceCount", 0f);
        }

        foreach (var support in ValidLinkedSupports)
        {
            if (support.Modifiers != null)
            {
                pierce += (int)GetFloat(support.Modifiers, "extraPierce", 0f);
            }
        }
        return Math.Max(0, pierce);
    }

    /// <summary>
    /// Gets projectile flight speed in pixels per second.
    /// </summary>
    public float GetProjectileSpeed()
    {
        if (ActiveGem?.BaseStats != null)
        {
            float speed = GetFloat(ActiveGem.BaseStats, "projectileSpeed", 0f);
            if (speed > 0f) return speed;
        }
        return 520f;
    }

    // ─── Helpers for reading dynamic JSON dictionary values ───

    private static float GetFloat(Dictionary<string, object> dict, string key, float defaultValue)
    {
        if (!dict.TryGetValue(key, out var val) || val == null)
            return defaultValue;

        if (val is JsonElement elem)
        {
            if (elem.ValueKind == JsonValueKind.Number && elem.TryGetSingle(out float f))
                return f;
            if (elem.ValueKind == JsonValueKind.String && float.TryParse(elem.GetString(), out float parsed))
                return parsed;
        }

        try
        {
            return Convert.ToSingle(val);
        }
        catch
        {
            return defaultValue;
        }
    }

    private static string GetString(Dictionary<string, object> dict, string key, string defaultValue)
    {
        if (!dict.TryGetValue(key, out var val) || val == null)
            return defaultValue;

        if (val is JsonElement elem)
        {
            return elem.GetString() ?? defaultValue;
        }

        return val.ToString() ?? defaultValue;
    }
}

/// <summary>
/// Runtime state of a Guard Skill (e.g. Steelskin, Molten Shell).
/// Handles absorption buffer and cooldown tracking.
/// </summary>
public sealed class GuardSkillState
{
    public string Name { get; set; } = "Steelskin";
    public float CooldownDuration { get; set; } = 4.0f;
    public float CooldownRemaining { get; private set; } = 0f;

    public float BuffDuration { get; set; } = 3.0f;
    public float BuffRemaining { get; private set; } = 0f;

    public float MaxAbsorbPool { get; set; } = 500f;
    public float CurrentAbsorbPool { get; private set; } = 0f;

    public float AbsorbPercent { get; set; } = 70.0f; // Absorbs 70% of damage taken

    public bool IsActive => BuffRemaining > 0f && CurrentAbsorbPool > 0f;
    public bool IsOnCooldown => CooldownRemaining > 0f;

    public event Action? OnGuardActivated;
    public event Action? OnGuardExpired;

    /// <summary>
    /// Attempts to activate the guard skill.
    /// </summary>
    public bool TryActivate()
    {
        if (IsOnCooldown || IsActive)
            return false;

        BuffRemaining = BuffDuration;
        CurrentAbsorbPool = MaxAbsorbPool;
        CooldownRemaining = CooldownDuration;
        OnGuardActivated?.Invoke();
        return true;
    }

    /// <summary>
    /// Absorbs a portion of incoming damage using the guard buffer.
    /// Returns the unabsorbed remaining damage.
    /// </summary>
    public float AbsorbDamage(float incomingDamage)
    {
        if (!IsActive || incomingDamage <= 0f)
            return incomingDamage;

        float absorbable = incomingDamage * (AbsorbPercent / 100f);
        float absorbed = MathF.Min(absorbable, CurrentAbsorbPool);

        CurrentAbsorbPool -= absorbed;
        float remaining = incomingDamage - absorbed;

        if (CurrentAbsorbPool <= 0f)
        {
            CurrentAbsorbPool = 0f;
            BuffRemaining = 0f;
            OnGuardExpired?.Invoke();
        }

        return remaining;
    }

    /// <summary>
    /// Advances timers by deltaTime.
    /// </summary>
    public void Tick(float deltaTime)
    {
        if (BuffRemaining > 0f)
        {
            BuffRemaining -= deltaTime;
            if (BuffRemaining <= 0f)
            {
                BuffRemaining = 0f;
                CurrentAbsorbPool = 0f;
                OnGuardExpired?.Invoke();
            }
        }

        if (CooldownRemaining > 0f)
        {
            CooldownRemaining = MathF.Max(0f, CooldownRemaining - deltaTime);
        }
    }

    public void Reset()
    {
        CooldownRemaining = 0f;
        BuffRemaining = 0f;
        CurrentAbsorbPool = 0f;
    }
}

/// <summary>
/// Supported flask types matching GDD section 3.3.
/// </summary>
public enum FlaskType
{
    Life,
    Quicksilver,
    Granite,
    Diamond,
    Quartz
}

/// <summary>
/// Auto-instill conditions for automated flask consumption.
/// </summary>
public enum FlaskAutoInstillCondition
{
    Manual,
    LifeBelowPercent,
    OnCombatStart,
    OnCooldownReady
}

/// <summary>
/// Represents a single flask slot with charges and auto-instill behavior.
/// </summary>
public sealed class FlaskSlot
{
    public FlaskType Type { get; }
    public string Name { get; }
    public float MaxCharges { get; }
    public float CurrentCharges { get; private set; }
    public float ChargesPerUse { get; }
    public float DurationSeconds { get; }
    public float RemainingDuration { get; private set; }

    public FlaskAutoInstillCondition AutoInstillCondition { get; set; }
    public float AutoInstillThreshold { get; set; } // e.g. 50% for Life flask

    public bool IsActive => RemainingDuration > 0f;
    public bool CanUse => CurrentCharges >= ChargesPerUse;

    public event Action<FlaskSlot>? OnFlaskUsed;

    public FlaskSlot(
        FlaskType type,
        string name,
        float maxCharges,
        float chargesPerUse,
        float durationSeconds,
        FlaskAutoInstillCondition autoCondition = FlaskAutoInstillCondition.Manual,
        float autoThreshold = 50f)
    {
        Type = type;
        Name = name;
        MaxCharges = maxCharges;
        CurrentCharges = maxCharges;
        ChargesPerUse = chargesPerUse;
        DurationSeconds = durationSeconds;
        RemainingDuration = 0f;
        AutoInstillCondition = autoCondition;
        AutoInstillThreshold = autoThreshold;
    }

    /// <summary>
    /// Adds flask charges gained on monster kill.
    /// </summary>
    public void AddCharges(float amount)
    {
        CurrentCharges = MathF.Min(MaxCharges, CurrentCharges + MathF.Max(0f, amount));
    }

    /// <summary>
    /// Attempts to drink this flask, applying its effect and consuming charges.
    /// </summary>
    public bool TryDrink(PlayerState? player = null)
    {
        if (!CanUse)
            return false;

        CurrentCharges -= ChargesPerUse;
        RemainingDuration = DurationSeconds;

        if (Type == FlaskType.Life && player != null)
        {
            // Instantly restores 35% of max life
            player.HealLife(player.MaxLife * 0.35f);
        }

        OnFlaskUsed?.Invoke(this);
        return true;
    }

    /// <summary>
    /// Ticks down active duration and evaluates auto-instill trigger conditions.
    /// </summary>
    public void Tick(float deltaTime, PlayerState? player = null)
    {
        if (RemainingDuration > 0f)
        {
            RemainingDuration = MathF.Max(0f, RemainingDuration - deltaTime);
        }

        // Auto-instill evaluation
        if (!IsActive && CanUse && player != null)
        {
            switch (AutoInstillCondition)
            {
                case FlaskAutoInstillCondition.LifeBelowPercent:
                    if (player.LifePercent * 100f <= AutoInstillThreshold)
                    {
                        TryDrink(player);
                    }
                    break;

                case FlaskAutoInstillCondition.OnCooldownReady:
                    TryDrink(player);
                    break;
            }
        }
    }

    public void RefillCharges()
    {
        CurrentCharges = MaxCharges;
    }
}

/// <summary>
/// Complete player skill and utility loadout matching the GDD Skill Matrix:
/// - Slot 1: Primary DPS (Active Gem + up to 5 Supports)
/// - Slot 2: Secondary / Elite Cooldown
/// - Slot 3: Automated Trigger / Guard
/// - Slot 4 & 5: Auras / Reservoirs
/// - 5 Flask Slots with Auto-Instill
/// </summary>
public sealed class SkillLoadout
{
    public SkillSocketSetup PrimarySkill { get; } = new();
    public SkillSocketSetup SecondarySkill { get; } = new();
    public SkillSocketSetup TriggerSkill { get; } = new();
    public SkillSocketSetup Aura1 { get; } = new();
    public SkillSocketSetup Aura2 { get; } = new();

    public GuardSkillState GuardSkill { get; } = new();
    public IReadOnlyList<FlaskSlot> Flasks { get; }

    /// <summary>
    /// Returns the socket setup at the specified slot index:
    /// 0: Primary DPS, 1: Secondary, 2: Trigger, 3: Aura 1, 4: Aura 2.
    /// </summary>
    public SkillSocketSetup GetSetupAt(int index) => index switch
    {
        0 => PrimarySkill,
        1 => SecondarySkill,
        2 => TriggerSkill,
        3 => Aura1,
        4 => Aura2,
        _ => PrimarySkill
    };

    /// <summary>
    /// Returns the display label for the specified slot index.
    /// </summary>
    public string GetSlotName(int index) => index switch
    {
        0 => "Primary DPS",
        1 => "Secondary / Cooldown",
        2 => "Trigger / Guard",
        3 => "Aura Reservoir I",
        4 => "Aura Reservoir II",
        _ => "Skill Slot"
    };

    public SkillLoadout()
    {
        // 5 Standard PoE Flask slots: Life, Quicksilver, Granite, Diamond, Quartz
        Flasks = new List<FlaskSlot>
        {
            new(FlaskType.Life, "Divine Life Flask", 60f, 20f, 4.0f, FlaskAutoInstillCondition.LifeBelowPercent, 50f),
            new(FlaskType.Quicksilver, "Quicksilver Flask", 60f, 20f, 5.0f, FlaskAutoInstillCondition.OnCooldownReady),
            new(FlaskType.Granite, "Granite Flask", 60f, 30f, 4.0f, FlaskAutoInstillCondition.OnCooldownReady),
            new(FlaskType.Diamond, "Diamond Flask", 60f, 20f, 4.0f, FlaskAutoInstillCondition.OnCooldownReady),
            new(FlaskType.Quartz, "Quartz Flask", 60f, 30f, 4.0f, FlaskAutoInstillCondition.OnCooldownReady)
        };
    }

    /// <summary>
    /// Adds charges to all flasks upon defeating a monster.
    /// </summary>
    public void AddChargesOnKill(MonsterRarity rarity)
    {
        float charges = rarity switch
        {
            MonsterRarity.Normal => 1f,
            MonsterRarity.Magic => 3f,
            MonsterRarity.Rare => 6f,
            MonsterRarity.Boss => 15f,
            _ => 1f
        };

        foreach (var flask in Flasks)
        {
            flask.AddCharges(charges);
        }
    }

    /// <summary>
    /// Fully refills all flask charges (e.g. from Sulphite Vein harvest or town fountain).
    /// </summary>
    public void RefillAllFlaskCharges()
    {
        foreach (var flask in Flasks)
        {
            flask.AddCharges(flask.MaxCharges);
        }
    }

    /// <summary>
    /// Ticks guard skill and all flasks.
    /// </summary>
    public void Tick(float deltaTime, PlayerState? player = null)
    {
        GuardSkill.Tick(deltaTime);

        foreach (var flask in Flasks)
        {
            flask.Tick(deltaTime, player);
        }
    }
}
