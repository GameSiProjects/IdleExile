using System;
using Godot;

namespace IdleExile.UI.Components;

/// <summary>
/// Controller and visual renderer for Task 6.2: Viền Hào Quang Thông Báo (Edge Glow Alerts).
/// 1. Top Window Edge Glow: Flashes radiant golden shimmer along the top border when a Tier 1 item drops
///    (Divine / Exalt / Mirror / 6-Link) and plays a crisp metallic "Ting!" chime.
/// 2. Passive Tree Tab Alert: Gently pulsates an amber/gold halo on the Tree tab when unspent passive points
///    are available, nudging the player to allocate skills, and fires a celebratory burst on Level Up.
/// </summary>
public partial class EdgeGlowAlert : Control
{
    private float _glowAlpha = 0f;
    private Tween? _glowTween;
    private AudioStreamPlayer? _tingAudioPlayer;
    private Button? _tabPassivesBtn;
    private PassiveTabGlowOverlay? _passiveTabGlow;

    public bool IsGlowActive => _glowAlpha > 0.001f;
    public float CurrentGlowAlpha => _glowAlpha;
    public bool IsPassiveTabGlowActive => _passiveTabGlow?.IsActive ?? false;

    /// <summary>Fired when a Tier 1 drop alert is triggered. Arg: dropName.</summary>
    public event Action<string?>? OnTier1DropAlertTriggered;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        SetAnchorsPreset(LayoutPreset.TopWide);
        CustomMinimumSize = new Vector2(0, 30f);
        Visible = false;

        // Initialize AudioStreamPlayer with procedural metallic drop chime
        _tingAudioPlayer = new AudioStreamPlayer
        {
            Name = "Tier1DropAudioPlayer",
            Stream = DropSoundSynthesizer.Tier1DropChime,
            VolumeDb = -2.0f,
            Bus = "Master"
        };
        AddChild(_tingAudioPlayer);
    }

    /// <summary>
    /// Binds the Passive Tree tab button to host the gentle pulsating halo overlay.
    /// </summary>
    public void AttachPassiveTabGlow(Button tabPassivesBtn)
    {
        _tabPassivesBtn = tabPassivesBtn ?? throw new ArgumentNullException(nameof(tabPassivesBtn));

        // Create overlay child inside button
        _passiveTabGlow = new PassiveTabGlowOverlay { Name = "PassiveTabGlowOverlay" };
        _tabPassivesBtn.AddChild(_passiveTabGlow);
    }

    /// <summary>
    /// Triggers the full Tier 1 Drop Alert:
    /// 1. Top Edge Golden Glow shimmer flash & undulation.
    /// 2. Metallic "Ting!" drop chime playback.
    /// </summary>
    public void TriggerTier1Alert(string? dropName = null)
    {
        TriggerGoldGlow();
        PlayTingSound();
        OnTier1DropAlertTriggered?.Invoke(dropName);
    }

    /// <summary>
    /// Plays the synthesized crisp metallic "Ting!" drop chime.
    /// </summary>
    public void PlayTingSound()
    {
        try
        {
            if (_tingAudioPlayer != null && IsInstanceValid(_tingAudioPlayer))
            {
                _tingAudioPlayer.Play();
            }
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[EdgeGlowAlert] Failed to play Ting audio: {ex.Message}");
        }
    }

    /// <summary>
    /// Current alpha value of the top edge golden glow (0.0 to 1.0).
    /// </summary>
    public float GlowAlpha
    {
        get => _glowAlpha;
        set
        {
            _glowAlpha = Mathf.Clamp(value, 0f, 1f);
            Visible = _glowAlpha > 0.001f;
            QueueRedraw();
        }
    }

    /// <summary>
    /// Triggers the visual Golden Edge Glow animation on the top border.
    /// Immediate radiant flash (alpha = 1.0) -> 2-phase golden shimmer undulation -> smooth fade out.
    /// </summary>
    public void TriggerGoldGlow()
    {
        Visible = true;
        GlowAlpha = 1.0f;

        _glowTween?.Kill();
        _glowTween = CreateTween();
        _glowTween.SetTrans(Tween.TransitionType.Cubic);
        _glowTween.SetEase(Tween.EaseType.Out);

        // Stage 1: First shimmer pulse decay
        _glowTween.TweenProperty(this, nameof(GlowAlpha), 0.45f, 0.22f);
        // Stage 2: First shimmer pulse surge
        _glowTween.TweenProperty(this, nameof(GlowAlpha), 0.88f, 0.20f);
        // Stage 3: Second shimmer pulse decay
        _glowTween.TweenProperty(this, nameof(GlowAlpha), 0.35f, 0.24f);
        // Stage 4: Second shimmer pulse surge
        _glowTween.TweenProperty(this, nameof(GlowAlpha), 0.72f, 0.20f);
        // Stage 5: Smooth exponential decay to silence
        _glowTween.TweenProperty(this, nameof(GlowAlpha), 0.0f, 0.55f);

        _glowTween.Finished += () =>
        {
            _glowAlpha = 0f;
            Visible = false;
        };
    }

    /// <summary>
    /// Directly sets the glow alpha (used in headless testing).
    /// </summary>
    public void SetGlowAlpha(float alpha)
    {
        _glowTween?.Kill();
        _glowTween = null;
        GlowAlpha = alpha;
    }

    /// <summary>
    /// Updates the Passive Tree tab alert status:
    /// - If unspent points exist and the player is not currently on the Passives tab:
    ///   activates the gentle breathing amber/gold halo.
    /// - If active tab is Passives or unspent points == 0:
    ///   deactivates the halo.
    /// - Updates button text to display available points badge (e.g. "🌳 Tree (+2)").
    /// </summary>
    public void UpdatePassiveTreeAlert(int unspentPoints, bool isPassiveTabActive)
    {
        if (_tabPassivesBtn != null)
        {
            if (unspentPoints > 0)
            {
                _tabPassivesBtn.Text = $"🌳 Tree (+{unspentPoints})";
                _tabPassivesBtn.TooltipText = Loc.Format("UI_TAB_TREE_UNSPENT_TOOLTIP", unspentPoints);
            }
            else
            {
                _tabPassivesBtn.Text = "🌳 Tree";
                _tabPassivesBtn.TooltipText = Loc.Tr("UI_TAB_TREE_TOOLTIP");
            }
        }

        bool shouldGlow = unspentPoints > 0 && !isPassiveTabActive;
        _passiveTabGlow?.SetAlertState(shouldGlow);
    }

    /// <summary>
    /// Fires an energetic double-pulse burst on the Passive Tree tab button upon Level Up.
    /// </summary>
    public void TriggerLevelUpPulse()
    {
        _passiveTabGlow?.TriggerLevelUpBurst();
    }

    public override void _Draw()
    {
        if (_glowAlpha <= 0.001f) return;

        float width = Size.X;
        float height = Size.Y;

        // 1. Ambient downward golden radiant bloom gradient
        for (int y = 0; y < (int)height; y++)
        {
            float normY = (float)y / height;
            float falloff = MathF.Pow(1.0f - normY, 2.0f);
            float alpha = _glowAlpha * 0.65f * falloff;

            var bloomColor = new Color(1.0f, 0.78f, 0.16f, alpha);
            DrawLine(new Vector2(0, y), new Vector2(width, y), bloomColor);
        }

        // 2. Solid top golden filaments (triple-pass intensity)
        // Y = 0: Brilliant white-gold core highlight
        DrawLine(new Vector2(0, 0), new Vector2(width, 0), new Color(1.0f, 0.98f, 0.85f, _glowAlpha * 0.98f));
        // Y = 1: Intense molten gold
        DrawLine(new Vector2(0, 1), new Vector2(width, 1), new Color(1.0f, 0.84f, 0.12f, _glowAlpha * 0.95f));
        // Y = 2: Warm amber transition
        DrawLine(new Vector2(0, 2), new Vector2(width, 2), new Color(0.92f, 0.60f, 0.08f, _glowAlpha * 0.75f));

        // 3. Central lens flare highlight (PoE Divine divine focus)
        float centerX = width * 0.5f;
        DrawCircle(new Vector2(centerX, 1f), 24f, new Color(1.0f, 0.90f, 0.45f, _glowAlpha * 0.35f));
        DrawCircle(new Vector2(centerX, 1f), 10f, new Color(1.0f, 1.0f, 0.92f, _glowAlpha * 0.75f));
    }
}
