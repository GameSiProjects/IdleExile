using System;
using System.Collections.Generic;

namespace IdleExile.Core.Models;

/// <summary>
/// Runtime instance of an equipped or inventoried armour item (Helmet, Body, Gloves, Boots, Shield).
/// Computes local defensive values (Armour, Evasion, ES) and aggregates affixes with 0 GC allocations.
/// Implements IGearItem for universal UI display, smart tooltip comparison, and stash routing.
/// Pure C# model — zero Godot dependencies.
/// </summary>
public sealed class ArmourItemInstance : IGearItem
{
    public string InstanceId { get; init; } = Guid.NewGuid().ToString("N");
    public ArmourBaseDefinition BaseDefinition { get; }
    public string CustomName { get; set; } = string.Empty;
    public string Name => string.IsNullOrWhiteSpace(CustomName) ? BaseDefinition.Name : CustomName;
    public string BaseName => BaseDefinition.Name;

    public GearSlot TargetSlot => BaseDefinition.ArmourSlot switch
    {
        ArmourSlot.Helmet => GearSlot.Helmet,
        ArmourSlot.BodyArmour => GearSlot.BodyArmour,
        ArmourSlot.Gloves => GearSlot.Gloves,
        ArmourSlot.Boots => GearSlot.Boots,
        ArmourSlot.Shield => GearSlot.OffHand,
        _ => GearSlot.BodyArmour
    };

    public string Rarity { get; set; } = "Normal";
    public int ItemLevel { get; set; } = 1;
    public int Quality { get; set; } = 0; // 0 to 20%
    public int ReqLevel => BaseDefinition.ReqLevel;
    public bool IsLocked { get; set; } = false;
    public List<AffixDefinition> Affixes { get; } = new();

    public string ImplicitDescription => !string.IsNullOrEmpty(BaseDefinition.ImplicitDescription)
        ? BaseDefinition.ImplicitDescription
        : string.Empty;

    // ─────────────────────────────────────────────
    //  Precomputed Local & Global Stats
    // ─────────────────────────────────────────────

    public float LocalArmour { get; private set; }
    public float LocalEvasion { get; private set; }
    public float LocalEnergyShield { get; private set; }
    public float LocalBlockChance { get; private set; }

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

    // Armour items generally do not have local weapon damage
    public float LocalMinDamage => 0f;
    public float LocalMaxDamage => 0f;
    public float LocalAps => 0f;
    public float LocalCritChance => 0f;
    public float LocalDps => 0f;
    public float GlobalIncreasedDamagePercent { get; private set; }
    public float GlobalAttackSpeedPercent { get; private set; }
    public float MovementSpeedBonus { get; private set; }

    public ArmourItemInstance(ArmourBaseDefinition baseDefinition, string rarity = "Normal", int quality = 0, int itemLevel = 1)
    {
        BaseDefinition = baseDefinition ?? throw new ArgumentNullException(nameof(baseDefinition));
        Rarity = rarity;
        Quality = Math.Clamp(quality, 0, 20);
        ItemLevel = Math.Max(1, itemLevel);
        RecalculateStats();
    }

    /// <summary>
    /// Recalculates all local and global modifiers granted by this armour piece.
    /// Follows PoE local formula:
    /// Local Defense = (BaseFlat + LocalFlatAdded) * (1 + (Quality + Local%IncDefense) / 100)
    /// </summary>
    public void RecalculateStats()
    {
        float baseArmour = BaseDefinition.BaseArmour;
        float baseEva = BaseDefinition.BaseEvasion;
        float baseEs = BaseDefinition.BaseEnergyShield;
        float baseBlock = BaseDefinition.BaseBlockChance;

        float localFlatArmour = 0f;
        float localFlatEva = 0f;
        float localFlatEs = 0f;

        // Quality acts as local increased defense for all present defensive stats
        float localIncArmour = Quality;
        float localIncEva = Quality;
        float localIncEs = Quality;

        float life = 0f;
        float mana = 0f;
        float fireRes = 0f;
        float coldRes = 0f;
        float lightRes = 0f;
        float chaosRes = 0f;
        int str = 0;
        int dex = 0;
        int intel = 0;
        float incDmg = 0f;
        float incSpeed = 0f;
        float moveSpeed = 0f;

        // Handle implicit modifiers from base definition
        if (!string.IsNullOrEmpty(BaseDefinition.ImplicitDescription))
        {
            if (BaseDefinition.ImplicitDescription.Contains("all Elemental Resistances", StringComparison.OrdinalIgnoreCase))
            {
                fireRes += BaseDefinition.ImplicitValue;
                coldRes += BaseDefinition.ImplicitValue;
                lightRes += BaseDefinition.ImplicitValue;
            }
        }

        // Process explicit affixes
        foreach (var affix in Affixes)
        {
            if (affix.Tags == null) continue;

            float avgVal = (affix.MinValue + affix.MaxValue) / 2f;
            bool isLocal = Array.Exists(affix.Tags, t => string.Equals(t, "local", StringComparison.OrdinalIgnoreCase));

            if (isLocal)
            {
                if (Array.Exists(affix.Tags, t => string.Equals(t, "armour", StringComparison.OrdinalIgnoreCase)))
                {
                    localIncArmour += avgVal;
                }
                else if (Array.Exists(affix.Tags, t => string.Equals(t, "evasion", StringComparison.OrdinalIgnoreCase)))
                {
                    localIncEva += avgVal;
                }
                else if (Array.Exists(affix.Tags, t => string.Equals(t, "energy_shield", StringComparison.OrdinalIgnoreCase)))
                {
                    localIncEs += avgVal;
                }
            }
            else
            {
                // Global stats
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
                else if (Array.Exists(affix.Tags, t => string.Equals(t, "movement_speed", StringComparison.OrdinalIgnoreCase)))
                {
                    moveSpeed += avgVal;
                }
                else if (Array.Exists(affix.Tags, t => string.Equals(t, "speed", StringComparison.OrdinalIgnoreCase)))
                {
                    incSpeed += avgVal;
                }
                else if (Array.Exists(affix.Tags, t => string.Equals(t, "damage", StringComparison.OrdinalIgnoreCase)))
                {
                    incDmg += avgVal;
                }
            }
        }

        // Implicit movement speed (e.g. boots +3% Movement Speed)
        if (string.Equals(BaseDefinition.ImplicitAffixId, "movement_speed", StringComparison.OrdinalIgnoreCase))
        {
            moveSpeed += 3f;
        }

        LocalArmour = MathF.Round((baseArmour + localFlatArmour) * (1f + localIncArmour / 100f), 0);
        LocalEvasion = MathF.Round((baseEva + localFlatEva) * (1f + localIncEva / 100f), 0);
        LocalEnergyShield = MathF.Round((baseEs + localFlatEs) * (1f + localIncEs / 100f), 0);
        LocalBlockChance = baseBlock;

        FlatLifeBonus = MathF.Round(life, 0);
        FlatEnergyShieldBonus = 0f; // Handled locally on base
        FlatManaBonus = MathF.Round(mana, 0);
        FireResistanceBonus = MathF.Round(fireRes, 0);
        ColdResistanceBonus = MathF.Round(coldRes, 0);
        LightningResistanceBonus = MathF.Round(lightRes, 0);
        ChaosResistanceBonus = MathF.Round(chaosRes, 0);

        StrengthBonus = str;
        DexterityBonus = dex;
        IntelligenceBonus = intel;
        GlobalIncreasedDamagePercent = incDmg;
        GlobalAttackSpeedPercent = incSpeed;
        MovementSpeedBonus = moveSpeed;
    }
}
