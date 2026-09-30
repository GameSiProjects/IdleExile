using System.Collections.Generic;
using System.Linq;

namespace IdleExile.Core.Models;

/// <summary>
/// Result of an executed damage hit calculation, containing damage breakdown,
/// critical strike information, and any status ailments inflicted.
/// </summary>
public sealed record HitResult(
    HitDamageBreakdown Damage,
    float TotalDamage,
    bool IsCrit,
    float CritMultiplier,
    IReadOnlyList<AilmentInstance> InflictedAilments,
    HitDamageBreakdown? UnmitigatedDamage = null
)
{
    public bool HasAilment(AilmentType type) =>
        InflictedAilments.Any(a => a.Type == type);

    public AilmentInstance? GetAilment(AilmentType type) =>
        InflictedAilments.FirstOrDefault(a => a.Type == type);
}
