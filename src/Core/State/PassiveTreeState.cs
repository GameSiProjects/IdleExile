using System;
using System.Collections.Generic;
using System.Linq;
using IdleExile.Core.Models;

namespace IdleExile.Core.State;

/// <summary>
/// Aggregated total of one (stat, modifier-kind) pair across all allocated passives.
/// </summary>
public sealed record PassiveStatTotal(string Stat, string Mod, float Value, string Label)
{
    /// <summary>PoE-style display line, e.g. "24% increased maximum Life" or "+20% to all Elemental Resistances".</summary>
    public string DisplayText => PassiveStatFormatter.Format(Mod, Value, Label);
}

/// <summary>
/// Formats passive stat totals using PoE wording. Increased/Reduced (additive) and More/Less (multiplicative)
/// are always worded distinctly so they are never conflated.
/// </summary>
public static class PassiveStatFormatter
{
    public static string Format(string mod, float value, string label)
    {
        string v = value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
        return mod switch
        {
            PassiveModType.Increased => $"{v}% increased {label}",
            PassiveModType.Reduced => $"{v}% reduced {label}",
            PassiveModType.More => $"{v}% more {label}",
            PassiveModType.Less => $"{v}% less {label}",
            PassiveModType.Flat => $"+{v} to {label}",
            PassiveModType.FlatPercent => $"+{v}% to {label}",
            PassiveModType.Chance => $"{v}% {label}",
            _ => label
        };
    }
}

/// <summary>
/// Runtime graph &amp; allocation state of the Passive Skill Web.
/// Pure C# (no Godot dependency): adjacency, point budget, allocation/refund rules,
/// BFS shortest-path auto-pathing and stat aggregation into <see cref="StatModifierBucket"/>.
/// </summary>
public sealed class PassiveTreeState
{
    private readonly Dictionary<string, PassiveNodeDefinition> _nodes;
    private readonly Dictionary<string, List<string>> _adjacency;
    private readonly HashSet<string> _allocated = new();

    /// <summary>Fired whenever allocations, start class or point budget change.</summary>
    public event Action? OnTreeChanged;

    public PassiveTreeDefinition Definition { get; }

    /// <summary>Active class origin node id (always allocated, free of charge).</summary>
    public string StartNodeId { get; private set; }

    /// <summary>Character level used to derive the passive point budget.</summary>
    public int CharacterLevel { get; private set; } = 1;

    /// <summary>Total passive points earned: (Level - 1) × PointsPerLevel + BonusPoints.</summary>
    public int TotalPoints => Math.Max(0, (CharacterLevel - 1) * Definition.PointsPerLevel + Definition.BonusPoints);

    /// <summary>Points spent on allocated non-start nodes.</summary>
    public int SpentPoints => _allocated.Count(id => !_nodes[id].IsStart);

    /// <summary>Unspent passive points.</summary>
    public int AvailablePoints => Math.Max(0, TotalPoints - SpentPoints);

    public IReadOnlyCollection<string> AllocatedNodeIds => _allocated;
    public IReadOnlyList<PassiveNodeDefinition> Nodes => Definition.Nodes;
    public int NodeCount => _nodes.Count;

    public PassiveTreeState(PassiveTreeDefinition definition)
    {
        Definition = definition ?? throw new ArgumentNullException(nameof(definition));

        _nodes = new Dictionary<string, PassiveNodeDefinition>();
        foreach (var node in definition.Nodes)
        {
            _nodes[node.Id] = node;
        }

        // Adjacency lists preserve JSON order → deterministic BFS tie-breaking.
        _adjacency = _nodes.Keys.ToDictionary(id => id, _ => new List<string>());
        foreach (var edge in definition.Connections)
        {
            if (edge == null || edge.Length != 2) continue;
            string a = edge[0], b = edge[1];
            if (a == b || !_nodes.ContainsKey(a) || !_nodes.ContainsKey(b)) continue;
            if (!_adjacency[a].Contains(b)) _adjacency[a].Add(b);
            if (!_adjacency[b].Contains(a)) _adjacency[b].Add(a);
        }

        string? start = definition.DefaultStartNode;
        if (string.IsNullOrEmpty(start) || !_nodes.TryGetValue(start, out var startNode) || !startNode.IsStart)
        {
            start = definition.Nodes.FirstOrDefault(n => n.IsStart)?.Id
                ?? throw new InvalidOperationException("Passive tree must contain at least one Start node.");
        }

        StartNodeId = start;
        _allocated.Add(StartNodeId);
    }

    // ─────────────────────────────────────────────
    //  Queries
    // ─────────────────────────────────────────────

    public PassiveNodeDefinition? GetNode(string nodeId) =>
        nodeId != null && _nodes.TryGetValue(nodeId, out var node) ? node : null;

    public IReadOnlyList<string> GetNeighbors(string nodeId) =>
        _adjacency.TryGetValue(nodeId, out var list) ? list : Array.Empty<string>();

    public bool IsAllocated(string nodeId) => _allocated.Contains(nodeId);

    /// <summary>True when the node touches the allocated tree (regardless of points).</summary>
    public bool IsAdjacentToAllocated(string nodeId) =>
        GetNeighbors(nodeId).Any(_allocated.Contains);

    /// <summary>
    /// Nodes that can be walked through / allocated. Other classes' start nodes are walls.
    /// </summary>
    private bool IsPathable(string nodeId) =>
        _nodes.TryGetValue(nodeId, out var node) && !node.IsStart;

    public bool CanAllocate(string nodeId) =>
        _nodes.ContainsKey(nodeId)
        && !_allocated.Contains(nodeId)
        && IsPathable(nodeId)
        && AvailablePoints > 0
        && IsAdjacentToAllocated(nodeId);

    /// <summary>
    /// A node can be refunded if it is allocated, is not the class start,
    /// and removing it keeps every other allocated node connected to the start.
    /// </summary>
    public bool CanRefund(string nodeId)
    {
        if (!_allocated.Contains(nodeId) || nodeId == StartNodeId) return false;

        var reached = new HashSet<string> { StartNodeId };
        var queue = new Queue<string>();
        queue.Enqueue(StartNodeId);

        while (queue.Count > 0)
        {
            string current = queue.Dequeue();
            foreach (var next in _adjacency[current])
            {
                if (next == nodeId || !_allocated.Contains(next) || !reached.Add(next)) continue;
                queue.Enqueue(next);
            }
        }

        return reached.Count == _allocated.Count - 1;
    }

    /// <summary>
    /// Multi-source BFS from the allocated tree to <paramref name="targetId"/>.
    /// Returns ordered unallocated node ids to allocate (nearest first, target last),
    /// an empty list if the target is already allocated, or null if unreachable.
    /// </summary>
    public IReadOnlyList<string>? FindPath(string targetId)
    {
        if (!_nodes.ContainsKey(targetId)) return null;
        if (_allocated.Contains(targetId)) return Array.Empty<string>();
        if (!IsPathable(targetId)) return null;

        var previous = new Dictionary<string, string?>();
        var queue = new Queue<string>();

        // Seed sources in deterministic order (start first, then allocation order irrelevant for BFS layers).
        foreach (var id in _allocated.OrderBy(id => id == StartNodeId ? 0 : 1).ThenBy(id => id, StringComparer.Ordinal))
        {
            previous[id] = null;
            queue.Enqueue(id);
        }

        while (queue.Count > 0)
        {
            string current = queue.Dequeue();
            if (current == targetId) break;

            foreach (var next in _adjacency[current])
            {
                if (previous.ContainsKey(next) || !IsPathable(next)) continue;
                previous[next] = current;
                queue.Enqueue(next);
            }
        }

        if (!previous.ContainsKey(targetId)) return null;

        var path = new List<string>();
        string? step = targetId;
        while (step != null && !_allocated.Contains(step))
        {
            path.Add(step);
            step = previous[step];
        }

        path.Reverse();
        return path;
    }

    /// <summary>Point cost to reach &amp; allocate the target (0 = already allocated, -1 = unreachable).</summary>
    public int GetPathCost(string targetId) => FindPath(targetId)?.Count ?? -1;

    // ─────────────────────────────────────────────
    //  Mutations
    // ─────────────────────────────────────────────

    public bool Allocate(string nodeId)
    {
        if (!CanAllocate(nodeId)) return false;
        _allocated.Add(nodeId);
        OnTreeChanged?.Invoke();
        return true;
    }

    /// <summary>
    /// Auto-pathing: allocates every node on the shortest path to the target if the budget allows.
    /// All-or-nothing; returns false without side effects when unaffordable or unreachable.
    /// </summary>
    public bool AllocatePath(string targetId)
    {
        var path = FindPath(targetId);
        if (path == null || path.Count == 0 || path.Count > AvailablePoints) return false;

        foreach (var id in path)
        {
            _allocated.Add(id);
        }

        OnTreeChanged?.Invoke();
        return true;
    }

    public bool Refund(string nodeId)
    {
        if (!CanRefund(nodeId)) return false;
        _allocated.Remove(nodeId);
        OnTreeChanged?.Invoke();
        return true;
    }

    /// <summary>Updates the point budget from character level (clamped 1–100).</summary>
    public void SetCharacterLevel(int level)
    {
        int clamped = Math.Clamp(level, 1, 100);
        if (clamped == CharacterLevel) return;
        CharacterLevel = clamped;
        OnTreeChanged?.Invoke();
    }

    /// <summary>Switches class origin. Resets all allocations (full respec).</summary>
    public bool SetStartingClass(string startNodeId)
    {
        if (!_nodes.TryGetValue(startNodeId, out var node) || !node.IsStart) return false;

        StartNodeId = startNodeId;
        _allocated.Clear();
        _allocated.Add(StartNodeId);
        OnTreeChanged?.Invoke();
        return true;
    }

    /// <summary>Maps a starter archetype attribute ("Strength"/"Intelligence"/"Dexterity") to its class origin.</summary>
    public bool SetStartingClassByAttribute(string? attribute)
    {
        if (string.IsNullOrWhiteSpace(attribute)) return false;
        var start = Definition.Nodes.FirstOrDefault(n =>
            n.IsStart && string.Equals(n.Attribute, attribute, StringComparison.OrdinalIgnoreCase));
        return start != null && SetStartingClass(start.Id);
    }

    /// <summary>Clears every allocation except the class origin.</summary>
    public void ResetAllocations()
    {
        if (_allocated.Count == 1) return;
        _allocated.Clear();
        _allocated.Add(StartNodeId);
        OnTreeChanged?.Invoke();
    }

    // ─────────────────────────────────────────────
    //  Stat Aggregation
    // ─────────────────────────────────────────────

    public IEnumerable<PassiveNodeDefinition> AllocatedNodes =>
        _allocated.Select(id => _nodes[id]);

    public IReadOnlyList<PassiveNodeDefinition> AllocatedKeystones =>
        AllocatedNodes.Where(n => n.Type == PassiveNodeType.Keystone).OrderBy(n => n.Name).ToList();

    /// <summary>
    /// Sums allocated stats per (stat, modifier kind). Increased and More totals are kept in separate rows.
    /// Keystones are excluded (see <see cref="AllocatedKeystones"/>).
    /// </summary>
    public IReadOnlyList<PassiveStatTotal> GetStatTotals()
    {
        return AllocatedNodes
            .SelectMany(n => n.StatList)
            .Where(s => s.Mod != PassiveModType.Keystone)
            .GroupBy(s => (s.Stat, s.Mod))
            .Select(g => new PassiveStatTotal(g.Key.Stat, g.Key.Mod, g.Sum(s => s.Value), Definition.GetStatLabel(g.Key.Stat)))
            .OrderByDescending(t => t.Value)
            .ThenBy(t => t.Stat, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// Builds a PoE modifier bucket for one stat: Increased/Reduced summed additively,
    /// More/Less multiplied per source (never conflated).
    /// </summary>
    public StatModifierBucket BuildStatBucket(string statId)
    {
        var bucket = new StatModifierBucket();
        foreach (var stat in AllocatedNodes.SelectMany(n => n.StatList).Where(s => s.Stat == statId))
        {
            switch (stat.Mod)
            {
                case PassiveModType.Increased: bucket.AddIncreased(stat.Value); break;
                case PassiveModType.Reduced: bucket.AddReduced(stat.Value); break;
                case PassiveModType.More: bucket.AddMore(stat.Value); break;
                case PassiveModType.Less: bucket.AddLess(stat.Value); break;
            }
        }
        return bucket;
    }

    /// <summary>Sum of flat / flat-percent values for a stat (e.g. "+20% to Fire Resistance").</summary>
    public float GetFlatTotal(string statId) =>
        AllocatedNodes
            .SelectMany(n => n.StatList)
            .Where(s => s.Stat == statId && (s.Mod == PassiveModType.Flat || s.Mod == PassiveModType.FlatPercent))
            .Sum(s => s.Value);

    /// <summary>
    /// Builds an aggregated modifier bucket containing all allocated passive damage and offensive multipliers.
    /// </summary>
    public StatModifierBucket BuildAggregatedOffensiveBucket()
    {
        var bucket = new StatModifierBucket();
        var offensiveStats = new HashSet<string> { "damage", "physical_damage", "fire_damage", "spell_damage", "attack_damage", "melee_damage", "projectile_damage", "burning_damage" };

        foreach (var stat in AllocatedNodes.SelectMany(n => n.StatList).Where(s => offensiveStats.Contains(s.Stat)))
        {
            switch (stat.Mod)
            {
                case PassiveModType.Increased: bucket.AddIncreased(stat.Value); break;
                case PassiveModType.Reduced: bucket.AddReduced(stat.Value); break;
                case PassiveModType.More: bucket.AddMore(stat.Value); break;
                case PassiveModType.Less: bucket.AddLess(stat.Value); break;
            }
        }
        return bucket;
    }
}

