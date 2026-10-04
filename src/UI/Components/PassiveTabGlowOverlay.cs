using System;
using Godot;

namespace IdleExile.UI.Components;

/// <summary>
/// Overlay control for the Passive Tree tab button (Task 6.2).
/// Renders a gentle, breathing golden/amber halo to nudge the player when unspent passive
/// points are available, and plays an energetic double-pulse burst on Level Up.
/// MouseFilter is set to Ignore to ensure zero interference with button clicks.
/// </summary>
public partial class PassiveTabGlowOverlay : Control
{
    private float _pulseAlpha = 0f;
    private float _pulseTimer = 0f;
    private bool _isActive = false;
    private Tween? _burstTween;

    private readonly StyleBoxFlat _glowBox = new()
    {
        BorderWidthLeft = 1,
        BorderWidthTop = 1,
        BorderWidthRight = 1,
        BorderWidthBottom = 1,
        CornerRadiusTopLeft = 3,
        CornerRadiusTopRight = 3,
        CornerRadiusBottomRight = 3,
        CornerRadiusBottomLeft = 3,
        ShadowSize = 3
    };

    public bool IsActive => _isActive;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        SetAnchorsPreset(LayoutPreset.FullRect);
        Visible = false;
        SetProcess(false);
    }

    /// <summary>
    /// Configures the active alert state of the Passive Tree tab.
    /// When active is true, activates the gentle breathing pulse.
    /// When active is false, smoothly fades out and sleeps.
    /// </summary>
    public void SetAlertState(bool active)
    {
        _isActive = active;
        if (active)
        {
            Visible = true;
            SetProcess(true);
        }
        else
        {
            _burstTween?.Kill();
            _burstTween = null;
            if (_pulseAlpha <= 0.001f)
            {
                Visible = false;
                SetProcess(false);
            }
        }
    }

    /// <summary>
    /// Current alpha value of the tab button pulsating halo (0.0 to 1.0).
    /// </summary>
    public float PulseAlpha
    {
        get => _pulseAlpha;
        set
        {
            _pulseAlpha = Mathf.Clamp(value, 0f, 1f);
            QueueRedraw();
        }
    }

    /// <summary>
    /// Triggers an immediate celebratory double-pulse burst upon leveling up.
    /// </summary>
    public void TriggerLevelUpBurst()
    {
        Visible = true;
        _isActive = true;
        PulseAlpha = 1.0f;
        SetProcess(false); // Hand over control to Tween temporarily

        _burstTween?.Kill();
        _burstTween = CreateTween();
        _burstTween.SetTrans(Tween.TransitionType.Cubic);
        _burstTween.SetEase(Tween.EaseType.Out);

        // Quick burst 1 decay
        _burstTween.TweenProperty(this, nameof(PulseAlpha), 0.35f, 0.16f);
        // Quick burst 2 surge
        _burstTween.TweenProperty(this, nameof(PulseAlpha), 0.95f, 0.14f);
        // Quick burst 2 decay
        _burstTween.TweenProperty(this, nameof(PulseAlpha), 0.50f, 0.20f);

        _burstTween.Finished += () =>
        {
            _pulseTimer = 0f;
            if (_isActive)
            {
                SetProcess(true);
            }
            else
            {
                _pulseAlpha = 0f;
                Visible = false;
            }
        };
    }

    public override void _Process(double delta)
    {
        if (_isActive)
        {
            _pulseTimer += (float)delta;
            // Gentle sinusoidal breathing pulse between 0.28 and 0.88 (period ~1.8s)
            float sineWave = 0.5f + 0.5f * MathF.Sin(_pulseTimer * 3.5f);
            _pulseAlpha = 0.28f + 0.60f * sineWave;
            QueueRedraw();
        }
        else
        {
            // Smooth fade out
            _pulseAlpha = Mathf.MoveToward(_pulseAlpha, 0f, (float)delta * 3.0f);
            QueueRedraw();

            if (_pulseAlpha <= 0.001f)
            {
                _pulseAlpha = 0f;
                Visible = false;
                SetProcess(false);
            }
        }
    }

    public override void _Draw()
    {
        if (_pulseAlpha <= 0.001f) return;

        // Warm amber-gold glow aesthetic
        _glowBox.BgColor = new Color(1.0f, 0.76f, 0.18f, _pulseAlpha * 0.16f);
        _glowBox.BorderColor = new Color(1.0f, 0.82f, 0.24f, _pulseAlpha * 0.95f);
        _glowBox.ShadowColor = new Color(1.0f, 0.72f, 0.12f, _pulseAlpha * 0.40f);

        DrawStyleBox(_glowBox, new Rect2(Vector2.Zero, Size));
    }
}
