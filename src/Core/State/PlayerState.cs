using System;
using System.Collections.Generic;
using IdleExile.Core.Models;
using IdleExile.Core.Simulation;

namespace IdleExile.Core.State;

/// <summary>
/// Mutable runtime state of the player character during combat.
/// Manages the Effective Health Pool (Life, Energy Shield, Mana) and
/// tracks combat-relevant timers (ES recharge delay, evasion entropy).
///
/// This class is Pure C# — no Godot.Node dependency.
/// UI subscribes to events emitted here to update health bars, death screens, etc.
/// </summary>
public sealed class PlayerState : ICombatEntity
{
    // ─────────────────────────────────────────────
    //  Maximum Resource Pools
    // ─────────────────────────────────────────────

    /// <summary>Maximum Life. If Chaos Inoculation is active, this is locked to 1.</summary>
    public float MaxLife { get; private set; }

    /// <summary>Maximum Energy Shield.</summary>
    public float MaxEnergyShield { get; private set; }

    /// <summary>Maximum Mana (total pool before reservation).</summary>
    public float MaxMana { get; private set; }

    /// <summary>
    /// Amount of mana permanently reserved by auras/buffs.
    /// Unreserved Mana = MaxMana - ReservedMana.
    /// </summary>
    public float ReservedMana { get; private set; }

    /// <summary>Effective maximum usable Mana (MaxMana - ReservedMana).</summary>
    public float UnreservedMaxMana => MathF.Max(0f, MaxMana - ReservedMana);

    // ─────────────────────────────────────────────
    //  Current Resource Pools
    // ─────────────────────────────────────────────

    /// <summary>Current Life. Character dies when this reaches 0.</summary>
    public float CurrentLife { get; private set; }

    /// <summary>Current Energy Shield. Acts as a buffer on top of Life.</summary>
    public float CurrentEnergyShield { get; private set; }

    /// <summary>Current Mana. Used for skills and absorbing damage via Mind over Matter.</summary>
    public float CurrentMana { get; private set; }

    // ─────────────────────────────────────────────
    //  Progression & Experience (PoE Standard)
    // ─────────────────────────────────────────────

    /// <summary>Character level (1 to 100).</summary>
    public int Level { get; private set; } = 1;

    /// <summary>Current experience accumulated towards the next level.</summary>
    public float CurrentExperience { get; private set; } = 0f;

    /// <summary>Total experience required to advance from the current level to the next.</summary>
    public float ExperienceToNextLevel => GetExperienceForLevel(Level);

    /// <summary>Current level progress as fraction [0.0, 1.0].</summary>
    public float ExperiencePercent => ExperienceToNextLevel > 0f
        ? Math.Clamp(CurrentExperience / ExperienceToNextLevel, 0f, 1f)
        : 0f;

    /// <summary>Calculates experience required to advance to the next level.</summary>
    public static float GetExperienceForLevel(int level)
    {
        int safeLevel = Math.Clamp(level, 1, 100);
        return 50f * MathF.Pow(safeLevel, 2.2f) + 50f * safeLevel;
    }

    /// <summary>
    /// Adds experience, handling multiple level-ups if applicable.
    /// Returns true if character leveled up at least once.
    /// </summary>
    public bool AddExperience(float amount)
    {
        if (amount <= 0f || Level >= 100)
            return false;

        CurrentExperience += amount;
        bool leveledUp = false;

        while (CurrentExperience >= ExperienceToNextLevel && Level < 100)
        {
            CurrentExperience -= ExperienceToNextLevel;
            Level++;
            leveledUp = true;
            OnLevelUp?.Invoke(Level);
        }

        if (Level >= 100)
        {
            CurrentExperience = 0f;
        }

        OnExperienceChanged?.Invoke(CurrentExperience, ExperienceToNextLevel);
        return leveledUp;
    }

    /// <summary>
    /// Deducts death penalty (-10% of current level requirement, standard PoE).
    /// Cannot de-level below 0% of the current level. Returns actual EXP deducted.
    /// </summary>
    public float DeductDeathPenalty(float percent = 10f)
    {
        if (CurrentExperience <= 0f)
            return 0f;

        float penalty = ExperienceToNextLevel * (percent / 100f);
        float deducted = MathF.Min(CurrentExperience, penalty);
        CurrentExperience = MathF.Max(0f, CurrentExperience - deducted);

        OnExperienceChanged?.Invoke(CurrentExperience, ExperienceToNextLevel);
        return deducted;
    }

    // ─────────────────────────────────────────────
    //  Evasion Entropy Counter (Deterministic Avoidance)
    // ─────────────────────────────────────────────

    /// <summary>
    /// Evasion entropy accumulator (0–99).
    /// Each attack adds the monster's accuracy chance; when it overflows 100, the hit lands.
    /// This eliminates streak-based "bad luck" in RNG-based evasion.
    /// </summary>
    public int EvasionEntropy { get; private set; } = 0;

    // ─────────────────────────────────────────────
    //  Energy Shield Recharge Timer
    // ─────────────────────────────────────────────

    /// <summary>
    /// Seconds remaining before Energy Shield begins recharging.
    /// Resets to DefensiveStats.EnergyShieldRechargeDelay whenever damage hits ES or Life.
    /// When this reaches 0 and CurrentES &lt; MaxES, recharge begins.
    /// </summary>
    public float EsRechargeTimer { get; private set; } = 0f;

    /// <summary>True when ES is actively recharging (timer expired, ES not full).</summary>
    public bool IsEsRecharging { get; private set; } = false;

    // ─────────────────────────────────────────────
    //  Alive State & ICombatEntity Identity
    // ─────────────────────────────────────────────

    /// <summary>Entity ID (0 for player).</summary>
    public long Id => 0;

    /// <summary>Display name of player character.</summary>
    public string Name => Archetype?.DisplayName ?? "Exile";

    /// <summary>2D coordinate (X, Y) within the arena (starts at left entrance X = 40, Y = 104).</summary>
    public Vector2D Position { get; set; } = new Vector2D(40f, 104f);

    /// <summary>Effective collision/hitbox radius in pixels.</summary>
    public float Radius => 10f;

    /// <summary>Active status ailments tracker (Ignite, Chill, Freeze, Shock, Bleed, Poison).</summary>
    public AilmentTracker Ailments { get; } = new();

    /// <summary>True while CurrentLife > 0.</summary>
    public bool IsAlive => CurrentLife > 0f;

    // ─────────────────────────────────────────────
    //  Events (subscribed by UI layer and CombatEngine)
    // ─────────────────────────────────────────────

    /// <summary>Fired whenever any resource pool value changes. Args: (life, es, mana).</summary>
    public event Action<float, float, float>? OnResourceChanged;

    /// <summary>Fired when the player dies (Life reaches 0).</summary>
    public event Action? OnPlayerDied;

    /// <summary>Fired when character levels up. Arg: newLevel.</summary>
    public event Action<int>? OnLevelUp;

    /// <summary>Fired when experience changes. Args: (currentExp, expToNextLevel).</summary>
    public event Action<float, float>? OnExperienceChanged;

    /// <summary>Fired when ES recharge starts.</summary>
    public event Action? OnEsRechargeStarted;

    /// <summary>Fired when ES recharge is interrupted by taking damage.</summary>
    public event Action? OnEsRechargeInterrupted;

    // ─────────────────────────────────────────────
    //  Initialization & Archetype Bootstrapping (Task 2.2)
    // ─────────────────────────────────────────────

    /// <summary>Raw base life before gear and attributes.</summary>
    public float BaseLife { get; set; } = 100f;
    /// <summary>Raw base ES before gear and attributes.</summary>
    public float BaseEnergyShield { get; set; } = 0f;
    /// <summary>Raw base mana before gear and attributes.</summary>
    public float BaseMana { get; set; } = 50f;

    /// <summary>Base Strength attribute (Juggernaut starts higher).</summary>
    public int BaseStrength { get; set; } = 30;
    /// <summary>Base Dexterity attribute (Deadeye starts higher).</summary>
    public int BaseDexterity { get; set; } = 15;
    /// <summary>Base Intelligence attribute (Elementalist starts higher).</summary>
    public int BaseIntelligence { get; set; } = 15;

    /// <summary>Total Strength (Base + Gear bonuses).</summary>
    public int TotalStrength => BaseStrength + Gear.GetTotalStrengthBonus();
    /// <summary>Total Dexterity (Base + Gear bonuses).</summary>
    public int TotalDexterity => BaseDexterity + Gear.GetTotalDexterityBonus();
    /// <summary>Total Intelligence (Base + Gear bonuses).</summary>
    public int TotalIntelligence => BaseIntelligence + Gear.GetTotalIntelligenceBonus();

    /// <summary>Total increased movement speed percentage from equipped gear.</summary>
    public float TotalMovementSpeedBonus => Gear.GetTotalMovementSpeedPercent();

    /// <summary>Chosen Starter Archetype, if any.</summary>
    public StarterArchetypeDefinition? Archetype { get; private set; }

    /// <summary>Base defensive stats configuration.</summary>
    public DefensiveStats Defenses { get; set; } = new();

    /// <summary>Equipped weapons, armour, and jewellery across 10 fixed slots.</summary>
    public EquippedGearState Gear { get; } = new();

    /// <summary>Currently equipped main-hand weapon, if any.</summary>
    public WeaponItemInstance? MainHand => Gear.MainHand;

    /// <summary>Currently equipped off-hand weapon, if any (null if shield/quiver or empty).</summary>
    public WeaponItemInstance? OffHand => Gear.OffHand as WeaponItemInstance;

    /// <summary>Currently equipped off-hand item (weapon, shield, or quiver).</summary>
    public IGearItem? OffHandItem => Gear.OffHand;

    /// <summary>True when dual-wielding two one-handed weapons.</summary>
    public bool IsDualWielding => Gear.IsDualWielding;

    /// <summary>Current items in player bag.</summary>
    public List<string> Inventory { get; } = new();

    /// <summary>Current currency stacks (e.g. ["scroll_of_wisdom"] = 3).</summary>
    public Dictionary<string, int> Currencies { get; } = new();

    /// <summary>Map slates available in the player's possession.</summary>
    public List<string> MapInventory { get; } = new();

    /// <summary>Fired when player state is bootstrapped from an archetype.</summary>
    public event Action<StarterArchetypeDefinition>? OnBootstrapped;

    public PlayerState()
    {
        Gear.OnEquipmentChanged += RecalculateStatsFromGear;
        RecalculateStatsFromGear();
        RestoreAllPools();
    }

    private static readonly Dictionary<string, WeaponBaseDefinition> FallbackStarterBases = new()
    {
        ["rusted_sword"] = new WeaponBaseDefinition("rusted_sword", "Rusted Sword", WeaponClass.OneHandedSword, HandSlotRequirement.OneHanded, 4, 9, 1.45f, 5.0f, ReqLevel: 1, ReqStr: 8, ReqDex: 8, ImplicitAffixId: "sword_implicit_accuracy", ImplicitValue: 18),
        ["driftwood_wand"] = new WeaponBaseDefinition("driftwood_wand", "Driftwood Wand", WeaponClass.Wand, HandSlotRequirement.OneHanded, 4, 7, 1.40f, 7.0f, ReqLevel: 1, ReqInt: 14, ImplicitAffixId: "wand_implicit_spell_damage", ImplicitValue: 10),
        ["crude_bow"] = new WeaponBaseDefinition("crude_bow", "Crude Bow", WeaponClass.Bow, HandSlotRequirement.TwoHanded, 5, 13, 1.40f, 5.0f, ReqLevel: 1, ReqDex: 14)
    };

    /// <summary>
    /// Bootstraps the player state from a chosen StarterArchetypeDefinition:
    /// sets base vitals, equips starting weapons, initial inventory, starter currency, and 3x Tier 1 Map slates.
    /// </summary>
    public void BootstrapFromArchetype(StarterArchetypeDefinition archetype, IReadOnlyDictionary<string, WeaponBaseDefinition>? weaponBases = null)
    {
        Archetype = archetype ?? throw new ArgumentNullException(nameof(archetype));
        Defenses = archetype.BaseDefenses ?? new DefensiveStats();
        BaseLife = archetype.BaseMaxLife;
        BaseEnergyShield = archetype.BaseMaxEnergyShield;
        BaseMana = archetype.BaseMaxMana;

        switch (archetype.PrimaryAttribute)
        {
            case "Strength":
                BaseStrength = 32; BaseDexterity = 14; BaseIntelligence = 14;
                break;
            case "Dexterity":
                BaseStrength = 14; BaseDexterity = 32; BaseIntelligence = 14;
                break;
            case "Intelligence":
                BaseStrength = 14; BaseDexterity = 14; BaseIntelligence = 32;
                break;
            default:
                BaseStrength = 20; BaseDexterity = 20; BaseIntelligence = 20;
                break;
        }

        Initialize(archetype.BaseMaxLife, archetype.BaseMaxEnergyShield, archetype.BaseMaxMana);

        // Equip starting weapon in Main Hand
        Gear.UnequipMainHand();
        Gear.UnequipOffHand();

        if (!string.IsNullOrEmpty(archetype.StartingMainHandWeaponId))
        {
            WeaponBaseDefinition? mainDef = null;
            if (weaponBases?.TryGetValue(archetype.StartingMainHandWeaponId, out var foundDef) == true)
            {
                mainDef = foundDef;
            }
            else
            {
                FallbackStarterBases.TryGetValue(archetype.StartingMainHandWeaponId, out mainDef);
            }

            if (mainDef != null)
            {
                Gear.EquipMainHand(new WeaponItemInstance(mainDef));
            }
        }

        // Equip starting weapon / offhand in Off Hand
        if (!string.IsNullOrEmpty(archetype.StartingOffHandWeaponId))
        {
            WeaponBaseDefinition? offDef = null;
            if (weaponBases?.TryGetValue(archetype.StartingOffHandWeaponId, out var foundOffDef) == true)
            {
                offDef = foundOffDef;
            }
            else
            {
                FallbackStarterBases.TryGetValue(archetype.StartingOffHandWeaponId, out offDef);
            }

            if (offDef != null)
            {
                Gear.EquipOffHand(new WeaponItemInstance(offDef));
            }
        }

        RecalculateStatsFromGear();

        Inventory.Clear();
        Inventory.AddRange(archetype.InitialInventory);

        Currencies.Clear();
        foreach (var kvp in archetype.InitialCurrencies)
        {
            Currencies[kvp.Key] = kvp.Value;
        }

        MapInventory.Clear();
        MapInventory.AddRange(archetype.InitialMaps);

        OnBootstrapped?.Invoke(archetype);
    }

    /// <summary>
    /// Recalculates all defensive and vital stats incorporating 10 gear slots and Soft Attributes:
    /// - 10 Str: +5 Life, +2% Increased Armour
    /// - 10 Dex: +20 Accuracy, +2% Increased Evasion
    /// - 10 Int: +5 Mana, +2% Increased Energy Shield
    /// </summary>
    public void RecalculateStatsFromGear()
    {
        float strBonusLife = (TotalStrength / 10f) * 5f;
        float intBonusMana = (TotalIntelligence / 10f) * 5f;

        float strIncArmour = (TotalStrength / 10f) * 2f;
        float dexIncEva = (TotalDexterity / 10f) * 2f;
        float intIncEs = (TotalIntelligence / 10f) * 2f;

        float newMaxLife = BaseLife + Gear.GetTotalFlatLife() + strBonusLife;
        float flatEs = BaseEnergyShield + Gear.GetTotalFlatEnergyShield();
        float newMaxEs = flatEs * (1f + intIncEs / 100f);
        float newMaxMana = BaseMana + Gear.GetTotalFlatMana() + intBonusMana;

        MaxLife = MathF.Max(1f, newMaxLife);
        MaxEnergyShield = MathF.Max(0f, newMaxEs);
        MaxMana = MathF.Max(0f, newMaxMana);

        CurrentLife = Math.Clamp(CurrentLife, 0f, MaxLife);
        CurrentEnergyShield = Math.Clamp(CurrentEnergyShield, 0f, MaxEnergyShield);
        CurrentMana = Math.Clamp(CurrentMana, 0f, UnreservedMaxMana);

        float baseArmour = Archetype?.BaseDefenses.Armour ?? 0f;
        float baseEva = Archetype?.BaseDefenses.EvasionChance ?? 0f;

        float totalArmour = (baseArmour + Gear.GetTotalArmour()) * (1f + strIncArmour / 100f);
        float totalEva = (baseEva + Gear.GetTotalEvasion()) * (1f + dexIncEva / 100f);

        // Life and Mana Regeneration:
        // Base from Archetype + PoE inherent 1.75% of Max Mana per second
        float baseLifeRegen = Archetype?.BaseDefenses.LifeRegenPerSecond ?? Defenses.LifeRegenPerSecond;
        float baseManaRegen = Archetype?.BaseDefenses.ManaRegenPerSecond ?? Defenses.ManaRegenPerSecond;
        float poePercentManaRegen = Archetype != null ? MaxMana * 0.0175f : 0f;
        float totalManaRegen = baseManaRegen + poePercentManaRegen;

        Defenses = Defenses with
        {
            Armour = totalArmour,
            EvasionChance = totalEva,
            AttackBlockChance = (Archetype?.BaseDefenses.AttackBlockChance ?? 0f) + Gear.GetTotalBlockChance(),
            FireResistance = (Archetype?.BaseDefenses.FireResistance ?? 0f) + Gear.GetTotalFireRes(),
            ColdResistance = (Archetype?.BaseDefenses.ColdResistance ?? 0f) + Gear.GetTotalColdRes(),
            LightningResistance = (Archetype?.BaseDefenses.LightningResistance ?? 0f) + Gear.GetTotalLightningRes(),
            ChaosResistance = (Archetype?.BaseDefenses.ChaosResistance ?? 0f) + Gear.GetTotalChaosRes(),
            LifeRegenPerSecond = baseLifeRegen,
            ManaRegenPerSecond = totalManaRegen
        };

        NotifyResourceChanged();
    }

    /// <summary>
    /// Initializes or resets the player state with specified maximum values.
    /// All pools are filled to max. Entropy resets to 0.
    /// </summary>
    public void Initialize(float maxLife, float maxEnergyShield, float maxMana, float reservedMana = 0f)
    {
        MaxLife = MathF.Max(1f, maxLife);
        MaxEnergyShield = MathF.Max(0f, maxEnergyShield);
        MaxMana = MathF.Max(0f, maxMana);
        ReservedMana = Math.Clamp(reservedMana, 0f, MaxMana);

        CurrentLife = MaxLife;
        CurrentEnergyShield = MaxEnergyShield;
        CurrentMana = UnreservedMaxMana;

        EvasionEntropy = 0;
        EsRechargeTimer = 0f;
        IsEsRecharging = false;
    }

    /// <summary>
    /// Refills all resource pools (Life, ES, Mana) to maximum and resets evasion entropy.
    /// Used on respawn or town return.
    /// </summary>
    public void RestoreAllPools()
    {
        CurrentLife = MaxLife;
        CurrentEnergyShield = MaxEnergyShield;
        CurrentMana = UnreservedMaxMana;
        EvasionEntropy = 0;
        EsRechargeTimer = 0f;
        IsEsRecharging = false;
        NotifyResourceChanged();
    }

    // ─────────────────────────────────────────────
    //  Damage Application (called by DefensiveLayers after mitigation)
    // ─────────────────────────────────────────────

    /// <summary>
    /// Applies mitigated damage to the player's EHP layers in PoE order:
    /// 1. Guard Skill absorption (future — placeholder for Guard buffer)
    /// 2. Energy Shield (unless Chaos bypass)
    /// 3. Mind over Matter → Mana
    /// 4. Life
    ///
    /// Returns the total damage actually absorbed.
    /// </summary>
    /// <param name="damage">Already-mitigated damage amount.</param>
    /// <param name="isChaos">True if the damage type is Chaos (bypasses ES by default).</param>
    /// <param name="chaosDoesNotBypassEs">True if the character has the "Chaos does not bypass ES" modifier.</param>
    /// <param name="mindOverMatterPercent">Percentage of damage absorbed by Mana before Life (0–100).</param>
    public float ApplyDamage(float damage, bool isChaos, bool chaosDoesNotBypassEs, float mindOverMatterPercent)
    {
        if (damage <= 0f || !IsAlive)
            return 0f;

        float remaining = damage;

        // Interrupt ES recharge on any damage
        InterruptEsRecharge();

        // Step 1: Energy Shield absorption
        // Chaos damage bypasses ES unless character has the specific modifier
        bool hitEs = !isChaos || chaosDoesNotBypassEs;
        if (hitEs && CurrentEnergyShield > 0f)
        {
            float esAbsorbed = MathF.Min(remaining, CurrentEnergyShield);
            CurrentEnergyShield -= esAbsorbed;
            remaining -= esAbsorbed;
        }

        if (remaining <= 0f)
        {
            NotifyResourceChanged();
            return damage;
        }

        // Step 2: Mind over Matter — redirect a portion to Mana
        float momPercent = Math.Clamp(mindOverMatterPercent, 0f, 100f);
        if (momPercent > 0f && CurrentMana > 0f)
        {
            float manaShare = remaining * (momPercent / 100f);
            float manaAbsorbed = MathF.Min(manaShare, CurrentMana);
            CurrentMana -= manaAbsorbed;
            remaining -= manaAbsorbed;
        }

        // Step 3: Life
        if (remaining > 0f)
        {
            CurrentLife -= remaining;
            if (CurrentLife <= 0f)
            {
                CurrentLife = 0f;
                NotifyResourceChanged();
                OnPlayerDied?.Invoke();
                return damage;
            }
        }

        NotifyResourceChanged();
        return damage;
    }

    // ─────────────────────────────────────────────
    //  Evasion Entropy System
    // ─────────────────────────────────────────────

    /// <summary>
    /// Evaluates whether an incoming attack lands using the deterministic entropy counter.
    /// Each call adds the monster's effective chance-to-hit to the accumulator.
    /// When the accumulator reaches or exceeds 100, the attack lands and 100 is subtracted.
    ///
    /// Returns true if the attack HITS, false if the player EVADES.
    /// </summary>
    /// <param name="evasionChance">Player's evasion chance (0–100%).</param>
    public bool DoesAttackHit(float evasionChance)
    {
        // Chance to hit = 100 - EvasionChance, clamped [5, 100]
        // (In PoE, there's always at least a 5% chance to hit / 95% evasion cap)
        float chanceToHit = Math.Clamp(100f - evasionChance, 5f, 100f);

        EvasionEntropy += (int)chanceToHit;
        if (EvasionEntropy >= 100)
        {
            EvasionEntropy -= 100;
            return true; // Attack lands
        }
        return false; // Evaded
    }

    // ─────────────────────────────────────────────
    //  Energy Shield Recharge Logic
    // ─────────────────────────────────────────────

    /// <summary>
    /// Advances ES recharge timer by the given delta time (in seconds).
    /// Called each tick by the combat engine.
    /// </summary>
    /// <param name="deltaTime">Time elapsed since last tick, in seconds.</param>
    /// <param name="rechargeDelay">Delay before recharge starts (from DefensiveStats).</param>
    /// <param name="rechargeRatePercent">% of MaxES recharged per second.</param>
    public void UpdateEsRecharge(float deltaTime, float rechargeDelay, float rechargeRatePercent)
    {
        if (CurrentEnergyShield >= MaxEnergyShield || MaxEnergyShield <= 0f)
            return;

        if (!IsEsRecharging)
        {
            // Count down the delay timer
            EsRechargeTimer += deltaTime;
            if (EsRechargeTimer >= rechargeDelay)
            {
                IsEsRecharging = true;
                OnEsRechargeStarted?.Invoke();
            }
            else
            {
                return;
            }
        }

        // Recharge ES
        float rechargePerSecond = MaxEnergyShield * (rechargeRatePercent / 100f);
        CurrentEnergyShield += rechargePerSecond * deltaTime;
        if (CurrentEnergyShield >= MaxEnergyShield)
        {
            CurrentEnergyShield = MaxEnergyShield;
            IsEsRecharging = false;
        }
        NotifyResourceChanged();
    }

    /// <summary>
    /// Interrupts ES recharge: resets the delay timer and stops active recharging.
    /// Called whenever the player takes damage.
    /// </summary>
    public void InterruptEsRecharge()
    {
        if (IsEsRecharging)
        {
            OnEsRechargeInterrupted?.Invoke();
        }
        EsRechargeTimer = 0f;
        IsEsRecharging = false;
    }

    // ─────────────────────────────────────────────
    //  Regeneration (called each tick)
    // ─────────────────────────────────────────────

    /// <summary>
    /// Applies per-tick life and mana regeneration.
    /// </summary>
    /// <param name="deltaTime">Time elapsed since last tick, in seconds.</param>
    /// <param name="lifeRegenPerSecond">Flat life regen per second.</param>
    /// <param name="manaRegenPerSecond">Flat mana regen per second.</param>
    public void ApplyRegeneration(float deltaTime, float lifeRegenPerSecond, float manaRegenPerSecond)
    {
        if (!IsAlive)
            return;

        bool changed = false;

        if (lifeRegenPerSecond != 0f && CurrentLife < MaxLife)
        {
            CurrentLife = MathF.Min(MaxLife, CurrentLife + lifeRegenPerSecond * deltaTime);
            changed = true;
        }

        float manaMax = UnreservedMaxMana;
        if (manaRegenPerSecond != 0f && CurrentMana < manaMax)
        {
            CurrentMana = MathF.Min(manaMax, CurrentMana + manaRegenPerSecond * deltaTime);
            changed = true;
        }

        if (changed)
            NotifyResourceChanged();
    }

    // ─────────────────────────────────────────────
    //  Healing & Resource Restoration
    // ─────────────────────────────────────────────

    /// <summary>Instantly heals life up to MaxLife. Returns amount actually healed.</summary>
    public float HealLife(float amount)
    {
        if (amount <= 0f || !IsAlive) return 0f;
        float before = CurrentLife;
        CurrentLife = MathF.Min(MaxLife, CurrentLife + amount);
        float healed = CurrentLife - before;
        if (healed > 0f)
            NotifyResourceChanged();
        return healed;
    }

    /// <summary>Instantly restores mana up to UnreservedMaxMana. Returns amount restored.</summary>
    public float RestoreMana(float amount)
    {
        if (amount <= 0f) return 0f;
        float manaMax = UnreservedMaxMana;
        float before = CurrentMana;
        CurrentMana = MathF.Min(manaMax, CurrentMana + amount);
        float restored = CurrentMana - before;
        if (restored > 0f)
            NotifyResourceChanged();
        return restored;
    }

    /// <summary>
    /// Restores a flat amount of mana on hitting an enemy (PoE Mana Gain on Hit).
    /// Sustains active skill casting during continuous combat.
    /// </summary>
    public float RestoreManaOnHit(float amount = 2.0f)
    {
        return RestoreMana(amount);
    }

    /// <summary>
    /// Attempts to deduct the specified mana cost.
    /// Returns true if successful (or cost is 0), false if insufficient mana.
    /// </summary>
    public bool TrySpendMana(float cost)
    {
        if (cost <= 0f)
            return true;

        if (CurrentMana < cost)
            return false;

        CurrentMana -= cost;
        NotifyResourceChanged();
        return true;
    }

    /// <summary>Instantly restores ES up to MaxEnergyShield. Returns amount restored.</summary>
    public float RestoreEnergyShield(float amount)
    {
        if (amount <= 0f) return 0f;
        float before = CurrentEnergyShield;
        CurrentEnergyShield = MathF.Min(MaxEnergyShield, CurrentEnergyShield + amount);
        float restored = CurrentEnergyShield - before;
        if (restored > 0f)
            NotifyResourceChanged();
        return restored;
    }

    // ─────────────────────────────────────────────
    //  Resource Queries (for UI / Tooltips)
    // ─────────────────────────────────────────────

    /// <summary>Returns current Life as a fraction (0.0 to 1.0).</summary>
    public float LifePercent => MaxLife > 0f ? CurrentLife / MaxLife : 0f;

    /// <summary>Returns current ES as a fraction (0.0 to 1.0).</summary>
    public float EnergyShieldPercent => MaxEnergyShield > 0f ? CurrentEnergyShield / MaxEnergyShield : 0f;

    /// <summary>Returns current Mana as a fraction of unreserved max (0.0 to 1.0).</summary>
    public float ManaPercent => UnreservedMaxMana > 0f ? CurrentMana / UnreservedMaxMana : 0f;

    /// <summary>
    /// Total Effective Health Pool = Current Life + Current ES + Current Mana (MoM portion).
    /// Simplified metric for survival estimation.
    /// </summary>
    public float EffectiveHealthPool(float mindOverMatterPercent)
    {
        float momEffective = mindOverMatterPercent > 0f
            ? CurrentMana / (mindOverMatterPercent / 100f)
            : 0f;
        return CurrentLife + CurrentEnergyShield + MathF.Min(momEffective, CurrentLife);
    }

    // ─────────────────────────────────────────────
    //  Internal
    // ─────────────────────────────────────────────

    private void NotifyResourceChanged()
    {
        OnResourceChanged?.Invoke(CurrentLife, CurrentEnergyShield, CurrentMana);
    }
}
