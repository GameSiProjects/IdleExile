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
/// Mini-Window Sanctuary View Controller (640 x 420 px workspace inside Expanded Mode).
/// Houses the 4 central hub management panels:
/// 1. The Crucible Anvil (Pure Currency & Fossil Slamming with Smart Stat Delta Preview).
/// 2. Thaumaturgic Smelter (Automated Dust breakdown & Map Infusion).
/// 3. The Abyssal Resonator (Voltaxic Sulphite Tank & Endless Abyss Descent).
/// 4. Atlas Relics & Pinnacle Hall (Voidstones & Pinnacle Boss Trophies).
/// </summary>
public partial class SanctuaryView : Control
{
    // ─────────────────────────────────────────────
    //  Signals & Events
    // ─────────────────────────────────────────────

    public event Action? OnCloseRequested;
    public event Action<string, Color>? OnToastRequested;
    public event Action? OnCraftCompleted;
    public event Action? OnDescendToAbyssRequested;

    public void RequestClose() => OnCloseRequested?.Invoke();

    // ─────────────────────────────────────────────
    //  Dependencies & State
    // ─────────────────────────────────────────────

    public PlayerState? Player { get; private set; }
    public DatabaseLoader? Db { get; private set; }
    public MapQueueManager? MapQueue { get; private set; }

    private readonly TheCrucibleAnvilService _anvilService = new();
    private readonly SmelterService _smelterService = new();
    private readonly List<IGearItem> _targetItemChoices = new();

    private IGearItem? _selectedTargetItem;
    private AnvilCraftResult? _lastCraftResult;

    // ─────────────────────────────────────────────
    //  UI References — 1. The Crucible Anvil
    // ─────────────────────────────────────────────

    private OptionButton? _targetItemOption;
    private Label? _targetItemHeaderLabel;
    private Label? _targetItemAffixCountLabel;
    private VBoxContainer? _targetItemAffixesContainer;
    private Button? _btnTransmute;
    private Button? _btnAlteration;
    private Button? _btnChaos;
    private Button? _btnExalt;
    private Button? _btnAnnul;
    private Button? _btnDivine;
    private Button? _btnVaal;

    // Fossils & Resonators
    private OptionButton? _fossilSlot1;
    private OptionButton? _fossilSlot2;
    private OptionButton? _fossilSlot3;
    private OptionButton? _fossilSlot4;
    private Label? _fossilBiasesLabel;
    private Button? _btnSlamResonator;

    // Smart Stat Delta Preview Card
    private PanelContainer? _deltaPreviewCard;
    private Label? _deltaVerdictLabel;
    private Label? _deltaEhpLabel;
    private Label? _deltaDpsLabel;
    private Label? _deltaResistsLabel;
    private Label? _deltaMessageLabel;

    // ─────────────────────────────────────────────
    //  UI References — 2. Thaumaturgic Smelter
    // ─────────────────────────────────────────────

    private Label? _dustAmountLabel;
    private CheckBox? _autoSmeltCheckBox;
    private Label? _infusionStatusLabel;
    private Button? _btnInfuseMaps;
    private Button? _btnTransmuteUnmaking;
    private Button? _btnTransmuteChaos;
    private Label? _smelterToastLabel;

    // ─────────────────────────────────────────────
    //  UI References — 3. The Abyssal Resonator
    // ─────────────────────────────────────────────

    private ProgressBar? _sulphiteProgressBar;
    private Label? _sulphiteAmountLabel;
    private Label? _depthRecordLabel;
    private Button? _btnDescendAbyss;

    // ─────────────────────────────────────────────
    //  UI References — 4. Atlas Relics & Hall of Fame
    // ─────────────────────────────────────────────

    private Label? _hubTierLabel;
    private Label? _hubPerksLabel;
    private Label? _voidstone1Label;
    private Label? _voidstone2Label;
    private Label? _voidstone3Label;
    private Label? _voidstone4Label;
    private Button? _btnBossEater;
    private Button? _btnBossExarch;
    private Button? _btnBossMaven;
    private Button? _btnBossElder;

    public TheCrucibleAnvilService AnvilService => _anvilService;
    public SmelterService SmelterService => _smelterService;
    public IGearItem? SelectedTargetItem => _selectedTargetItem;
    public AnvilCraftResult? LastCraftResult => _lastCraftResult;

    public override void _Ready()
    {
        BindNodes();
        WireEvents();
    }

    public void Initialize(PlayerState player, DatabaseLoader db, MapQueueManager? mapQueue = null)
    {
        Player = player;
        Db = db;
        MapQueue = mapQueue;

        if (Player?.Sanctuary != null)
        {
            Player.Sanctuary.OnSanctuaryUpdated += RefreshView;
        }

        if (Player?.Stash != null)
        {
            Player.Stash.OnStashChanged += RefreshView;
        }

        RefreshTargetItemList();
        RefreshView();
    }

    private void BindNodes()
    {
        // 1. The Crucible Anvil
        _targetItemOption = GetNodeOrNull<OptionButton>("%TargetItemOption");
        _targetItemHeaderLabel = GetNodeOrNull<Label>("%TargetItemHeaderLabel");
        _targetItemAffixCountLabel = GetNodeOrNull<Label>("%TargetItemAffixCountLabel");
        _targetItemAffixesContainer = GetNodeOrNull<VBoxContainer>("%TargetItemAffixesContainer");

        _btnTransmute = GetNodeOrNull<Button>("%BtnTransmute");
        _btnAlteration = GetNodeOrNull<Button>("%BtnAlteration");
        _btnChaos = GetNodeOrNull<Button>("%BtnChaos");
        _btnExalt = GetNodeOrNull<Button>("%BtnExalt");
        _btnAnnul = GetNodeOrNull<Button>("%BtnAnnul");
        _btnDivine = GetNodeOrNull<Button>("%BtnDivine");
        _btnVaal = GetNodeOrNull<Button>("%BtnVaal");

        _fossilSlot1 = GetNodeOrNull<OptionButton>("%FossilSlot1");
        _fossilSlot2 = GetNodeOrNull<OptionButton>("%FossilSlot2");
        _fossilSlot3 = GetNodeOrNull<OptionButton>("%FossilSlot3");
        _fossilSlot4 = GetNodeOrNull<OptionButton>("%FossilSlot4");
        _fossilBiasesLabel = GetNodeOrNull<Label>("%FossilBiasesLabel");
        _btnSlamResonator = GetNodeOrNull<Button>("%BtnSlamResonator");

        _deltaPreviewCard = GetNodeOrNull<PanelContainer>("%DeltaPreviewCard");
        _deltaVerdictLabel = GetNodeOrNull<Label>("%DeltaVerdictLabel");
        _deltaEhpLabel = GetNodeOrNull<Label>("%DeltaEhpLabel");
        _deltaDpsLabel = GetNodeOrNull<Label>("%DeltaDpsLabel");
        _deltaResistsLabel = GetNodeOrNull<Label>("%DeltaResistsLabel");
        _deltaMessageLabel = GetNodeOrNull<Label>("%DeltaMessageLabel");

        // 2. Thaumaturgic Smelter
        _dustAmountLabel = GetNodeOrNull<Label>("%DustAmountLabel");
        _autoSmeltCheckBox = GetNodeOrNull<CheckBox>("%AutoSmeltCheckBox");
        _infusionStatusLabel = GetNodeOrNull<Label>("%InfusionStatusLabel");
        _btnInfuseMaps = GetNodeOrNull<Button>("%BtnInfuseMaps");
        _btnTransmuteUnmaking = GetNodeOrNull<Button>("%BtnTransmuteUnmaking");
        _btnTransmuteChaos = GetNodeOrNull<Button>("%BtnTransmuteChaos");
        _smelterToastLabel = GetNodeOrNull<Label>("%SmelterToastLabel");

        // 3. The Abyssal Resonator
        _sulphiteProgressBar = GetNodeOrNull<ProgressBar>("%SulphiteProgressBar");
        _sulphiteAmountLabel = GetNodeOrNull<Label>("%SulphiteAmountLabel");
        _depthRecordLabel = GetNodeOrNull<Label>("%DepthRecordLabel");
        _btnDescendAbyss = GetNodeOrNull<Button>("%BtnDescendAbyss");

        // 4. Atlas Relics & Hall of Fame
        _hubTierLabel = GetNodeOrNull<Label>("%HubTierLabel");
        _hubPerksLabel = GetNodeOrNull<Label>("%HubPerksLabel");
        _voidstone1Label = GetNodeOrNull<Label>("%Voidstone1Label");
        _voidstone2Label = GetNodeOrNull<Label>("%Voidstone2Label");
        _voidstone3Label = GetNodeOrNull<Label>("%Voidstone3Label");
        _voidstone4Label = GetNodeOrNull<Label>("%Voidstone4Label");
        _btnBossEater = GetNodeOrNull<Button>("%BtnBossEater");
        _btnBossExarch = GetNodeOrNull<Button>("%BtnBossExarch");
        _btnBossMaven = GetNodeOrNull<Button>("%BtnBossMaven");
        _btnBossElder = GetNodeOrNull<Button>("%BtnBossElder");

        PopulateFossilDropdowns();
    }

    private void WireEvents()
    {
        if (_targetItemOption != null)
        {
            _targetItemOption.ItemSelected += OnTargetItemSelected;
        }

        // Anvil Currency Buttons
        if (_btnTransmute != null) _btnTransmute.Pressed += () => ExecuteCurrencySlam(TheCrucibleAnvilService.TransmuteId);
        if (_btnAlteration != null) _btnAlteration.Pressed += () => ExecuteCurrencySlam(TheCrucibleAnvilService.AlterationId);
        if (_btnChaos != null) _btnChaos.Pressed += () => ExecuteCurrencySlam(TheCrucibleAnvilService.ChaosId);
        if (_btnExalt != null) _btnExalt.Pressed += () => ExecuteCurrencySlam(TheCrucibleAnvilService.ExaltedId);
        if (_btnAnnul != null) _btnAnnul.Pressed += () => ExecuteCurrencySlam(TheCrucibleAnvilService.AnnulmentId);
        if (_btnDivine != null) _btnDivine.Pressed += () => ExecuteCurrencySlam(TheCrucibleAnvilService.DivineId);
        if (_btnVaal != null) _btnVaal.Pressed += () => ExecuteCurrencySlam(TheCrucibleAnvilService.VaalId);

        // Resonator Slam
        if (_btnSlamResonator != null)
        {
            _btnSlamResonator.Pressed += ExecuteResonatorSlam;
        }

        void OnFossilSelected(long _) => UpdateFossilBiasesSummary();
        if (_fossilSlot1 != null) _fossilSlot1.ItemSelected += OnFossilSelected;
        if (_fossilSlot2 != null) _fossilSlot2.ItemSelected += OnFossilSelected;
        if (_fossilSlot3 != null) _fossilSlot3.ItemSelected += OnFossilSelected;
        if (_fossilSlot4 != null) _fossilSlot4.ItemSelected += OnFossilSelected;

        // Smelter Actions
        if (_autoSmeltCheckBox != null)
        {
            _autoSmeltCheckBox.Toggled += toggled =>
            {
                if (Player?.Sanctuary != null)
                {
                    Player.Sanctuary.AutoSmeltActive = toggled;
                }
            };
        }

        if (_btnInfuseMaps != null)
        {
            _btnInfuseMaps.Pressed += OnInfuseMapsPressed;
        }

        if (_btnTransmuteUnmaking != null)
        {
            _btnTransmuteUnmaking.Pressed += OnTransmuteUnmakingPressed;
        }

        if (_btnTransmuteChaos != null)
        {
            _btnTransmuteChaos.Pressed += OnTransmuteChaosPressed;
        }

        // Abyssal Resonator Descent
        if (_btnDescendAbyss != null)
        {
            _btnDescendAbyss.Pressed += OnDescendAbyssPressed;
        }

        // Pinnacle Bosses Toggles
        if (_btnBossEater != null) _btnBossEater.Pressed += () => TogglePinnacleBoss("eater");
        if (_btnBossExarch != null) _btnBossExarch.Pressed += () => TogglePinnacleBoss("exarch");
        if (_btnBossMaven != null) _btnBossMaven.Pressed += () => TogglePinnacleBoss("maven");
        if (_btnBossElder != null) _btnBossElder.Pressed += () => TogglePinnacleBoss("elder");
    }

    private void PopulateFossilDropdowns()
    {
        var slots = new[] { _fossilSlot1, _fossilSlot2, _fossilSlot3, _fossilSlot4 };
        string[] fossilNames = { "(None)", "Pristine", "Metallic", "Scorched", "Frigid", "Dense", "Corroded" };

        foreach (var slot in slots)
        {
            if (slot == null) continue;
            slot.Clear();
            for (int i = 0; i < fossilNames.Length; i++)
            {
                slot.AddItem(fossilNames[i], i);
            }
            slot.Selected = 0;
        }
    }

    public void FocusAnvil()
    {
        if (_targetItemOption != null && _targetItemOption.IsInsideTree())
        {
            _targetItemOption.GrabFocus();
            var tween = CreateTween();
            tween.TweenProperty(_targetItemOption, "modulate", new Color(1.35f, 1.25f, 0.7f), 0.12f);
            tween.TweenProperty(_targetItemOption, "modulate", new Color(1f, 1f, 1f), 0.18f);
        }
    }

    // ─────────────────────────────────────────────
    //  1. The Crucible Anvil Thao Tác & Delta
    // ─────────────────────────────────────────────

    public void RefreshTargetItemList()
    {
        if (_targetItemOption == null || Player == null) return;

        _targetItemChoices.Clear();
        _targetItemOption.Clear();
        int idx = 0;
        int selectedIndex = 0;

        // 1. Equipped Items
        foreach (GearSlot slot in Enum.GetValues<GearSlot>())
        {
            var item = Player.Gear.GetItem(slot);
            if (item != null)
            {
                string label = $"[{slot}] {item.Name}";
                _targetItemOption.AddItem(label, idx);
                _targetItemChoices.Add(item);

                if (_selectedTargetItem == null || _selectedTargetItem.InstanceId == item.InstanceId)
                {
                    selectedIndex = idx;
                    _selectedTargetItem = item;
                }
                idx++;
            }
        }

        // 2. Reserve Stash Items
        if (Player.Stash != null)
        {
            foreach (var item in Player.Stash.GearItems)
            {
                string label = $"[Stash] {item.Name}";
                _targetItemOption.AddItem(label, idx);
                _targetItemChoices.Add(item);
                if (_selectedTargetItem != null && _selectedTargetItem.InstanceId == item.InstanceId)
                {
                    selectedIndex = idx;
                }
                idx++;
            }
        }

        if (_targetItemOption.ItemCount > 0)
        {
            _targetItemOption.Selected = selectedIndex;
            OnTargetItemSelected(selectedIndex);
        }
    }

    private void OnTargetItemSelected(long index)
    {
        IGearItem? item = null;
        if (index >= 0 && index < _targetItemChoices.Count)
        {
            item = _targetItemChoices[(int)index];
        }

        item ??= (IGearItem?)Player?.Gear.BodyArmour ?? (IGearItem?)Player?.Gear.MainHand;

        _selectedTargetItem = item;
        RenderTargetItemCard();
        UpdateCurrencyButtonStates();
        UpdateFossilBiasesSummary();
    }

    private void RenderTargetItemCard()
    {
        if (_selectedTargetItem == null)
        {
            if (_targetItemHeaderLabel != null) _targetItemHeaderLabel.Text = Loc.Tr("UI_ANVIL_SLOT_EMPTY");
            if (_targetItemAffixCountLabel != null) _targetItemAffixCountLabel.Text = string.Empty;
            ClearAffixContainer();
            return;
        }

        Color rarityColor = _selectedTargetItem.Rarity switch
        {
            "Magic" => new Color(0.35f, 0.65f, 1.0f),
            "Rare" => new Color(1.0f, 0.85f, 0.35f),
            "Unique" => new Color(0.85f, 0.45f, 0.15f),
            _ => new Color(0.85f, 0.85f, 0.85f)
        };

        if (TheCrucibleAnvilService.IsCorrupted(_selectedTargetItem))
        {
            rarityColor = new Color(0.85f, 0.3f, 0.3f);
        }

        if (_targetItemHeaderLabel != null)
        {
            _targetItemHeaderLabel.Text = $"{_selectedTargetItem.Name} (ilvl {_selectedTargetItem.ItemLevel})";
            _targetItemHeaderLabel.Modulate = rarityColor;
        }

        int pCount = _selectedTargetItem.Affixes.Count(a => a.Type == AffixType.Prefix);
        int sCount = _selectedTargetItem.Affixes.Count(a => a.Type == AffixType.Suffix);
        string corruptedText = TheCrucibleAnvilService.IsCorrupted(_selectedTargetItem) ? " • 🩸 CORRUPTED" : string.Empty;

        if (_targetItemAffixCountLabel != null)
        {
            _targetItemAffixCountLabel.Text = $"{_selectedTargetItem.Rarity} • {pCount} Prefixes / {sCount} Suffixes{corruptedText}";
        }

        RenderAffixesList();
    }

    private void RenderAffixesList()
    {
        ClearAffixContainer();
        if (_targetItemAffixesContainer == null || _selectedTargetItem == null) return;

        if (!string.IsNullOrEmpty(_selectedTargetItem.ImplicitDescription))
        {
            var implicitLabel = new Label
            {
                Text = $"• {_selectedTargetItem.ImplicitDescription}",
                Modulate = new Color(0.55f, 0.75f, 0.95f),
                AutowrapMode = TextServer.AutowrapMode.WordSmart
            };
            implicitLabel.AddThemeFontSizeOverride("font_size", 10);
            _targetItemAffixesContainer.AddChild(implicitLabel);
        }

        if (_selectedTargetItem.Affixes.Count == 0)
        {
            var emptyLabel = new Label
            {
                Text = "(No explicit modifiers)",
                Modulate = new Color(0.5f, 0.5f, 0.55f)
            };
            emptyLabel.AddThemeFontSizeOverride("font_size", 10);
            _targetItemAffixesContainer.AddChild(emptyLabel);
            return;
        }

        foreach (var affix in _selectedTargetItem.Affixes)
        {
            string tierTag = affix.Tier <= 2 ? $"[T{affix.Tier} ★]" : $"[T{affix.Tier}]";
            Color textColor = affix.Tier <= 2 ? new Color(1.0f, 0.88f, 0.4f) : new Color(0.85f, 0.85f, 0.9f);
            if (affix.Type == AffixType.Prefix)
            {
                textColor = affix.Tier <= 2 ? new Color(0.5f, 0.85f, 1.0f) : new Color(0.4f, 0.7f, 0.95f);
            }

            var lbl = new Label
            {
                Text = $"{tierTag} {affix.NameTemplate}",
                Modulate = textColor,
                AutowrapMode = TextServer.AutowrapMode.WordSmart
            };
            lbl.AddThemeFontSizeOverride("font_size", 10);
            _targetItemAffixesContainer.AddChild(lbl);
        }
    }

    private void ClearAffixContainer()
    {
        if (_targetItemAffixesContainer == null) return;
        foreach (Node child in _targetItemAffixesContainer.GetChildren())
        {
            child.QueueFree();
        }
    }

    private void UpdateCurrencyButtonStates()
    {
        var buttons = new (Button? btn, string id, string name)[]
        {
            (_btnTransmute, TheCrucibleAnvilService.TransmuteId, "Trans"),
            (_btnAlteration, TheCrucibleAnvilService.AlterationId, "Alt"),
            (_btnChaos, TheCrucibleAnvilService.ChaosId, "Chaos"),
            (_btnExalt, TheCrucibleAnvilService.ExaltedId, "Exalt"),
            (_btnAnnul, TheCrucibleAnvilService.AnnulmentId, "Annul"),
            (_btnDivine, TheCrucibleAnvilService.DivineId, "Divine"),
            (_btnVaal, TheCrucibleAnvilService.VaalId, "Vaal")
        };

        foreach (var (btn, id, name) in buttons)
        {
            if (btn == null) continue;
            int count = Player?.Stash?.GetCurrencyCount(id) ?? 0;
            bool canSlam = _selectedTargetItem != null && _anvilService.CanSlamCurrency(_selectedTargetItem, id);

            btn.Text = $"{name} ({count})";
            btn.TooltipText = $"Slam {name} Orb (Available: {count})";
            btn.Disabled = !canSlam || count <= 0;
        }
    }

    private void UpdateFossilBiasesSummary()
    {
        var fossilIds = GetSelectedFossilIds();
        if (_fossilBiasesLabel != null)
        {
            if (fossilIds.Count == 0)
            {
                _fossilBiasesLabel.Text = "Socket 1-4 Fossils to weight affixes & block groups.";
                _fossilBiasesLabel.Modulate = new Color(0.6f, 0.65f, 0.75f);
            }
            else
            {
                var bonuses = new List<string>();
                foreach (var id in fossilIds)
                {
                    if (Db != null && Db.Fossils.TryGetValue(id, out var f))
                    {
                        bonuses.Add($"{f.Name} ({f.PrimaryBonus})");
                    }
                }
                _fossilBiasesLabel.Text = string.Join(" • ", bonuses);
                _fossilBiasesLabel.Modulate = new Color(1.0f, 0.85f, 0.35f);
            }
        }

        if (_btnSlamResonator != null)
        {
            bool hasTarget = _selectedTargetItem != null && !_anvilService.CanSlamCurrency(_selectedTargetItem, "vaal") ? false : true;
            bool hasFossils = fossilIds.Count > 0 && fossilIds.All(id => Player?.Stash?.GetFossilCount(id) > 0);
            _btnSlamResonator.Disabled = fossilIds.Count == 0 || !hasFossils || _selectedTargetItem == null || TheCrucibleAnvilService.IsCorrupted(_selectedTargetItem);
        }
    }

    private List<string> GetSelectedFossilIds()
    {
        var list = new List<string>();
        var slots = new[] { _fossilSlot1, _fossilSlot2, _fossilSlot3, _fossilSlot4 };
        string[] idMap = { "", "pristine_fossil", "metallic_fossil", "scorched_fossil", "frigid_fossil", "dense_fossil", "corroded_fossil" };

        foreach (var slot in slots)
        {
            if (slot == null || slot.Selected <= 0 || slot.Selected >= idMap.Length) continue;
            list.Add(idMap[slot.Selected]);
        }
        return list;
    }

    public void ExecuteCurrencySlam(string currencyId)
    {
        if (_selectedTargetItem == null || Db == null || Player == null) return;

        _lastCraftResult = _anvilService.SlamCurrency(_selectedTargetItem, currencyId, Db, Player);
        if (!_lastCraftResult.Success)
        {
            OnToastRequested?.Invoke(_lastCraftResult.Message, new Color(0.9f, 0.3f, 0.3f));
            return;
        }
        OnCraftFinished();
    }

    public void ExecuteResonatorSlam()
    {
        if (_selectedTargetItem == null || Db == null || Player == null) return;
        var fossilIds = GetSelectedFossilIds();
        if (fossilIds.Count == 0) return;

        _lastCraftResult = _anvilService.SlamFossils(_selectedTargetItem, fossilIds, Db, Player);
        if (!_lastCraftResult.Success)
        {
            OnToastRequested?.Invoke(_lastCraftResult.Message, new Color(0.9f, 0.3f, 0.3f));
            return;
        }
        OnCraftFinished();
    }

    private void OnCraftFinished()
    {
        RenderTargetItemCard();
        UpdateCurrencyButtonStates();
        UpdateFossilBiasesSummary();
        RenderSmartStatDelta();

        OnCraftCompleted?.Invoke();
        if (_lastCraftResult != null)
        {
            OnToastRequested?.Invoke(_lastCraftResult.Message, new Color(0.35f, 0.85f, 0.45f));
        }
    }

    private void RenderSmartStatDelta()
    {
        if (_lastCraftResult == null || _deltaPreviewCard == null) return;

        _deltaPreviewCard.Visible = true;

        if (_deltaMessageLabel != null)
        {
            _deltaMessageLabel.Text = _lastCraftResult.Message;
        }

        // Format Delta EHP
        if (_deltaEhpLabel != null)
        {
            string sign = _lastCraftResult.DeltaEhp >= 0f ? "+" : "";
            _deltaEhpLabel.Text = $"EHP: {_lastCraftResult.NewEhp:N0} ({sign}{_lastCraftResult.DeltaEhp:N0})";
            _deltaEhpLabel.Modulate = _lastCraftResult.DeltaEhp > 0f
                ? new Color(0.35f, 0.85f, 0.45f)
                : (_lastCraftResult.DeltaEhp < 0f ? new Color(0.95f, 0.4f, 0.35f) : new Color(0.8f, 0.8f, 0.85f));
        }

        // Format Delta DPS
        if (_deltaDpsLabel != null)
        {
            string sign = _lastCraftResult.DeltaDps >= 0f ? "+" : "";
            _deltaDpsLabel.Text = $"DPS: {_lastCraftResult.NewDps:N0} ({sign}{_lastCraftResult.DeltaDps:N0})";
            _deltaDpsLabel.Modulate = _lastCraftResult.DeltaDps > 0f
                ? new Color(0.35f, 0.85f, 0.45f)
                : (_lastCraftResult.DeltaDps < 0f ? new Color(0.95f, 0.4f, 0.35f) : new Color(0.8f, 0.8f, 0.85f));
        }

        // Format Resistances Delta
        if (_deltaResistsLabel != null)
        {
            _deltaResistsLabel.Text = $"Fire: {FormatDeltaRes(_lastCraftResult.DeltaFireRes)} | " +
                                      $"Cold: {FormatDeltaRes(_lastCraftResult.DeltaColdRes)} | " +
                                      $"Light: {FormatDeltaRes(_lastCraftResult.DeltaLightningRes)} | " +
                                      $"Chaos: {FormatDeltaRes(_lastCraftResult.DeltaChaosRes)}";
        }

        // Verdict pill
        if (_deltaVerdictLabel != null)
        {
            if (_lastCraftResult.IsCorrupted)
            {
                _deltaVerdictLabel.Text = "[ 🩸 VAAL CORRUPTED ]";
                _deltaVerdictLabel.Modulate = new Color(0.95f, 0.35f, 0.35f);
            }
            else if (_lastCraftResult.DeltaEhp >= 0f && _lastCraftResult.DeltaDps >= 0f && (_lastCraftResult.DeltaEhp > 0f || _lastCraftResult.DeltaDps > 0f))
            {
                _deltaVerdictLabel.Text = "[ ▲ UPGRADE ]";
                _deltaVerdictLabel.Modulate = new Color(0.35f, 0.85f, 0.45f);
            }
            else if (_lastCraftResult.DeltaEhp <= 0f && _lastCraftResult.DeltaDps <= 0f && (_lastCraftResult.DeltaEhp < 0f || _lastCraftResult.DeltaDps < 0f))
            {
                _deltaVerdictLabel.Text = "[ ▼ DOWNGRADE ]";
                _deltaVerdictLabel.Modulate = new Color(0.95f, 0.4f, 0.35f);
            }
            else
            {
                _deltaVerdictLabel.Text = "[ ◆ TRADEOFF ]";
                _deltaVerdictLabel.Modulate = new Color(1.0f, 0.75f, 0.3f);
            }
        }
    }

    private static string FormatDeltaRes(float delta)
    {
        if (MathF.Abs(delta) < 0.1f) return "0%";
        return delta > 0 ? $"+{delta:F0}%" : $"{delta:F0}%";
    }

    // ─────────────────────────────────────────────
    //  2. Thaumaturgic Smelter Panel
    // ─────────────────────────────────────────────

    private void RefreshSmelterPanel()
    {
        if (Player?.Sanctuary == null) return;

        if (_dustAmountLabel != null)
        {
            _dustAmountLabel.Text = $"✨ Dust: {Player.Sanctuary.ThaumaturgicDust:N0}";
        }

        if (_autoSmeltCheckBox != null)
        {
            _autoSmeltCheckBox.ButtonPressed = Player.Sanctuary.AutoSmeltActive;
        }

        if (_infusionStatusLabel != null)
        {
            if (Player.Sanctuary.InfusedMapsRemaining > 0)
            {
                _infusionStatusLabel.Text = $"Infused: {Player.Sanctuary.InfusedMapsRemaining} Maps (+{Player.Sanctuary.InfusedQuantityBonus * 100:F0}% IIQ)";
                _infusionStatusLabel.Modulate = new Color(0.2f, 0.95f, 0.8f);
            }
            else
            {
                _infusionStatusLabel.Text = "Infusion Inactive (Standard IIQ)";
                _infusionStatusLabel.Modulate = new Color(0.6f, 0.65f, 0.75f);
            }
        }

        if (_btnInfuseMaps != null)
        {
            _btnInfuseMaps.Text = "✨ Infuse 5 Maps (+10% IIQ) • 2.5k Dust";
            _btnInfuseMaps.Disabled = Player.Sanctuary.ThaumaturgicDust < SmelterService.DefaultInfuseCost;
        }

        if (_btnTransmuteUnmaking != null)
        {
            _btnTransmuteUnmaking.Text = "🔮 Transmute Unmaking (10k Dust)";
            _btnTransmuteUnmaking.Disabled = Player.Sanctuary.ThaumaturgicDust < SmelterService.DefaultTransmuteCost;
        }

        if (_btnTransmuteChaos != null)
        {
            _btnTransmuteChaos.Text = "🪙 Transmute Chaos Orb (10k Dust)";
            _btnTransmuteChaos.Disabled = Player.Sanctuary.ThaumaturgicDust < SmelterService.DefaultTransmuteCost;
        }
    }

    private void OnInfuseMapsPressed()
    {
        if (Player?.Sanctuary == null) return;
        bool ok = _smelterService.TryInfuseNextMaps(Player.Sanctuary);
        if (ok)
        {
            SetSmelterToast("✦ Infused next 5 Maps with +10% Item Quantity!", new Color(0.2f, 0.95f, 0.8f));
        }
        else
        {
            SetSmelterToast("Not enough Thaumaturgic Dust (requires 2,500)!", new Color(0.95f, 0.4f, 0.35f));
        }
        RefreshSmelterPanel();
    }

    private void OnTransmuteUnmakingPressed()
    {
        if (Player?.Sanctuary == null || Player.Stash == null) return;
        bool ok = _smelterService.TryTransmuteToUnmaking(Player.Sanctuary, Player.Stash);
        if (ok)
        {
            SetSmelterToast("✦ Transmuted 10,000 Dust -> 1x Orb of Unmaking!", new Color(0.7f, 0.4f, 0.95f));
        }
        else
        {
            SetSmelterToast("Not enough Dust (requires 10,000)!", new Color(0.95f, 0.4f, 0.35f));
        }
        RefreshSmelterPanel();
        UpdateCurrencyButtonStates();
    }

    private void OnTransmuteChaosPressed()
    {
        if (Player?.Sanctuary == null || Player.Stash == null) return;
        bool ok = _smelterService.TryTransmuteToChaos(Player.Sanctuary, Player.Stash);
        if (ok)
        {
            SetSmelterToast("✦ Transmuted 10,000 Dust -> 1x Chaos Orb!", new Color(1.0f, 0.85f, 0.35f));
        }
        else
        {
            SetSmelterToast("Not enough Dust (requires 10,000)!", new Color(0.95f, 0.4f, 0.35f));
        }
        RefreshSmelterPanel();
        UpdateCurrencyButtonStates();
    }

    private void SetSmelterToast(string message, Color color)
    {
        if (_smelterToastLabel != null)
        {
            _smelterToastLabel.Text = message;
            _smelterToastLabel.Modulate = color;
        }
        OnToastRequested?.Invoke(message, color);
    }

    // ─────────────────────────────────────────────
    //  3. The Abyssal Resonator Panel
    // ─────────────────────────────────────────────

    private void RefreshAbyssPanel()
    {
        if (Player?.Sanctuary == null) return;

        if (_sulphiteProgressBar != null)
        {
            _sulphiteProgressBar.MaxValue = Player.Sanctuary.MaxVoltaxicSulphite;
            _sulphiteProgressBar.Value = Player.Sanctuary.VoltaxicSulphite;
        }

        if (_sulphiteAmountLabel != null)
        {
            _sulphiteAmountLabel.Text = $"Sulphite: {Player.Sanctuary.VoltaxicSulphite:N0} / {Player.Sanctuary.MaxVoltaxicSulphite:N0}";
        }

        if (_depthRecordLabel != null)
        {
            int depth = Player.Sanctuary.DeepestDepth;
            int packBonus = depth / 5;
            _depthRecordLabel.Text = $"Deepest Depth: {depth}m (+{packBonus}% Pack Size)";
        }

        if (_btnDescendAbyss != null)
        {
            _btnDescendAbyss.Text = "⛏ Descend to Abyss (250 Sulphite)";
            _btnDescendAbyss.Disabled = Player.Sanctuary.VoltaxicSulphite < 250;
        }
    }

    private void OnDescendAbyssPressed()
    {
        if (Player?.Sanctuary == null) return;
        bool descended = Player.Sanctuary.DescendAbyss(1, 250);
        if (descended)
        {
            OnToastRequested?.Invoke($"⛏ Descended to Endless Abyss Depth {Player.Sanctuary.DeepestDepth}m!", new Color(0.95f, 0.85f, 0.3f));
            OnDescendToAbyssRequested?.Invoke();
        }
        else
        {
            OnToastRequested?.Invoke("Insufficient Sulphite to delve (requires 250)!", new Color(0.95f, 0.4f, 0.35f));
        }
        RefreshAbyssPanel();
    }

    // ─────────────────────────────────────────────
    //  4. Atlas Relics & Pinnacle Hall Panel
    // ─────────────────────────────────────────────

    private void RefreshRelicsPanel()
    {
        if (Player?.Sanctuary == null) return;

        int tier = Player.Sanctuary.CurrentTier;
        string tierName = "Hollow Crypt";
        if (Db != null && Db.SanctuaryTiers.TryGetValue($"tier_{tier}", out var def))
        {
            tierName = def.Name;
        }

        if (_hubTierLabel != null)
        {
            _hubTierLabel.Text = $"Sanctuary Tier {tier}: {tierName}";
        }

        if (_hubPerksLabel != null)
        {
            _hubPerksLabel.Text = tier switch
            {
                >= 4 => "Celestial Void: 5 Map Slots • 15 Queue Slots • +100% Smelt Speed • Relics Active",
                3 => "Eldritch Sanctum: 4 Map Slots • 15 Queue Slots • +50% Smelt Speed",
                2 => "Awakened Foundry: 4 Map Slots • 10 Queue Slots • Smelter & Abyss Unlocked",
                _ => "Hollow Crypt: 4 Map Slots • 10 Queue Slots • Base Crucible Anvil"
            };
        }

        // Voidstones
        UpdateVoidstoneLabel(_voidstone1Label, "🔵 Grasped", Player.Sanctuary.MavenDefeated);
        UpdateVoidstoneLabel(_voidstone2Label, "🟣 Decaying", Player.Sanctuary.UberElderDefeated);
        UpdateVoidstoneLabel(_voidstone3Label, "🔴 Polaric", Player.Sanctuary.SearingExarchDefeated);
        UpdateVoidstoneLabel(_voidstone4Label, "🟢 Omniscient", Player.Sanctuary.EaterOfWorldsDefeated);

        // Pinnacle Bosses Buttons
        UpdateBossButton(_btnBossEater, "Eater", Player.Sanctuary.EaterOfWorldsDefeated);
        UpdateBossButton(_btnBossExarch, "Exarch", Player.Sanctuary.SearingExarchDefeated);
        UpdateBossButton(_btnBossMaven, "Maven", Player.Sanctuary.MavenDefeated);
        UpdateBossButton(_btnBossElder, "Elder", Player.Sanctuary.UberElderDefeated);
    }

    private static void UpdateVoidstoneLabel(Label? lbl, string name, bool unlocked)
    {
        if (lbl == null) return;
        lbl.Text = unlocked ? $"{name} ✓" : $"{name} ✗";
        lbl.Modulate = unlocked ? new Color(1.0f, 0.9f, 0.4f) : new Color(0.45f, 0.5f, 0.6f);
    }

    private static void UpdateBossButton(Button? btn, string name, bool defeated)
    {
        if (btn == null) return;
        btn.Text = defeated ? $"{name}: [DEAD]" : $"{name}: [OPEN]";
        btn.Modulate = defeated ? new Color(0.35f, 0.85f, 0.45f) : new Color(0.85f, 0.85f, 0.9f);
        btn.TooltipText = defeated ? $"{name} Defeated! Voidstone unlocked." : $"Challenge {name} (Pinnacle Encounter)";
    }

    private void TogglePinnacleBoss(string bossId)
    {
        if (Player?.Sanctuary == null) return;

        switch (bossId.ToLowerInvariant())
        {
            case "eater":
                Player.Sanctuary.EaterOfWorldsDefeated = !Player.Sanctuary.EaterOfWorldsDefeated;
                break;
            case "exarch":
                Player.Sanctuary.SearingExarchDefeated = !Player.Sanctuary.SearingExarchDefeated;
                break;
            case "maven":
                Player.Sanctuary.MavenDefeated = !Player.Sanctuary.MavenDefeated;
                break;
            case "elder":
                Player.Sanctuary.UberElderDefeated = !Player.Sanctuary.UberElderDefeated;
                break;
        }

        RefreshRelicsPanel();
        OnToastRequested?.Invoke($"Updated {bossId} Pinnacle status! Voidstones: {Player.Sanctuary.VoidstonesCount}/4", new Color(0.9f, 0.85f, 0.4f));
    }

    public void RefreshView()
    {
        RenderTargetItemCard();
        UpdateCurrencyButtonStates();
        UpdateFossilBiasesSummary();
        RefreshSmelterPanel();
        RefreshAbyssPanel();
        RefreshRelicsPanel();
    }
}
