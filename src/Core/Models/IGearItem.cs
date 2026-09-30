using System.Collections.Generic;

namespace IdleExile.Core.Models;

/// <summary>
/// Common interface for any equippable gear item in Idle Exile.
/// Conforms to docs/11_equipment_system_design.md:
/// - Supports 10 fixed equipment slots.
/// - Pure C# model — zero Godot dependencies.
/// - Enables instant comparisons (Delta EHP / Delta DPS), locking, and stash routing.
/// </summary>
public interface IGearItem
{
    /// <summary>Unique runtime instance identifier.</summary>
    string InstanceId { get; }

    /// <summary>Display name (e.g. "Astral Plate of the Colossus", "Jewelled Foil of Celebration").</summary>
    string Name { get; }

    /// <summary>Underlying item base name (e.g. "Astral Plate", "Jewelled Foil").</summary>
    string BaseName { get; }

    /// <summary>Designated or default slot for this equipment item.</summary>
    GearSlot TargetSlot { get; }

    /// <summary>Rarity tier: "Normal", "Magic", "Rare", "Unique".</summary>
    string Rarity { get; set; }

    /// <summary>Item level (1-100), governs maximum affix tier.</summary>
    int ItemLevel { get; set; }

    /// <summary>Item quality (0-20%), acts as local increased defenses or physical damage.</summary>
    int Quality { get; set; }

    /// <summary>Character level required to equip.</summary>
    int ReqLevel { get; }

    /// <summary>Safety lock status [Khóa Đồ] preventing accidental sell or dismantle.</summary>
    bool IsLocked { get; set; }

    /// <summary>Implicit affix text or description.</summary>
    string ImplicitDescription { get; }

    /// <summary>Explicit affixes (Prefixes and Suffixes).</summary>
    List<AffixDefinition> Affixes { get; }

    // ─────────────────────────────────────────────
    //  Defensive Properties (Local & Global)
    // ─────────────────────────────────────────────

    /// <summary>Local Armour value after local flat and increased modifiers.</summary>
    float LocalArmour { get; }

    /// <summary>Local Evasion value after local flat and increased modifiers.</summary>
    float LocalEvasion { get; }

    /// <summary>Local Energy Shield value after local flat and increased modifiers.</summary>
    float LocalEnergyShield { get; }

    /// <summary>Local Block chance granted (e.g. by shields).</summary>
    float LocalBlockChance { get; }

    /// <summary>Flat bonus to maximum Life.</summary>
    float FlatLifeBonus { get; }

    /// <summary>Flat bonus to maximum Energy Shield.</summary>
    float FlatEnergyShieldBonus { get; }

    /// <summary>Flat bonus to maximum Mana.</summary>
    float FlatManaBonus { get; }

    /// <summary>Additive percentage bonus to Fire Resistance.</summary>
    float FireResistanceBonus { get; }

    /// <summary>Additive percentage bonus to Cold Resistance.</summary>
    float ColdResistanceBonus { get; }

    /// <summary>Additive percentage bonus to Lightning Resistance.</summary>
    float LightningResistanceBonus { get; }

    /// <summary>Additive percentage bonus to Chaos Resistance.</summary>
    float ChaosResistanceBonus { get; }

    // ─────────────────────────────────────────────
    //  Attribute Bonuses (Soft Attributes)
    // ─────────────────────────────────────────────

    /// <summary>Strength bonus granted by this item.</summary>
    int StrengthBonus { get; }

    /// <summary>Dexterity bonus granted by this item.</summary>
    int DexterityBonus { get; }

    /// <summary>Intelligence bonus granted by this item.</summary>
    int IntelligenceBonus { get; }

    // ─────────────────────────────────────────────
    //  Offensive Properties (Local & Global)
    // ─────────────────────────────────────────────

    /// <summary>Minimum physical damage per hit after local modifiers.</summary>
    float LocalMinDamage { get; }

    /// <summary>Maximum physical damage per hit after local modifiers.</summary>
    float LocalMaxDamage { get; }

    /// <summary>Attacks per second after local speed modifiers.</summary>
    float LocalAps { get; }

    /// <summary>Base critical strike chance percentage.</summary>
    float LocalCritChance { get; }

    /// <summary>Local physical/total weapon DPS.</summary>
    float LocalDps { get; }

    /// <summary>Global increased damage multiplier bonus in percent.</summary>
    float GlobalIncreasedDamagePercent { get; }

    /// <summary>Global increased attack speed bonus in percent.</summary>
    float GlobalAttackSpeedPercent { get; }

    /// <summary>Increased Movement Speed bonus granted by this item in percent.</summary>
    float MovementSpeedBonus => 0f;
}
