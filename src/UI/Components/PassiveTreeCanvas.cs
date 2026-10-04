using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using IdleExile.Core.Models;
using IdleExile.Core.State;

namespace IdleExile.UI.Components;

/// <summary>
/// Interactive 2D procedural canvas for the Passive Skill Web.
/// Renders constellation clusters, celestial connections, nodes (Minor, Notable, Keystone, Start),
/// search highlight halos, pathfinding previews, and supports smooth pan &amp; zoom.
/// Follows gl_compatibility and low-processor guidelines (pure vector draw calls).
/// </summary>
public partial class PassiveTreeCanvas : Control
{
    // ─────────────────────────────────────────────
    //  Signals & Events
    // ─────────────────────────────────────────────

    public event Action<PassiveNodeDefinition>? OnNodeSelected;
    public event Action<PassiveNodeDefinition>? OnNodeDoubleClicked;
    public event Action<PassiveNodeDefinition?>? OnNodeHovered;
    public event Action<float>? OnZoomChanged;

    // ─────────────────────────────────────────────
    //  State
    // ─────────────────────────────────────────────

    public PassiveTreeState? TreeState { get; private set; }

    public Vector2 PanOffset { get; set; } = new(0f, 40f);
    public float Zoom { get; private set; } = 0.95f;

    public const float MinZoom = 0.45f;
    public const float MaxZoom = 2.40f;
    public const float ZoomStep = 0.15f;

    public PassiveNodeDefinition? SelectedNode { get; private set; }
    public PassiveNodeDefinition? HoveredNode { get; private set; }

    private readonly HashSet<string> _highlightedNodeIds = new();
    private readonly HashSet<string> _pathPreviewNodeIds = new();

    private bool _isDragging = false;
    private Vector2 _dragStartMousePos = Vector2.Zero;
    private Vector2 _dragStartPanOffset = Vector2.Zero;
    private float _pulsePhase = 0f;

    // Palette Colors (PoE Dark Fantasy Palette)
    private static readonly Color ColorBackdrop = new(0.045f, 0.048f, 0.062f, 1f);
    private static readonly Color ColorGridDots = new(0.14f, 0.16f, 0.22f, 0.35f);
    private static readonly Color ColorEdgeMuted = new(0.19f, 0.21f, 0.27f, 0.65f);
    private static readonly Color ColorEdgeAllocated = new(0.92f, 0.70f, 0.26f, 0.95f);
    private static readonly Color ColorEdgeAllocatedGlow = new(0.92f, 0.68f, 0.22f, 0.28f);
    private static readonly Color ColorEdgePath = new(0.20f, 0.85f, 1.0f, 0.90f);

    private static readonly Color ColorStartMarauder = new(0.88f, 0.34f, 0.22f, 1f);
    private static readonly Color ColorStartWitch = new(0.42f, 0.55f, 1.0f, 1f);
    private static readonly Color ColorStartRanger = new(0.33f, 0.84f, 0.56f, 1f);

    private static readonly Color ColorNodeAllocated = new(0.94f, 0.76f, 0.28f, 1f);
    private static readonly Color ColorNodeCanAllocate = new(0.90f, 0.80f, 0.45f, 0.95f);
    private static readonly Color ColorNodeUnallocated = new(0.11f, 0.12f, 0.15f, 1f);
    private static readonly Color ColorNodeRimMuted = new(0.35f, 0.38f, 0.45f, 0.8f);

    private static readonly Color ColorSearchHighlight = new(0.25f, 0.92f, 1.0f, 0.85f);
    private static readonly Color ColorSelectionRing = new(1.0f, 1.0f, 1.0f, 0.95f);

    public override void _Ready()
    {
        ClipContents = true;
        MouseFilter = MouseFilterEnum.Stop;
    }

    public void Initialize(PassiveTreeState treeState)
    {
        TreeState = treeState ?? throw new ArgumentNullException(nameof(treeState));
        TreeState.OnTreeChanged += QueueRedraw;
        ResetView();
        QueueRedraw();
    }

    public void ResetView()
    {
        Zoom = 0.95f;
        // Center around the current start node if available
        if (TreeState != null && TreeState.GetNode(TreeState.StartNodeId) is { } startNode)
        {
            PanOffset = new Vector2(-startNode.X * Zoom, -startNode.Y * Zoom);
        }
        else
        {
            PanOffset = Vector2.Zero;
        }

        OnZoomChanged?.Invoke(Zoom);
        QueueRedraw();
    }

    public void SetZoom(float newZoom, Vector2? pivotScreenPos = null)
    {
        float clamped = Mathf.Clamp(newZoom, MinZoom, MaxZoom);
        if (Mathf.IsEqualApprox(clamped, Zoom)) return;

        Vector2 pivot = pivotScreenPos ?? (Size * 0.5f);
        Vector2 worldBefore = ScreenToWorld(pivot);

        Zoom = clamped;
        Vector2 screenAfter = WorldToScreen(worldBefore);
        PanOffset += (pivot - screenAfter);

        OnZoomChanged?.Invoke(Zoom);
        QueueRedraw();
    }

    public void ZoomIn() => SetZoom(Zoom + ZoomStep);
    public void ZoomOut() => SetZoom(Zoom - ZoomStep);

    public void SetSearchHighlights(IEnumerable<string>? matchingNodeIds)
    {
        _highlightedNodeIds.Clear();
        if (matchingNodeIds != null)
        {
            foreach (var id in matchingNodeIds) _highlightedNodeIds.Add(id);
        }
        QueueRedraw();
    }

    public void SetPathPreview(IEnumerable<string>? pathNodeIds)
    {
        _pathPreviewNodeIds.Clear();
        if (pathNodeIds != null)
        {
            foreach (var id in pathNodeIds) _pathPreviewNodeIds.Add(id);
        }
        QueueRedraw();
    }

    public void SelectNode(PassiveNodeDefinition? node)
    {
        SelectedNode = node;
        OnNodeSelected?.Invoke(node!);

        if (node != null && TreeState != null)
        {
            // Auto-preview path to selected node if unallocated
            if (!TreeState.IsAllocated(node.Id))
            {
                var path = TreeState.FindPath(node.Id);
                SetPathPreview(path);
            }
            else
            {
                SetPathPreview(null);
            }
        }
        else
        {
            SetPathPreview(null);
        }

        QueueRedraw();
    }

    public Vector2 WorldToScreen(Vector2 worldPos)
    {
        Vector2 center = Size * 0.5f;
        return center + PanOffset + worldPos * Zoom;
    }

    public Vector2 ScreenToWorld(Vector2 screenPos)
    {
        Vector2 center = Size * 0.5f;
        return (screenPos - center - PanOffset) / Zoom;
    }

    public PassiveNodeDefinition? GetNodeAtScreenPos(Vector2 screenPos)
    {
        if (TreeState == null) return null;

        foreach (var node in TreeState.Nodes)
        {
            Vector2 sPos = WorldToScreen(new Vector2(node.X, node.Y));
            float hitRadius = GetNodeHitRadius(node.Type) * Zoom;
            if (screenPos.DistanceTo(sPos) <= hitRadius)
            {
                return node;
            }
        }

        return null;
    }

    private static float GetNodeHitRadius(PassiveNodeType type) => type switch
    {
        PassiveNodeType.Start => 18f,
        PassiveNodeType.Keystone => 18f,
        PassiveNodeType.Notable => 14f,
        _ => 10f
    };

    // ─────────────────────────────────────────────
    //  Input Handling (Pan & Zoom & Selection)
    // ─────────────────────────────────────────────

    public override void _GuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton mb)
        {
            if (mb.ButtonIndex == MouseButton.Left)
            {
                if (mb.Pressed)
                {
                    var clickedNode = GetNodeAtScreenPos(mb.Position);
                    if (clickedNode != null)
                    {
                        if (mb.DoubleClick)
                        {
                            OnNodeDoubleClicked?.Invoke(clickedNode);
                        }
                        else
                        {
                            SelectNode(clickedNode);
                        }
                    }
                    else
                    {
                        // Clicked empty canvas space: start panning
                        _isDragging = true;
                        _dragStartMousePos = mb.Position;
                        _dragStartPanOffset = PanOffset;
                    }
                }
                else
                {
                    _isDragging = false;
                }
            }
            else if (mb.ButtonIndex == MouseButton.Middle)
            {
                if (mb.Pressed)
                {
                    _isDragging = true;
                    _dragStartMousePos = mb.Position;
                    _dragStartPanOffset = PanOffset;
                }
                else
                {
                    _isDragging = false;
                }
            }
            else if (mb.ButtonIndex == MouseButton.WheelUp && mb.Pressed)
            {
                SetZoom(Zoom + ZoomStep, mb.Position);
                AcceptEvent();
            }
            else if (mb.ButtonIndex == MouseButton.WheelDown && mb.Pressed)
            {
                SetZoom(Zoom - ZoomStep, mb.Position);
                AcceptEvent();
            }
        }
        else if (@event is InputEventMouseMotion mm)
        {
            if (_isDragging)
            {
                PanOffset = _dragStartPanOffset + (mm.Position - _dragStartMousePos);
                QueueRedraw();
            }
            else
            {
                // Hover tracking
                var nodeUnderMouse = GetNodeAtScreenPos(mm.Position);
                if (nodeUnderMouse != HoveredNode)
                {
                    HoveredNode = nodeUnderMouse;
                    OnNodeHovered?.Invoke(HoveredNode);

                    // Update path preview if hovering an unallocated node and no node selected
                    if (SelectedNode == null && TreeState != null)
                    {
                        if (HoveredNode != null && !TreeState.IsAllocated(HoveredNode.Id))
                        {
                            SetPathPreview(TreeState.FindPath(HoveredNode.Id));
                        }
                        else
                        {
                            SetPathPreview(null);
                        }
                    }

                    QueueRedraw();
                }
            }
        }
    }

    public override void _Process(double delta)
    {
        if (!IsVisibleInTree()) return;

        _pulsePhase += (float)delta * 3.5f;
        if (_pulsePhase > Mathf.Tau) _pulsePhase -= Mathf.Tau;

        // Redraw when highlights or selections are active to animate pulses
        if (_highlightedNodeIds.Count > 0 || _pathPreviewNodeIds.Count > 0 || (TreeState?.AvailablePoints ?? 0) > 0)
        {
            QueueRedraw();
        }
    }

    // ─────────────────────────────────────────────
    //  Procedural Rendering (_Draw)
    // ─────────────────────────────────────────────

    public override void _Draw()
    {
        // 1. Fill backdrop
        DrawRect(new Rect2(Vector2.Zero, Size), ColorBackdrop);

        // 2. Draw subtle celestial background grid dots
        DrawCelestialGrid();

        if (TreeState == null) return;

        // 3. Draw constellation cluster watermarks / labels
        DrawClusters();

        // 4. Draw connection edges
        DrawConnections();

        // 5. Draw nodes (Start, Minor, Notable, Keystone)
        DrawNodes();
    }

    private void DrawCelestialGrid()
    {
        float gridSpacing = 60f * Zoom;
        if (gridSpacing < 20f) return;

        Vector2 center = Size * 0.5f;
        float startX = (center.X + PanOffset.X) % gridSpacing;
        float startY = (center.Y + PanOffset.Y) % gridSpacing;

        for (float x = startX; x < Size.X; x += gridSpacing)
        {
            for (float y = startY; y < Size.Y; y += gridSpacing)
            {
                DrawCircle(new Vector2(x, y), 1.0f, ColorGridDots);
            }
        }
    }

    private void DrawClusters()
    {
        if (TreeState?.Definition.Clusters == null) return;

        var font = ThemeDB.FallbackFont;
        int fontSize = Math.Clamp((int)(11f * Zoom), 8, 14);

        foreach (var cluster in TreeState.Definition.Clusters)
        {
            Vector2 screenPos = WorldToScreen(new Vector2(cluster.LabelX, cluster.LabelY));
            if (!GetRect().Grow(80f).HasPoint(screenPos)) continue;

            Color clusterCol = Color.FromString(cluster.Color, new Color(0.6f, 0.6f, 0.7f));
            Color labelCol = new(clusterCol.R, clusterCol.G, clusterCol.B, 0.45f);

            // Subtle cluster watermark text
            DrawString(font, screenPos + new Vector2(-40f * Zoom, 0f), cluster.Name.ToUpperInvariant(), HorizontalAlignment.Center, -1, fontSize, labelCol);
        }
    }

    private void DrawConnections()
    {
        if (TreeState == null) return;

        foreach (var edge in TreeState.Definition.Connections)
        {
            if (edge == null || edge.Length != 2) continue;
            var nodeA = TreeState.GetNode(edge[0]);
            var nodeB = TreeState.GetNode(edge[1]);
            if (nodeA == null || nodeB == null) continue;

            Vector2 posA = WorldToScreen(new Vector2(nodeA.X, nodeA.Y));
            Vector2 posB = WorldToScreen(new Vector2(nodeB.X, nodeB.Y));

            bool bothAllocated = TreeState.IsAllocated(nodeA.Id) && TreeState.IsAllocated(nodeB.Id);
            bool isPathPreview = (_pathPreviewNodeIds.Contains(nodeA.Id) && _pathPreviewNodeIds.Contains(nodeB.Id))
                              || (_pathPreviewNodeIds.Contains(nodeA.Id) && TreeState.IsAllocated(nodeB.Id))
                              || (_pathPreviewNodeIds.Contains(nodeB.Id) && TreeState.IsAllocated(nodeA.Id));

            if (bothAllocated)
            {
                // Glowing golden connection
                DrawLine(posA, posB, ColorEdgeAllocatedGlow, 5.0f * Zoom, antialiased: true);
                DrawLine(posA, posB, ColorEdgeAllocated, 2.5f * Zoom, antialiased: true);
            }
            else if (isPathPreview)
            {
                // Pulsing cyan auto-path preview connection
                float pulse = 0.6f + MathF.Sin(_pulsePhase) * 0.4f;
                DrawLine(posA, posB, new Color(ColorEdgePath.R, ColorEdgePath.G, ColorEdgePath.B, pulse), 2.8f * Zoom, antialiased: true);
            }
            else
            {
                // Unallocated subtle connection
                DrawLine(posA, posB, ColorEdgeMuted, 1.4f * Zoom, antialiased: true);
            }
        }
    }

    private void DrawNodes()
    {
        if (TreeState == null) return;

        // Render in order: Minor -> Notable -> Keystone -> Start (so important nodes draw on top)
        var orderedNodes = TreeState.Nodes
            .OrderBy(n => n.Type switch
            {
                PassiveNodeType.Minor => 0,
                PassiveNodeType.Notable => 1,
                PassiveNodeType.Keystone => 2,
                _ => 3
            });

        foreach (var node in orderedNodes)
        {
            Vector2 pos = WorldToScreen(new Vector2(node.X, node.Y));
            if (!GetRect().Grow(40f).HasPoint(pos)) continue;

            bool isAllocated = TreeState.IsAllocated(node.Id);
            bool canAllocate = TreeState.CanAllocate(node.Id);
            bool isHighlighted = _highlightedNodeIds.Contains(node.Id);
            bool isInPath = _pathPreviewNodeIds.Contains(node.Id);
            bool isSelected = SelectedNode?.Id == node.Id;
            bool isHovered = HoveredNode?.Id == node.Id;

            // 1. Search Highlight Pulse Halo
            if (isHighlighted)
            {
                float pulseScale = 1.0f + MathF.Sin(_pulsePhase * 1.5f) * 0.25f;
                float highlightRadius = (GetNodeRadius(node.Type) + 8f) * Zoom * pulseScale;
                DrawArc(pos, highlightRadius, 0f, Mathf.Tau, 24, ColorSearchHighlight, 2.4f * Zoom, antialiased: true);
                DrawCircle(pos, highlightRadius * 0.85f, new Color(0.2f, 0.85f, 1.0f, 0.15f));
            }

            // 2. Path Preview Halo
            if (isInPath && !isAllocated)
            {
                float pathPulse = 0.5f + MathF.Sin(_pulsePhase) * 0.35f;
                DrawArc(pos, (GetNodeRadius(node.Type) + 4f) * Zoom, 0f, Mathf.Tau, 20, new Color(ColorEdgePath.R, ColorEdgePath.G, ColorEdgePath.B, pathPulse), 2.0f * Zoom, antialiased: true);
            }

            // 3. Draw Specific Node Glyphs
            switch (node.Type)
            {
                case PassiveNodeType.Start:
                    DrawStartNode(pos, node, isAllocated, isSelected, isHovered);
                    break;
                case PassiveNodeType.Keystone:
                    DrawKeystoneNode(pos, node, isAllocated, canAllocate, isSelected, isHovered);
                    break;
                case PassiveNodeType.Notable:
                    DrawNotableNode(pos, node, isAllocated, canAllocate, isSelected, isHovered);
                    break;
                default:
                    DrawMinorNode(pos, node, isAllocated, canAllocate, isSelected, isHovered);
                    break;
            }

            // 4. Selection Brackets
            if (isSelected)
            {
                float selRadius = (GetNodeRadius(node.Type) + 5f) * Zoom;
                DrawArc(pos, selRadius, 0f, Mathf.Tau, 32, ColorSelectionRing, 1.8f * Zoom, antialiased: true);
            }
        }
    }

    private static float GetNodeRadius(PassiveNodeType type) => type switch
    {
        PassiveNodeType.Start => 14f,
        PassiveNodeType.Keystone => 14f,
        PassiveNodeType.Notable => 9.5f,
        _ => 6.0f
    };

    private void DrawMinorNode(Vector2 pos, PassiveNodeDefinition node, bool isAllocated, bool canAllocate, bool isSelected, bool isHovered)
    {
        float r = 6.0f * Zoom;

        Color fill = isAllocated ? ColorNodeAllocated : (isHovered ? new Color(0.22f, 0.25f, 0.32f) : ColorNodeUnallocated);
        Color rim = isAllocated ? new Color(1f, 0.88f, 0.45f) : (canAllocate ? ColorNodeCanAllocate : ColorNodeRimMuted);
        float rimWidth = isAllocated ? 1.8f * Zoom : (canAllocate ? 1.6f * Zoom : 1.2f * Zoom);

        if (canAllocate && !isAllocated)
        {
            float pulse = 0.7f + MathF.Sin(_pulsePhase) * 0.3f;
            rim = new Color(rim.R, rim.G, rim.B, pulse);
        }

        DrawCircle(pos, r, fill);
        DrawArc(pos, r, 0f, Mathf.Tau, 16, rim, rimWidth, antialiased: true);
    }

    private void DrawNotableNode(Vector2 pos, PassiveNodeDefinition node, bool isAllocated, bool canAllocate, bool isSelected, bool isHovered)
    {
        float r = 9.5f * Zoom;

        Color fill = isAllocated ? ColorNodeAllocated : (isHovered ? new Color(0.24f, 0.28f, 0.36f) : ColorNodeUnallocated);
        Color rim = isAllocated ? new Color(1.0f, 0.92f, 0.55f) : (canAllocate ? ColorNodeCanAllocate : new Color(0.70f, 0.73f, 0.82f));
        float rimWidth = isAllocated ? 2.4f * Zoom : 1.6f * Zoom;

        if (isAllocated)
        {
            // Outer golden glow halo
            DrawCircle(pos, r * 1.35f, new Color(0.92f, 0.68f, 0.22f, 0.25f));
        }

        DrawCircle(pos, r, fill);
        DrawArc(pos, r, 0f, Mathf.Tau, 20, rim, rimWidth, antialiased: true);

        // Inner decorative circle
        DrawArc(pos, r * 0.55f, 0f, Mathf.Tau, 16, rim, 1.2f * Zoom, antialiased: true);
    }

    private void DrawKeystoneNode(Vector2 pos, PassiveNodeDefinition node, bool isAllocated, bool canAllocate, bool isSelected, bool isHovered)
    {
        float r = 14f * Zoom;

        Color fill = isAllocated ? new Color(0.85f, 0.25f, 0.15f) : (isHovered ? new Color(0.25f, 0.22f, 0.35f) : ColorNodeUnallocated);
        Color rim = isAllocated ? new Color(1.0f, 0.85f, 0.35f) : (canAllocate ? ColorNodeCanAllocate : new Color(0.85f, 0.75f, 0.45f));

        if (isAllocated)
        {
            // Radiant golden halo
            DrawCircle(pos, r * 1.5f, new Color(0.95f, 0.65f, 0.20f, 0.35f));
        }

        DrawCircle(pos, r, fill);
        DrawArc(pos, r, 0f, Mathf.Tau, 28, rim, 2.5f * Zoom, antialiased: true);
        DrawArc(pos, r * 0.72f, 0f, Mathf.Tau, 24, rim, 1.4f * Zoom, antialiased: true);

        // 4 Cardinal Star Points (Keystone cross)
        for (int i = 0; i < 4; i++)
        {
            float a = i * Mathf.Pi * 0.5f;
            Vector2 p1 = pos + new Vector2(MathF.Cos(a), MathF.Sin(a)) * (r - 2f * Zoom);
            Vector2 p2 = pos + new Vector2(MathF.Cos(a), MathF.Sin(a)) * (r + 5f * Zoom);
            DrawLine(p1, p2, rim, 2.0f * Zoom, antialiased: true);
        }
    }

    private void DrawStartNode(Vector2 pos, PassiveNodeDefinition node, bool isAllocated, bool isSelected, bool isHovered)
    {
        float r = 13.5f * Zoom;

        Color classColor = node.Attribute switch
        {
            "Strength" => ColorStartMarauder,
            "Intelligence" => ColorStartWitch,
            "Dexterity" => ColorStartRanger,
            _ => ColorNodeAllocated
        };

        // Class crest diamond
        Vector2[] diamond =
        {
            pos + new Vector2(0, -r),
            pos + new Vector2(r, 0),
            pos + new Vector2(0, r),
            pos + new Vector2(-r, 0)
        };

        DrawColoredPolygon(diamond, isAllocated ? classColor : new Color(0.18f, 0.20f, 0.25f));
        DrawPolyline(diamond.Concat(new[] { diamond[0] }).ToArray(), isAllocated ? new Color(1f, 1f, 1f, 0.9f) : ColorNodeRimMuted, 2.0f * Zoom, antialiased: true);

        // Center dot
        DrawCircle(pos, 3.5f * Zoom, isAllocated ? Colors.White : classColor);
    }
}
