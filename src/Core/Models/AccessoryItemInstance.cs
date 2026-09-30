using System;
using System.Collections.Generic;

namespace IdleExile.Core.Models;

/// <summary>
/// Runtime instance of an equipped or inventoried accessory (Amulet, Ring, Belt).
/// Provides high-budget global affixes (Life, Resistances, Crit Multi, Mana, Attributes).
/// Implements IGearItem for UI display, smart comparison, and auto-stash routing.
/// Pure C# model — zero Godot dependencies.
/// </summary>
public sealed class AccessoryItemInstance : IGearItem
{
    public string InstanceId { get; init; } = Guid.NewGuid().ToString("N");
    public AccessoryBaseDefinition BaseDefinition { get; }
    public string CustomName { get; set; } = string.Empty;
    public string Name => string.IsNullOrWhiteSpace(CustomName) ? BaseDefinition.Name : CustomName;
    public string BaseName => BaseDefinition.Name;

    public GearSlot TargetSlot => BaseDefinition.AccessorySlot switch
    {
        AccessorySlot.Amulet => GearSlot.Amulet,
        AccessorySlot.Ring => GearSlot.Ring1,
        AccessorySlot.Belt => GearSlot.Belt,
        _ => GearSlot.Belt
    };

    public string Rarity { get; set; } = "Normal";
    public int ItemLevel { get; set; } = 1;
    public int Quality { get; set; } = 0; // Catalysts in future
    public int ReqLevel => BaseDefinition.ReqLevel;
    public bool IsLocked { get; set; } = false;
    public List<AffixDefinition> Affixes { get; } = new();

    public string ImplicitDescription => !string.IsNullOrEmpty(BaseDefinition.ImplicitDescription)
        ? BaseDefinition.ImplicitDescription
        : string.Empty;

    // ─────────────────────────────────────────────
    //  Precomputed Stats
    // ─────────────────────────────────────────────

    public float LocalArmour => 0f;
    public float LocalEvasion => 0f;
    public float LocalEnergyShield => 0f;
    public float LocalBlockChance => 0f;

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

    public float LocalMinDamage => 0f;
    public float LocalMaxDamage => 0f;
    public float LocalAps => 0f;
    public float LocalCritChance => 0f;
    public float LocalDps => 0f;
    public float GlobalIncreasedDamagePercent { get; private set; }
    public float GlobalAttackSpeedPercent { get; private set; }

    public AccessoryItemInstance(AccessoryBaseDefinition baseDefinition, string rarity = "Normal", int itemLevel = 1)
    {
        BaseDefinition = baseDefinition ?? throw new ArgumentNullException(nameof(baseDefinition));
        Rarity = rarity;
        ItemLevel = Math.Max(1, itemLevel);
        RecalculateStats();
    }

    /// <summary>
    /// Recalculates stats from implicit and explicit affixes.
    /// </summary>
    public void RecalculateStats()
    {
        float life = 0f;
        float es = 0f;
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

        // Process implicit modifier
        if (!string.IsNullOrEmpty(BaseDefinition.ImplicitDescription))
        {
            float val = BaseDefinition.ImplicitValue;
            string imp = BaseDefinition.ImplicitDescription;

            if (imp.Contains("Maximum Life", StringComparison.OrdinalIgnoreCase))
            {
                life += val;
            }
            else if (imp.Contains("Energy Shield", StringComparison.OrdinalIgnoreCase))
            {
                es += val;
            }
            else if (imp.Contains("Strength", StringComparison.OrdinalIgnoreCase))
            {
                str += (int)val;
            }
            else if (imp.Contains("Dexterity", StringComparison.OrdinalIgnoreCase))
            {
                dex += (int)val;
            }
            else if (imp.Contains("Intelligence", StringComparison.OrdinalIgnoreCase))
            {
                intel += (int)val;
            }
            else if (imp.Contains("all Attributes", StringComparison.OrdinalIgnoreCase))
            {
                str += (int)val;
                dex += (int)val;
                intel += (int)val;
            }
            else if (imp.Contains("Fire and Cold Resistances", StringComparison.OrdinalIgnoreCase))
            {
                fireRes += val;
                coldRes += val;
            }
            else if (imp.Contains("Physical Damage", StringComparison.OrdinalIgnoreCase))
            {
                incDmg += val;
            }
        }

        // Process explicit affixes
        foreach (var affix in Affixes)
        {
            if (affix.Tags == null) continue;

            float avgVal = (affix.MinValue + affix.MaxValue) / 2f;

            if (Array.Exists(affix.Tags, t => string.Equals(t, "life", StringComparison.OrdinalIgnoreCase)))
            {
                life += avgVal;
            }
            else if (Array.Exists(affix.Tags, t => string.Equals(t, "energy_shield", StringComparison.OrdinalIgnoreCase)))
            {
                es += avgVal;
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
                incDmg += avgVal;
            }
            else if (Array.Exists(affix.Tags, t => string.Equals(t, "speed", StringComparison.OrdinalIgnoreCase)))
            {
                incSpeed += avgVal;
            }
        }

        FlatLifeBonus = MathF.Round(life, 0);
        FlatEnergyShieldBonus = MathF.Round(es, 0);
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
    }
}
