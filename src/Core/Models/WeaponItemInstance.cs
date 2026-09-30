using System;
using System.Collections.Generic;

namespace IdleExile.Core.Models;

/// <summary>
/// Runtime instance of an equipped or inventoried weapon item.
/// Caches precomputed local stats (min/max damage, APS, crit chance, DPS)
/// to ensure 0 GC allocations and $O(1)$ performance during the 30Hz combat simulation.
/// Pure C# model — zero Godot dependencies.
/// </summary>
public sealed class WeaponItemInstance : IGearItem
{
    public string InstanceId { get; init; } = Guid.NewGuid().ToString("N");
    public WeaponBaseDefinition BaseDefinition { get; }
    public string CustomName { get; set; } = string.Empty;
    public string Name => string.IsNullOrWhiteSpace(CustomName) ? BaseDefinition.Name : CustomName;
    public string BaseName => BaseDefinition.Name;

    public GearSlot TargetSlot => (BaseDefinition.WeaponClass == WeaponClass.Shield || BaseDefinition.WeaponClass == WeaponClass.Quiver)
        ? GearSlot.OffHand
        : GearSlot.MainHand;

    public string Rarity { get; set; } = "Normal";
    public int ItemLevel { get; set; } = 1;
    public int Quality { get; set; } = 0; // 0 to 20%
    public int ReqLevel => BaseDefinition.ReqLevel;
    public bool IsLocked { get; set; } = false;
    public List<AffixDefinition> Affixes { get; } = new();

    public string ImplicitDescription => WeaponBaseDefinition.FormatImplicit(BaseDefinition.ImplicitAffixId, BaseDefinition.ImplicitValue);
    public string FlavorDescription => BaseDefinition.Description;
    public string WeaponClassDisplayName => BaseDefinition.WeaponClass.GetDisplayName();
    public string HandRequirementDisplayName => BaseDefinition.HandRequirement.GetDisplayName();
    public string WeaponIcon => BaseDefinition.WeaponClass.GetIcon();
    public bool IsTwoHanded => BaseDefinition.HandRequirement == HandSlotRequirement.TwoHanded;
    public bool IsOneHanded => BaseDefinition.HandRequirement == HandSlotRequirement.OneHanded;
    public bool IsRanged => BaseDefinition.WeaponClass == WeaponClass.Bow;

    // ─────────────────────────────────────────────
    //  Precomputed Local Stats (Cached on Craft/Equip)
    // ─────────────────────────────────────────────

    /// <summary>Minimum physical damage per hit after local modifiers.</summary>
    public float ComputedMinDamage { get; private set; }

    /// <summary>Maximum physical damage per hit after local modifiers.</summary>
    public float ComputedMaxDamage { get; private set; }

    /// <summary>Attacks per second after local attack speed modifiers.</summary>
    public float ComputedAps { get; private set; }

    /// <summary>Base critical strike chance percentage after local crit modifiers.</summary>
    public float ComputedCritChance { get; private set; }

    /// <summary>Physical Damage Per Second (pDPS).</summary>
    public float PhysicalDps => ((ComputedMinDamage + ComputedMaxDamage) / 2f) * ComputedAps;

    /// <summary>Total Damage Per Second (Total DPS).</summary>
    public float TotalDps => PhysicalDps;

    // ─────────────────────────────────────────────
    //  IGearItem Implementation Members
    // ─────────────────────────────────────────────

    public float LocalArmour => BaseDefinition.BaseArmour;
    public float LocalEvasion => BaseDefinition.BaseEvasion;
    public float LocalEnergyShield => BaseDefinition.BaseEnergyShield;
    public float LocalBlockChance => BaseDefinition.BaseBlockChance;

    public float FlatLifeBonus { get; private set; }
    public float FlatEnergyShieldBonus { get; private set; }
    public float FlatManaBonus { get; private set; }
    public float FireResistanceBonus { get; private set; }
    public float ColdResistanceBonus { get; private set; }
    public float LightningResistanceBonus { get; private set; }
    public float ChaosResistanceBonus { get; private set; }

    public int StrengthBonus { get; private set; }
    public int DexterityBonus { get; private set; }
    public int IntelligenceBonus { get; private set; }

    public float LocalMinDamage => ComputedMinDamage;
    public float LocalMaxDamage => ComputedMaxDamage;
    public float LocalAps => ComputedAps;
    public float LocalCritChance => ComputedCritChance;
    public float LocalDps => TotalDps;
    public float GlobalIncreasedDamagePercent { get; private set; }
    public float GlobalAttackSpeedPercent { get; private set; }

    public WeaponItemInstance(WeaponBaseDefinition baseDefinition, string rarity = "Normal", int quality = 0, int itemLevel = 1)
    {
        BaseDefinition = baseDefinition ?? throw new ArgumentNullException(nameof(baseDefinition));
        Rarity = rarity;
        Quality = Math.Clamp(quality, 0, 20);
        ItemLevel = Math.Max(1, itemLevel);
        RecalculateLocalStats();
    }

    /// <summary>
    /// Recalculates all local cached weapon stats.
    /// Follows Path of Exile's local weapon formula:
    /// Damage = (BaseFlat + LocalFlatAdded) * (1 + (Quality + Local%Phys) / 100)
    /// APS = BaseAps * (1 + Local%AttackSpeed / 100)
    /// Crit = BaseCrit * (1 + Local%CritChance / 100)
    /// </summary>
    public void RecalculateLocalStats()
    {
        float minPhys = BaseDefinition.BaseMinPhys;
        float maxPhys = BaseDefinition.BaseMaxPhys;
        float aps = BaseDefinition.BaseAps;
        float crit = BaseDefinition.BaseCritChance;

        float localIncPhys = Quality; // Quality acts as local increased physical damage
        float localFlatPhysMin = 0f;
        float localFlatPhysMax = 0f;
        float localIncAps = 0f;
        float localIncCrit = 0f;

        float life = 0f;
        float mana = 0f;
        float fireRes = 0f;
        float coldRes = 0f;
        float lightRes = 0f;
        float chaosRes = 0f;
        float globalIncDmg = 0f;
        float globalIncAps = 0f;

        // Process affixes if any
        foreach (var affix in Affixes)
        {
            if (affix.Tags == null) continue;

            float avgVal = (affix.MinValue + affix.MaxValue) / 2f;
            bool isLocal = Array.Exists(affix.Tags, t => string.Equals(t, "local", StringComparison.OrdinalIgnoreCase));

            if (isLocal)
            {
                if (Array.Exists(affix.Tags, t => string.Equals(t, "physical", StringComparison.OrdinalIgnoreCase)))
                {
                    if (Array.Exists(affix.Tags, t => string.Equals(t, "damage", StringComparison.OrdinalIgnoreCase)))
                    {
                        localIncPhys += avgVal;
                    }
                }
                else if (Array.Exists(affix.Tags, t => string.Equals(t, "speed", StringComparison.OrdinalIgnoreCase)))
                {
                    localIncAps += avgVal;
                }
                else if (Array.Exists(affix.Tags, t => string.Equals(t, "critical", StringComparison.OrdinalIgnoreCase)))
                {
                    localIncCrit += avgVal;
                }
            }
            else
            {
                // Global stats on weapons
                if (Array.Exists(affix.Tags, t => string.Equals(t, "life", StringComparison.OrdinalIgnoreCase)))
                {
                    life += avgVal;
                }
                else if (Array.Exists(affix.Tags, t => string.Equals(t, "mana", StringComparison.OrdinalIgnoreCase)))
                {
                    mana += avgVal;
                }
                else if (Array.Exists(affix.Tags, t => string.Equals(t, "fire", StringComparison.OrdinalIgnoreCase)))
                {
                    fireRes += avgVal;
                }
                else if (Array.Exists(affix.Tags, t => string.Equals(t, "cold", StringComparison.OrdinalIgnoreCase)))
                {
                    coldRes += avgVal;
                }
                else if (Array.Exists(affix.Tags, t => string.Equals(t, "lightning", StringComparison.OrdinalIgnoreCase)))
                {
                    lightRes += avgVal;
                }
                else if (Array.Exists(affix.Tags, t => string.Equals(t, "chaos", StringComparison.OrdinalIgnoreCase)))
                {
                    chaosRes += avgVal;
                }
                else if (Array.Exists(affix.Tags, t => string.Equals(t, "damage", StringComparison.OrdinalIgnoreCase)))
                {
                    globalIncDmg += avgVal;
                }
                else if (Array.Exists(affix.Tags, t => string.Equals(t, "speed", StringComparison.OrdinalIgnoreCase)))
                {
                    globalIncAps += avgVal;
                }
            }
        }

        ComputedMinDamage = MathF.Round((minPhys + localFlatPhysMin) * (1f + localIncPhys / 100f), 1);
        ComputedMaxDamage = MathF.Round((maxPhys + localFlatPhysMax) * (1f + localIncPhys / 100f), 1);
        ComputedAps = MathF.Round(aps * (1f + localIncAps / 100f), 2);
        ComputedCritChance = MathF.Round(crit * (1f + localIncCrit / 100f), 2);

        FlatLifeBonus = MathF.Round(life, 0);
        FlatEnergyShieldBonus = 0f;
        FlatManaBonus = MathF.Round(mana, 0);
        FireResistanceBonus = MathF.Round(fireRes, 0);
        ColdResistanceBonus = MathF.Round(coldRes, 0);
        LightningResistanceBonus = MathF.Round(lightRes, 0);
        ChaosResistanceBonus = MathF.Round(chaosRes, 0);

        GlobalIncreasedDamagePercent = globalIncDmg;
        GlobalAttackSpeedPercent = globalIncAps;
    }
}
