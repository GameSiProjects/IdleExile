using System;
using System.Text.Json.Serialization;

namespace IdleExile.Core.Models;

/// <summary>
/// Weapon archetype classification conforming to Path of Exile weapon classes.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum WeaponClass
{
    OneHandedSword,
    OneHandedAxe,
    OneHandedMace,
    Claw,
    Dagger,
    Wand,
    Sceptre,
    TwoHandedSword,
    TwoHandedAxe,
    TwoHandedMace,
    Bow,
    Staff,
    Shield,
    Quiver
}

/// <summary>
/// Hand slot requirement constraints for equipping weapons.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum HandSlotRequirement
{
    OneHanded,
    TwoHanded,
    OffHandOnly
}

/// <summary>
/// Immutable definition of a weapon or off-hand base item loaded from data/weapon_bases.json.
/// Pure C# model — zero Godot Node dependencies.
/// </summary>
public sealed record WeaponBaseDefinition(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("weaponClass")] WeaponClass WeaponClass,
    [property: JsonPropertyName("handRequirement")] HandSlotRequirement HandRequirement,
    [property: JsonPropertyName("baseMinPhys")] float BaseMinPhys,
    [property: JsonPropertyName("baseMaxPhys")] float BaseMaxPhys,
    [property: JsonPropertyName("baseAps")] float BaseAps,
    [property: JsonPropertyName("baseCritChance")] float BaseCritChance,
    [property: JsonPropertyName("baseArmour")] float BaseArmour = 0f,
    [property: JsonPropertyName("baseEvasion")] float BaseEvasion = 0f,
    [property: JsonPropertyName("baseEnergyShield")] float BaseEnergyShield = 0f,
    [property: JsonPropertyName("baseBlockChance")] float BaseBlockChance = 0f,
    [property: JsonPropertyName("reqLevel")] int ReqLevel = 1,
    [property: JsonPropertyName("reqStr")] int ReqStr = 0,
    [property: JsonPropertyName("reqDex")] int ReqDex = 0,
    [property: JsonPropertyName("reqInt")] int ReqInt = 0,
    [property: JsonPropertyName("implicitAffixId")] string? ImplicitAffixId = null,
    [property: JsonPropertyName("implicitValue")] float ImplicitValue = 0f,
    [property: JsonPropertyName("description")] string Description = ""
)
{
    /// <summary>
    /// Formats the weapon's implicit affix into standard Path of Exile modifier text.
    /// </summary>
    public static string FormatImplicit(string? affixId, float value)
    {
        if (string.IsNullOrEmpty(affixId)) return string.Empty;
        return affixId switch
        {
            "sword_implicit_accuracy" => $"+{value:F0} to Accuracy Rating",
            "foil_implicit_crit_multi" => $"+{value:F0}% to Global Critical Strike Multiplier",
            "mace_implicit_stun_threshold" => $"{value:F0}% Reduced Enemy Stun Threshold",
            "mace_implicit_stun_duration" => $"{value:F0}% Increased Stun Duration on Enemies",
            "claw_implicit_life_on_hit" => $"+{value:F0} Life gained for each Enemy hit by Attacks",
            "dagger_implicit_global_crit" => $"+{value:F0}% to Global Critical Strike Chance",
            "wand_implicit_spell_damage" => $"{value:F0}% Increased Spell Damage",
            "sceptre_implicit_elemental_damage" => $"{value:F0}% Increased Elemental Damage",
            "staff_implicit_block" => $"+{value:F0}% Chance to Block Attack Damage",
            "quiver_implicit_flat_phys" => $"Adds {value:F0} to {MathF.Round(value * 1.6f):F0} Physical Damage to Bow Attacks",
            "quiver_implicit_crit_multi" => $"+{value:F0}% to Global Critical Strike Multiplier",
            "shield_implicit_flat_life" => $"+{value:F0} to Maximum Life",
            "shield_implicit_spell_suppress" => $"+{value:F0}% Chance to Suppress Spell Damage",
            "shield_implicit_spell_damage" => $"{value:F0}% Increased Spell Damage",
            _ => string.Empty
        };
    }
}

/// <summary>
/// Display formatting helpers and emoji badge icons for weapon classes and requirements.
/// </summary>
public static class WeaponDisplayExtensions
{
    public static string GetDisplayName(this WeaponClass wc) => wc switch
    {
        WeaponClass.OneHandedSword => "Kiếm Một Tay (1H Sword)",
        WeaponClass.OneHandedAxe => "Rìu Một Tay (1H Axe)",
        WeaponClass.OneHandedMace => "Chùy Một Tay (1H Mace)",
        WeaponClass.Claw => "Vuốt (Claw)",
        WeaponClass.Dagger => "Dao Găm (Dagger)",
        WeaponClass.Wand => "Đũa Phép (Wand)",
        WeaponClass.Sceptre => "Pháp Trượng (Sceptre)",
        WeaponClass.TwoHandedSword => "Đại Kiếm (2H Sword)",
        WeaponClass.TwoHandedAxe => "Đại Rìu (2H Axe)",
        WeaponClass.TwoHandedMace => "Đại Chùy (2H Mace)",
        WeaponClass.Bow => "Cung (Bow)",
        WeaponClass.Staff => "Quyền Trượng (Staff)",
        WeaponClass.Shield => "Khiên (Shield)",
        WeaponClass.Quiver => "Ống Tên (Quiver)",
        _ => wc.ToString()
    };

    public static string GetShortName(this WeaponClass wc) => wc switch
    {
        WeaponClass.OneHandedSword => "1H Sword",
        WeaponClass.OneHandedAxe => "1H Axe",
        WeaponClass.OneHandedMace => "1H Mace",
        WeaponClass.Claw => "Claw",
        WeaponClass.Dagger => "Dagger",
        WeaponClass.Wand => "Wand",
        WeaponClass.Sceptre => "Sceptre",
        WeaponClass.TwoHandedSword => "2H Sword",
        WeaponClass.TwoHandedAxe => "2H Axe",
        WeaponClass.TwoHandedMace => "2H Mace",
        WeaponClass.Bow => "Bow",
        WeaponClass.Staff => "Staff",
        WeaponClass.Shield => "Shield",
        WeaponClass.Quiver => "Quiver",
        _ => wc.ToString()
    };

    public static string GetIcon(this WeaponClass wc) => wc switch
    {
        WeaponClass.OneHandedSword => "⚔️",
        WeaponClass.OneHandedAxe => "🪓",
        WeaponClass.OneHandedMace => "🔨",
        WeaponClass.Claw => "🐾",
        WeaponClass.Dagger => "🗡️",
        WeaponClass.Wand => "🪄",
        WeaponClass.Sceptre => "🪔",
        WeaponClass.TwoHandedSword => "⚔️",
        WeaponClass.TwoHandedAxe => "🪓",
        WeaponClass.TwoHandedMace => "🔨",
        WeaponClass.Bow => "🏹",
        WeaponClass.Staff => "🦯",
        WeaponClass.Shield => "🛡️",
        WeaponClass.Quiver => "🏹",
        _ => "⚔️"
    };

    public static string GetDisplayName(this HandSlotRequirement req) => req switch
    {
        HandSlotRequirement.OneHanded => "Một Tay (1-Handed)",
        HandSlotRequirement.TwoHanded => "Hai Tay (2-Handed)",
        HandSlotRequirement.OffHandOnly => "Tay Phụ (Off-Hand Only)",
        _ => req.ToString()
    };
}
