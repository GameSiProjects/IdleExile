using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace IdleExile.Core.Models;

public sealed record MapDefinition(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("tier")] int Tier,
    [property: JsonPropertyName("monsterLevel")] int MonsterLevel,
    [property: JsonPropertyName("isFailSafe")] bool IsFailSafe,
    [property: JsonPropertyName("baseQuantity")] int BaseQuantity,
    [property: JsonPropertyName("basePackSize")] int BasePackSize,
    [property: JsonPropertyName("bossName")] string? BossName,
    [property: JsonPropertyName("description")] string Description
);

public sealed record MapModifierDefinition(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("nameTemplate")] string NameTemplate,
    [property: JsonPropertyName("bonusQuantity")] int BonusQuantity,
    [property: JsonPropertyName("bonusPackSize")] int BonusPackSize,
    [property: JsonPropertyName("min")] float Min,
    [property: JsonPropertyName("max")] float Max
);

public sealed record MapDatabaseRoot(
    [property: JsonPropertyName("maps")] List<MapDefinition> Maps,
    [property: JsonPropertyName("mapModifiers")] List<MapModifierDefinition> MapModifiers
);
