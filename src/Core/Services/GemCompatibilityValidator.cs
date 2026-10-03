using System;
using System.Collections.Generic;
using System.Linq;
using IdleExile.Core.Models;
using IdleExile.Core.State;

namespace IdleExile.Core.Services;

/// <summary>
/// Result of evaluating the tag compatibility between an Active Gem and a Support Gem.
/// </summary>
public sealed record GemCompatibilityResult(
    bool IsCompatible,
    string[] MatchingTags,
    string[] MissingRequiredTags,
    string Reason,
    float MoreDamagePercent,
    float LessDamagePercent,
    float ManaMultiplier
);

/// <summary>
/// A single step in the compounded damage multiplier calculation prod(1 + More) * prod(1 - Less).
/// </summary>
public sealed record MultiplierStep(
    string SourceName,
    string ModType, // "Base", "More", "Less"
    float Percent,
    float Factor
);

/// <summary>
/// Full mathematical breakdown of the compounded damage multiplier: prod(1 + More) * prod(1 - Less).
/// </summary>
public sealed record DamageMultiplierBreakdown(
    float TotalMultiplier,
    float NetMorePercent,
    IReadOnlyList<MultiplierStep> Steps,
    string FormulaString,
    string MathRepresentation
);

/// <summary>
/// Projection telemetry when previewing adding/replacing a gem in a skill setup.
/// </summary>
public sealed record MultiplierPreviewResult(
    DamageMultiplierBreakdown CurrentBreakdown,
    DamageMultiplierBreakdown ProjectedBreakdown,
    float DeltaMultiplier,
    float DeltaPercent,
    bool IsIncrease
);

/// <summary>
/// Pure C# Gem Tag Matching Validator & Compounded Damage Multiplier Engine (Task 3.2).
/// Follows Path of Exile and PoE 2 decoupled gem design:
/// - Support Gems automatically link to Active Gems if required tags are satisfied.
/// - Calculates the exact compounded damage multiplier: prod(1 + More Damage) * prod(1 - Less Damage).
/// - 100% pure C# with zero Godot Node dependencies, fully headless testable.
/// </summary>
public static class GemCompatibilityValidator
{
    /// <summary>
    /// Evaluates whether a Support Gem can support a given Active Gem based on required tags.
    /// In PoE:
    /// - Active Gem must have Category == "Active"
    /// - Support Gem must have Category == "Support"
    /// - If Support has no requiredTags, it supports any skill (universal).
    /// - If Support has specific archetype tags (e.g. melee, projectile, aoe):
    ///   - "melee": Active Gem MUST possess the melee tag.
    ///   - "projectile": Active Gem MUST possess the projectile tag.
    ///   - "aoe": Active Gem MUST possess the aoe tag.
    ///   - "spell": Active Gem MUST possess spell (unless attack is also accepted).
    /// </summary>
    public static GemCompatibilityResult ValidateCompatibility(GemDefinition? activeGem, GemDefinition? supportGem)
    {
        if (activeGem == null)
        {
            return new GemCompatibilityResult(
                IsCompatible: false,
                MatchingTags: Array.Empty<string>(),
                MissingRequiredTags: supportGem?.RequiredTags ?? Array.Empty<string>(),
                Reason: "Chưa có Active Gem trong khay kỹ năng.",
                MoreDamagePercent: 0f,
                LessDamagePercent: 0f,
                ManaMultiplier: 100f
            );
        }

        if (supportGem == null || !string.Equals(supportGem.Category, "Support", StringComparison.OrdinalIgnoreCase))
        {
            return new GemCompatibilityResult(
                IsCompatible: false,
                MatchingTags: Array.Empty<string>(),
                MissingRequiredTags: Array.Empty<string>(),
                Reason: "Ngọc được chọn không phải là Support Gem hợp lệ.",
                MoreDamagePercent: 0f,
                LessDamagePercent: 0f,
                ManaMultiplier: 100f
            );
        }

        float morePercent = ExtractMoreDamagePercent(supportGem);
        float lessPercent = ExtractLessDamagePercent(supportGem);
        float manaMultiplier = ExtractManaMultiplier(supportGem);

        // Universal support (no tag restrictions)
        if (supportGem.RequiredTags == null || supportGem.RequiredTags.Length == 0)
        {
            return new GemCompatibilityResult(
                IsCompatible: true,
                MatchingTags: activeGem.Tags ?? Array.Empty<string>(),
                MissingRequiredTags: Array.Empty<string>(),
                Reason: "Universal Support: Không giới hạn thẻ kỹ năng.",
                MoreDamagePercent: morePercent,
                LessDamagePercent: lessPercent,
                ManaMultiplier: manaMultiplier
            );
        }

        var activeTags = new HashSet<string>(activeGem.Tags ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);
        if (activeTags.Count == 0)
        {
            return new GemCompatibilityResult(
                IsCompatible: false,
                MatchingTags: Array.Empty<string>(),
                MissingRequiredTags: supportGem.RequiredTags,
                Reason: $"Active Gem [{activeGem.Name}] không có thẻ kỹ năng nào.",
                MoreDamagePercent: 0f,
                LessDamagePercent: 0f,
                ManaMultiplier: manaMultiplier
            );
        }

        var matchingTags = supportGem.RequiredTags
            .Where(t => activeTags.Contains(t))
            .ToArray();

        var missingTags = supportGem.RequiredTags
            .Where(t => !activeTags.Contains(t))
            .ToArray();

        // Archetype strict constraints:
        // 1. Melee constraint: Support requires melee -> Active skill MUST be melee (cannot support bow/spells).
        bool requiresMelee = supportGem.RequiredTags.Contains("melee", StringComparer.OrdinalIgnoreCase);
        if (requiresMelee && !activeTags.Contains("melee"))
        {
            return new GemCompatibilityResult(
                IsCompatible: false,
                MatchingTags: matchingTags,
                MissingRequiredTags: new[] { "melee" },
                Reason: $"Không tương thích: Yêu cầu thẻ [melee] (Kỹ năng [{activeGem.Name}] không phải cận chiến).",
                MoreDamagePercent: 0f,
                LessDamagePercent: 0f,
                ManaMultiplier: manaMultiplier
            );
        }

        // 2. Projectile constraint: Support requires projectile -> Active skill MUST be projectile.
        bool requiresProj = supportGem.RequiredTags.Contains("projectile", StringComparer.OrdinalIgnoreCase);
        if (requiresProj && !activeTags.Contains("projectile"))
        {
            return new GemCompatibilityResult(
                IsCompatible: false,
                MatchingTags: matchingTags,
                MissingRequiredTags: new[] { "projectile" },
                Reason: $"Không tương thích: Yêu cầu thẻ [projectile] (Kỹ năng [{activeGem.Name}] không có đạn đạo).",
                MoreDamagePercent: 0f,
                LessDamagePercent: 0f,
                ManaMultiplier: manaMultiplier
            );
        }

        // 3. AoE constraint: Support requires aoe -> Active skill MUST be aoe.
        bool requiresAoe = supportGem.RequiredTags.Contains("aoe", StringComparer.OrdinalIgnoreCase);
        if (requiresAoe && !activeTags.Contains("aoe"))
        {
            return new GemCompatibilityResult(
                IsCompatible: false,
                MatchingTags: matchingTags,
                MissingRequiredTags: new[] { "aoe" },
                Reason: $"Không tương thích: Yêu cầu thẻ [aoe] (Kỹ năng [{activeGem.Name}] không có sát thương diện rộng).",
                MoreDamagePercent: 0f,
                LessDamagePercent: 0f,
                ManaMultiplier: manaMultiplier
            );
        }

        // General compatibility: at least one required tag matches
        if (matchingTags.Length > 0)
        {
            string tagList = string.Join(", ", matchingTags.Select(t => t.ToUpperInvariant()));
            return new GemCompatibilityResult(
                IsCompatible: true,
                MatchingTags: matchingTags,
                MissingRequiredTags: missingTags,
                Reason: $"✓ Tương thích: Chia sẻ thẻ [{tagList}] với [{activeGem.Name}].",
                MoreDamagePercent: morePercent,
                LessDamagePercent: lessPercent,
                ManaMultiplier: manaMultiplier
            );
        }

        string requiredList = string.Join(", ", supportGem.RequiredTags.Select(t => t.ToUpperInvariant()));
        return new GemCompatibilityResult(
            IsCompatible: false,
            MatchingTags: Array.Empty<string>(),
            MissingRequiredTags: missingTags,
            Reason: $"⚠ Không tương thích: Cần ít nhất một thẻ trong [{requiredList}] trên Active Gem.",
            MoreDamagePercent: 0f,
            LessDamagePercent: 0f,
            ManaMultiplier: manaMultiplier
        );
    }

    /// <summary>
    /// Quick boolean check whether an Active Gem and Support Gem are compatible.
    /// </summary>
    public static bool IsCompatible(GemDefinition? activeGem, GemDefinition? supportGem)
    {
        return ValidateCompatibility(activeGem, supportGem).IsCompatible;
    }

    /// <summary>
    /// Computes the exact compounded damage multiplier breakdown:
    /// prod(ActiveGemBaseMultiplier) * prod(1 + More/100) * prod(1 - Less/100).
    /// </summary>
    public static DamageMultiplierBreakdown CalculateBreakdown(
        GemDefinition? activeGem,
        IEnumerable<GemDefinition>? supports)
    {
        var steps = new List<MultiplierStep>();
        float runningMultiplier = 1.0f;

        // 1. Active Gem Base Damage Multiplier
        if (activeGem != null && activeGem.BaseStats != null)
        {
            float activeMult = GetFloat(activeGem.BaseStats, "damageMultiplier", 100f);
            if (activeMult > 0f)
            {
                float factor = activeMult / 100f;
                runningMultiplier *= factor;
                steps.Add(new MultiplierStep(
                    SourceName: activeGem.Name,
                    ModType: "Base",
                    Percent: activeMult,
                    Factor: factor
                ));
            }
        }

        // 2. Compounded Valid Linked Support Gems
        if (activeGem != null && supports != null)
        {
            foreach (var support in supports)
            {
                if (support == null || support.Modifiers == null)
                    continue;

                // Validate compatibility: Incompatible supports do not link and grant 0 multiplier!
                if (!IsCompatible(activeGem, support))
                    continue;

                // A. Generic damageMultiplier (positive = More, negative = Less)
                if (support.Modifiers.TryGetValue("damageMultiplier", out _))
                {
                    float dmgMult = GetFloat(support.Modifiers, "damageMultiplier", 0f);
                    if (dmgMult > 0f)
                    {
                        float factor = 1f + dmgMult / 100f;
                        runningMultiplier *= factor;
                        steps.Add(new MultiplierStep(support.Name, "More", dmgMult, factor));
                    }
                    else if (dmgMult < 0f)
                    {
                        float factor = 1f + dmgMult / 100f; // e.g. 1 - 0.25 = 0.75
                        runningMultiplier *= factor;
                        steps.Add(new MultiplierStep(support.Name, "Less", MathF.Abs(dmgMult), factor));
                    }
                }

                // B. morePhysicalDamagePercent
                if (support.Modifiers.TryGetValue("morePhysicalDamagePercent", out _))
                {
                    float morePhys = GetFloat(support.Modifiers, "morePhysicalDamagePercent", 0f);
                    if (morePhys > 0f)
                    {
                        float factor = 1f + morePhys / 100f;
                        runningMultiplier *= factor;
                        steps.Add(new MultiplierStep(support.Name, "More", morePhys, factor));
                    }
                }

                // C. moreFireDamagePercent
                if (support.Modifiers.TryGetValue("moreFireDamagePercent", out _))
                {
                    float moreFire = GetFloat(support.Modifiers, "moreFireDamagePercent", 0f);
                    if (moreFire > 0f)
                    {
                        float factor = 1f + moreFire / 100f;
                        runningMultiplier *= factor;
                        steps.Add(new MultiplierStep(support.Name, "More", moreFire, factor));
                    }
                }

                // D. moreAoeDamagePercent
                if (support.Modifiers.TryGetValue("moreAoeDamagePercent", out _))
                {
                    float moreAoe = GetFloat(support.Modifiers, "moreAoeDamagePercent", 0f);
                    if (moreAoe > 0f)
                    {
                        float factor = 1f + moreAoe / 100f;
                        runningMultiplier *= factor;
                        steps.Add(new MultiplierStep(support.Name, "More", moreAoe, factor));
                    }
                }
            }
        }

        runningMultiplier = MathF.Max(0.01f, runningMultiplier);
        float netMorePercent = (runningMultiplier - 1.0f) * 100f;

        // Build formula string: e.g. "1.25 (Cleave) × 1.35 (Melee Phys) = 1.69x (+69% More Damage)"
        string formulaString;
        string mathRepresentation;

        if (steps.Count == 0)
        {
            formulaString = "1.00x (Baseline)";
            mathRepresentation = "∏(1 + More) = 1.00x";
        }
        else
        {
            var factorStrs = steps.Select(s => $"{s.Factor:F2} ({GetShortGemName(s.SourceName)})");
            string stepFactors = string.Join(" × ", factorStrs);
            string sign = netMorePercent >= 0f ? "+" : "";
            formulaString = $"{stepFactors} = {runningMultiplier:F2}x ({sign}{netMorePercent:F0}% More Damage)";

            var mathFactors = steps.Select(s => $"{s.Factor:F2}");
            mathRepresentation = $"∏(1 + More) = {string.Join(" × ", mathFactors)} = {runningMultiplier:F2}x";
        }

        return new DamageMultiplierBreakdown(
            TotalMultiplier: runningMultiplier,
            NetMorePercent: netMorePercent,
            Steps: steps,
            FormulaString: formulaString,
            MathRepresentation: mathRepresentation
        );
    }

    /// <summary>
    /// Previews the impact on the compounded damage multiplier prod(1 + More)
    /// if candidateSupport is socketed into the setup.
    /// </summary>
    public static MultiplierPreviewResult PreviewSupportImpact(
        SkillSocketSetup setup,
        GemDefinition candidateSupport,
        int? targetSlotIndex = null)
    {
        if (setup == null) throw new ArgumentNullException(nameof(setup));
        if (candidateSupport == null) throw new ArgumentNullException(nameof(candidateSupport));

        var currentBreakdown = CalculateBreakdown(setup.ActiveGem, setup.SocketedSupports);

        // Build projected supports list
        var projectedSupports = new List<GemDefinition>(setup.SocketedSupports);
        int targetIdx = targetSlotIndex ?? -1;

        if (targetIdx >= 0 && targetIdx < projectedSupports.Count)
        {
            projectedSupports[targetIdx] = candidateSupport;
        }
        else if (targetIdx >= 0 && targetIdx == projectedSupports.Count && projectedSupports.Count < SkillSocketSetup.MaxSupportSlots)
        {
            projectedSupports.Add(candidateSupport);
        }
        else if (projectedSupports.Count < SkillSocketSetup.MaxSupportSlots)
        {
            projectedSupports.Add(candidateSupport);
        }
        else if (projectedSupports.Count > 0)
        {
            // Replace last if full
            projectedSupports[^1] = candidateSupport;
        }

        var projectedBreakdown = CalculateBreakdown(setup.ActiveGem, projectedSupports);
        float deltaMult = projectedBreakdown.TotalMultiplier - currentBreakdown.TotalMultiplier;
        float deltaPercent = projectedBreakdown.NetMorePercent - currentBreakdown.NetMorePercent;

        return new MultiplierPreviewResult(
            CurrentBreakdown: currentBreakdown,
            ProjectedBreakdown: projectedBreakdown,
            DeltaMultiplier: deltaMult,
            DeltaPercent: deltaPercent,
            IsIncrease: deltaMult >= 0f
        );
    }

    // ─────────────────────────────────────────────
    //  Internal Helpers
    // ─────────────────────────────────────────────

    private static float ExtractMoreDamagePercent(GemDefinition gem)
    {
        if (gem.Modifiers == null) return 0f;

        float more = 0f;
        if (gem.Modifiers.TryGetValue("damageMultiplier", out _) && GetFloat(gem.Modifiers, "damageMultiplier", 0f) > 0f)
            more += GetFloat(gem.Modifiers, "damageMultiplier", 0f);
        if (gem.Modifiers.TryGetValue("morePhysicalDamagePercent", out _))
            more += GetFloat(gem.Modifiers, "morePhysicalDamagePercent", 0f);
        if (gem.Modifiers.TryGetValue("moreFireDamagePercent", out _))
            more += GetFloat(gem.Modifiers, "moreFireDamagePercent", 0f);
        if (gem.Modifiers.TryGetValue("moreAoeDamagePercent", out _))
            more += GetFloat(gem.Modifiers, "moreAoeDamagePercent", 0f);

        return more;
    }

    private static float ExtractLessDamagePercent(GemDefinition gem)
    {
        if (gem.Modifiers == null) return 0f;

        if (gem.Modifiers.TryGetValue("damageMultiplier", out _) && GetFloat(gem.Modifiers, "damageMultiplier", 0f) < 0f)
        {
            return MathF.Abs(GetFloat(gem.Modifiers, "damageMultiplier", 0f));
        }

        return 0f;
    }

    private static float ExtractManaMultiplier(GemDefinition gem)
    {
        if (gem.Modifiers == null) return 100f;
        return GetFloat(gem.Modifiers, "manaMultiplier", 100f);
    }

    private static string GetShortGemName(string name)
    {
        string trimmed = name.Replace(" Support", "", StringComparison.OrdinalIgnoreCase);
        return trimmed switch
        {
            "Greater Multiple Projectiles" => "GMP",
            "Lesser Multiple Projectiles" => "LMP",
            "Melee Physical Damage" => "Melee Phys",
            "Added Fire Damage" => "Added Fire",
            "Concentrated Effect" => "Conc Effect",
            "Ignite Proliferation" => "Ignite Prolif",
            _ => trimmed.Length > 10 ? trimmed.Substring(0, 9) : trimmed
        };
    }

    private static float GetFloat(Dictionary<string, object> dict, string key, float defaultValue)
    {
        if (!dict.TryGetValue(key, out var val) || val == null)
            return defaultValue;

        if (val is float f) return f;
        if (val is double d) return (float)d;
        if (val is int i) return i;
        if (val is long l) return l;
        if (val is System.Text.Json.JsonElement je)
        {
            if (je.ValueKind == System.Text.Json.JsonValueKind.Number && je.TryGetSingle(out var s))
                return s;
        }

        if (float.TryParse(val.ToString(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var parsed))
            return parsed;

        return defaultValue;
    }
}
