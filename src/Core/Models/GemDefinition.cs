using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace IdleExile.Core.Models;

public sealed record GemDefinition(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("category")] string Category,
    [property: JsonPropertyName("color")] string Color,
    [property: JsonPropertyName("tags")] string[]? Tags,
    [property: JsonPropertyName("requiredTags")] string[]? RequiredTags,
    [property: JsonPropertyName("baseStats")] Dictionary<string, object>? BaseStats,
    [property: JsonPropertyName("modifiers")] Dictionary<string, object>? Modifiers,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("weaponRestrictions")] string[]? WeaponRestrictions = null
);
