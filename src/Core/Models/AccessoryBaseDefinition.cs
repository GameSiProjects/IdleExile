using System.Text.Json.Serialization;

namespace IdleExile.Core.Models;

/// <summary>
/// Accessory equipment categories (Amulets, Rings, Belts).
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AccessorySlot
{
    Amulet,
    Ring,
    Belt
}

/// <summary>
/// Immutable definition of an accessory or belt base item.
/// Pure C# model — zero Godot Node dependencies.
/// </summary>
public sealed record AccessoryBaseDefinition(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("accessorySlot")] AccessorySlot AccessorySlot,
    [property: JsonPropertyName("implicitDescription")] string ImplicitDescription = "",
    [property: JsonPropertyName("implicitAffixId")] string? ImplicitAffixId = null,
    [property: JsonPropertyName("implicitValue")] float ImplicitValue = 0f,
    [property: JsonPropertyName("reqLevel")] int ReqLevel = 1,
    [property: JsonPropertyName("description")] string Description = ""
);
