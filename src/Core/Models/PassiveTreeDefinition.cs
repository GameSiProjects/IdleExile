using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace IdleExile.Core.Models;

/// <summary>
/// Node archetypes on the Passive Skill Web (docs/04_gems_and_passives.md §4.1).
/// </summary>
public enum PassiveNodeType
{
    /// <summary>Class origin. Free, never allocatable by another class.</summary>
    Start,
    /// <summary>Minor node: small baseline stat (+8% Life, +12% Physical Damage).</summary>
    Minor,
    /// <summary>Notable: specialised cluster payoff.</summary>
    Notable,
    /// <summary>Keystone: build-defining mechanic change (CI, RT, Iron Reflexes...).</summary>
    Keystone
}

/// <summary>
/// Canonical modifier kinds for passive stats. Keeps PoE arithmetic explicit:
/// Increased/Reduced are additive, More/Less are multiplicative.
/// </summary>
public static class PassiveModType
{
    public const string Increased = "increased";
    public const string Reduced = "reduced";
    public const string More = "more";
    public const string Less = "less";
    public const string Flat = "flat";
    public const string FlatPercent = "flat_percent";
    public const string Chance = "chance";
    public const string Keystone = "keystone";
}

/// <summary>A single stat line granted by a passive node.</summary>
public sealed record PassiveStatDefinition(
    [property: JsonPropertyName("stat")] string Stat,
    [property: JsonPropertyName("mod")] string Mod,
    [property: JsonPropertyName("value")] float Value,
    [property: JsonPropertyName("text")] string Text
);

/// <summary>A node on the Passive Skill Web, positioned in tree-space units.</summary>
public sealed record PassiveNodeDefinition(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("type")] PassiveNodeType Type,
    [property: JsonPropertyName("cluster")] string Cluster,
    [property: JsonPropertyName("x")] float X,
    [property: JsonPropertyName("y")] float Y,
    [property: JsonPropertyName("stats")] PassiveStatDefinition[]? Stats = null,
    [property: JsonPropertyName("tags")] string[]? Tags = null,
    [property: JsonPropertyName("className")] string? ClassName = null,
    [property: JsonPropertyName("attribute")] string? Attribute = null,
    [property: JsonPropertyName("description")] string? Description = null
)
{
    [JsonIgnore]
    public IReadOnlyList<PassiveStatDefinition> StatList => Stats ?? Array.Empty<PassiveStatDefinition>();

    [JsonIgnore]
    public IReadOnlyList<string> TagList => Tags ?? Array.Empty<string>();

    [JsonIgnore]
    public bool IsStart => Type == PassiveNodeType.Start;
}

/// <summary>A themed constellation (cluster) of passive nodes.</summary>
public sealed record PassiveClusterDefinition(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("color")] string Color,
    [property: JsonPropertyName("labelX")] float LabelX = 0f,
    [property: JsonPropertyName("labelY")] float LabelY = 0f
);

/// <summary>
/// Root of data/passive_tree.json. Pure data; graph logic lives in <see cref="IdleExile.Core.State.PassiveTreeState"/>.
/// </summary>
public sealed record PassiveTreeDefinition(
    [property: JsonPropertyName("pointsPerLevel")] int PointsPerLevel,
    [property: JsonPropertyName("bonusPoints")] int BonusPoints,
    [property: JsonPropertyName("defaultStartNode")] string? DefaultStartNode,
    [property: JsonPropertyName("statLabels")] Dictionary<string, string>? StatLabels,
    [property: JsonPropertyName("clusters")] List<PassiveClusterDefinition> Clusters,
    [property: JsonPropertyName("nodes")] List<PassiveNodeDefinition> Nodes,
    [property: JsonPropertyName("connections")] List<string[]> Connections
)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter() }
    };

    /// <summary>
    /// Parses a passive tree JSON document. Pure C# (no Godot file APIs) so it is headless testable.
    /// </summary>
    public static PassiveTreeDefinition Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            throw new ArgumentException("Passive tree JSON is empty.", nameof(json));

        var tree = JsonSerializer.Deserialize<PassiveTreeDefinition>(json, JsonOptions)
            ?? throw new JsonException("Passive tree JSON deserialized to null.");

        return tree with
        {
            Clusters = tree.Clusters ?? new List<PassiveClusterDefinition>(),
            Nodes = tree.Nodes ?? new List<PassiveNodeDefinition>(),
            Connections = tree.Connections ?? new List<string[]>()
        };
    }

    /// <summary>Resolves the display label for a stat id (falls back to a humanised id).</summary>
    public string GetStatLabel(string statId)
    {
        if (StatLabels != null && StatLabels.TryGetValue(statId, out var label) && !string.IsNullOrWhiteSpace(label))
            return label;
        return statId.Replace('_', ' ');
    }

    /// <summary>Finds a cluster definition by id.</summary>
    public PassiveClusterDefinition? GetCluster(string clusterId) =>
        Clusters.FirstOrDefault(c => c.Id == clusterId);

    /// <summary>
    /// Validates referential integrity: unique node ids, valid cluster refs, valid connection endpoints.
    /// Returns a list of human-readable problems (empty when valid).
    /// </summary>
    public IReadOnlyList<string> Validate()
    {
        var problems = new List<string>();
        var ids = new HashSet<string>();
        var clusterIds = new HashSet<string>(Clusters.Select(c => c.Id));

        foreach (var node in Nodes)
        {
            if (!ids.Add(node.Id))
                problems.Add($"Duplicate node id '{node.Id}'.");
            if (!clusterIds.Contains(node.Cluster))
                problems.Add($"Node '{node.Id}' references unknown cluster '{node.Cluster}'.");
        }

        foreach (var edge in Connections)
        {
            if (edge == null || edge.Length != 2)
            {
                problems.Add("Connection entries must contain exactly 2 node ids.");
                continue;
            }

            if (!ids.Contains(edge[0])) problems.Add($"Connection references unknown node '{edge[0]}'.");
            if (!ids.Contains(edge[1])) problems.Add($"Connection references unknown node '{edge[1]}'.");
            if (edge[0] == edge[1]) problems.Add($"Self-connection on node '{edge[0]}'.");
        }

        if (!string.IsNullOrEmpty(DefaultStartNode) && !Nodes.Any(n => n.Id == DefaultStartNode && n.IsStart))
            problems.Add($"DefaultStartNode '{DefaultStartNode}' is not a Start node.");

        return problems;
    }
}
