using System;
using Godot;
using IdleExile.Core.Models;

namespace IdleExile.UI;

/// <summary>
/// Controller for the Starter Archetype Selection Modal (Task 2.1).
/// Presents the player with 3 core archetypes (Juggernaut, Elementalist, Deadeye)
/// with 1-click selection and rich dark fantasy visual presentation.
/// </summary>
public partial class StarterSelectionModal : Control
{
    private Button? _juggernautBtn;
    private Button? _elementalistBtn;
    private Button? _deadeyeBtn;

    private PanelContainer? _juggernautCard;
    private PanelContainer? _elementalistCard;
    private PanelContainer? _deadeyeCard;

    private bool _isDismissed = false;

    /// <summary>Fired when the player chooses an archetype.</summary>
    public event Action<StarterArchetypeDefinition>? OnArchetypeSelected;

    public override void _Ready()
    {
        // 1. Resolve UI Nodes
        _juggernautBtn = GetNodeOrNull<Button>("%JuggernautBtn");
        _elementalistBtn = GetNodeOrNull<Button>("%ElementalistBtn");
        _deadeyeBtn = GetNodeOrNull<Button>("%DeadeyeBtn");

        _juggernautCard = GetNodeOrNull<PanelContainer>("%JuggernautCard");
        _elementalistCard = GetNodeOrNull<PanelContainer>("%ElementalistCard");
        _deadeyeCard = GetNodeOrNull<PanelContainer>("%DeadeyeCard");

        // 2. Wire button click signals
        if (_juggernautBtn != null)
            _juggernautBtn.Pressed += () => Choose(StarterArchetype.Juggernaut);

        if (_elementalistBtn != null)
            _elementalistBtn.Pressed += () => Choose(StarterArchetype.Elementalist);

        if (_deadeyeBtn != null)
            _deadeyeBtn.Pressed += () => Choose(StarterArchetype.Deadeye);

        // 3. Wire hover feedback animations
        WireCardHover(_juggernautCard, _juggernautBtn);
        WireCardHover(_elementalistCard, _elementalistBtn);
        WireCardHover(_deadeyeCard, _deadeyeBtn);

        // 4. Subtle entrance fade-in
        if (IsInsideTree())
        {
            Modulate = new Color(1f, 1f, 1f, 0f);
            var tween = CreateTween();
            tween.TweenProperty(this, "modulate:a", 1.0f, 0.25f)
                 .SetTrans(Tween.TransitionType.Cubic)
                 .SetEase(Tween.EaseType.Out);
        }
    }

    /// <summary>
    /// Chooses the specified archetype, plays dismiss transition, and fires event.
    /// </summary>
    public void Choose(StarterArchetype archetype)
    {
        if (_isDismissed)
            return;

        _isDismissed = true;

        // Disable buttons to prevent spam
        if (_juggernautBtn != null) _juggernautBtn.Disabled = true;
        if (_elementalistBtn != null) _elementalistBtn.Disabled = true;
        if (_deadeyeBtn != null) _deadeyeBtn.Disabled = true;

        var definition = StarterArchetypeCatalog.Get(archetype);
        GD.Print($"[StarterSelectionModal] Player chosen archetype: {definition.DisplayName} ({definition.PrimaryAttribute})");

        // Fire event immediately so player state initializes without delay
        OnArchetypeSelected?.Invoke(definition);

        if (IsInsideTree())
        {
            // Fade out animation
            var tween = CreateTween();
            tween.TweenProperty(this, "modulate:a", 0.0f, 0.25f)
                 .SetTrans(Tween.TransitionType.Cubic)
                 .SetEase(Tween.EaseType.In);

            tween.Finished += () =>
            {
                Visible = false;
                QueueFree();
            };
        }
        else
        {
            Visible = false;
        }
    }

    private void WireCardHover(PanelContainer? card, Button? button)
    {
        if (card == null || button == null)
            return;

        button.MouseEntered += () =>
        {
            if (_isDismissed || !card.IsInsideTree()) return;
            var tween = card.CreateTween();
            tween.TweenProperty(card, "scale", new Vector2(1.02f, 1.02f), 0.12f)
                 .SetTrans(Tween.TransitionType.Quad)
                 .SetEase(Tween.EaseType.Out);
        };

        button.MouseExited += () =>
        {
            if (_isDismissed || !card.IsInsideTree()) return;
            var tween = card.CreateTween();
            tween.TweenProperty(card, "scale", Vector2.One, 0.12f)
                 .SetTrans(Tween.TransitionType.Quad)
                 .SetEase(Tween.EaseType.Out);
        };
    }
}
