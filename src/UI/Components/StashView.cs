using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using IdleExile.Core.Models;
using IdleExile.Core.Services;
using IdleExile.Core.Simulation;
using IdleExile.Core.State;

namespace IdleExile.UI.Components;

/// <summary>
/// Controller for the 4-tab Affinity Stash View (Currency, Gear, Maps, Fossils) and 1-Click Dump.
/// Strictly conforms to:
/// - docs/05_items_and_crafting.md Section 5.2
/// - docs/UI_OVERHAUL_IMPLEMENTATION_PLAN.md Task 5.2
/// - 410px vertical height budget in TabWorkspaceContainer.
/// - Persistent Combat Viewport integration.
/// - Full localization via Loc.Tr() / Loc.Format().
/// </summary>
public partial class StashView : PanelContainer
{
    private PlayerState? _player;
    private DatabaseLoader? _db;
    private MapQueueManager? _mapQueueManager;
    private CombatEngine? _combatEngine;
    private Func<IEnumerable<IGearItem>>? _candidateGearProvider;

    /// <summary>Fired when player directly activates a map from Stash.</summary>
    public event Action<MapDefinition, IReadOnlyList<RolledMapModifier>, string>? OnMapActivated;

    private StashTabType _currentTab = StashTabType.Currency;
    private string _currentGearFilter = "All";
    private int _mapFilterMinTier = 0;
    private int _mapFilterMaxTier = 20;

    private IGearItem? _selectedGearItem;
    private CurrencyDefinition? _selectedCurrency;

    // UI Node References
    private Button? _tabCurrencyBtn;
    private Button? _tabGearBtn;
    private Button? _tabMapsBtn;
    private Button? _tabFossilsBtn;
    private Button? _dumpAllBtn;
    private Label? _dumpStatusLabel;

    private Control? _currencyPanel;
    private GridContainer? _currencyGrid;
    private Label? _currencyInspectorLabel;

    private Control? _gearPanel;
    private VBoxContainer? _gearItemList;
    private Label? _gearCountLabel;
    private Label? _gearInspectorNameLabel;
    private Label? _gearInspectorStatsLabel;
    private Button? _gearEquipBtn;
    private Button? _gearLockBtn;
    private Button? _gearVendorBtn;

    private Control? _mapsPanel;
    private VBoxContainer? _mapsListContainer;
    private Label? _mapCountLabel;

    private Control? _fossilsPanel;
    private GridContainer? _fossilsGrid;

    private Label? _summaryLabel;

    public StashTabType CurrentTab => _currentTab;

    public override void _Ready()
    {
        BindNodes();
        ConnectSignals();
        ApplyTabVisibility(_currentTab);
    }

    private void BindNodes()
    {
        _tabCurrencyBtn = GetNodeOrNull<Button>("%TabCurrencyBtn");
        _tabGearBtn = GetNodeOrNull<Button>("%TabGearBtn");
        _tabMapsBtn = GetNodeOrNull<Button>("%TabMapsBtn");
        _tabFossilsBtn = GetNodeOrNull<Button>("%TabFossilsBtn");
        _dumpAllBtn = GetNodeOrNull<Button>("%DumpAllButton");
        _dumpStatusLabel = GetNodeOrNull<Label>("%DumpStatusLabel");

        _currencyPanel = GetNodeOrNull<Control>("%CurrencyPanel");
        _currencyGrid = GetNodeOrNull<GridContainer>("%CurrencyGrid");
        _currencyInspectorLabel = GetNodeOrNull<Label>("%CurrencyInspectorLabel");

        _gearPanel = GetNodeOrNull<Control>("%GearPanel");
        _gearItemList = GetNodeOrNull<VBoxContainer>("%GearItemList");
        _gearCountLabel = GetNodeOrNull<Label>("%GearCountLabel");
        _gearInspectorNameLabel = GetNodeOrNull<Label>("%GearInspectorNameLabel");
        _gearInspectorStatsLabel = GetNodeOrNull<Label>("%GearInspectorStatsLabel");
        _gearEquipBtn = GetNodeOrNull<Button>("%GearEquipBtn");
        _gearLockBtn = GetNodeOrNull<Button>("%GearLockBtn");
        _gearVendorBtn = GetNodeOrNull<Button>("%GearVendorBtn");

        _mapsPanel = GetNodeOrNull<Control>("%MapsPanel");
        _mapsListContainer = GetNodeOrNull<VBoxContainer>("%MapsListContainer");
        _mapCountLabel = GetNodeOrNull<Label>("%MapCountLabel");

        _fossilsPanel = GetNodeOrNull<Control>("%FossilsPanel");
        _fossilsGrid = GetNodeOrNull<GridContainer>("%FossilsGrid");

        _summaryLabel = GetNodeOrNull<Label>("%SummaryLabel");
    }

    private void ConnectSignals()
    {
        if (_tabCurrencyBtn != null) _tabCurrencyBtn.Pressed += () => SwitchTab(StashTabType.Currency);
        if (_tabGearBtn != null) _tabGearBtn.Pressed += () => SwitchTab(StashTabType.Gear);
        if (_tabMapsBtn != null) _tabMapsBtn.Pressed += () => SwitchTab(StashTabType.Maps);
        if (_tabFossilsBtn != null) _tabFossilsBtn.Pressed += () => SwitchTab(StashTabType.Fossils);

        if (_dumpAllBtn != null) _dumpAllBtn.Pressed += OnDumpAllPressed;

        // Gear Filter Buttons
        var gearFilterAll = GetNodeOrNull<Button>("%GearFilterAllBtn");
        var gearFilterWeapons = GetNodeOrNull<Button>("%GearFilterWeaponsBtn");
        var gearFilterArmour = GetNodeOrNull<Button>("%GearFilterArmourBtn");
        var gearFilterJewel = GetNodeOrNull<Button>("%GearFilterJewelBtn");
        var gearFilterLocked = GetNodeOrNull<Button>("%GearFilterLockedBtn");

        if (gearFilterAll != null) gearFilterAll.Pressed += () => SetGearFilter("All");
        if (gearFilterWeapons != null) gearFilterWeapons.Pressed += () => SetGearFilter("Weapons");
        if (gearFilterArmour != null) gearFilterArmour.Pressed += () => SetGearFilter("Armour");
        if (gearFilterJewel != null) gearFilterJewel.Pressed += () => SetGearFilter("Jewellery");
        if (gearFilterLocked != null) gearFilterLocked.Pressed += () => SetGearFilter("Locked");

        if (_gearEquipBtn != null) _gearEquipBtn.Pressed += OnEquipSelectedGear;
        if (_gearLockBtn != null) _gearLockBtn.Pressed += OnToggleSelectedGearLock;
        if (_gearVendorBtn != null) _gearVendorBtn.Pressed += OnVendorSelectedGear;

        // Map Filter Buttons
        var mapFilterAll = GetNodeOrNull<Button>("%MapFilterAllBtn");
        var mapFilterWhite = GetNodeOrNull<Button>("%MapFilterWhiteBtn");
        var mapFilterYellow = GetNodeOrNull<Button>("%MapFilterYellowBtn");
        var mapFilterRed = GetNodeOrNull<Button>("%MapFilterRedBtn");

        if (mapFilterAll != null) mapFilterAll.Pressed += () => SetMapTierFilter(0, 20);
        if (mapFilterWhite != null) mapFilterWhite.Pressed += () => SetMapTierFilter(1, 5);
        if (mapFilterYellow != null) mapFilterYellow.Pressed += () => SetMapTierFilter(6, 10);
        if (mapFilterRed != null) mapFilterRed.Pressed += () => SetMapTierFilter(11, 20);
    }

    /// <summary>
    /// Initializes the StashView with player state, database loader, and optional queue/combat coordinators.
    /// </summary>
    public void Initialize(
        PlayerState player,
        DatabaseLoader db,
        MapQueueManager? mapQueue = null,
        CombatEngine? engine = null,
        Func<IEnumerable<IGearItem>>? candidateGearProvider = null)
    {
        _player = player;
        _db = db;
        _mapQueueManager = mapQueue;
        _combatEngine = engine;
        _candidateGearProvider = candidateGearProvider;

        if (_player != null)
        {
            _player.Stash.OnStashChanged += RefreshView;
            _player.Stash.OnCurrencyChanged += (id, count) => RefreshSummary();
            _player.Stash.OnFossilChanged += (id, count) => RefreshSummary();
        }

        RefreshView();
    }

    /// <summary>
    /// Switches the active affinity sub-tab.
    /// </summary>
    public void SwitchTab(StashTabType tab)
    {
        _currentTab = tab;
        ApplyTabVisibility(tab);
        RefreshCurrentTabView();
    }

    private void ApplyTabVisibility(StashTabType tab)
    {
        if (_tabCurrencyBtn != null) _tabCurrencyBtn.ButtonPressed = (tab == StashTabType.Currency);
        if (_tabGearBtn != null) _tabGearBtn.ButtonPressed = (tab == StashTabType.Gear);
        if (_tabMapsBtn != null) _tabMapsBtn.ButtonPressed = (tab == StashTabType.Maps);
        if (_tabFossilsBtn != null) _tabFossilsBtn.ButtonPressed = (tab == StashTabType.Fossils);

        if (_currencyPanel != null) _currencyPanel.Visible = (tab == StashTabType.Currency);
        if (_gearPanel != null) _gearPanel.Visible = (tab == StashTabType.Gear);
        if (_mapsPanel != null) _mapsPanel.Visible = (tab == StashTabType.Maps);
        if (_fossilsPanel != null) _fossilsPanel.Visible = (tab == StashTabType.Fossils);
    }

    /// <summary>
    /// Refreshes all visual components and counters in the StashView.
    /// </summary>
    public void RefreshView()
    {
        RefreshCurrentTabView();
        RefreshSummary();
    }

    private void RefreshCurrentTabView()
    {
        switch (_currentTab)
        {
            case StashTabType.Currency:
                RefreshCurrencyTab();
                break;
            case StashTabType.Gear:
                RefreshGearTab();
                break;
            case StashTabType.Maps:
                RefreshMapsTab();
                break;
            case StashTabType.Fossils:
                RefreshFossilsTab();
                break;
        }
    }

    #region Currency Tab Presentation

    private void RefreshCurrencyTab()
    {
        if (_currencyGrid == null || _db == null || _player == null) return;

        // Clear existing tiles
        foreach (Node child in _currencyGrid.GetChildren())
        {
            _currencyGrid.RemoveChild(child);
            child.QueueFree();
        }

        var currencies = _db.Currencies.Values.OrderBy(c => c.Tier).ThenBy(c => c.Name);

        foreach (var currency in currencies)
        {
            int count = _player.Stash.GetCurrencyCount(currency.Id);
            var card = CreateCurrencyCard(currency, count);
            _currencyGrid.AddChild(card);
        }

        if (_selectedCurrency != null)
        {
            InspectCurrency(_selectedCurrency);
        }
        else
        {
            var first = currencies.FirstOrDefault();
            if (first != null) InspectCurrency(first);
        }
    }

    private Button CreateCurrencyCard(CurrencyDefinition currency, int count)
    {
        var btn = new Button
        {
            CustomMinimumSize = new Vector2(96, 52),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            ToggleMode = false,
            TooltipText = $"{currency.Name} (Tier {currency.Tier})\nStack: {count:N0} / {currency.MaxStackSize:N0}\n\n{currency.Description}"
        };

        var style = new StyleBoxFlat
        {
            BgColor = new Color(0.08f, 0.085f, 0.11f, 0.95f),
            CornerRadiusBottomLeft = 3,
            CornerRadiusBottomRight = 3,
            CornerRadiusTopLeft = 3,
            CornerRadiusTopRight = 3,
            BorderWidthBottom = 1,
            BorderWidthTop = 1,
            BorderWidthLeft = 1,
            BorderWidthRight = 1,
            BorderColor = GetCurrencyBorderColor(currency.Tier)
        };

        btn.AddThemeStyleboxOverride("normal", style);

        var vbox = new VBoxContainer
        {
            MouseFilter = MouseFilterEnum.Ignore,
            Alignment = BoxContainer.AlignmentMode.Center,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };

        var topHBox = new HBoxContainer
        {
            MouseFilter = MouseFilterEnum.Ignore,
            Alignment = BoxContainer.AlignmentMode.Center
        };

        var glyphLabel = new Label
        {
            Text = currency.IconGlyph,
            MouseFilter = MouseFilterEnum.Ignore
        };
        glyphLabel.AddThemeFontSizeOverride("font_size", 13);
        topHBox.AddChild(glyphLabel);

        var countLabel = new Label
        {
            Text = count > 0 ? $"x{count:N0}" : "0",
            MouseFilter = MouseFilterEnum.Ignore,
            Modulate = count > 0 ? new Color(1f, 0.9f, 0.5f) : new Color(0.45f, 0.45f, 0.45f)
        };
        countLabel.AddThemeFontSizeOverride("font_size", 11);
        topHBox.AddChild(countLabel);

        vbox.AddChild(topHBox);

        var nameLabel = new Label
        {
            Text = currency.Name,
            HorizontalAlignment = HorizontalAlignment.Center,
            MouseFilter = MouseFilterEnum.Ignore,
            TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
            Modulate = count > 0 ? Colors.White : new Color(0.55f, 0.55f, 0.55f)
        };
        nameLabel.AddThemeFontSizeOverride("font_size", 9);
        vbox.AddChild(nameLabel);

        btn.AddChild(vbox);
        btn.Pressed += () => InspectCurrency(currency);

        return btn;
    }

    private void InspectCurrency(CurrencyDefinition currency)
    {
        _selectedCurrency = currency;
        if (_currencyInspectorLabel == null || _player == null) return;

        int count = _player.Stash.GetCurrencyCount(currency.Id);
        _currencyInspectorLabel.Text = $"{currency.IconGlyph} {currency.Name} [x{count:N0}] — {currency.Description}";
    }

    private static Color GetCurrencyBorderColor(int tier) => tier switch
    {
        1 => new Color(0.4f, 0.42f, 0.48f, 0.8f),
        2 => new Color(0.35f, 0.65f, 0.95f, 0.85f),
        3 => new Color(0.9f, 0.55f, 0.15f, 0.9f),
        4 => new Color(0.95f, 0.8f, 0.2f, 0.95f),
        5 => new Color(0.98f, 0.9f, 0.6f, 1f),
        _ => new Color(0.3f, 0.3f, 0.35f)
    };

    #endregion

    #region Gear Tab Presentation

    public void SetGearFilter(string filter)
    {
        _currentGearFilter = filter;
        RefreshGearTab();
    }

    private void RefreshGearTab()
    {
        if (_gearItemList == null || _player == null) return;

        foreach (Node child in _gearItemList.GetChildren())
        {
            _gearItemList.RemoveChild(child);
            child.QueueFree();
        }

        var items = _player.Stash.GetFilteredGear(_currentGearFilter);

        if (_gearCountLabel != null)
        {
            _gearCountLabel.Text = $"Stored: {items.Count} / {_player.Stash.MaxGearSlots}";
        }

        if (items.Count == 0)
        {
            var emptyLabel = new Label
            {
                Text = Loc.Tr("UI_STASH_EMPTY_GEAR"),
                HorizontalAlignment = HorizontalAlignment.Center,
                Modulate = new Color(0.5f, 0.5f, 0.5f)
            };
            emptyLabel.AddThemeFontSizeOverride("font_size", 11);
            _gearItemList.AddChild(emptyLabel);
            UpdateGearInspector(null);
            return;
        }

        foreach (var item in items)
        {
            var row = CreateGearItemRow(item);
            _gearItemList.AddChild(row);
        }

        if (_selectedGearItem != null && items.Any(i => i.InstanceId == _selectedGearItem.InstanceId))
        {
            UpdateGearInspector(_selectedGearItem);
        }
        else
        {
            UpdateGearInspector(items.FirstOrDefault());
        }
    }

    private PanelContainer CreateGearItemRow(IGearItem item)
    {
        var rowPanel = new PanelContainer
        {
            CustomMinimumSize = new Vector2(0, 32),
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };

        var rowStyle = new StyleBoxFlat
        {
            BgColor = new Color(0.07f, 0.075f, 0.09f, 0.95f),
            CornerRadiusBottomLeft = 3,
            CornerRadiusBottomRight = 3,
            CornerRadiusTopLeft = 3,
            CornerRadiusTopRight = 3,
            BorderWidthBottom = 1,
            BorderColor = new Color(0.15f, 0.16f, 0.2f)
        };
        rowPanel.AddThemeStyleboxOverride("panel", rowStyle);

        var hbox = new HBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            Alignment = BoxContainer.AlignmentMode.Begin
        };
        hbox.AddThemeConstantOverride("separation", 6);

        // Slot glyph
        var glyphLabel = new Label
        {
            Text = GetSlotGlyph(item.TargetSlot),
            CustomMinimumSize = new Vector2(24, 0),
            HorizontalAlignment = HorizontalAlignment.Center
        };
        glyphLabel.AddThemeFontSizeOverride("font_size", 12);
        hbox.AddChild(glyphLabel);

        // Item Name Button
        var nameBtn = new Button
        {
            Text = item.Name,
            Flat = true,
            Alignment = HorizontalAlignment.Left,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        nameBtn.AddThemeFontSizeOverride("font_size", 11);
        nameBtn.AddThemeColorOverride("font_color", GetRarityColor(item.Rarity));
        nameBtn.Pressed += () => UpdateGearInspector(item);
        hbox.AddChild(nameBtn);

        // Item Level
        var ilvlBadge = new Label
        {
            Text = $"ilvl {item.ItemLevel}",
            Modulate = new Color(0.6f, 0.65f, 0.75f)
        };
        ilvlBadge.AddThemeFontSizeOverride("font_size", 10);
        hbox.AddChild(ilvlBadge);

        // Lock icon
        if (item.IsLocked)
        {
            var lockIcon = new Label
            {
                Text = "🔒",
                TooltipText = "Item is locked against vendor or salvage."
            };
            lockIcon.AddThemeFontSizeOverride("font_size", 10);
            hbox.AddChild(lockIcon);
        }

        // Quick Equip Button
        var equipBtn = new Button
        {
            Text = Loc.Tr("UI_STASH_EQUIP_BTN"),
            CustomMinimumSize = new Vector2(48, 22)
        };
        equipBtn.AddThemeFontSizeOverride("font_size", 10);
        equipBtn.Pressed += () =>
        {
            _selectedGearItem = item;
            OnEquipSelectedGear();
        };
        hbox.AddChild(equipBtn);

        rowPanel.AddChild(hbox);
        return rowPanel;
    }

    private void UpdateGearInspector(IGearItem? item)
    {
        _selectedGearItem = item;

        if (_gearInspectorNameLabel == null || _gearInspectorStatsLabel == null) return;

        if (item == null)
        {
            _gearInspectorNameLabel.Text = "No gear selected";
            _gearInspectorStatsLabel.Text = "";
            if (_gearEquipBtn != null) _gearEquipBtn.Disabled = true;
            if (_gearLockBtn != null) _gearLockBtn.Disabled = true;
            if (_gearVendorBtn != null) _gearVendorBtn.Disabled = true;
            return;
        }

        _gearInspectorNameLabel.Text = $"{GetSlotGlyph(item.TargetSlot)} {item.Name} ({item.Rarity} {item.BaseName})";
        _gearInspectorNameLabel.Modulate = GetRarityColor(item.Rarity);

        var statLines = new List<string>();
        if (!string.IsNullOrWhiteSpace(item.ImplicitDescription))
        {
            statLines.Add($"✦ {item.ImplicitDescription}");
        }

        if (item.LocalArmour > 0) statLines.Add($"Armour: {item.LocalArmour:F0}");
        if (item.LocalEvasion > 0) statLines.Add($"Evasion: {item.LocalEvasion:F0}");
        if (item.LocalEnergyShield > 0) statLines.Add($"Energy Shield: {item.LocalEnergyShield:F0}");

        foreach (var affix in item.Affixes.Take(3))
        {
            statLines.Add($"• {affix.NameTemplate}");
        }

        _gearInspectorStatsLabel.Text = string.Join(" | ", statLines);

        if (_gearEquipBtn != null) _gearEquipBtn.Disabled = false;
        if (_gearLockBtn != null)
        {
            _gearLockBtn.Disabled = false;
            _gearLockBtn.Text = item.IsLocked ? Loc.Tr("UI_STASH_UNLOCK_BTN") : Loc.Tr("UI_STASH_LOCK_BTN");
        }
        if (_gearVendorBtn != null)
        {
            _gearVendorBtn.Disabled = item.IsLocked;
        }
    }

    private void OnEquipSelectedGear()
    {
        if (_selectedGearItem == null || _player == null) return;

        var targetSlot = _selectedGearItem.TargetSlot;
        var currentlyEquipped = _player.Gear.GetItem(targetSlot);

        // Withdraw from stash
        if (_player.Stash.WithdrawGear(_selectedGearItem.InstanceId, out var itemToEquip) && itemToEquip != null)
        {
            // Equip new item
            _player.Gear.Equip(targetSlot, itemToEquip);

            // Stash old equipped item if any
            if (currentlyEquipped != null)
            {
                _player.Stash.DepositGear(currentlyEquipped);
            }

            _player.RecalculateStatsFromGear();
            RefreshGearTab();
            ShowDumpToast($"Equipped {itemToEquip.Name}!");
        }
    }

    private void OnToggleSelectedGearLock()
    {
        if (_selectedGearItem == null) return;

        _selectedGearItem.IsLocked = !_selectedGearItem.IsLocked;
        UpdateGearInspector(_selectedGearItem);
        RefreshGearTab();
    }

    private void OnVendorSelectedGear()
    {
        if (_selectedGearItem == null || _selectedGearItem.IsLocked || _player == null) return;

        string soldName = _selectedGearItem.Name;
        string rarity = _selectedGearItem.Rarity;

        if (_player.Stash.RemoveGear(_selectedGearItem.InstanceId))
        {
            // Award shards/currency based on rarity
            if (string.Equals(rarity, "Rare", StringComparison.OrdinalIgnoreCase))
            {
                _player.Stash.DepositCurrency("orb_of_alchemy", 1);
            }
            else
            {
                _player.Stash.DepositCurrency("orb_of_alteration", 1);
            }

            _selectedGearItem = null;
            RefreshGearTab();
            ShowDumpToast($"Vendored {soldName} for currency!");
        }
    }

    private static string GetSlotGlyph(GearSlot slot) => slot switch
    {
        GearSlot.MainHand => "⚔️",
        GearSlot.OffHand => "🛡️",
        GearSlot.Helmet => "🪖",
        GearSlot.BodyArmour => "🥋",
        GearSlot.Gloves => "🧤",
        GearSlot.Boots => "👢",
        GearSlot.Amulet => "📿",
        GearSlot.Ring1 or GearSlot.Ring2 => "💍",
        GearSlot.Belt => "🪢",
        _ => "📦"
    };

    private static Color GetRarityColor(string rarity) => rarity.ToLowerInvariant() switch
    {
        "magic" => new Color(0.53f, 0.53f, 1f),
        "rare" => new Color(1f, 0.95f, 0.45f),
        "unique" => new Color(0.85f, 0.45f, 0.15f),
        _ => new Color(0.85f, 0.85f, 0.85f)
    };

    #endregion

    #region Maps Tab Presentation

    public void SetMapTierFilter(int minTier, int maxTier)
    {
        _mapFilterMinTier = minTier;
        _mapFilterMaxTier = maxTier;
        RefreshMapsTab();
    }

    private void RefreshMapsTab()
    {
        if (_mapsListContainer == null || _player == null) return;

        foreach (Node child in _mapsListContainer.GetChildren())
        {
            _mapsListContainer.RemoveChild(child);
            child.QueueFree();
        }

        var maps = _player.Stash.GetFilteredMaps(_mapFilterMinTier, _mapFilterMaxTier);

        if (_mapCountLabel != null)
        {
            _mapCountLabel.Text = $"Maps: {_player.Stash.TotalMapCount}";
        }

        if (maps.Count == 0)
        {
            var emptyLabel = new Label
            {
                Text = Loc.Tr("UI_STASH_EMPTY_MAPS"),
                HorizontalAlignment = HorizontalAlignment.Center,
                Modulate = new Color(0.5f, 0.5f, 0.5f)
            };
            emptyLabel.AddThemeFontSizeOverride("font_size", 11);
            _mapsListContainer.AddChild(emptyLabel);
            return;
        }

        foreach (var map in maps)
        {
            var row = CreateMapItemRow(map);
            _mapsListContainer.AddChild(row);
        }
    }

    private PanelContainer CreateMapItemRow(StashedMapSlate slate)
    {
        var rowPanel = new PanelContainer
        {
            CustomMinimumSize = new Vector2(0, 32),
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };

        var rowStyle = new StyleBoxFlat
        {
            BgColor = new Color(0.07f, 0.075f, 0.09f, 0.95f),
            CornerRadiusBottomLeft = 3,
            CornerRadiusBottomRight = 3,
            CornerRadiusTopLeft = 3,
            CornerRadiusTopRight = 3,
            BorderWidthBottom = 1,
            BorderColor = new Color(0.15f, 0.16f, 0.2f)
        };
        rowPanel.AddThemeStyleboxOverride("panel", rowStyle);

        var hbox = new HBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            Alignment = BoxContainer.AlignmentMode.Begin
        };
        hbox.AddThemeConstantOverride("separation", 6);

        // Tier badge
        var tierBadge = new Label
        {
            Text = slate.Tier == 0 ? "T0" : $"T{slate.Tier}",
            CustomMinimumSize = new Vector2(32, 0),
            HorizontalAlignment = HorizontalAlignment.Center,
            Modulate = GetTierColor(slate.Tier)
        };
        tierBadge.AddThemeFontSizeOverride("font_size", 11);
        hbox.AddChild(tierBadge);

        // Map name and quantity
        var nameLabel = new Label
        {
            Text = $"{slate.MapName} (x{slate.Quantity})",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis
        };
        nameLabel.AddThemeFontSizeOverride("font_size", 11);
        hbox.AddChild(nameLabel);

        // 1-Click + Queue Button
        var queueBtn = new Button
        {
            Text = Loc.Tr("UI_STASH_QUEUE_MAP_BTN"),
            CustomMinimumSize = new Vector2(56, 22),
            TooltipText = "Add 1 copy of this map to the 10-slot Map Queue."
        };
        queueBtn.AddThemeFontSizeOverride("font_size", 10);
        queueBtn.Pressed += () => OnQueueMapPressed(slate);
        hbox.AddChild(queueBtn);

        // 1-Click Activate Button
        var activateBtn = new Button
        {
            Text = Loc.Tr("UI_STASH_ACTIVATE_MAP_BTN"),
            CustomMinimumSize = new Vector2(64, 22),
            TooltipText = "Directly open portal and activate this map on Crucible device."
        };
        activateBtn.AddThemeFontSizeOverride("font_size", 10);
        activateBtn.Pressed += () => OnActivateMapPressed(slate);
        hbox.AddChild(activateBtn);

        rowPanel.AddChild(hbox);
        return rowPanel;
    }

    private void OnQueueMapPressed(StashedMapSlate slate)
    {
        if (_mapQueueManager == null || _player == null) return;

        if (_mapQueueManager.IsFull)
        {
            ShowDumpToast("Map Queue is full (10/10 slots)!");
            return;
        }

        if (_player.Stash.WithdrawMap(slate.MapId, slate.Tier, slate.Rarity, 1))
        {
            _mapQueueManager.Enqueue(slate.ToQueueItem());
            RefreshMapsTab();
            ShowDumpToast($"Added {slate.MapName} to Queue!");
        }
    }

    private void OnActivateMapPressed(StashedMapSlate slate)
    {
        if (_player == null) return;

        if (_player.Stash.WithdrawMap(slate.MapId, slate.Tier, slate.Rarity, 1))
        {
            OnMapActivated?.Invoke(slate.Map, slate.Modifiers, slate.Rarity);
            RefreshMapsTab();
            ShowDumpToast($"Activated {slate.MapName} (T{slate.Tier})!");
        }
    }

    private static Color GetTierColor(int tier) => tier switch
    {
        0 => new Color(0.75f, 0.5f, 1f),
        >= 1 and <= 5 => new Color(0.9f, 0.9f, 0.9f),
        >= 6 and <= 10 => new Color(1f, 0.9f, 0.3f),
        >= 11 => new Color(0.95f, 0.35f, 0.35f),
        _ => Colors.White
    };

    #endregion

    #region Fossils Tab Presentation

    private void RefreshFossilsTab()
    {
        if (_fossilsGrid == null || _db == null || _player == null) return;

        foreach (Node child in _fossilsGrid.GetChildren())
        {
            _fossilsGrid.RemoveChild(child);
            child.QueueFree();
        }

        var fossils = _db.Fossils.Values.ToList();

        foreach (var fossil in fossils)
        {
            int count = _player.Stash.GetFossilCount(fossil.Id);
            var card = CreateFossilCard(fossil, count);
            _fossilsGrid.AddChild(card);
        }
    }

    private PanelContainer CreateFossilCard(FossilDefinition fossil, int count)
    {
        var card = new PanelContainer
        {
            CustomMinimumSize = new Vector2(190, 52),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            TooltipText = $"{fossil.Name}\n{fossil.Description}"
        };

        var style = new StyleBoxFlat
        {
            BgColor = new Color(0.08f, 0.085f, 0.11f, 0.95f),
            CornerRadiusBottomLeft = 3,
            CornerRadiusBottomRight = 3,
            CornerRadiusTopLeft = 3,
            CornerRadiusTopRight = 3,
            BorderWidthBottom = 1,
            BorderWidthTop = 1,
            BorderWidthLeft = 1,
            BorderWidthRight = 1,
            BorderColor = new Color(fossil.ColorHex)
        };
        card.AddThemeStyleboxOverride("panel", style);

        var hbox = new HBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            Alignment = BoxContainer.AlignmentMode.Begin
        };
        hbox.AddThemeConstantOverride("separation", 8);

        // Glyph
        var glyph = new Label
        {
            Text = fossil.IconGlyph,
            CustomMinimumSize = new Vector2(30, 0),
            HorizontalAlignment = HorizontalAlignment.Center
        };
        glyph.AddThemeFontSizeOverride("font_size", 16);
        hbox.AddChild(glyph);

        // Details VBox
        var vbox = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };

        var nameHBox = new HBoxContainer();
        var nameLabel = new Label
        {
            Text = fossil.Name,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        nameLabel.AddThemeFontSizeOverride("font_size", 11);
        nameHBox.AddChild(nameLabel);

        var countLabel = new Label
        {
            Text = $"x{count}",
            Modulate = count > 0 ? new Color(1f, 0.9f, 0.5f) : new Color(0.5f, 0.5f, 0.5f)
        };
        countLabel.AddThemeFontSizeOverride("font_size", 11);
        nameHBox.AddChild(countLabel);

        vbox.AddChild(nameHBox);

        var bonusLabel = new Label
        {
            Text = $"{fossil.PrimaryBonus} | {fossil.BlockedGroup}",
            Modulate = new Color(0.7f, 0.75f, 0.85f)
        };
        bonusLabel.AddThemeFontSizeOverride("font_size", 9);
        vbox.AddChild(bonusLabel);

        hbox.AddChild(vbox);
        card.AddChild(hbox);
        return card;
    }

    #endregion

    #region 1-Click Dump & Footer Summary

    public void OnDumpAllPressed()
    {
        if (_player == null) return;

        var candidates = _candidateGearProvider?.Invoke();
        var result = _player.Stash.DumpAll(_player, _db?.Maps, candidates);

        RefreshView();

        if (result.IsEmpty)
        {
            ShowDumpToast(Loc.Tr("UI_STASH_DUMP_NOTHING"));
        }
        else
        {
            ShowDumpToast(Loc.Format("UI_STASH_DUMP_SUCCESS", result.TotalItemsDumped));
        }
    }

    private void ShowDumpToast(string message)
    {
        if (_dumpStatusLabel != null)
        {
            _dumpStatusLabel.Text = message;
            _dumpStatusLabel.Modulate = new Color(1f, 0.9f, 0.4f);
        }
    }

    public void RefreshSummary()
    {
        if (_summaryLabel == null || _player == null) return;

        var stash = _player.Stash;
        _summaryLabel.Text = Loc.Format(
            "UI_STASH_SUMMARY_BAR",
            stash.TotalCurrencyCount,
            stash.TotalGearCount,
            stash.TotalMapCount,
            stash.TotalFossilCount);
    }

    #endregion
}
