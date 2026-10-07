using System;
using Godot;
using IdleExile.Core.Models;
using IdleExile.Core.Simulation;

namespace IdleExile.UI.Components;

/// <summary>
/// UI Component representing the Crucible Map Device Controls (Task 2.3 & Task 2.7).
/// Manages the slotted map slate display, the primary [ACTIVATE MAP] action button
/// with contextual Golden Pulse guidance, and the Emergency Portal escape action.
/// </summary>
public partial class MapDeviceBar : PanelContainer
{
    private Label? _slottedTierBadge;
    private Label? _slottedMapNameLabel;
    private Label? _slottedMapInfoLabel;
    private Button? _activateMapBtn;
    private Button? _emergencyPortalBtn;

    private Tween? _pulseTween;
    private bool _isPulseActive = false;
    private bool _isPortalActive = false;

    public MapDefinition? SlottedMap { get; private set; }

    /// <summary>Fired when the player activates the slotted map.</summary>
    public event Action<MapDefinition>? OnMapActivated;

    /// <summary>Fired when emergency portal is triggered.</summary>
    public event Action? OnEmergencyPortalTriggered;

    public override void _Ready()
    {
        _slottedTierBadge = GetNodeOrNull<Label>("%SlottedTierBadge");
        _slottedMapNameLabel = GetNodeOrNull<Label>("%SlottedMapNameLabel");
        _slottedMapInfoLabel = GetNodeOrNull<Label>("%SlottedMapInfoLabel");
        _activateMapBtn = GetNodeOrNull<Button>("%ActivateMapBtn");
        _emergencyPortalBtn = GetNodeOrNull<Button>("%EmergencyPortalBtn");

        if (_activateMapBtn != null)
        {
            _activateMapBtn.Pressed += OnActivatePressed;
        }

        if (_emergencyPortalBtn != null)
        {
            _emergencyPortalBtn.Pressed += OnEmergencyPortalPressed;
        }
    }

    public override void _UnhandledKeyInput(InputEvent @event)
    {
        if (@event is InputEventKey keyEvent && keyEvent.Pressed && !keyEvent.Echo)
        {
            if (keyEvent.Keycode == Key.Space && _isPortalActive)
            {
                OnEmergencyPortalPressed();
                GetViewport()?.SetInputAsHandled();
            }
        }
    }

    /// <summary>
    /// Slots a map slate into the Map Device tray.
    /// </summary>
    public void SlotMap(MapDefinition map, string rarity = "Normal")
    {
        SlottedMap = map ?? throw new ArgumentNullException(nameof(map));

        if (_slottedTierBadge != null)
        {
            _slottedTierBadge.Text = $"T{map.Tier}";
        }

        if (_slottedMapNameLabel != null)
        {
            _slottedMapNameLabel.Text = map.Name;
            _slottedMapNameLabel.Modulate = rarity.ToLowerInvariant() switch
            {
                "rare" => new Color(1.0f, 1.0f, 0.45f),
                "magic" => new Color(0.55f, 0.55f, 1.0f),
                _ => new Color(0.92f, 0.92f, 0.95f)
            };
        }

        if (_slottedMapInfoLabel != null)
        {
            _slottedMapInfoLabel.Text = $"Qty: +{map.BaseQuantity}% | Mobs: {map.BasePackSize}%";
        }
    }

    /// <summary>
    /// Toggles the Golden Pulse visual effect on the [ACTIVATE MAP] button.
    /// Creates a smooth looping tween highlighting the button border to guide the player.
    /// </summary>
    public void SetGoldenPulse(bool enable)
    {
        _isPulseActive = enable;

        _pulseTween?.Kill();
        _pulseTween = null;

        if (_activateMapBtn == null) return;

        if (enable && IsInsideTree())
        {
            _pulseTween = CreateTween().SetLoops();
            _pulseTween.TweenProperty(_activateMapBtn, "modulate", new Color(1.35f, 1.25f, 0.65f, 1.0f), 0.65f)
                       .SetTrans(Tween.TransitionType.Sine)
                       .SetEase(Tween.EaseType.InOut);
            _pulseTween.TweenProperty(_activateMapBtn, "modulate", new Color(1.0f, 1.0f, 1.0f, 0.85f), 0.65f)
                       .SetTrans(Tween.TransitionType.Sine)
                       .SetEase(Tween.EaseType.InOut);
        }
        else
        {
            _activateMapBtn.Modulate = new Color(1f, 1f, 1f, 1f);
        }
    }

    /// <summary>
    /// Updates portal activity state and button label.
    /// </summary>
    public void SetPortalActive(bool isActive)
    {
        _isPortalActive = isActive;
        if (_activateMapBtn != null)
        {
            if (isActive)
            {
                _activateMapBtn.Text = "PORTAL ACTIVE";
                _activateMapBtn.Disabled = true;
            }
            else
            {
                _activateMapBtn.Text = "⚔ ACTIVATE MAP";
                _activateMapBtn.Disabled = false;
            }
        }

        if (_emergencyPortalBtn != null)
        {
            _emergencyPortalBtn.Disabled = !isActive;
        }
    }

    private void OnActivatePressed()
    {
        if (_isPortalActive || SlottedMap == null) return;

        SetGoldenPulse(false);
        SetPortalActive(true);
        OnMapActivated?.Invoke(SlottedMap);
    }

    private void OnEmergencyPortalPressed()
    {
        if (!_isPortalActive) return;

        SetPortalActive(false);
        OnEmergencyPortalTriggered?.Invoke();
    }
}
