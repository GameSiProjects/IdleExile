namespace IdleExile.Core.Models;

/// <summary>
/// Data definition for a Crucible Sanctuary progression tier.
/// Pure C# immutable record deserialized from data/sanctuary_tiers.json.
/// </summary>
public sealed record SanctuaryTierDefinition(
    string Id,
    int Tier,
    string Name,
    int RequiredMapTier,
    int MapSlots,
    int QueueSlots,
    bool SmelterUnlocked,
    bool AbyssUnlocked,
    float SmelterSpeedBonus,
    bool PinnacleTrophiesUnlocked,
    string Description
);
