using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using IdleExile.Core.Models;
using IdleExile.Core.Services;
using IdleExile.Core.State;

namespace IdleExile.UI.Components;

/// <summary>
/// Controller for Task 5.1: Mini-Window Passive Skill Web View (`PassiveTreeView.tscn`).
/// Embeds <see cref="PassiveTreeCanvas"/> with interactive constellation clusters,
/// search bar filter with dynamic match halos, auto-pathing execution,
/// sticky node inspector drawer, and passive stat totals summary.
/// Conforms to the 410px vertical pixel budget in TabWorkspaceContainer.
/// </summary>
public partial class PassiveTreeView : Control
{
    // ─────────────────────────────────────────────
    //  State & Dependencies
    // ─────────────────────────────────────────────

    public DatabaseLoader? Database { get; private set; }
    public PlayerState? Player { get; private set; }
    public PassiveTreeState? TreeState { get; private set; }

    public string CurrentSearchQuery { get; private set; } = string.Empty;

    // ─────────────────────────────────────────────
    //  Node References
    // ─────────────────────────────────────────────

    private LineEdit? _searchInput;
    private Button? _clearSearchBtn;
    private Label? _matchCountLabel;
    private Label? _pointsBadgeLabel;
    private Button? _zoomInBtn;
    private Button? _zoomOutBtn;
    private Button? _resetViewBtn;
    private Button? _toggleStatsBtn;
    private Button? _respecBtn;

    private PassiveTreeCanvas? _canvas;

    // Sticky Node Inspector Drawer
    private PanelContainer? _nodeInspectorPanel;
    private Label? _nodeTypeBadge;
    private Label? _nodeNameLabel;
    private Label? _nodeClusterLabel;
    private VBoxContainer? _nodeStatsVBox;
    private Button? _nodeActionBtn;
    private Label? _actionHintLabel;

    // Stats Summary Overlay / Drawer
    private PanelContainer? _statsSummaryDrawer;
    private VBoxContainer? _statsListVBox;
    private Button? _closeStatsBtn;

    public PassiveTreeCanvas? Canvas => _canvas;

    public override void _Ready()
    {
        // 1. Bind Header Controls
        _searchInput = GetNodeOrNull<LineEdit>("%SearchInput");
        _clearSearchBtn = GetNodeOrNull<Button>("%ClearSearchBtn");
        _matchCountLabel = GetNodeOrNull<Label>("%MatchCountLabel");
        _pointsBadgeLabel = GetNodeOrNull<Label>("%PointsBadgeLabel");
        _zoomInBtn = GetNodeOrNull<Button>("%ZoomInBtn");
        _zoomOutBtn = GetNodeOrNull<Button>("%ZoomOutBtn");
        _resetViewBtn = GetNodeOrNull<Button>("%ResetViewBtn");
        _toggleStatsBtn = GetNodeOrNull<Button>("%ToggleStatsBtn");
        _respecBtn = GetNodeOrNull<Button>("%RespecBtn");

        // 2. Bind Canvas
        _canvas = GetNodeOrNull<PassiveTreeCanvas>("%PassiveTreeCanvas");

        // 3. Bind Inspector Drawer
        _nodeInspectorPanel = GetNodeOrNull<PanelContainer>("%NodeInspectorPanel");
        _nodeTypeBadge = GetNodeOrNull<Label>("%NodeTypeBadge");
        _nodeNameLabel = GetNodeOrNull<Label>("%NodeNameLabel");
        _nodeClusterLabel = GetNodeOrNull<Label>("%NodeClusterLabel");
        _nodeStatsVBox = GetNodeOrNull<VBoxContainer>("%NodeStatsVBox");
        _nodeActionBtn = GetNodeOrNull<Button>("%NodeActionBtn");
        _actionHintLabel = GetNodeOrNull<Label>("%ActionHintLabel");

        // 4. Bind Stats Summary Drawer
        _statsSummaryDrawer = GetNodeOrNull<PanelContainer>("%StatsSummaryDrawer");
        _statsListVBox = GetNodeOrNull<VBoxContainer>("%StatsListVBox");
        _closeStatsBtn = GetNodeOrNull<Button>("%CloseStatsBtn");

        // Wire Header Events
        if (_searchInput != null)
        {
            _searchInput.TextChanged += OnSearchInputChanged;
        }

        if (_clearSearchBtn != null)
        {
            _clearSearchBtn.Pressed += ClearSearch;
        }

        if (_zoomInBtn != null) _zoomInBtn.Pressed += () => _canvas?.ZoomIn();
        if (_zoomOutBtn != null) _zoomOutBtn.Pressed += () => _canvas?.ZoomOut();
        if (_resetViewBtn != null) _resetViewBtn.Pressed += () => _canvas?.ResetView();

        if (_toggleStatsBtn != null)
        {
            _toggleStatsBtn.Pressed += ToggleStatsSummary;
        }

        if (_closeStatsBtn != null)
        {
            _closeStatsBtn.Pressed += () => SetStatsSummaryVisible(false);
        }

        if (_respecBtn != null)
        {
            _respecBtn.Pressed += OnRespecPressed;
        }

        // Wire Canvas Events
        if (_canvas != null)
        {
            _canvas.OnNodeSelected += OnCanvasNodeSelected;
            _canvas.OnNodeDoubleClicked += OnCanvasNodeDoubleClicked;
        }

        // Wire Inspector Action Button
        if (_nodeActionBtn != null)
        {
            _nodeActionBtn.Pressed += OnNodeActionPressed;
        }
    }

    /// <summary>
    /// Initializes PassiveTreeView with runtime PlayerState and DatabaseLoader.
    /// </summary>
    public void Initialize(PlayerState player, DatabaseLoader db)
    {
        Player = player ?? throw new ArgumentNullException(nameof(player));
        Database = db ?? throw new ArgumentNullException(nameof(db));

        var treeDef = db.PassiveTree;
        if (treeDef == null)
        {
            GD.PrintErr("[PassiveTreeView] DatabaseLoader does not have PassiveTree loaded!");
            return;
        }

        TreeState = new PassiveTreeState(treeDef);
        TreeState.SetCharacterLevel(Player.Level);

        // Sync starting class with player archetype if available
        if (Player.Archetype != null)
        {
            TreeState.SetStartingClassByAttribute(Player.Archetype.PrimaryAttribute);
        }

        // Wire Level Up to advance passive point budget
        Player.OnLevelUp += newLevel =>
        {
            TreeState.SetCharacterLevel(newLevel);
            RefreshPointsDisplay();
        };

        // Wire tree changes
        TreeState.OnTreeChanged += () =>
        {
            RefreshPointsDisplay();
            if (_canvas?.SelectedNode != null)
            {
                UpdateInspector(_canvas.SelectedNode);
            }
            if (_statsSummaryDrawer?.Visible == true)
            {
                PopulateStatsSummary();
            }
        };

        _canvas?.Initialize(TreeState);

        // Select the starting node by default
        var startNode = TreeState.GetNode(TreeState.StartNodeId);
        if (startNode != null)
        {
            _canvas?.SelectNode(startNode);
        }

        RefreshPointsDisplay();
    }

    /// <summary>
    /// Switches the starting class origin matching archetype primary attribute (e.g. "Strength" -> Marauder).
    /// </summary>
    public void SetStartingClassByAttribute(string? attribute)
    {
        if (TreeState == null || string.IsNullOrWhiteSpace(attribute)) return;
        TreeState.SetStartingClassByAttribute(attribute);
        _canvas?.ResetView();
        var startNode = TreeState.GetNode(TreeState.StartNodeId);
        if (startNode != null)
        {
            _canvas?.SelectNode(startNode);
        }
    }

    public void RefreshView()
    {
        RefreshPointsDisplay();
        if (_canvas?.SelectedNode != null)
        {
            UpdateInspector(_canvas.SelectedNode);
        }
        else if (TreeState != null)
        {
            var startNode = TreeState.GetNode(TreeState.StartNodeId);
            if (startNode != null) _canvas?.SelectNode(startNode);
        }
        _canvas?.QueueRedraw();
    }

    // ─────────────────────────────────────────────
    //  Search Filter & Highlights
    // ─────────────────────────────────────────────

    private void OnSearchInputChanged(string newText)
    {
        SetSearchQuery(newText);
    }

    public void SetSearchQuery(string query)
    {
        CurrentSearchQuery = (query ?? string.Empty).Trim();
        if (_searchInput != null && _searchInput.Text != CurrentSearchQuery)
        {
            _searchInput.Text = CurrentSearchQuery;
        }

        if (_clearSearchBtn != null)
        {
            _clearSearchBtn.Visible = !string.IsNullOrEmpty(CurrentSearchQuery);
        }

        if (string.IsNullOrEmpty(CurrentSearchQuery) || TreeState == null)
        {
            if (_matchCountLabel != null) _matchCountLabel.Text = string.Empty;
            _canvas?.SetSearchHighlights(null);
            return;
        }

        // Search matching nodes
        var q = CurrentSearchQuery;
        var matches = TreeState.Nodes.Where(n =>
            n.Name.Contains(q, StringComparison.InvariantCultureIgnoreCase)
            || n.Cluster.Contains(q, StringComparison.InvariantCultureIgnoreCase)
            || n.StatList.Any(s => s.Text.Contains(q, StringComparison.InvariantCultureIgnoreCase) || s.Stat.Contains(q, StringComparison.InvariantCultureIgnoreCase))
            || n.TagList.Any(t => t.Contains(q, StringComparison.InvariantCultureIgnoreCase))
            || (n.Description != null && n.Description.Contains(q, StringComparison.InvariantCultureIgnoreCase))
        ).Select(n => n.Id).ToList();

        if (_matchCountLabel != null)
        {
            _matchCountLabel.Text = Loc.Format("UI_TREE_MATCH_COUNT", matches.Count);
        }

        _canvas?.SetSearchHighlights(matches);
    }

    public void ClearSearch()
    {
        SetSearchQuery(string.Empty);
    }

    // ─────────────────────────────────────────────
    //  Inspector Drawer Logic
    // ─────────────────────────────────────────────

    private void OnCanvasNodeSelected(PassiveNodeDefinition node)
    {
        UpdateInspector(node);
    }

    private void OnCanvasNodeDoubleClicked(PassiveNodeDefinition node)
    {
        if (TreeState == null || node == null) return;

        if (TreeState.CanAllocate(node.Id))
        {
            TreeState.Allocate(node.Id);
            UpdateInspector(node);
        }
        else if (!TreeState.IsAllocated(node.Id) && TreeState.GetPathCost(node.Id) is int cost && cost > 0 && cost <= TreeState.AvailablePoints)
        {
            TreeState.AllocatePath(node.Id);
            UpdateInspector(node);
        }
    }

    private void UpdateInspector(PassiveNodeDefinition? node)
    {
        if (node == null || TreeState == null)
        {
            if (_nodeNameLabel != null) _nodeNameLabel.Text = "Select a passive node";
            if (_nodeTypeBadge != null) _nodeTypeBadge.Text = "";
            if (_nodeClusterLabel != null) _nodeClusterLabel.Text = "";
            if (_nodeStatsVBox != null)
            {
                foreach (var child in _nodeStatsVBox.GetChildren()) child.QueueFree();
            }
            if (_nodeActionBtn != null) _nodeActionBtn.Disabled = true;
            if (_actionHintLabel != null) _actionHintLabel.Text = "";
            return;
        }

        // 1. Node Title & Cluster
        if (_nodeNameLabel != null) _nodeNameLabel.Text = node.Name;
        if (_nodeClusterLabel != null)
        {
            var cluster = TreeState.Definition.GetCluster(node.Cluster);
            _nodeClusterLabel.Text = cluster != null ? $"Cluster: {cluster.Name}" : node.Cluster;
        }

        // 2. Node Type Badge
        if (_nodeTypeBadge != null)
        {
            _nodeTypeBadge.Text = node.Type switch
            {
                PassiveNodeType.Start => "★ CLASS ORIGIN",
                PassiveNodeType.Keystone => "✦ KEYSTONE",
                PassiveNodeType.Notable => "◆ NOTABLE",
                _ => "• MINOR"
            };

            var badgeColor = node.Type switch
            {
                PassiveNodeType.Start => new Color(0.35f, 0.75f, 1.0f),
                PassiveNodeType.Keystone => new Color(1.0f, 0.45f, 0.25f),
                PassiveNodeType.Notable => new Color(0.95f, 0.78f, 0.32f),
                _ => new Color(0.65f, 0.68f, 0.75f)
            };
            _nodeTypeBadge.AddThemeColorOverride("font_color", badgeColor);
        }

        // 3. Node Stat Lines
        if (_nodeStatsVBox != null)
        {
            foreach (var child in _nodeStatsVBox.GetChildren()) child.QueueFree();

            if (node.IsStart)
            {
                var descLabel = new Label
                {
                    Text = node.Description ?? "Class origin. Starting nexus for skill pathing.",
                    AutowrapMode = TextServer.AutowrapMode.WordSmart
                };
                descLabel.AddThemeColorOverride("font_color", new Color(0.75f, 0.78f, 0.85f));
                descLabel.AddThemeFontSizeOverride("font_size", 10);
                _nodeStatsVBox.AddChild(descLabel);
            }
            else
            {
                foreach (var stat in node.StatList)
                {
                    var statLabel = new Label
                    {
                        Text = $"• {stat.Text}",
                        AutowrapMode = TextServer.AutowrapMode.WordSmart
                    };

                    Color statColor = stat.Mod switch
                    {
                        PassiveModType.Keystone => new Color(1.0f, 0.40f, 0.25f),
                        PassiveModType.More => new Color(1.0f, 0.85f, 0.35f),
                        _ => new Color(0.85f, 0.88f, 0.95f)
                    };
                    statLabel.AddThemeColorOverride("font_color", statColor);
                    statLabel.AddThemeFontSizeOverride("font_size", 10);
                    _nodeStatsVBox.AddChild(statLabel);
                }
            }
        }

        // 4. Action Button State
        bool isAllocated = TreeState.IsAllocated(node.Id);
        bool canAllocate = TreeState.CanAllocate(node.Id);
        bool canRefund = TreeState.CanRefund(node.Id);

        if (_nodeActionBtn != null && _actionHintLabel != null)
        {
            if (node.IsStart)
            {
                _nodeActionBtn.Text = Loc.Tr("UI_TREE_BTN_ORIGIN");
                _nodeActionBtn.Disabled = true;
                _actionHintLabel.Text = "Class origin is permanently active.";
            }
            else if (isAllocated)
            {
                if (canRefund)
                {
                    _nodeActionBtn.Text = Loc.Tr("UI_TREE_BTN_REFUND");
                    _nodeActionBtn.Disabled = false;
                    _actionHintLabel.Text = "1-Click to refund this passive point.";
                }
                else
                {
                    _nodeActionBtn.Text = Loc.Tr("UI_TREE_BTN_ALLOCATED");
                    _nodeActionBtn.Disabled = true;
                    _actionHintLabel.Text = "Cannot refund: required to maintain tree connection.";
                }
            }
            else
            {
                if (canAllocate)
                {
                    _nodeActionBtn.Text = Loc.Tr("UI_TREE_BTN_ALLOCATE");
                    _nodeActionBtn.Disabled = false;
                    _actionHintLabel.Text = "Adjacent node. 1-Click to allocate.";
                }
                else
                {
                    int pathCost = TreeState.GetPathCost(node.Id);
                    if (pathCost > 0 && pathCost <= TreeState.AvailablePoints)
                    {
                        _nodeActionBtn.Text = Loc.Format("UI_TREE_BTN_AUTOPATH", pathCost);
                        _nodeActionBtn.Disabled = false;
                        _actionHintLabel.Text = $"Auto-pathing: allocates {pathCost} nodes along shortest path.";
                    }
                    else if (pathCost > TreeState.AvailablePoints)
                    {
                        _nodeActionBtn.Text = Loc.Format("UI_TREE_BTN_NEED_POINTS", pathCost, TreeState.AvailablePoints);
                        _nodeActionBtn.Disabled = true;
                        _actionHintLabel.Text = $"Need {pathCost - TreeState.AvailablePoints} more level(s) to reach this node.";
                    }
                    else
                    {
                        _nodeActionBtn.Text = Loc.Tr("UI_TREE_BTN_ALLOCATE");
                        _nodeActionBtn.Disabled = true;
                        _actionHintLabel.Text = TreeState.AvailablePoints == 0 ? "No available passive points." : "Not connected to tree.";
                    }
                }
            }
        }
    }

    private void OnNodeActionPressed()
    {
        var node = _canvas?.SelectedNode;
        if (node == null || TreeState == null) return;

        if (TreeState.IsAllocated(node.Id))
        {
            if (TreeState.CanRefund(node.Id))
            {
                TreeState.Refund(node.Id);
                UpdateInspector(node);
            }
        }
        else
        {
            if (TreeState.CanAllocate(node.Id))
            {
                TreeState.Allocate(node.Id);
                UpdateInspector(node);
            }
            else if (TreeState.GetPathCost(node.Id) is int cost && cost > 0 && cost <= TreeState.AvailablePoints)
            {
                TreeState.AllocatePath(node.Id);
                UpdateInspector(node);
            }
        }
    }

    private void OnRespecPressed()
    {
        if (TreeState == null) return;
        TreeState.ResetAllocations();
        if (_canvas?.SelectedNode != null)
        {
            UpdateInspector(_canvas.SelectedNode);
        }
        _canvas?.ResetView();
    }

    // ─────────────────────────────────────────────
    //  Points Badge & Telemetry
    // ─────────────────────────────────────────────

    private void RefreshPointsDisplay()
    {
        if (_pointsBadgeLabel == null || TreeState == null) return;

        int avail = TreeState.AvailablePoints;
        int total = TreeState.TotalPoints;
        _pointsBadgeLabel.Text = Loc.Format("UI_TREE_POINTS_BADGE", avail);
        _pointsBadgeLabel.TooltipText = Loc.Format("UI_TREE_POINTS_TOOLTIP", avail, TreeState.SpentPoints, total);

        if (avail > 0)
        {
            _pointsBadgeLabel.AddThemeColorOverride("font_color", new Color(1.0f, 0.85f, 0.35f));
        }
        else
        {
            _pointsBadgeLabel.AddThemeColorOverride("font_color", new Color(0.60f, 0.64f, 0.72f));
        }
    }

    // ─────────────────────────────────────────────
    //  Stats Summary Overlay Drawer
    // ─────────────────────────────────────────────

    public bool IsStatsSummaryVisible => _statsSummaryDrawer?.Visible ?? false;

    public void ToggleStatsSummary()
    {
        if (_statsSummaryDrawer == null) return;
        SetStatsSummaryVisible(!_statsSummaryDrawer.Visible);
    }

    public void SetStatsSummaryVisible(bool visible)
    {
        if (_statsSummaryDrawer == null) return;
        _statsSummaryDrawer.Visible = visible;
        if (visible)
        {
            PopulateStatsSummary();
        }
    }

    private void PopulateStatsSummary()
    {
        if (_statsListVBox == null || TreeState == null) return;

        foreach (var child in _statsListVBox.GetChildren()) child.QueueFree();

        var totals = TreeState.GetStatTotals();
        var keystones = TreeState.AllocatedKeystones;

        if (totals.Count == 0 && keystones.Count == 0)
        {
            var emptyLabel = new Label
            {
                Text = Loc.Tr("UI_TREE_NO_STATS"),
                AutowrapMode = TextServer.AutowrapMode.WordSmart
            };
            emptyLabel.AddThemeColorOverride("font_color", new Color(0.55f, 0.58f, 0.65f));
            emptyLabel.AddThemeFontSizeOverride("font_size", 10);
            _statsListVBox.AddChild(emptyLabel);
            return;
        }

        // List Keystones first
        foreach (var ks in keystones)
        {
            var ksLabel = new Label
            {
                Text = $"★ {ks.Name} (Keystone)",
                AutowrapMode = TextServer.AutowrapMode.WordSmart
            };
            ksLabel.AddThemeColorOverride("font_color", new Color(1.0f, 0.45f, 0.25f));
            ksLabel.AddThemeFontSizeOverride("font_size", 10);
            _statsListVBox.AddChild(ksLabel);
        }

        // List Totals
        foreach (var total in totals)
        {
            var statLabel = new Label
            {
                Text = $"▲ {total.DisplayText}",
                AutowrapMode = TextServer.AutowrapMode.WordSmart
            };

            Color col = total.Mod switch
            {
                PassiveModType.More => new Color(1.0f, 0.85f, 0.35f),
                PassiveModType.Flat or PassiveModType.FlatPercent => new Color(0.45f, 0.85f, 1.0f),
                _ => new Color(0.85f, 0.90f, 0.98f)
            };
            statLabel.AddThemeColorOverride("font_color", col);
            statLabel.AddThemeFontSizeOverride("font_size", 10);
            _statsListVBox.AddChild(statLabel);
        }
    }

    // ─────────────────────────────────────────────
    //  Public Helpers for Testing & Automation
    // ─────────────────────────────────────────────

    public void SelectNodeById(string nodeId)
    {
        if (TreeState == null) return;
        var node = TreeState.GetNode(nodeId);
        _canvas?.SelectNode(node);
    }

    public void AllocateSelectedNode()
    {
        if (_canvas?.SelectedNode == null || TreeState == null) return;
        TreeState.Allocate(_canvas.SelectedNode.Id);
        UpdateInspector(_canvas.SelectedNode);
    }

    public void AutoPathToSelectedNode()
    {
        if (_canvas?.SelectedNode == null || TreeState == null) return;
        TreeState.AllocatePath(_canvas.SelectedNode.Id);
        UpdateInspector(_canvas.SelectedNode);
    }

    public void RefundSelectedNode()
    {
        if (_canvas?.SelectedNode == null || TreeState == null) return;
        TreeState.Refund(_canvas.SelectedNode.Id);
        UpdateInspector(_canvas.SelectedNode);
    }
}
