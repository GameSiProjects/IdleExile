using System.Text.Json.Serialization;

namespace IdleExile.Core.Models;

/// <summary>
/// Armour equipment categories conforming to Path of Exile gear slots.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ArmourSlot
{
    Helmet,
    BodyArmour,
    Gloves,
    Boots,
    Shield
}

/// <summary>
/// Immutable definition of an armour base item.
/// Pure C# model — zero Godot Node dependencies.
/// </summary>
public sealed record ArmourBaseDefinition(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("armourSlot")] ArmourSlot ArmourSlot,
    [property: JsonPropertyName("baseArmour")] float BaseArmour = 0f,
    [property: JsonPropertyName("baseEvasion")] float BaseEvasion = 0f,
    [property: JsonPropertyName("baseEnergyShield")] float BaseEnergyShield = 0f,
    [property: JsonPropertyName("baseBlockChance")] float BaseBlockChance = 0f,
    [property: JsonPropertyName("reqLevel")] int ReqLevel = 1,
    [property: JsonPropertyName("reqStr")] int ReqStr = 0,
    [property: JsonPropertyName("reqDex")] int ReqDex = 0,
    [property: JsonPropertyName("reqInt")] int ReqInt = 0,
    [property: JsonPropertyName("implicitAffixId")] string? ImplicitAffixId = null,
    [property: JsonPropertyName("implicitDescription")] string ImplicitDescription = "",
    [property: JsonPropertyName("implicitValue")] float ImplicitValue = 0f,
    [property: JsonPropertyName("description")] string Description = ""
);
