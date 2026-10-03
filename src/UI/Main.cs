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
    private CheckBox? _alwaysOnTopCheckBox;
    private BattleViewport? _battleViewport;
    private MapDeviceBar? _mapDeviceBar;
    private EquipmentView? _equipmentView;
    private Button? _gearNavButton;
    private Button? _resizeModeButton;
    private Button? _screenCycleButton;
    private Button? _emergencyPortalButton;
    private Button? _tabGearBtn;
    private Button? _tabSkillsBtn;
    private Button? _tabAtlasBtn;
    private Button? _tabPassivesBtn;
    private Button? _tabStashBtn;
    private Control? _subTabBarContainer;
    private Control? _tabWorkspaceContainer;
    private Control? _mapDeviceMargin;
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
        Stash
    }

    private WorkspaceTab _activeTab = WorkspaceTab.Gear;

    public WindowController WindowController => _windowController;
    public WorkspaceTab ActiveTab => _activeTab;
    public Control? SubTabBarContainer => _subTabBarContainer;
    public Control? TabWorkspaceContainer => _tabWorkspaceContainer;
    public BattleViewport? BattleViewport => _battleViewport;
    public EquipmentView? EquipmentView => _equipmentView;
    public Control? MapDeviceMargin => _mapDeviceMargin;
    public Button? ResizeModeButton => _resizeModeButton;

    public DatabaseLoader Db { get; } = new();
    public PlayerState Player { get; } = new();
    public SkillLoadout Loadout { get; } = new();
    public FtueManager Ftue { get; } = new();
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
        _battleViewport = GetNodeOrNull<BattleViewport>("%BattleViewport");
        _mapDeviceBar = GetNodeOrNull<MapDeviceBar>("%MapDeviceBar");
        _equipmentView = GetNodeOrNull<EquipmentView>("%EquipmentView");
        _emergencyPortalButton = GetNodeOrNull<Button>("%EmergencyPortalButton");
        _subTabBarContainer = GetNodeOrNull<Control>("%SubTabBarContainer");
        _tabWorkspaceContainer = GetNodeOrNull<Control>("%TabWorkspaceContainer");
        _mapDeviceMargin = GetNodeOrNull<Control>("%MapDeviceMargin");

        _tabGearBtn = GetNodeOrNull<Button>("%TabGearBtn");
        _tabSkillsBtn = GetNodeOrNull<Button>("%TabSkillsBtn");
        _tabAtlasBtn = GetNodeOrNull<Button>("%TabAtlasBtn");
        _tabPassivesBtn = GetNodeOrNull<Button>("%TabPassivesBtn");
        _tabStashBtn = GetNodeOrNull<Button>("%TabStashBtn");
        _gearNavButton = _tabGearBtn ?? GetNodeOrNull<Button>("%GearNavButton");

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

        if (_emergencyPortalButton != null)
        {
            _emergencyPortalButton.Pressed += OnEmergencyPortalTriggered;
        }

        // Wire Tab Navigation with exclusive ButtonGroup
        var tabGroup = new ButtonGroup();
        if (_tabGearBtn != null) _tabGearBtn.ButtonGroup = tabGroup;
        if (_tabSkillsBtn != null) _tabSkillsBtn.ButtonGroup = tabGroup;
        if (_tabAtlasBtn != null) _tabAtlasBtn.ButtonGroup = tabGroup;
        if (_tabPassivesBtn != null) _tabPassivesBtn.ButtonGroup = tabGroup;
        if (_tabStashBtn != null) _tabStashBtn.ButtonGroup = tabGroup;

        if (_tabGearBtn != null) _tabGearBtn.Pressed += () => SwitchWorkspaceTab(WorkspaceTab.Gear);
        if (_tabSkillsBtn != null) _tabSkillsBtn.Pressed += () => SwitchWorkspaceTab(WorkspaceTab.Skills);
        if (_tabAtlasBtn != null) _tabAtlasBtn.Pressed += () => SwitchWorkspaceTab(WorkspaceTab.Atlas);
        if (_tabPassivesBtn != null) _tabPassivesBtn.Pressed += () => SwitchWorkspaceTab(WorkspaceTab.Passives);
        if (_tabStashBtn != null) _tabStashBtn.Pressed += () => SwitchWorkspaceTab(WorkspaceTab.Stash);

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

        // 4. Load JSON Database
        Db.LoadAll();

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
        }

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

        GD.Print($"[Main] Character bootstrapped: {archetype.DisplayName}, Life: {Player.MaxLife}, ES: {Player.MaxEnergyShield}, Mana: {Player.MaxMana}");
        GD.Print($"[Main] Equipped Weapon: MainHand={Player.MainHand?.BaseDefinition.Name ?? "None"} ({Player.MainHand?.ComputedMinDamage}-{Player.MainHand?.ComputedMaxDamage} Phys, APS: {Player.MainHand?.ComputedAps}), OffHand={Player.OffHand?.BaseDefinition.Name ?? "None"}, DualWield={Player.IsDualWielding}");
        GD.Print($"[Main] FTUE Step: {Ftue.CurrentStep} ({Ftue.GetCurrentInstruction()})");
        GD.Print($"[Main] Inventory: {Player.Inventory.Count} items, {Player.MapInventory.Count} maps");
    }

    private void OnMapActivated(MapDefinition map)
    {
        // 1. Advance FTUE Step 1 -> Step 2 (FirstCombatWave)
        if (Ftue.CurrentStep == FtueStep.AwaitingFirstMapActivate)
        {
            Ftue.ActivateFirstMap();
        }

        // 2. Turn off Golden Pulse & set portal active
        _mapDeviceBar?.SetGoldenPulse(false);
        _mapDeviceBar?.SetPortalActive(true);

        // 3. Configure BattleViewport with the active map & start CombatEngine
        var config = new MapRunConfig(map, System.Array.Empty<RolledMapModifier>());
        _battleViewport?.SetMap(config, progressPercent: 0f, rarity: "Normal");
        _battleViewport?.BindPlayer(Player, Loadout);

        Engine = new CombatEngine(Player, Loadout, dropCoordinator: DropCoordinator);

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

            if (drop.Type == LootDropType.Equipment)
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

        Engine.StartMap(config);

        // 4. Update status display
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
        Engine?.TriggerEmergencyPortal();
        _mapDeviceBar?.SetPortalActive(false);

        if (_statusLabel != null)
        {
            _statusLabel.Text = "Crucible Online • Safely retreated to Hideout";
        }

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

    public override void _Process(double delta)
    {
        Engine?.Update((float)delta);
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
        _battleViewport.SetMap(config, progressPercent: 68.0f, rarity: "Rare", leagueEncounter: "Breach");
        _battleViewport.UpdateVitals(3450, 3450, 1200, 1200, 82, 820, 90.0, "EHP: 14.2k | Armour: 8.5k | Block: 75%");
        _battleViewport.SetGuardSkill(true, 480f, 500f, "Steelskin");
        _battleViewport.SpawnFloatingDamage(8420f, new Vector2(165, 120), DamageType.Cold, isCrit: true);

        _mapDeviceBar?.SlotMap(mapDef, "Rare");
        _mapDeviceBar?.SetPortalActive(true);
    }

    /// <summary>
    /// Switches the active view inside TabWorkspaceContainer.
    /// In accordance with Core Objective #1, BattleViewport is persistent and NEVER hidden.
    /// If currently in Compact Mode, automatically expands the window upward.
    /// </summary>
    public void SwitchWorkspaceTab(WorkspaceTab tab)
    {
        _activeTab = tab;

        // If window is currently in compact mode, auto-expand upward to full height!
        if (!_windowController.IsExpanded)
        {
            _windowController.ExpandWindow(keepBottom: true);
            return;
        }

        ApplyTabVisibility(tab);
    }

    /// <summary>
    /// Applies segmented tab button states and switches child view visibility in TabWorkspaceContainer.
    /// In accordance with Core Objective #1, BattleViewport is persistent and NEVER hidden.
    /// </summary>
    public void ApplyTabVisibility(WorkspaceTab tab)
    {
        // Update segmented tab button states
        if (_tabGearBtn != null) _tabGearBtn.ButtonPressed = (tab == WorkspaceTab.Gear);
        if (_tabSkillsBtn != null) _tabSkillsBtn.ButtonPressed = (tab == WorkspaceTab.Skills);
        if (_tabAtlasBtn != null) _tabAtlasBtn.ButtonPressed = (tab == WorkspaceTab.Atlas);
        if (_tabPassivesBtn != null) _tabPassivesBtn.ButtonPressed = (tab == WorkspaceTab.Passives);
        if (_tabStashBtn != null) _tabStashBtn.ButtonPressed = (tab == WorkspaceTab.Stash);

        // Switch workspace views
        if (_equipmentView != null)
        {
            _equipmentView.Visible = (tab == WorkspaceTab.Gear);
            if (tab == WorkspaceTab.Gear)
            {
                _equipmentView.SetPlayer(Player);
                _equipmentView.RefreshView();
            }
        }

        if (_mapDeviceMargin != null)
        {
            _mapDeviceMargin.Visible = (tab == WorkspaceTab.Atlas);
        }

        // Persistent Combat Viewport: NEVER hide BattleViewport!
        if (_battleViewport != null)
        {
            _battleViewport.Visible = true;
        }

        if (tab != WorkspaceTab.Gear && tab != WorkspaceTab.Atlas && _statusLabel != null)
        {
            _statusLabel.Text = $"{tab} Tab coming in upcoming phase!";
        }
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
        if (_subTabBarContainer != null) _subTabBarContainer.Visible = isExpanded;
        if (_tabWorkspaceContainer != null) _tabWorkspaceContainer.Visible = isExpanded;

        // Persistent Combat Viewport is NEVER hidden!
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
                ? "Thu nhỏ Compact (440x380) - Neo đáy thanh Taskbar"
                : "Phóng to Expanded (440x780) - Mở rộng Theorycrafting";
        }
    }

    private void OnEquipmentClosed()
    {
        // When equipment view is closed via [X], shrink window back to Compact Mode
        _windowController.ShrinkWindow(keepBottom: true);
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventKey keyEvent && keyEvent.Pressed && !keyEvent.Echo)
        {
            if (keyEvent.Keycode == Key.Space)
            {
                OnEmergencyPortalTriggered();
                GetViewport().SetInputAsHandled();
            }
#if DEBUG
            else if (keyEvent.Keycode == Key.F12)
            {
                ToggleDiagnosticModal();
                GetViewport().SetInputAsHandled();
            }
#endif
        }
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
        IdleExile.UI.MainLayoutTests.RunAll(mainInstance);
        GD.Print("[Tests] All Core Unit Tests passed successfully!");
    }
}
