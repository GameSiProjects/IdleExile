using System;
using System.Linq;
using Godot;
using IdleExile.Core.Models;
using IdleExile.Core.Services;
using IdleExile.Core.Simulation;
using IdleExile.Core.State;

namespace IdleExile.UI.Components;

/// <summary>
/// Controller for the System & Database Diagnostics Modal (Task 1.2).
/// Displays loaded database metrics, live combat engine telemetry, and system stats.
/// Fully gated with #if DEBUG and OS.IsDebugBuild() to guarantee zero overhead in Release builds.
/// </summary>
public partial class DiagnosticModal : Control
{
#if DEBUG
    private Label? _affixesCountLabel;
    private Label? _weaponBasesCountLabel;
    private Label? _armourBasesCountLabel;
    private Label? _accessoryBasesCountLabel;
    private Label? _gemsCountLabel;
    private Label? _mapsCountLabel;
    private Label? _modifiersCountLabel;
    private Label? _archetypesCountLabel;

    private Label? _engineStateLabel;
    private Label? _tickRateLabel;
    private Label? _exilePosLabel;
    private Label? _monstersLabel;

    private Label? _windowModeLabel;
    private Label? _memoryUsageLabel;
    private Label? _engineVersionLabel;

    private Button? _closeButton;
    private Button? _dismissButton;

    public event Action? OnClosed;

    public override void _Ready()
    {
        if (!OS.IsDebugBuild())
        {
            Visible = false;
            QueueFree();
            return;
        }

        _affixesCountLabel = GetNodeOrNull<Label>("%AffixesCountLabel");
        _weaponBasesCountLabel = GetNodeOrNull<Label>("%WeaponBasesCountLabel");
        _armourBasesCountLabel = GetNodeOrNull<Label>("%ArmourBasesCountLabel");
        _accessoryBasesCountLabel = GetNodeOrNull<Label>("%AccessoryBasesCountLabel");
        _gemsCountLabel = GetNodeOrNull<Label>("%GemsCountLabel");
        _mapsCountLabel = GetNodeOrNull<Label>("%MapsCountLabel");
        _modifiersCountLabel = GetNodeOrNull<Label>("%ModifiersCountLabel");
        _archetypesCountLabel = GetNodeOrNull<Label>("%ArchetypesCountLabel");

        _engineStateLabel = GetNodeOrNull<Label>("%EngineStateLabel");
        _tickRateLabel = GetNodeOrNull<Label>("%TickRateLabel");
        _exilePosLabel = GetNodeOrNull<Label>("%ExilePosLabel");
        _monstersLabel = GetNodeOrNull<Label>("%MonstersLabel");

        _windowModeLabel = GetNodeOrNull<Label>("%WindowModeLabel");
        _memoryUsageLabel = GetNodeOrNull<Label>("%MemoryUsageLabel");
        _engineVersionLabel = GetNodeOrNull<Label>("%EngineVersionLabel");

        _closeButton = GetNodeOrNull<Button>("%CloseButton");
        _dismissButton = GetNodeOrNull<Button>("%DismissButton");

        if (_closeButton != null) _closeButton.Pressed += Close;
        if (_dismissButton != null) _dismissButton.Pressed += Close;

        // Entrance animation
        Modulate = new Color(1f, 1f, 1f, 0f);
        var tween = CreateTween();
        tween.TweenProperty(this, "modulate:a", 1.0f, 0.18f)
             .SetTrans(Tween.TransitionType.Cubic)
             .SetEase(Tween.EaseType.Out);
    }

    public void Setup(DatabaseLoader db, PlayerState player, CombatEngine? engine, WindowController? windowController)
    {
        if (!OS.IsDebugBuild()) return;

        // 1. Populate Database Registry counts
        if (_affixesCountLabel != null) _affixesCountLabel.Text = $"{db.Affixes.Count} affixes";
        if (_weaponBasesCountLabel != null) _weaponBasesCountLabel.Text = $"{db.WeaponBases.Count} bases";
        if (_armourBasesCountLabel != null) _armourBasesCountLabel.Text = $"{db.ArmourBases.Count} bases";
        if (_accessoryBasesCountLabel != null) _accessoryBasesCountLabel.Text = $"{db.AccessoryBases.Count} bases";
        if (_gemsCountLabel != null) _gemsCountLabel.Text = $"{db.Gems.Count} gems";
        if (_mapsCountLabel != null) _mapsCountLabel.Text = $"{db.Maps.Count} maps";
        if (_modifiersCountLabel != null) _modifiersCountLabel.Text = $"{db.MapModifiers.Count} modifiers";
        if (_archetypesCountLabel != null) _archetypesCountLabel.Text = $"{StarterArchetypeCatalog.All.Count} classes";

        // 2. Populate Engine Telemetry
        UpdateLiveTelemetry(engine);

        // 3. Populate System & Window
        if (_windowModeLabel != null && windowController != null)
        {
            _windowModeLabel.Text = $"{windowController.CurrentMode} ({windowController.WindowSize.X}x{windowController.WindowSize.Y})";
        }

        if (_memoryUsageLabel != null)
        {
            long gcMem = GC.GetTotalMemory(false) / (1024 * 1024);
            _memoryUsageLabel.Text = $"{gcMem} MB (GC Heap)";
        }

        if (_engineVersionLabel != null)
        {
            _engineVersionLabel.Text = "Godot 4.7.2 Mono • .NET 8 (gl_compat)";
        }
    }

    public void UpdateLiveTelemetry(CombatEngine? engine)
    {
        if (!OS.IsDebugBuild()) return;

        if (engine != null)
        {
            if (_engineStateLabel != null)
                _engineStateLabel.Text = $"Active ({engine.Movement.CurrentState})";

            if (_tickRateLabel != null)
                _tickRateLabel.Text = "30 Hz (Fixed Simulation)";

            if (_exilePosLabel != null)
                _exilePosLabel.Text = $"({engine.ExilePosition.X:F0}, {engine.ExilePosition.Y:F0}) • {engine.Movement.DistanceTraveled:F0}px ({engine.Movement.MapProgressPercent:F1}%)";

            if (_monstersLabel != null && engine.CurrentWave != null)
            {
                int alive = engine.CurrentWave.Monsters.Count(m => m.IsAlive);
                _monstersLabel.Text = $"{alive} / {engine.CurrentWave.Monsters.Count} alive";
            }
        }
        else
        {
            if (_engineStateLabel != null) _engineStateLabel.Text = "Idle in Hideout";
            if (_tickRateLabel != null) _tickRateLabel.Text = "30 Hz (Standby)";
            if (_exilePosLabel != null) _exilePosLabel.Text = "(0, 0)";
            if (_monstersLabel != null) _monstersLabel.Text = "0 alive";
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventKey keyEvent && keyEvent.Pressed && !keyEvent.Echo)
        {
            if (keyEvent.Keycode == Key.Escape || keyEvent.Keycode == Key.F12)
            {
                Close();
                GetViewport().SetInputAsHandled();
            }
        }
    }

    public void Close()
    {
        var tween = CreateTween();
        tween.TweenProperty(this, "modulate:a", 0.0f, 0.15f)
             .SetTrans(Tween.TransitionType.Cubic)
             .SetEase(Tween.EaseType.In);

        tween.Finished += () =>
        {
            OnClosed?.Invoke();
            QueueFree();
        };
    }
#else
    public override void _Ready()
    {
        Visible = false;
        QueueFree();
    }
#endif
}
