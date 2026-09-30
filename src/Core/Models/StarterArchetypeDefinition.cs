using System;
using System.Collections.Generic;
using System.Linq;

namespace IdleExile.Core.Models;

/// <summary>
/// Core attribute archetypes representing the 3 primary Path of Exile attribute alignments.
/// </summary>
public enum StarterArchetype
{
    /// <summary>Strength alignment: High Life, heavy Armour, melee AoE.</summary>
    Juggernaut,

    /// <summary>Intelligence alignment: Energy Shield, Spells, Fire and Ignite ailments.</summary>
    Elementalist,

    /// <summary>Dexterity alignment: Evasion, Bow Projectiles, high attack speed.</summary>
    Deadeye
}

/// <summary>
/// Data definition for a starter archetype chosen by the player during FTUE (First Time User Experience).
/// Encapsulates base vitals, starting skills, guaranteed drops, and initial inventory.
/// Pure C# model — no Godot dependencies.
/// </summary>
public sealed record StarterArchetypeDefinition
{
    /// <summary>Unique identifier of the archetype.</summary>
    public StarterArchetype Id { get; init; }

    /// <summary>Localized display name (e.g. "Juggernaut").</summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>Flavor title / subtitle (e.g. "The Unyielding Colossus").</summary>
    public string Subtitle { get; init; } = string.Empty;

    /// <summary>Summary description of combat playstyle.</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>Primary attribute alignment ("Strength", "Intelligence", or "Dexterity").</summary>
    public string PrimaryAttribute { get; init; } = "Strength";

    /// <summary>Visual hex color token for UI cards, badges, and aura highlights.</summary>
    public string ColorHex { get; init; } = "#E74C3C";

    /// <summary>Id of the initial Active Gem slotted into Skill Slot 1.</summary>
    public string ActiveGemId { get; init; } = string.Empty;

    /// <summary>Id of the guaranteed Support Gem dropped during the first Magic pack encounter.</summary>
    public string GuaranteedSupportGemId { get; init; } = string.Empty;

    /// <summary>Id of the initial weapon equipped in Main Hand.</summary>
    public string StartingMainHandWeaponId { get; init; } = string.Empty;

    /// <summary>Optional Id of the initial weapon/off-hand equipped in Off Hand.</summary>
    public string? StartingOffHandWeaponId { get; init; } = null;

    /// <summary>Initial maximum Life pool at Level 1.</summary>
    public float BaseMaxLife { get; init; } = 100f;

    /// <summary>Initial maximum Energy Shield pool at Level 1.</summary>
    public float BaseMaxEnergyShield { get; init; } = 0f;

    /// <summary>Initial maximum Mana pool at Level 1.</summary>
    public float BaseMaxMana { get; init; } = 60f;

    /// <summary>Baseline defensive stats (Armour, Evasion, Block, Resistances).</summary>
    public DefensiveStats BaseDefenses { get; init; } = new();

    /// <summary>Flat list of item/currency IDs placed into the player's initial backpack.</summary>
    public IReadOnlyList<string> InitialInventory { get; init; } = Array.Empty<string>();

    /// <summary>Initial currency stack counts granted at character inception.</summary>
    public IReadOnlyDictionary<string, int> InitialCurrencies { get; init; } = new Dictionary<string, int>();

    /// <summary>Initial Tier 1 Map Slates granted to kickstart Immediate Mapping.</summary>
    public IReadOnlyList<string> InitialMaps { get; init; } = Array.Empty<string>();
}

/// <summary>
/// Static catalog providing canonical definitions and lookup utilities for starter archetypes.
/// </summary>
public static class StarterArchetypeCatalog
{
    public static readonly StarterArchetypeDefinition Juggernaut = new()
    {
        Id = StarterArchetype.Juggernaut,
        DisplayName = "Juggernaut",
        Subtitle = "The Unyielding Colossus",
        Description = "Heavy armor, crushing melee strikes, and unwavering endurance.",
        PrimaryAttribute = "Strength",
        ColorHex = "#E74C3C",
        ActiveGemId = "cleave",
        GuaranteedSupportGemId = "added_fire_damage",
        StartingMainHandWeaponId = "rusted_sword",
        StartingOffHandWeaponId = "rusted_sword",
        BaseMaxLife = 120f,
        BaseMaxEnergyShield = 0f,
        BaseMaxMana = 60f,
        BaseDefenses = new DefensiveStats
        {
            Armour = 25f,
            LifeRegenPerSecond = 4.0f,
            ManaRegenPerSecond = 12.0f
        },
        InitialInventory = new[]
        {
            "scroll_of_wisdom", "scroll_of_wisdom", "scroll_of_wisdom",
            "orb_of_transmutation",
            "strand", "strand", "strand"
        },
        InitialCurrencies = new Dictionary<string, int>
        {
            ["scroll_of_wisdom"] = 3,
            ["orb_of_transmutation"] = 1
        },
        InitialMaps = new[] { "strand", "strand", "strand" }
    };

    public static readonly StarterArchetypeDefinition Elementalist = new()
    {
        Id = StarterArchetype.Elementalist,
        DisplayName = "Elementalist",
        Subtitle = "The Primordial Evoker",
        Description = "Explosive fire spells, arcane energy shield buffer, and cascading ignite ailments.",
        PrimaryAttribute = "Intelligence",
        ColorHex = "#3498DB",
        ActiveGemId = "fireball",
        GuaranteedSupportGemId = "ignite_proliferation",
        StartingMainHandWeaponId = "driftwood_wand",
        StartingOffHandWeaponId = null,
        BaseMaxLife = 90f,
        BaseMaxEnergyShield = 45f,
        BaseMaxMana = 100f,
        BaseDefenses = new DefensiveStats
        {
            FireResistance = 10f,
            LifeRegenPerSecond = 2.0f,
            ManaRegenPerSecond = 14.0f
        },
        InitialInventory = new[]
        {
            "scroll_of_wisdom", "scroll_of_wisdom", "scroll_of_wisdom",
            "orb_of_transmutation",
            "strand", "strand", "strand"
        },
        InitialCurrencies = new Dictionary<string, int>
        {
            ["scroll_of_wisdom"] = 3,
            ["orb_of_transmutation"] = 1
        },
        InitialMaps = new[] { "strand", "strand", "strand" }
    };

    public static readonly StarterArchetypeDefinition Deadeye = new()
    {
        Id = StarterArchetype.Deadeye,
        DisplayName = "Deadeye",
        Subtitle = "The Windrunning Marksman",
        Description = "Rapid projectile volleys, deterministic evasion entropy, and lethal critical strikes.",
        PrimaryAttribute = "Dexterity",
        ColorHex = "#2ECC71",
        ActiveGemId = "split_arrow",
        GuaranteedSupportGemId = "multiple_projectiles",
        StartingMainHandWeaponId = "crude_bow",
        StartingOffHandWeaponId = null,
        BaseMaxLife = 105f,
        BaseMaxEnergyShield = 0f,
        BaseMaxMana = 75f,
        BaseDefenses = new DefensiveStats
        {
            EvasionChance = 25f,
            LifeRegenPerSecond = 3.0f,
            ManaRegenPerSecond = 11.0f
        },
        InitialInventory = new[]
        {
            "scroll_of_wisdom", "scroll_of_wisdom", "scroll_of_wisdom",
            "orb_of_transmutation",
            "strand", "strand", "strand"
        },
        InitialCurrencies = new Dictionary<string, int>
        {
            ["scroll_of_wisdom"] = 3,
            ["orb_of_transmutation"] = 1
        },
        InitialMaps = new[] { "strand", "strand", "strand" }
    };

    /// <summary>Immutable list of all available starter archetypes in display order.</summary>
    public static readonly IReadOnlyList<StarterArchetypeDefinition> All = new[]
    {
        Juggernaut,
        Elementalist,
        Deadeye
    };

    /// <summary>
    /// Looks up a starter archetype by enum identifier.
    /// </summary>
    public static StarterArchetypeDefinition Get(StarterArchetype archetype) => archetype switch
    {
        StarterArchetype.Juggernaut => Juggernaut,
        StarterArchetype.Elementalist => Elementalist,
        StarterArchetype.Deadeye => Deadeye,
        _ => throw new ArgumentOutOfRangeException(nameof(archetype), archetype, "Unknown StarterArchetype")
    };

    /// <summary>
    /// Attempts to parse and retrieve an archetype by string name (case-insensitive).
    /// </summary>
    public static bool TryGet(string? name, out StarterArchetypeDefinition? definition)
    {
        definition = null;
        if (string.IsNullOrWhiteSpace(name))
            return false;

        definition = All.FirstOrDefault(a => 
            string.Equals(a.DisplayName, name, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(a.Id.ToString(), name, StringComparison.OrdinalIgnoreCase));

        return definition != null;
    }
}
