using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using IdleExile.Core.Models;
using IdleExile.Core.Services;
using IdleExile.Core.Simulation;
using IdleExile.Core.State;
using IdleExile.UI.Components;

namespace IdleExile.UI;

public partial class Main : Control
{
    private Label? _statusLabel;
    private Label? _statsLabel;
    private Label? _charInfoLabel;
    private Label? _dpsLabel;
    private CheckBox? _alwaysOnTopCheckBox;
    private BattleViewport? _battleViewport;
    private MapDeviceBar? _mapDeviceBar;
    private AtlasView? _atlasView;
    private EquipmentView? _equipmentView;
    private GemView? _gemView;
    private PassiveTreeView? _passiveTreeView;
    private StashView? _stashView;
    private SanctuaryView? _sanctuaryView;
    private Button? _gearNavButton;
    private Button? _resizeModeButton;
    private Button? _screenCycleButton;
    private Button? _emergencyPortalButton;
    private Button? _tabGearBtn;
    private Button? _tabSkillsBtn;
    private Button? _tabAtlasBtn;
    private Button? _tabPassivesBtn;
    private Button? _tabStashBtn;
    private Button? _tabSanctuaryBtn;
    private Control? _subTabBarContainer;
    private Control? _tabWorkspaceContainer;
    private Control? _combatViewportContainer;
    private Control? _mapDeviceMargin;
    private EdgeGlowAlert? _edgeGlowAlert;
    private Button? _closeButton;
    private Control? _topHeaderBar;
    private readonly WindowController _windowController = new();

#if DEBUG
    private DiagnosticModal? _diagnosticModal;
#endif

    public enum WorkspaceTab
    {
        Gear,
        Skills,
        Atlas,
        Passives,
        Stash,
        Sanctuary
    }

    private WorkspaceTab _activeTab = WorkspaceTab.Gear;
    private WorkspaceTab _previousTab = WorkspaceTab.Gear;
    private Tween? _tabTransitionTween;
    public const float TabTransitionDuration = 0.15f;
    public const float TabTransitionSlideOffset = 18.0f;

    public Tween? ActiveTabTransitionTween => _tabTransitionTween;
    public WorkspaceTab PreviousTab => _previousTab;

    public WindowController WindowController => _windowController;
    public WorkspaceTab ActiveTab => _activeTab;
    public Control? SubTabBarContainer => _subTabBarContainer;
    public Control? TabWorkspaceContainer => _tabWorkspaceContainer;
    public Control? CombatViewportContainer => _combatViewportContainer;
    public BattleViewport? BattleViewport => _battleViewport;
    public EquipmentView? EquipmentView => _equipmentView;
    public GemView? GemView => _gemView;
    public AtlasView? AtlasView => _atlasView;
    public PassiveTreeView? PassiveTreeView => _passiveTreeView;
    public StashView? StashView => _stashView;
    public SanctuaryView? SanctuaryView => _sanctuaryView;
    public Control? MapDeviceMargin => _mapDeviceMargin;
    public EdgeGlowAlert? EdgeGlowAlert => _edgeGlowAlert;
    public Button? ResizeModeButton => _resizeModeButton;
    public Button? CloseButton => _closeButton;
    public Control? TopHeaderBar => _topHeaderBar;
    public Button? TabSanctuaryBtn => _tabSanctuaryBtn;
    public Label? DpsLabel => _dpsLabel;

    public DatabaseLoader Db { get; } = new();
    public PlayerState Player { get; } = new();
    public SkillLoadout Loadout { get; } = new();
    public FtueManager Ftue { get; } = new();
    public MapQueueManager MapQueue { get; } = new();
    public SeededDropCoordinator DropCoordinator { get; private set; } = null!;
    public CombatEngine? Engine { get; private set; }

    public override void _Ready()
    {
        // 1. Set window title
        DisplayServer.WindowSetTitle("Idle Exile - Actless ARPG");

        // 2. Setup dynamic WindowController (from reference/windows)
        AddChild(_windowController);
        _windowController.Setup(GetWindow());
        _windowController.WindowResized += OnWindowResized;

        // 3. UI Node binding conforming to 5-tier layout
        _statusLabel = GetNodeOrNull<Label>("%StatusLabel");
        _statsLabel = GetNodeOrNull<Label>("%StatsLabel");
        _charInfoLabel = GetNodeOrNull<Label>("%CharInfoLabel");
        _alwaysOnTopCheckBox = GetNodeOrNull<CheckBox>("%AlwaysOnTopCheck");
        _resizeModeButton = GetNodeOrNull<Button>("%ResizeModeButton");
        _screenCycleButton = GetNodeOrNull<Button>("%ScreenCycleButton");
        _closeButton = GetNodeOrNull<Button>("%CloseButton");
        _topHeaderBar = GetNodeOrNull<Control>("%TopHeaderBar");
        _battleViewport = GetNodeOrNull<BattleViewport>("%BattleViewport");
        _dpsLabel = GetNodeOrNull<Label>("%DpsLabel");
        if (_battleViewport != null)
        {
            _battleViewport.DpsLabel = _dpsLabel;
            _battleViewport.StatusLabel = _statusLabel;
            _battleViewport.CurrentMode = Arena2D.ViewportMode.SanctuaryIdle;
            _battleViewport.SetSanctuaryStatus();
        }
        _mapDeviceBar = GetNodeOrNull<MapDeviceBar>("%MapDeviceBar");
        _atlasView = GetNodeOrNull<AtlasView>("%AtlasView");
        _equipmentView = GetNodeOrNull<EquipmentView>("%EquipmentView");
        _gemView = GetNodeOrNull<GemView>("%GemView");
        _passiveTreeView = GetNodeOrNull<PassiveTreeView>("%PassiveTreeView");
        _stashView = GetNodeOrNull<StashView>("%StashView");
        _sanctuaryView = GetNodeOrNull<SanctuaryView>("%SanctuaryView");
        _emergencyPortalButton = GetNodeOrNull<Button>("%EmergencyPortalButton");
        _subTabBarContainer = GetNodeOrNull<Control>("%SubTabBarContainer");
        _tabWorkspaceContainer = GetNodeOrNull<Control>("%TabWorkspaceContainer");
        _combatViewportContainer = GetNodeOrNull<Control>("%CombatViewportContainer");
        _mapDeviceMargin = GetNodeOrNull<Control>("%MapDeviceMargin");
        _edgeGlowAlert = GetNodeOrNull<EdgeGlowAlert>("%EdgeGlowAlert");

        _tabGearBtn = GetNodeOrNull<Button>("%TabGearBtn");
        _tabSkillsBtn = GetNodeOrNull<Button>("%TabSkillsBtn");
        _tabAtlasBtn = GetNodeOrNull<Button>("%TabAtlasBtn");
        _tabPassivesBtn = GetNodeOrNull<Button>("%TabPassivesBtn");
        _tabStashBtn = GetNodeOrNull<Button>("%TabStashBtn");
        _tabSanctuaryBtn = GetNodeOrNull<Button>("%TabSanctuaryBtn");
        _gearNavButton = _tabGearBtn ?? GetNodeOrNull<Button>("%GearNavButton");

        if (_tabPassivesBtn != null)
        {
            _edgeGlowAlert?.AttachPassiveTabGlow(_tabPassivesBtn);
        }

        Player.OnLevelUp += OnPlayerLevelUp;
        Player.OnBootstrapped += archetype =>
        {
            if (_charInfoLabel != null)
            {
                _charInfoLabel.Text = $"Lv.{Player.Level} {archetype.DisplayName}";
            }
            UpdatePassiveTreeAlert();
        };

        if (_charInfoLabel != null)
        {
            string charName = Player.Archetype?.DisplayName ?? "Exile";
            _charInfoLabel.Text = $"Lv.{Player.Level} {charName}";
        }

        if (_alwaysOnTopCheckBox != null)
        {
            _alwaysOnTopCheckBox.ButtonPressed = _windowController.DefaultAlwaysOnTop;
            _alwaysOnTopCheckBox.Toggled += OnAlwaysOnTopToggled;
        }

        if (_resizeModeButton != null)
        {
            _resizeModeButton.Pressed += OnResizeButtonPressed;
            UpdateResizeButtonText(_windowController.IsExpanded);
        }

        if (_screenCycleButton != null)
        {
            _screenCycleButton.Pressed += OnScreenCyclePressed;
        }

        if (_closeButton != null)
        {
            _closeButton.Pressed += OnCloseButtonPressed;
            _closeButton.TooltipText = Loc.Tr("UI_WINDOW_CLOSE_TOOLTIP");
        }

        if (_topHeaderBar != null)
        {
            _topHeaderBar.GuiInput += OnTopHeaderBarGuiInput;
        }

        if (_emergencyPortalButton != null)
        {
            _emergencyPortalButton.Text = "🚀 Launch Map";
            _emergencyPortalButton.Pressed += () =>
            {
                if (Engine is { IsInCombat: true })
                {
                    OnEmergencyPortalTriggered();
                }
                else
                {
                    LaunchNextMapFromSanctuary();
                }
            };
        }

        // Wire Tab Navigation with exclusive ButtonGroup
        var tabGroup = new ButtonGroup();
        if (_tabGearBtn != null) _tabGearBtn.ButtonGroup = tabGroup;
        if (_tabSkillsBtn != null) _tabSkillsBtn.ButtonGroup = tabGroup;
        if (_tabAtlasBtn != null) _tabAtlasBtn.ButtonGroup = tabGroup;
        if (_tabPassivesBtn != null) _tabPassivesBtn.ButtonGroup = tabGroup;
        if (_tabStashBtn != null) _tabStashBtn.ButtonGroup = tabGroup;
        if (_tabSanctuaryBtn != null) _tabSanctuaryBtn.ButtonGroup = tabGroup;

        if (_tabGearBtn != null) _tabGearBtn.Pressed += () => SwitchWorkspaceTab(WorkspaceTab.Gear);
        if (_tabSkillsBtn != null) _tabSkillsBtn.Pressed += () => SwitchWorkspaceTab(WorkspaceTab.Skills);
        if (_tabAtlasBtn != null) _tabAtlasBtn.Pressed += () => SwitchWorkspaceTab(WorkspaceTab.Atlas);
        if (_tabPassivesBtn != null) _tabPassivesBtn.Pressed += () => SwitchWorkspaceTab(WorkspaceTab.Passives);
        if (_tabStashBtn != null) _tabStashBtn.Pressed += () => SwitchWorkspaceTab(WorkspaceTab.Stash);
        if (_tabSanctuaryBtn != null) _tabSanctuaryBtn.Pressed += () => SwitchWorkspaceTab(WorkspaceTab.Sanctuary);

        if (_gearNavButton != null && _gearNavButton != _tabGearBtn)
        {
            _gearNavButton.Pressed += OnGearNavPressed;
        }

        if (_equipmentView != null)
        {
            _equipmentView.OnCloseRequested += OnEquipmentClosed;
            _equipmentView.SetPlayer(Player);
        }

        Player.Gear.OnEquipmentChanged += () =>
        {
            _battleViewport?.UpdateEquippedWeapons(Player.MainHand?.BaseDefinition, Player.Gear.OffHand);
        };

        if (_mapDeviceBar != null)
        {
            _mapDeviceBar.OnMapActivated += OnMapActivated;
            _mapDeviceBar.OnEmergencyPortalTriggered += OnEmergencyPortalTriggered;
        }

        if (_atlasView != null)
        {
            _atlasView.OnMapActivated += (map, mods, rarity) => OnMapActivatedWithMods(map, mods, rarity);
            _atlasView.OnEmergencyPortalTriggered += OnEmergencyPortalTriggered;
        }

        // 4. Load JSON Database
        Db.LoadAll();

        // 4b. Initialize GemView, AtlasView, PassiveTreeView, and StashView with database and state
        _gemView?.Initialize(Loadout, Db, Player);
        _atlasView?.Initialize(Db, Player, MapQueue, Ftue);
        _passiveTreeView?.Initialize(Player, Db);
        _stashView?.Initialize(
            Player,
            Db,
            MapQueue,
            Engine,
            () => _equipmentView?.CandidateItems ?? Enumerable.Empty<IGearItem>());

        _sanctuaryView?.Initialize(Player, Db, MapQueue);
        if (_sanctuaryView != null)
        {
            _sanctuaryView.OnToastRequested += (msg, col) => _battleViewport?.ShowLootToast(msg, col);
            _sanctuaryView.OnDescendToAbyssRequested += () =>
            {
                _battleViewport?.ShowLootToast("⛏ Subterranean Endless Abyss initiated!", new Color(0.95f, 0.85f, 0.3f));
            };
        }

        if (_stashView != null)
        {
            _stashView.OnMapActivated += (map, mods, rarity) => OnMapActivatedWithMods(map, mods, rarity);
        }

        if (_equipmentView != null)
        {
            _equipmentView.OnItemStashed += (slot, item) =>
            {
                Player.Stash.DepositGear(item);
                _stashView?.RefreshView();
            };
        }

        if (_passiveTreeView?.TreeState != null)
        {
            _passiveTreeView.TreeState.OnTreeChanged += () =>
            {
                if (Engine != null && _passiveTreeView.TreeState != null)
                {
                    Engine.PassiveStats = _passiveTreeView.TreeState.BuildAggregatedOffensiveBucket();
                }
                UpdatePassiveTreeAlert();
            };
            UpdatePassiveTreeAlert();
        }

        MapQueue.OnCountdownTick += seconds =>
        {
            if (_statusLabel != null && MapQueue.IsCountdownActive)
            {
                _statusLabel.Text = $"Map Cleared! Loot window • Next map launching in {seconds:F1}s... [Queue: {MapQueue.Count}]";
            }
        };

        // 5. Initialize FTUE Seeded Drop Coordinator
        DropCoordinator = new SeededDropCoordinator(Ftue);

        // 6. Initialize Macro Footer Telemetry & Diagnostics
        if (_statusLabel != null)
        {
            _statusLabel.Text = "Crucible Online • Ready to Map";
        }

#if DEBUG
        if (_statsLabel != null)
        {
            _statsLabel.Text = "Auto-Run • Space: Escape [F12: Diag]";
            _statsLabel.TooltipText = $"Database Registry:\n" +
                $"• {Db.Affixes.Count} Affixes\n" +
                $"• {Db.WeaponBases.Count} Weapon Bases\n" +
                $"• {Db.ArmourBases.Count} Armour Bases\n" +
                $"• {Db.AccessoryBases.Count} Accessory Bases\n" +
                $"• {Db.Gems.Count} Skill Gems\n" +
                $"• {Db.Maps.Count} Maps ({Db.MapModifiers.Count} Modifiers)\n" +
                $"\nClick or Press F12 to inspect System Diagnostics.";

            _statsLabel.MouseFilter = Control.MouseFilterEnum.Stop;
            _statsLabel.GuiInput += @ev =>
            {
                if (@ev is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
                {
                    ToggleDiagnosticModal();
                }
            };
        }
#else
        if (_statsLabel != null)
        {
            _statsLabel.Text = "Auto-Run • Space: Escape";
            _statsLabel.TooltipText = "Idle Exile Companion • Auto-Mapping Engine";
        }
#endif

        // 7. Synchronize initial layout presentation with WindowController mode
        OnWindowResized(_windowController.WindowSize, _windowController.IsExpanded);

        // 8. Run Core Unit Tests only if explicitly requested via CLI argument ('--run-core-tests')
        if (OS.GetCmdlineArgs().Contains("--run-core-tests") || OS.GetCmdlineUserArgs().Contains("--run-core-tests"))
        {
            RunCoreUnitTests(this);
        }

        // 7. Check FTUE: If awaiting archetype selection, spawn the 1-click modal overlay
        if (Ftue.CurrentStep == FtueStep.AwaitingArchetypeSelect)
        {
            var modalScene = GD.Load<PackedScene>("res://scenes/StarterSelectionModal.tscn");
            if (modalScene != null)
            {
                var modal = modalScene.Instantiate<StarterSelectionModal>();
                modal.OnArchetypeSelected += OnStarterArchetypeChosen;
                AddChild(modal);
            }
        }
        else
        {
            // Default demo telemetry if already bootstrapped
            InitializeDemoViewport();
        }
    }

    private void OnStarterArchetypeChosen(StarterArchetypeDefinition archetype)
    {
        // 1. Advance FTUE Step 0 -> Step 1 (AwaitingFirstMapActivate)
        Ftue.SelectArchetype(archetype);

        // 2. Initialize PlayerState with chosen archetype vitals, weapons, and inventory
        Player.BootstrapFromArchetype(archetype, Db.WeaponBases);
        if (Db.Gems.TryGetValue(archetype.ActiveGemId, out var starterGem))
        {
            Loadout.PrimarySkill.SetActiveGem(starterGem);
            _gemView?.RefreshView();
        }
        _passiveTreeView?.SetStartingClassByAttribute(archetype.PrimaryAttribute);

        // 3. Update Status and Telemetry
        if (_charInfoLabel != null)
        {
            _charInfoLabel.Text = $"Lv.{Player.Level} {archetype.DisplayName}";
        }

        if (_statusLabel != null)
        {
            _statusLabel.Text = $"Exile Awakened: {archetype.DisplayName} • Step 1: Click [ACTIVATE MAP]";
        }

        // 4. Setup Strand Map (Tier 1) as the active starter map
        Db.Maps.TryGetValue("strand", out var mapDef);
        mapDef ??= Db.Maps.GetValueOrDefault("overgrown_beach") ?? new MapDefinition(
            Id: "strand",
            Name: "Strand Map",
            Tier: 1,
            MonsterLevel: 1,
            IsFailSafe: false,
            BaseQuantity: 10,
            BasePackSize: 100,
            BossName: "Tidal Behemoth",
            Description: "A windswept coastline."
        );

        // 5. Update Vitometer with actual PlayerState numbers
        if (_battleViewport != null)
        {
            _battleViewport.BindPlayer(Player, Loadout);

            float maxEhp = Player.MaxLife + Player.MaxEnergyShield;
            string defenseSummary = archetype.PrimaryAttribute switch
            {
                "Strength" => $"EHP: {maxEhp:F0} | Armour: {archetype.BaseDefenses.Armour:F0}",
                "Intelligence" => $"EHP: {maxEhp:F0} | Fire Res: {archetype.BaseDefenses.FireResistance:F0}%",
                "Dexterity" => $"EHP: {maxEhp:F0} | Evasion: {archetype.BaseDefenses.EvasionChance:F0}%",
                _ => $"EHP: {maxEhp:F0}"
            };

            _battleViewport.UpdateVitals(
                Player.CurrentLife, Player.MaxLife,
                Player.CurrentEnergyShield, Player.MaxEnergyShield,
                Player.CurrentMana, Player.MaxMana,
                0.0,
                defenseSummary
            );

            var config = new MapRunConfig(mapDef, System.Array.Empty<RolledMapModifier>());
            _battleViewport.SetMap(config, progressPercent: 0f, rarity: "Normal");
        }

        // 6. Equip starter armour & jewellery conforming to archetype
        string helmId = archetype.PrimaryAttribute switch { "Intelligence" => "mind_cage", "Dexterity" => "leather_cap", _ => "iron_helmet" };
        string bodyId = archetype.PrimaryAttribute switch { "Intelligence" => "simple_robe", "Dexterity" => "leather_tunic", _ => "plate_vest" };
        string gloveId = archetype.PrimaryAttribute switch { "Intelligence" => "silk_gloves", "Dexterity" => "rawhide_gloves", _ => "iron_gauntlets" };
        string bootId = archetype.PrimaryAttribute switch { "Intelligence" => "scholar_boots", "Dexterity" => "rawhide_boots", _ => "iron_greaves" };
        string amuId = archetype.PrimaryAttribute switch { "Intelligence" => "lapis_amulet", "Dexterity" => "jade_amulet", _ => "amber_amulet" };

        if (Db.ArmourBases.TryGetValue(helmId, out var helmDef)) Player.Gear.Equip(GearSlot.Helmet, new ArmourItemInstance(helmDef));
        if (Db.ArmourBases.TryGetValue(bodyId, out var bodyDef)) Player.Gear.Equip(GearSlot.BodyArmour, new ArmourItemInstance(bodyDef));
        if (Db.ArmourBases.TryGetValue(gloveId, out var gloveDef)) Player.Gear.Equip(GearSlot.Gloves, new ArmourItemInstance(gloveDef));
        if (Db.ArmourBases.TryGetValue(bootId, out var bootDef)) Player.Gear.Equip(GearSlot.Boots, new ArmourItemInstance(bootDef));
        if (Db.AccessoryBases.TryGetValue(amuId, out var amuDef)) Player.Gear.Equip(GearSlot.Amulet, new AccessoryItemInstance(amuDef));
        if (Db.AccessoryBases.TryGetValue("leather_belt", out var beltDef)) Player.Gear.Equip(GearSlot.Belt, new AccessoryItemInstance(beltDef));
        if (Db.AccessoryBases.TryGetValue("iron_ring", out var ring1Def)) Player.Gear.Equip(GearSlot.Ring1, new AccessoryItemInstance(ring1Def));
        if (Db.AccessoryBases.TryGetValue("two_stone_ring", out var ring2Def)) Player.Gear.Equip(GearSlot.Ring2, new AccessoryItemInstance(ring2Def));

        _equipmentView?.SetPlayer(Player);
        _battleViewport?.UpdateEquippedWeapons(Player.MainHand?.BaseDefinition, Player.Gear.OffHand);

        // 7. Task 2.3: Auto-load Tier 1 Strand Map into Map Device tray & Golden Pulse
        _mapDeviceBar?.SlotMap(mapDef, "Normal");
        _mapDeviceBar?.SetGoldenPulse(true);
        _atlasView?.SlotMap(mapDef, null, "Normal");
        _atlasView?.SetGoldenPulse(true);

        // Seed starter crafting currencies and fossils for Sanctuary Anvil testing
        Player.Stash.DepositCurrency("orb_of_transmutation", 50);
        Player.Stash.DepositCurrency("orb_of_alteration", 50);
        Player.Stash.DepositCurrency("chaos_orb", 30);
        Player.Stash.DepositCurrency("exalted_orb", 5);
        Player.Stash.DepositCurrency("orb_of_annulment", 10);
        Player.Stash.DepositCurrency("divine_orb", 5);
        Player.Stash.DepositCurrency("vaal_orb", 10);

        Player.Stash.DepositFossil("pristine_fossil", 10);
        Player.Stash.DepositFossil("metallic_fossil", 10);
        Player.Stash.DepositFossil("scorched_fossil", 10);
        Player.Stash.DepositFossil("frigid_fossil", 10);
        Player.Stash.DepositFossil("dense_fossil", 10);
        Player.Stash.DepositFossil("corroded_fossil", 10);

        Player.Sanctuary.AddDust(14200);
        _sanctuaryView?.RefreshTargetItemList();
        _sanctuaryView?.RefreshView();

        GD.Print($"[Main] Character bootstrapped: {archetype.DisplayName}, Life: {Player.MaxLife}, ES: {Player.MaxEnergyShield}, Mana: {Player.MaxMana}");
        GD.Print($"[Main] Equipped Weapon: MainHand={Player.MainHand?.BaseDefinition.Name ?? "None"} ({Player.MainHand?.ComputedMinDamage}-{Player.MainHand?.ComputedMaxDamage} Phys, APS: {Player.MainHand?.ComputedAps}), OffHand={Player.OffHand?.BaseDefinition.Name ?? "None"}, DualWield={Player.IsDualWielding}");
        GD.Print($"[Main] FTUE Step: {Ftue.CurrentStep} ({Ftue.GetCurrentInstruction()})");
        GD.Print($"[Main] Inventory: {Player.Inventory.Count} items, {Player.MapInventory.Count} maps");
    }

    private void OnMapActivated(MapDefinition map)
    {
        OnMapActivatedWithMods(map, System.Array.Empty<RolledMapModifier>(), "Normal");
    }

    private void OnMapActivatedWithMods(MapDefinition map, IReadOnlyList<RolledMapModifier>? modifiers, string rarity = "Normal")
    {
        // 1. Advance FTUE Step 1 -> Step 2 (FirstCombatWave)
        if (Ftue.CurrentStep == FtueStep.AwaitingFirstMapActivate)
        {
            Ftue.ActivateFirstMap();
        }

        // 2. Turn off Golden Pulse & set portal active
        _mapDeviceBar?.SetGoldenPulse(false);
        _mapDeviceBar?.SetPortalActive(true);
        _atlasView?.SetGoldenPulse(false);
        _atlasView?.SetPortalActive(true);

        // 3. Configure BattleViewport with the active map & start CombatEngine
        var rolledMods = modifiers ?? System.Array.Empty<RolledMapModifier>();
        var config = new MapRunConfig(map, rolledMods);
        _battleViewport?.ResetForNewMap();
        _battleViewport?.SetMap(config, progressPercent: 0f, rarity: rarity);
        _battleViewport?.BindPlayer(Player, Loadout);
        _battleViewport?.SetViewportMode(Arena2D.ViewportMode.ExpeditionCombat);

        Engine = new CombatEngine(Player, Loadout, dropCoordinator: DropCoordinator, smelter: new SmelterService(Db.SmelterRecipes));
        if (_passiveTreeView?.TreeState != null)
        {
            Engine.PassiveStats = _passiveTreeView.TreeState.BuildAggregatedOffensiveBucket();
        }

        Engine.OnWaveSpawned += wave =>
        {
            _battleViewport?.SetArenaMonsters(wave.Monsters);
        };

        Engine.OnDamageDealt += args =>
        {
            if (_battleViewport?.Arena != null)
            {
                var pos = _battleViewport.Arena.GetMonsterPosition(args.Target.InstanceId);
                _battleViewport.HandleMonsterDamageDealt(
                    args.Target.InstanceId,
                    args.TotalDamageDealt,
                    args.Target.CurrentLife,
                    pos,
                    args.Target.PrimaryDamageType,
                    args.IsCrit,
                    args.IsDoT
                );
            }
        };

        Engine.OnPlayerDamaged += args =>
        {
            if (_battleViewport != null)
            {
                float reservedPct = Player.MaxMana > 0f ? (Player.ReservedMana / Player.MaxMana) * 100f : 0f;
                _battleViewport.UpdateVitals(
                    Player.CurrentLife, Player.MaxLife,
                    Player.CurrentEnergyShield, Player.MaxEnergyShield,
                    Player.CurrentMana, Player.MaxMana,
                    reservedPct
                );
            }
        };

        Engine.OnTickProcessed += args =>
        {
            _battleViewport?.SetMapProgress(args.ClearProgress * 100f);
            if (Engine != null)
            {
                _battleViewport?.UpdateDps(Engine.DpsTracker.GetSnapshot());
                _battleViewport?.UpdateExileWorldPosition(
                    new Vector2(Engine.ExilePosition.X, Engine.ExilePosition.Y),
                    Engine.Movement.CurrentState,
                    Engine.Movement.CurrentSpeed,
                    Engine.Movement.MapLength
                );

#if DEBUG
                if (OS.IsDebugBuild())
                {
                    _battleViewport?.UpdateDebugCoordinates(
                        new Vector2(Engine.ExilePosition.X, Engine.ExilePosition.Y),
                        Engine.Movement.CurrentState,
                        Engine.Movement.CurrentSpeed,
                        Engine.Movement.DistanceTraveled,
                        Engine.Movement.MapLength,
                        Engine.Movement.MapProgressPercent
                    );
                    _diagnosticModal?.UpdateLiveTelemetry(Engine);
                }
#endif
            }

            // Sync monster positions from core simulation (monsters advance toward Exile each tick)
            if (_battleViewport?.Arena != null && Engine?.CurrentWave != null)
            {
                _battleViewport.Arena.SyncMonsterPositions(Engine.CurrentWave.Monsters);

                // Track CombatEngine current target for continuous Auto-Facing
                if (Engine?.CurrentTarget != null && Engine.CurrentTarget.IsAlive)
                {
                    Vector2 screenTarget;
                    if (!_battleViewport.TryGetMonsterPosition(Engine.CurrentTarget.InstanceId, out screenTarget))
                    {
                        screenTarget = _battleViewport.Arena.Camera.WorldToScreen(
                            new Vector2(Engine.CurrentTarget.Position.X, Engine.CurrentTarget.Position.Y),
                            Arena2D.CenterPosition);
                    }

                    _battleViewport.SetTarget(Engine.CurrentTarget.InstanceId, screenTarget);
                }
                else
                {
                    _battleViewport.SetTarget(null, null);
                }
            }

            if (_statusLabel != null && Engine != null)
            {
                _statusLabel.Text = $"Portal Active • Mapping: {map.Name} (T{map.Tier}) • State: {Engine.Movement.CurrentState} • Dist: {Engine.Movement.DistanceTraveled:F0}px ({Engine.Movement.MapProgressPercent:F1}%)";
            }
        };

        Engine.OnMovementStateChanged += (oldState, newState) =>
        {
            GD.Print($"[Movement] State transition: {oldState} -> {newState} (Position: {Engine?.Movement.Position})");
        };

        Engine.OnFogGateCrossed += () =>
        {
            GD.Print("[Movement] Fog Gate crossed at 85% map progress! Approaching Boss Arena.");
            _battleViewport?.SetFogGateSealed(true);
        };

        Engine.OnFogGateSealed += () =>
        {
            GD.Print("[Movement] Fog Gate sealed behind the Exile!");
            _battleViewport?.SetFogGateSealed(true);
        };

        Engine.OnBossArenaEntered += () =>
        {
            GD.Print("[Movement] Entered Boss Arena Chamber! Final Map Boss encounter ready.");
            _battleViewport?.AwakenBoss();
        };

        Engine.OnLeagueEncounterTriggered += args =>
        {
            _battleViewport?.WakeUpAllMonsters();
            string modInfo = !string.IsNullOrEmpty(args.Encounter.EssenceModifierName) ? $" ({args.Encounter.EssenceModifierName})" : "";
            GD.Print($"[Encounter] ✦ Activated: {args.Encounter.Name}{modInfo}!");
        };

        Engine.OnLeagueEncounterCompleted += args =>
        {
            GD.Print($"[Encounter] ✦ Completed: {args.Encounter.Name}! {args.Encounter.RewardDescription}");
        };

        Engine.OnLootDropped += drop =>
        {
            _battleViewport?.ShowLootToast($"Dropped: {drop.DisplayName}", new Color(drop.ColorHex));

            if (LootDropEvaluationHelper.IsTier1Drop(drop))
            {
                _edgeGlowAlert?.TriggerTier1Alert(drop.DisplayName);
            }

            if (drop.Type == LootDropType.Currency)
            {
                if (drop.Id.EndsWith("_fossil", StringComparison.OrdinalIgnoreCase))
                {
                    Player.Stash.DepositFossil(drop.Id, drop.Quantity);
                }
                else
                {
                    Player.Stash.DepositCurrency(drop.Id, drop.Quantity);
                }
                _stashView?.RefreshView();
            }
            else if (drop.Type == LootDropType.Map)
            {
                if (Db.Maps.TryGetValue(drop.Id, out var mapDef))
                {
                    Player.Stash.DepositMap(mapDef, drop.Quantity, drop.Rarity.ToString());
                }
                _stashView?.RefreshView();
            }
            else if (drop.Type == LootDropType.Equipment)
            {
                // Route equipment drop into candidate drawer for instant comparison
                if (Db.ArmourBases.TryGetValue("astral_plate", out var sampleArmourDef))
                {
                    var sampleArmour = new ArmourItemInstance(sampleArmourDef, drop.Rarity.ToString(), quality: 20, itemLevel: 84)
                    {
                        CustomName = drop.DisplayName
                    };
                    sampleArmour.Affixes.Add(new AffixDefinition("flat_maximum_life_t2", "+{0} to Maximum Life", AffixType.Prefix, 2, 74, 115, 115, new[] { "life", "defenses" }));
                    sampleArmour.Affixes.Add(new AffixDefinition("fire_resistance_t1", "+{0}% to Fire Resistance", AffixType.Suffix, 1, 84, 45, 45, new[] { "fire", "resistance" }));
                    sampleArmour.RecalculateStats();
                    _equipmentView?.AddCandidateItem(sampleArmour);
                }
            }
        };

        Engine.OnFilteredItemSmelted += (drop, dust) =>
        {
#if DEBUG
            if (OS.IsDebugBuild())
            {
                GD.Print($"[Smelter] Auto-smelted {drop.DisplayName} -> +{dust} Thaumaturgic Dust (Total: {Player.Sanctuary.ThaumaturgicDust:N0})");
            }
#endif
        };

        Engine.OnBossSlamTelegraphed += args =>
        {
            if (_battleViewport?.Arena != null)
            {
                var slamPos = args.TargetPosition.HasValue && args.TargetPosition.Value != Vector2D.Zero
                    ? _battleViewport.Arena.Camera.WorldToScreen(new Vector2(args.TargetPosition.Value.X, args.TargetPosition.Value.Y), Arena2D.CenterPosition)
                    : _battleViewport.Arena.GetMonsterPosition(args.Boss.InstanceId);
                _battleViewport.TriggerBossSlam(slamPos, args.WindUpDuration, args.Radius, args.ThreatEhpPercent);
            }
        };

        Engine.OnBossSlamDetonated += args =>
        {
            GD.Print($"[Combat] Boss Slam detonated at ({args.Position.X:F0}, {args.Position.Y:F0}) - Threat: {args.ThreatEhpPercent:F0}% EHP! Hit Player: {args.HitPlayer}");
        };

        // Unified Combat Attack Visual Pipeline (Both Player and Monsters use the same pipeline)
        Engine.OnAttackExecuted += args =>
        {
            _battleViewport?.ExecuteAttackVisual(args);
        };

        Engine.OnProjectileImpacted += args =>
        {
            if (_battleViewport?.Arena != null)
            {
                var screenPos = _battleViewport.Arena.Camera.WorldToScreen(
                    new Vector2(args.ImpactPoint.X, args.ImpactPoint.Y),
                    Arena2D.CenterPosition);
                _battleViewport.TriggerProjectileImpact(screenPos, args.DamageType, args.AoeRadius, args.IsCrit);
            }
        };

        Engine.OnMapCompleted += args =>
        {
            GD.Print($"[Main] Map cleared: {args.Config.Map.Name} (T{args.Config.Map.Tier}) in {args.ClearTimeSeconds:F1}s! Monsters slain: {args.TotalMonstersKilled}");

            if (MapQueue.AutoAdvanceEnabled && (MapQueue.Count > 0 || MapQueue.ContinuousRunEnabled))
            {
                MapQueue.StartCountdown(3.0f);
                if (_statusLabel != null)
                {
                    _statusLabel.Text = $"Map Cleared! Loot window • Next map in 3.0s... [Queue: {MapQueue.Count}]";
                }
                _battleViewport?.ShowLootToast($"Map Cleared! Next map in 3s... ({MapQueue.Count} queued)", new Color(1.0f, 0.85f, 0.35f));
            }
            else
            {
                _mapDeviceBar?.SetPortalActive(false);
                _atlasView?.SetPortalActive(false);
                if (_emergencyPortalButton != null)
                {
                    _emergencyPortalButton.Text = "🚀 Launch Map";
                }
                Player.Position = new Vector2D(ExileMovementController.DefaultStartX, ExileMovementController.DefaultCenterY);
                _battleViewport?.SetViewportMode(Arena2D.ViewportMode.SanctuaryIdle);
                _battleViewport?.SetSanctuaryStatus();
            }
        };

        Engine.StartMap(config);

        // 4. Update status display & buttons
        if (_emergencyPortalButton != null)
        {
            _emergencyPortalButton.Text = "🌀 Emergency Portal";
        }

        if (_statusLabel != null)
        {
            _statusLabel.Text = $"Portal Active • Mapping: {map.Name} (T{map.Tier}) • 30Hz Combat Engine Active";
        }

#if DEBUG
        if (_statsLabel != null)
        {
            _statsLabel.Text = "Auto-Run Active • Space: Emergency [F12: Diag]";
        }
#else
        if (_statsLabel != null)
        {
            _statsLabel.Text = "Auto-Run Active • Space: Emergency Portal";
        }
#endif

        GD.Print($"[Main] Map activated: {map.Name} (T{map.Tier}). CombatEngine 30Hz running.");
    }

    private void OnEmergencyPortalTriggered()
    {
        MapQueue.CancelCountdown();
        Engine?.TriggerEmergencyPortal();
        _mapDeviceBar?.SetPortalActive(false);
        _atlasView?.SetPortalActive(false);
        if (_emergencyPortalButton != null)
        {
            _emergencyPortalButton.Text = "🚀 Launch Map";
        }
        Player.Position = new Vector2D(ExileMovementController.DefaultStartX, ExileMovementController.DefaultCenterY);
        _battleViewport?.SetViewportMode(Arena2D.ViewportMode.SanctuaryIdle);
        _battleViewport?.SetSanctuaryStatus();

#if DEBUG
        if (_statsLabel != null)
        {
            _statsLabel.Text = "Auto-Run • Space: Escape [F12: Diag]";
        }
#else
        if (_statsLabel != null)
        {
            _statsLabel.Text = "Auto-Run • Space: Escape";
        }
#endif

        GD.Print("[Main] Returned to Crucible via Emergency Portal.");
    }

    /// <summary>
    /// Launches the next map from the Map Queue or slotted Atlas map (Prompt 5 / Task 5.5).
    /// Used by contextual Spacebar hotkey or bottom action bar when not in combat.
    /// </summary>
    public void LaunchNextMapFromSanctuary()
    {
        // 1. If map queue has items, dequeue and launch next map
        if (MapQueue.Count > 0)
        {
            var next = MapQueue.DequeueNext();
            if (next != null)
            {
                GD.Print($"[Sanctuary] Launched queued map: {next.Map.Name} (T{next.Map.Tier})");
                _atlasView?.SlotMap(next.Map, next.Modifiers, next.Rarity);
                _mapDeviceBar?.SlotMap(next.Map, next.Rarity);
                OnMapActivatedWithMods(next.Map, next.Modifiers, next.Rarity);
                return;
            }
        }

        // 2. Check if a map is already slotted in AtlasView or MapDeviceBar
        var slottedMap = _atlasView?.SlottedMap ?? _mapDeviceBar?.SlottedMap;
        if (slottedMap != null)
        {
            GD.Print($"[Sanctuary] Launched slotted map: {slottedMap.Name} (T{slottedMap.Tier})");
            OnMapActivatedWithMods(slottedMap, _atlasView?.SlottedModifiers, _atlasView?.SlottedRarity ?? "Normal");
            return;
        }

        // 3. Fallback to default Strand map if available in database
        if (Db.Maps.TryGetValue("strand", out var defaultMap) || Db.Maps.TryGetValue("ruined_courtyard", out defaultMap))
        {
            GD.Print($"[Sanctuary] Launched default map: {defaultMap.Name} (T{defaultMap.Tier})");
            OnMapActivatedWithMods(defaultMap, Array.Empty<RolledMapModifier>(), "Normal");
            return;
        }

        _battleViewport?.ShowLootToast("No maps queued or slotted in Crucible!", new Color(1.0f, 0.4f, 0.4f));
    }

    public override void _Process(double delta)
    {
        Engine?.Update((float)delta);

        if (Engine is { IsInCombat: true })
        {
            _battleViewport?.UpdateDps(Engine.DpsTracker.GetSnapshot());
        }

        if (MapQueue.UpdateTick((float)delta, out var nextMap) && nextMap != null)
        {
            GD.Print($"[Main] Auto-launching next queued map: {nextMap.Map.Name} (T{nextMap.Map.Tier} {nextMap.Rarity})");
            _atlasView?.SlotMap(nextMap.Map, nextMap.Modifiers, nextMap.Rarity);
            _mapDeviceBar?.SlotMap(nextMap.Map, nextMap.Rarity);
            OnMapActivatedWithMods(nextMap.Map, nextMap.Modifiers, nextMap.Rarity);
        }
    }

    private void InitializeDemoViewport()
    {
        if (_battleViewport == null) return;

        if (_charInfoLabel != null)
        {
            _charInfoLabel.Text = "Lv.72 Juggernaut";
        }

        _battleViewport.UpdateEquippedWeapons(Player.MainHand?.BaseDefinition, Player.Gear.OffHand);

        Db.Maps.TryGetValue("ruined_courtyard", out var mapDef);
        mapDef ??= new MapDefinition(
            Id: "strand",
            Name: "Strand Map",
            Tier: 14,
            MonsterLevel: 81,
            IsFailSafe: false,
            BaseQuantity: 20,
            BasePackSize: 100,
            BossName: "Tidal Behemoth",
            Description: "A windswept coastline."
        );

        var rolledMods = new List<RolledMapModifier>();
        foreach (var modDef in Db.MapModifiers)
        {
            rolledMods.Add(new RolledMapModifier(modDef, (modDef.Min + modDef.Max) / 2f));
        }

        var config = new MapRunConfig(mapDef, rolledMods);
        _battleViewport.SetViewportMode(Arena2D.ViewportMode.ExpeditionCombat);
        _battleViewport.SetMap(config, progressPercent: 68.0f, rarity: "Rare", leagueEncounter: "Breach");
        _battleViewport.UpdateVitals(3450, 3450, 1200, 1200, 82, 820, 90.0, "EHP: 14.2k | Armour: 8.5k | Block: 75%");
        _battleViewport.SetGuardSkill(true, 480f, 500f, "Steelskin");
        _battleViewport.SpawnFloatingDamage(8420f, new Vector2(165, 120), DamageType.Cold, isCrit: true);
        _battleViewport.UpdateDps(new CombatDpsSnapshot(
            CurrentDps: 142580f,
            PeakDps: 389120f,
            AverageDps: 118400f,
            PhysicalPercent: 42.0f,
            FirePercent: 38.0f,
            ColdPercent: 0.0f,
            LightningPercent: 15.0f,
            ChaosPercent: 5.0f,
            HitRateAps: 3.45f,
            AccuracyPercent: 100f
        ));

        _mapDeviceBar?.SlotMap(mapDef, "Rare");
        _mapDeviceBar?.SetPortalActive(true);
        _atlasView?.SlotMap(mapDef, rolledMods, "Rare");
        _atlasView?.SetPortalActive(true);

        // Ensure starter skill gems are set in demo mode if empty
        if (Loadout.PrimarySkill.ActiveGem == null)
        {
            if (Db.Gems.TryGetValue("cleave", out var cleaveGem))
            {
                Loadout.PrimarySkill.SetActiveGem(cleaveGem);
            }
            else if (Db.Gems.TryGetValue("fireball", out var fireballGem))
            {
                Loadout.PrimarySkill.SetActiveGem(fireballGem);
            }

            if (Db.Gems.TryGetValue("added_fire_damage", out var addedFire))
            {
                Loadout.PrimarySkill.AddSupportGem(addedFire);
            }
            if (Db.Gems.TryGetValue("melee_physical_damage", out var meleePhys))
            {
                Loadout.PrimarySkill.AddSupportGem(meleePhys);
            }
            _gemView?.RefreshView();
        }

        // Seed starter crafting currencies and fossils for Sanctuary Anvil testing
        Player.Stash.DepositCurrency("orb_of_transmutation", 50);
        Player.Stash.DepositCurrency("orb_of_alteration", 50);
        Player.Stash.DepositCurrency("chaos_orb", 30);
        Player.Stash.DepositCurrency("exalted_orb", 5);
        Player.Stash.DepositCurrency("orb_of_annulment", 10);
        Player.Stash.DepositCurrency("divine_orb", 5);
        Player.Stash.DepositCurrency("vaal_orb", 10);

        Player.Stash.DepositFossil("pristine_fossil", 10);
        Player.Stash.DepositFossil("metallic_fossil", 10);
        Player.Stash.DepositFossil("scorched_fossil", 10);
        Player.Stash.DepositFossil("frigid_fossil", 10);
        Player.Stash.DepositFossil("dense_fossil", 10);
        Player.Stash.DepositFossil("corroded_fossil", 10);

        Player.Sanctuary.AddDust(14200);
        _sanctuaryView?.RefreshTargetItemList();
        _sanctuaryView?.RefreshView();
    }

    /// <summary>
    /// Gets the associated Control view for the specified workspace tab.
    /// </summary>
    public Control? GetWorkspaceView(WorkspaceTab tab) => tab switch
    {
        WorkspaceTab.Gear => _equipmentView,
        WorkspaceTab.Skills => _gemView,
        WorkspaceTab.Atlas => _mapDeviceMargin,
        WorkspaceTab.Passives => _passiveTreeView,
        WorkspaceTab.Stash => _stashView,
        WorkspaceTab.Sanctuary => _sanctuaryView,
        _ => null
    };

    /// <summary>
    /// Returns the resting position for workspace tab views inside TabWorkspaceContainer.
    /// </summary>
    public Vector2 GetTabRestingPosition()
    {
        float left = 6f;
        float top = 4f;
        if (_tabWorkspaceContainer is MarginContainer mc)
        {
            left = mc.GetThemeConstant("margin_left");
            top = mc.GetThemeConstant("margin_top");
        }
        return new Vector2(left, top);
    }

    /// <summary>
    /// Switches the active view inside TabWorkspaceContainer.
    /// If animated is true, plays a 0.15s Fade In / subtle horizontal Slide micro-animation (Task 6.1).
    /// In accordance with Core Objective #1, BattleViewport is persistent and NEVER hidden.
    /// If currently in Compact Mode, automatically expands the window upward.
    /// </summary>
    public void SwitchWorkspaceTab(WorkspaceTab tab, bool animated = true)
    {
        var prev = _activeTab;
        _activeTab = tab;

        // If window is currently in compact mode, auto-expand upward to full height!
        if (!_windowController.IsExpanded)
        {
            _windowController.ExpandWindow(keepBottom: true);
            return;
        }

        ApplyTabVisibility(tab, animated: animated, previousTab: prev);
    }

    /// <summary>
    /// Applies segmented tab button states and switches child view visibility in TabWorkspaceContainer.
    /// If animated is true, smoothly transitions the newly active tab via Fade In (0->1) and horizontal Slide (18px->0px) over 0.15s.
    /// In accordance with Core Objective #1, BattleViewport is persistent and NEVER hidden.
    /// </summary>
    public void ApplyTabVisibility(WorkspaceTab tab, bool animated = false, WorkspaceTab? previousTab = null)
    {
        // Update segmented tab button states
        if (_tabGearBtn != null) _tabGearBtn.ButtonPressed = (tab == WorkspaceTab.Gear);
        if (_tabSkillsBtn != null) _tabSkillsBtn.ButtonPressed = (tab == WorkspaceTab.Skills);
        if (_tabAtlasBtn != null) _tabAtlasBtn.ButtonPressed = (tab == WorkspaceTab.Atlas);
        if (_tabPassivesBtn != null) _tabPassivesBtn.ButtonPressed = (tab == WorkspaceTab.Passives);
        if (_tabStashBtn != null) _tabStashBtn.ButtonPressed = (tab == WorkspaceTab.Stash);
        if (_tabSanctuaryBtn != null) _tabSanctuaryBtn.ButtonPressed = (tab == WorkspaceTab.Sanctuary);

        // Cancel any in-flight tab transition tween
        if (_tabTransitionTween != null && _tabTransitionTween.IsValid())
        {
            _tabTransitionTween.Kill();
            _tabTransitionTween = null;
        }

        Vector2 basePos = GetTabRestingPosition();
        Control? targetView = GetWorkspaceView(tab);

        // Switch workspace views visibility:
        // Outgoing views are immediately hidden and cleanly reset to resting transform/alpha.
        // Incoming view is made visible immediately.
        foreach (WorkspaceTab t in Enum.GetValues<WorkspaceTab>())
        {
            var view = GetWorkspaceView(t);
            if (view == null) continue;

            if (t == tab)
            {
                view.Visible = true;
            }
            else
            {
                view.Visible = false;
                view.Position = basePos;
                view.Modulate = Colors.White;
            }
        }

        // Trigger view-specific data refresh
        switch (tab)
        {
            case WorkspaceTab.Gear:
                if (_equipmentView != null)
                {
                    _equipmentView.SetPlayer(Player);
                    _equipmentView.RefreshView();
                }
                break;

            case WorkspaceTab.Skills:
                _gemView?.RefreshView();
                break;

            case WorkspaceTab.Atlas:
                _atlasView?.RefreshAvailableMaps();
                break;

            case WorkspaceTab.Passives:
                _passiveTreeView?.RefreshView();
                break;

            case WorkspaceTab.Stash:
                _stashView?.RefreshView();
                break;

            case WorkspaceTab.Sanctuary:
                _sanctuaryView?.RefreshView();
                break;
        }

        // Persistent Combat Viewport: NEVER hide BattleViewport!
        if (_combatViewportContainer != null)
        {
            _combatViewportContainer.Visible = true;
        }
        if (_battleViewport != null)
        {
            _battleViewport.Visible = true;
        }

        // Play smooth 0.15s Fade In / subtle horizontal Slide micro-animation (Task 6.1)
        if (animated && targetView != null && IsInsideTree())
        {
            int fromIdx = previousTab.HasValue ? (int)previousTab.Value : (int)tab;
            int toIdx = (int)tab;
            float slideDir = toIdx >= fromIdx ? 1.0f : -1.0f;
            float startOffsetX = slideDir * TabTransitionSlideOffset;

            targetView.Position = new Vector2(basePos.X + startOffsetX, basePos.Y);
            targetView.Modulate = new Color(1f, 1f, 1f, 0f);

            _tabTransitionTween = CreateTween();
            _tabTransitionTween.SetParallel(true);
            _tabTransitionTween.TweenProperty(targetView, "position:x", basePos.X, TabTransitionDuration)
                .SetTrans(Tween.TransitionType.Cubic)
                .SetEase(Tween.EaseType.Out);
            _tabTransitionTween.TweenProperty(targetView, "modulate:a", 1.0f, TabTransitionDuration)
                .SetTrans(Tween.TransitionType.Cubic)
                .SetEase(Tween.EaseType.Out);

            _tabTransitionTween.Finished += () =>
            {
                if (GodotObject.IsInstanceValid(targetView))
                {
                    targetView.Position = basePos;
                    targetView.Modulate = Colors.White;
                }
                _tabTransitionTween = null;
            };
        }
        else if (targetView != null)
        {
            targetView.Position = basePos;
            targetView.Modulate = Colors.White;
        }

        UpdatePassiveTreeAlert();

        _previousTab = tab;
    }

    private void OnGearNavPressed()
    {
        SwitchWorkspaceTab(WorkspaceTab.Gear);
    }

    private void OnResizeButtonPressed()
    {
        _windowController.ToggleExpand(keepBottom: true);
    }

    private void OnScreenCyclePressed()
    {
        _windowController.MoveToNextScreen();
    }

    private void OnWindowResized(Vector2I newSize, bool isExpanded)
    {
        UpdateResizeButtonText(isExpanded);

        // Compact Mode: hide sub tab bar and workspace
        // Expanded Mode: show sub tab bar and workspace, restoring active tab view
        if (_tabWorkspaceContainer != null) _tabWorkspaceContainer.Visible = isExpanded;
        if (_subTabBarContainer != null) _subTabBarContainer.Visible = isExpanded;

        // Persistent Combat Viewport is NEVER hidden!
        if (_combatViewportContainer != null)
        {
            _combatViewportContainer.Visible = true;
        }
        if (_battleViewport != null)
        {
            _battleViewport.Visible = true;
        }

        if (isExpanded)
        {
            ApplyTabVisibility(_activeTab);
        }

        GD.Print($"[Main] Window resized: {newSize.X}x{newSize.Y} (Expanded: {isExpanded}, ActiveTab: {_activeTab})");
    }

    private void UpdateResizeButtonText(bool isExpanded)
    {
        if (_resizeModeButton != null)
        {
            _resizeModeButton.Text = isExpanded ? "⤡ Min" : "⤢ Expand";
            _resizeModeButton.TooltipText = isExpanded
                ? Tr("UI_WINDOW_COMPACT_TOOLTIP")
                : Tr("UI_WINDOW_EXPANDED_TOOLTIP");
        }
    }

    private void OnEquipmentClosed()
    {
        // When equipment view is closed via [X], shrink window back to Compact Mode
        _windowController.ShrinkWindow(keepBottom: true);
    }

    private void OnCloseButtonPressed()
    {
        GetTree().Quit();
    }

    private void OnTopHeaderBarGuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton mouseButton && mouseButton.ButtonIndex == MouseButton.Left)
        {
            if (mouseButton.Pressed)
            {
                if (mouseButton.DoubleClick)
                {
                    // Double-clicking companion header toggles Compact/Expanded mode
                    _windowController.ToggleExpand(keepBottom: true);
                }
                else
                {
                    _windowController.StartDragging();
                }
            }
            else
            {
                _windowController.StopDragging();
            }
        }
    }

    /// <summary>
    /// Synchronizes the Passive Tree tab alert (breathing halo and unspent point badge)
    /// based on available unallocated passive points and active tab.
    /// </summary>
    public void UpdatePassiveTreeAlert()
    {
        int unspentPoints = _passiveTreeView?.TreeState?.AvailablePoints ?? 0;
        bool isPassiveTabActive = _activeTab == WorkspaceTab.Passives;
        _edgeGlowAlert?.UpdatePassiveTreeAlert(unspentPoints, isPassiveTabActive);
    }

    private void OnPlayerLevelUp(int newLevel)
    {
        if (_charInfoLabel != null)
        {
            string charName = Player.Archetype?.DisplayName ?? "Exile";
            _charInfoLabel.Text = $"Lv.{newLevel} {charName}";
        }

        _edgeGlowAlert?.TriggerLevelUpPulse();
        UpdatePassiveTreeAlert();
    }

    /// <summary>
    /// Explicitly triggers the Tier 1 drop alert (gold edge glow + Ting sound).
    /// </summary>
    public void TriggerTier1DropAlert(string? dropName = null)
    {
        _edgeGlowAlert?.TriggerTier1Alert(dropName);
    }

    /// <summary>
    /// Intercepts unhandled keyboard events when the game window is focused (Task 6.3).
    /// Strictly adheres to the "Zero System-Wide OS Hooking" policy (no Win32 RegisterHotKey).
    /// </summary>
    public override void _UnhandledKeyInput(InputEvent @event)
    {
        if (@event is InputEventKey keyEvent && keyEvent.Pressed && !keyEvent.Echo)
        {
            if (HandleInWindowShortcut(keyEvent.Keycode))
            {
                GetViewport()?.SetInputAsHandled();
            }
        }
    }

    /// <summary>
    /// Dispatches in-window keyboard shortcuts (Task 6.3).
    /// Returns true if the shortcut was recognized and handled; otherwise false.
    /// </summary>
    public bool HandleInWindowShortcut(Key keycode)
    {
        // Safety guard: Do not intercept shortcut keys if the player is actively typing into a text field
        var focusOwner = GetViewport()?.GuiGetFocusOwner();
        if (focusOwner is LineEdit or TextEdit)
        {
            if (keycode == Key.Escape)
            {
                focusOwner.ReleaseFocus();
                return true;
            }
            return false;
        }

        switch (keycode)
        {
            // Contextual Spacebar: In Combat -> Emergency Portal; In Sanctuary -> Launch Next Map (Prompt 5)
            case Key.Space:
                if (Engine is { IsInCombat: true })
                {
                    OnEmergencyPortalTriggered();
                }
                else
                {
                    LaunchNextMapFromSanctuary();
                }
                return true;

            // Crucible Sanctuary Tab (Prompt 5)
            case Key.H:
                ToggleOrSwitchWorkspaceTab(WorkspaceTab.Sanctuary);
                return true;

            // Quick-Focus The Crucible Anvil (Prompt 5)
            case Key.A:
                if (!_windowController.IsExpanded)
                {
                    _windowController.ExpandWindow(keepBottom: true);
                }
                SwitchWorkspaceTab(WorkspaceTab.Sanctuary);
                _sanctuaryView?.FocusAnvil();
                return true;

            // Gear & Inventory Tab (Supports both standard PoE 'I' and 'G')
            case Key.I:
            case Key.G:
                ToggleOrSwitchWorkspaceTab(WorkspaceTab.Gear);
                return true;

            // Passive Skill Tree Tab
            case Key.P:
                ToggleOrSwitchWorkspaceTab(WorkspaceTab.Passives);
                return true;

            // Crucible / Map Device / Atlas Tab
            case Key.M:
                ToggleOrSwitchWorkspaceTab(WorkspaceTab.Atlas);
                return true;

            // Affinity Stash Tab
            case Key.S:
                ToggleOrSwitchWorkspaceTab(WorkspaceTab.Stash);
                return true;

            // Skills & Gems Tab (PoE / ARPG standard 'K')
            case Key.K:
                ToggleOrSwitchWorkspaceTab(WorkspaceTab.Skills);
                return true;

            // Resolution Mode Toggle (Compact ↔ Expanded)
            case Key.Tab:
            case Key.F11:
                _windowController.ToggleExpand(keepBottom: true);
                return true;

            // Escape: Dismiss overlays / modal drawers first, or collapse to Compact Mode
            case Key.Escape:
                return HandleEscapeShortcut();

#if DEBUG
            case Key.F12:
                ToggleDiagnosticModal();
                return true;
#endif

            default:
                return false;
        }
    }

    /// <summary>
    /// PoE-style tab navigation: switches to the target tab, or collapses window to Compact mode
    /// if the target tab is already active and the window is expanded.
    /// If currently in Compact mode, auto-expands upward to display the target tab.
    /// </summary>
    public void ToggleOrSwitchWorkspaceTab(WorkspaceTab tab)
    {
        if (_windowController.IsExpanded && _activeTab == tab)
        {
            _windowController.ShrinkWindow(keepBottom: true);
        }
        else
        {
            SwitchWorkspaceTab(tab);
        }
    }

    /// <summary>
    /// Handles the Escape key: dismisses active modals/overlays first,
    /// or collapses the window back to Compact Mode if currently expanded.
    /// </summary>
    public bool HandleEscapeShortcut()
    {
#if DEBUG
        if (_diagnosticModal != null && IsInstanceValid(_diagnosticModal))
        {
            _diagnosticModal.Close();
            _diagnosticModal = null;
            return true;
        }
#endif

        if (_equipmentView != null && _equipmentView.IsHelpOverlayVisible)
        {
            _equipmentView.SetHelpOverlayVisible(false);
            return true;
        }

        if (_passiveTreeView != null && _passiveTreeView.IsStatsSummaryVisible)
        {
            _passiveTreeView.SetStatsSummaryVisible(false);
            return true;
        }

        if (_windowController.IsExpanded)
        {
            _windowController.ShrinkWindow(keepBottom: true);
            return true;
        }

        return false;
    }

#if DEBUG
    public void ToggleDiagnosticModal()
    {
        if (!OS.IsDebugBuild()) return;

        if (_diagnosticModal != null && IsInstanceValid(_diagnosticModal))
        {
            _diagnosticModal.Close();
            _diagnosticModal = null;
            return;
        }

        var modalScene = GD.Load<PackedScene>("res://scenes/DiagnosticModal.tscn");
        if (modalScene != null)
        {
            _diagnosticModal = modalScene.Instantiate<DiagnosticModal>();
            _diagnosticModal.Setup(Db, Player, Engine, _windowController);
            _diagnosticModal.OnClosed += () => { _diagnosticModal = null; };
            AddChild(_diagnosticModal);
        }
    }
#endif

    private void OnAlwaysOnTopToggled(bool isToggled)
    {
        _windowController.ToggleAlwaysOnTop(isToggled);
        GD.Print($"[Main] AlwaysOnTop: {isToggled}");
    }

    /// <summary>
    /// Explicit runner for pure C# Core simulation & math unit tests.
    /// Only executed if '--run-core-tests' is passed via command line.
    /// </summary>
    public static void RunCoreUnitTests(Main? mainInstance = null)
    {
        GD.Print("[Tests] Running pure C# Core Unit Tests...");
        IdleExile.Core.Tests.DamageCalculatorTests.RunAll();
        IdleExile.Core.Tests.CombatInfrastructureTests.RunAll();
        IdleExile.Core.Tests.CombatEngineTests.RunAll();
        IdleExile.Core.Tests.UnifiedCombatSystemTests.RunAll();
        IdleExile.Core.Tests.StarterArchetypeTests.RunAll();
        IdleExile.Core.Tests.FtueManagerTests.RunAll();
        IdleExile.Core.Tests.SeededDropTests.RunAll();
        IdleExile.Core.Tests.SpatialGeometryTests.RunAll();
        IdleExile.Core.Tests.ExileMovementControllerTests.RunAll();
        IdleExile.Core.Tests.CorridorMonsterLayoutTests.RunAll();
        IdleExile.Core.Tests.EquipmentSystemTests.RunAll();
        IdleExile.Core.Tests.GemSystemTests.RunAll();
        IdleExile.Core.Tests.AtlasSystemTests.RunAll();
        IdleExile.Core.Tests.PassiveTreeSystemTests.RunAll();
        IdleExile.Core.Tests.AffinityStashTests.RunAllTests();
        IdleExile.Core.Tests.EdgeGlowAlertTests.RunAll();
        IdleExile.Core.Tests.SanctuarySystemTests.RunAll();
        IdleExile.UI.MainLayoutTests.RunAll(mainInstance);
        GD.Print("[Tests] All Core Unit Tests passed successfully!");
    }
}
