using System.Text.Json.Serialization;

namespace IdleExile.Core.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AffixType
{
    Prefix,
    Suffix,
    Implicit
}

public sealed record AffixDefinition(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("nameTemplate")] string NameTemplate,
    [property: JsonPropertyName("type")] AffixType Type,
    [property: JsonPropertyName("tier")] int Tier,
    [property: JsonPropertyName("minIlvl")] int MinIlvl,
    [property: JsonPropertyName("minValue")] float MinValue,
    [property: JsonPropertyName("maxValue")] float MaxValue,
    [property: JsonPropertyName("tags")] string[] Tags
);
