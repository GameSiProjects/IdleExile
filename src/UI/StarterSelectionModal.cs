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

/// <summary>
/// Unit tests verifying StarterSelectionModal setup, node bindings, and archetype event dispatching.
/// </summary>
public static class StarterSelectionModalTests
{
    public static void RunAll()
    {
        Console.WriteLine("[Tests] Running StarterSelectionModalTests (Task 2.1)...");

        TestSceneInstantiationAndDefaults();
        TestArchetypeDispatching();
        TestTask22PlayerStateBootstrapIntegration();

        Console.WriteLine("[Tests] All StarterSelectionModalTests PASSED successfully!");
    }

    private static void TestSceneInstantiationAndDefaults()
    {
        var scene = GD.Load<PackedScene>("res://scenes/StarterSelectionModal.tscn");
        if (scene == null)
        {
            throw new Exception("StarterSelectionModal.tscn could not be loaded from res://scenes/");
        }

        var instance = scene.Instantiate<StarterSelectionModal>();
        if (instance == null)
        {
            throw new Exception("StarterSelectionModal failed to instantiate as StarterSelectionModal class.");
        }

        instance._Ready();

        if (instance.GetNodeOrNull<Button>("%JuggernautBtn") == null)
            throw new Exception("Missing %JuggernautBtn node.");
        if (instance.GetNodeOrNull<Button>("%ElementalistBtn") == null)
            throw new Exception("Missing %ElementalistBtn node.");
        if (instance.GetNodeOrNull<Button>("%DeadeyeBtn") == null)
            throw new Exception("Missing %DeadeyeBtn node.");

        instance.QueueFree();
    }

    private static void TestArchetypeDispatching()
    {
        var scene = GD.Load<PackedScene>("res://scenes/StarterSelectionModal.tscn");
        var instance = scene.Instantiate<StarterSelectionModal>();
        instance._Ready();

        StarterArchetypeDefinition? selected = null;
        instance.OnArchetypeSelected += a => selected = a;

        instance.Choose(StarterArchetype.Elementalist);

        if (selected == null || selected.DisplayName != "Elementalist")
        {
            throw new Exception($"Expected Elementalist, got {selected?.DisplayName}");
        }

        instance.QueueFree();
    }

    private static void TestTask22PlayerStateBootstrapIntegration()
    {
        var player = new IdleExile.Core.State.PlayerState();
        var ftue = new IdleExile.Core.Simulation.FtueManager();

        var scene = GD.Load<PackedScene>("res://scenes/StarterSelectionModal.tscn");
        var modal = scene.Instantiate<StarterSelectionModal>();
        modal._Ready();

        modal.OnArchetypeSelected += def =>
        {
            ftue.SelectArchetype(def);
            player.BootstrapFromArchetype(def);
        };

        modal.Choose(StarterArchetype.Juggernaut);

        if (ftue.CurrentStep != IdleExile.Core.Simulation.FtueStep.AwaitingFirstMapActivate)
            throw new Exception("FTUE step failed to advance to AwaitingFirstMapActivate.");
        if (player.CurrentLife != 120f)
            throw new Exception($"Expected Life 120 for Juggernaut, got {player.CurrentLife}");
        if (player.MapInventory.Count != 3)
            throw new Exception($"Expected 3 starting maps, got {player.MapInventory.Count}");
        if (!player.Currencies.ContainsKey("orb_of_transmutation"))
            throw new Exception("Missing starting Orb of Transmutation.");

        modal.QueueFree();
    }
}
