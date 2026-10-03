using System;
using System.Collections.Generic;
using Godot;
using IdleExile.Core.Models;
using IdleExile.Core.State;

namespace IdleExile.UI.Components;

/// <summary>
/// Custom Control rendering a single Flask Slot in the 5-Flask Rack.
/// Displays liquid charge gauge, threshold notch, active Auto-Instill glow, and handles click-to-drink.
/// </summary>
public partial class FlaskSlotView : Control
{
    public int SlotIndex { get; set; } = 0;
    public FlaskType Type { get; private set; } = FlaskType.Life;
    public string FlaskName { get; private set; } = "Life Flask";
    public float MaxCharges { get; private set; } = 60f;
    public float CurrentCharges { get; private set; } = 60f;
    public float ChargesPerUse { get; private set; } = 20f;
    public float DurationSeconds { get; private set; } = 4.0f;
    public float RemainingDuration { get; private set; } = 0f;
    private FlaskAutoInstillCondition _autoInstillCondition = FlaskAutoInstillCondition.Manual;
    public FlaskAutoInstillCondition AutoInstillCondition
    {
        get => _autoInstillCondition;
        set
        {
            _autoInstillCondition = value;
            UpdateTooltip();
        }
    }

    private float _autoInstillThreshold = 50f;
    public float AutoInstillThreshold
    {
        get => _autoInstillThreshold;
        set
        {
            _autoInstillThreshold = value;
            UpdateTooltip();
        }
    }

    public string KeyShortcut { get; set; } = "1";

    public bool IsActive => RemainingDuration > 0f;
    public bool CanUse => CurrentCharges >= ChargesPerUse;

    public FlaskSlot? BoundFlask { get; private set; }
    public PlayerState? BoundPlayer { get; private set; }

    public event Action<FlaskSlotView>? OnFlaskDrank;

    private float _animationTime = 0f;

    public FlaskSlotView()
    {
        CustomMinimumSize = new Vector2(20, 32);
        MouseFilter = MouseFilterEnum.Stop;
        MouseDefaultCursorShape = CursorShape.PointingHand;
    }

    public FlaskSlotView(FlaskType type, string name, float maxCharges, float chargesPerUse, float durationSeconds, string keyShortcut = "1")
        : this()
    {
        Type = type;
        FlaskName = name;
        MaxCharges = maxCharges;
        CurrentCharges = maxCharges;
        ChargesPerUse = chargesPerUse;
        DurationSeconds = durationSeconds;
        KeyShortcut = keyShortcut;
        UpdateTooltip();
    }

    public void Bind(FlaskSlot flask, PlayerState? player = null)
    {
        BoundFlask = flask;
        BoundPlayer = player;
        Type = flask.Type;
        FlaskName = flask.Name;
        MaxCharges = flask.MaxCharges;
        CurrentCharges = flask.CurrentCharges;
        ChargesPerUse = flask.ChargesPerUse;
        DurationSeconds = flask.DurationSeconds;
        RemainingDuration = flask.RemainingDuration;
        AutoInstillCondition = flask.AutoInstillCondition;
        AutoInstillThreshold = flask.AutoInstillThreshold;

        flask.OnFlaskUsed += OnBoundFlaskUsed;
        UpdateTooltip();
        QueueRedraw();
    }

    private void OnBoundFlaskUsed(FlaskSlot slot)
    {
        CurrentCharges = slot.CurrentCharges;
        RemainingDuration = slot.RemainingDuration;
        UpdateTooltip();
        QueueRedraw();
    }

    public void UpdateState(float currentCharges, float maxCharges, bool isActive, float remainingDuration)
    {
        CurrentCharges = Mathf.Clamp(currentCharges, 0f, maxCharges);
        MaxCharges = MathF.Max(1f, maxCharges);
        RemainingDuration = MathF.Max(0f, remainingDuration);
        UpdateTooltip();
        QueueRedraw();
    }

    public bool TryDrink()
    {
        if (BoundFlask != null)
        {
            bool drank = BoundFlask.TryDrink(BoundPlayer);
            if (drank)
            {
                CurrentCharges = BoundFlask.CurrentCharges;
                RemainingDuration = BoundFlask.RemainingDuration;
                UpdateTooltip();
                QueueRedraw();
                OnFlaskDrank?.Invoke(this);
            }
            return drank;
        }

        // Standalone manual use
        if (!CanUse)
            return false;

        CurrentCharges = MathF.Max(0f, CurrentCharges - ChargesPerUse);
        RemainingDuration = DurationSeconds;
        UpdateTooltip();
        QueueRedraw();
        OnFlaskDrank?.Invoke(this);
        return true;
    }

    public override void _Process(double delta)
    {
        _animationTime += (float)delta;

        if (BoundFlask != null)
        {
            if (CurrentCharges != BoundFlask.CurrentCharges || RemainingDuration != BoundFlask.RemainingDuration)
            {
                CurrentCharges = BoundFlask.CurrentCharges;
                RemainingDuration = BoundFlask.RemainingDuration;
                UpdateTooltip();
                QueueRedraw();
            }
        }
        else if (RemainingDuration > 0f)
        {
            RemainingDuration = MathF.Max(0f, RemainingDuration - (float)delta);
            QueueRedraw();
        }

        // Continuous redraw for pulsing auto-instill glowing border when active
        if (IsActive)
        {
            QueueRedraw();
        }
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton mouse && mouse.Pressed && mouse.ButtonIndex == MouseButton.Left)
        {
            TryDrink();
            AcceptEvent();
        }
    }

    public override void _Draw()
    {
        Vector2 size = Size;
        if (size.X <= 0 || size.Y <= 0) return;

        Rect2 slotRect = new(Vector2.Zero, size);

        // 1. Sunken background cavity
        Color bgColor = new(0.06f, 0.07f, 0.10f, 1.0f);
        DrawRect(slotRect, bgColor, filled: true);

        // 2. Liquid Charge Fill (Bottom to Top)
        float maxH = size.Y - 4f;
        float chargeRatio = MaxCharges > 0f ? Mathf.Clamp(CurrentCharges / MaxCharges, 0f, 1f) : 0f;
        float liquidH = maxH * chargeRatio;
        float liquidY = (size.Y - 2f) - liquidH;

        Color themeColor = GetFlaskThemeColor(Type);
        Color liquidColor = CanUse ? themeColor : themeColor * 0.4f;
        liquidColor.A = 0.85f;

        if (liquidH > 0f)
        {
            Rect2 liquidRect = new(2f, liquidY, size.X - 4f, liquidH);
            DrawRect(liquidRect, liquidColor, filled: true);

            // Subtle liquid top surface meniscus highlight
            if (CanUse)
            {
                Color highlight = new(1f, 1f, 1f, 0.45f);
                DrawLine(new Vector2(2f, liquidY), new Vector2(size.X - 2f, liquidY), highlight, 1f);
            }
        }

        // 3. Threshold notch (ChargesPerUse)
        float thresholdRatio = MaxCharges > 0f ? Mathf.Clamp(ChargesPerUse / MaxCharges, 0f, 1f) : 0f;
        float thresholdY = (size.Y - 2f) - (maxH * thresholdRatio);
        Color notchColor = new(1f, 1f, 1f, 0.45f);
        DrawLine(new Vector2(1f, thresholdY), new Vector2(size.X - 1f, thresholdY), notchColor, 1f);

        // 4. Border: standard metallic border vs glowing active Auto-Instill border
        if (IsActive)
        {
            // Pulsing golden/amber active glow
            float pulse = 0.65f + 0.35f * Mathf.Sin(_animationTime * 9f);
            Color glowColor = new(1.0f, 0.88f, 0.30f, pulse);
            DrawRect(slotRect, glowColor, filled: false, width: 2f);

            // Active duration remaining swipe indicator at top edge
            float durRatio = DurationSeconds > 0f ? Mathf.Clamp(RemainingDuration / DurationSeconds, 0f, 1f) : 0f;
            if (durRatio > 0f)
            {
                Color durColor = new(0.3f, 1.0f, 0.4f, 0.9f);
                DrawLine(new Vector2(1f, 1.5f), new Vector2(1f + (size.X - 2f) * durRatio, 1.5f), durColor, 2f);
            }
        }
        else
        {
            Color borderColor = CanUse ? new Color(0.25f, 0.30f, 0.40f) : new Color(0.16f, 0.18f, 0.24f);
            DrawRect(slotRect, borderColor, filled: false, width: 1f);
        }

        // 5. Flask abbreviation (top) & Key label (bottom)
        Font font = ThemeDB.FallbackFont;
        int fontSize = 8;

        string badge = GetFlaskAbbreviation(Type);
        Vector2 badgePos = new(size.X / 2f - 4f, 10f);
        DrawString(font, badgePos, badge, HorizontalAlignment.Center, -1, fontSize, new Color(0.85f, 0.85f, 0.90f, 0.85f));

        Vector2 keyPos = new(size.X / 2f - 3f, size.Y - 3f);
        Color keyColor = CanUse ? new Color(0.95f, 0.95f, 0.95f) : new Color(0.50f, 0.50f, 0.55f);
        DrawString(font, keyPos, KeyShortcut, HorizontalAlignment.Center, -1, fontSize, keyColor);
    }

    private void UpdateTooltip()
    {
        string instillText = AutoInstillCondition switch
        {
            FlaskAutoInstillCondition.LifeBelowPercent => $"Auto-Used when Life < {AutoInstillThreshold:F0}%",
            FlaskAutoInstillCondition.OnCooldownReady => "Auto-Used on cooldown ready",
            FlaskAutoInstillCondition.OnCombatStart => "Auto-Used on combat start",
            _ => "Manual use"
        };

        int usesAvailable = ChargesPerUse > 0 ? (int)(CurrentCharges / ChargesPerUse) : 0;
        string activeText = IsActive ? $"\n[ACTIVE: {RemainingDuration:F1}s remaining]" : "";

        TooltipText = $"{FlaskName}\n" +
                      $"Charges: {CurrentCharges:F0} / {MaxCharges:F0} (Uses: {usesAvailable})\n" +
                      $"Consumes: {ChargesPerUse:F0} charges • Duration: {DurationSeconds:F1}s\n" +
                      $"Trigger: {instillText}{activeText}\n" +
                      $"[Click or Press {KeyShortcut} to Drink]";
    }

    public static Color GetFlaskThemeColor(FlaskType type) => type switch
    {
        FlaskType.Life => new Color(0.90f, 0.18f, 0.18f),        // Crimson Red
        FlaskType.Quicksilver => new Color(0.00f, 0.90f, 1.00f), // Cyan Wind
        FlaskType.Granite => new Color(0.60f, 0.65f, 0.72f),     // Slate Stone
        FlaskType.Diamond => new Color(0.88f, 0.97f, 1.00f),     // Diamond Sparkle
        FlaskType.Quartz => new Color(0.78f, 0.45f, 0.85f),      // Lavender Phasing
        _ => new Color(0.6f, 0.6f, 0.6f)
    };

    public static string GetFlaskAbbreviation(FlaskType type) => type switch
    {
        FlaskType.Life => "HP",
        FlaskType.Quicksilver => "QS",
        FlaskType.Granite => "GR",
        FlaskType.Diamond => "DM",
        FlaskType.Quartz => "QZ",
        _ => "FL"
    };
}

/// <summary>
/// Custom Control rendering the Layered Health Bar:
/// Base crimson Life + Luminescent cyan Energy Shield overlay + Shimmering Golden Guard Skill border.
/// </summary>
public partial class LayeredHealthBar : Control
{
    public float CurrentLife { get; private set; } = 100f;
    public float MaxLife { get; private set; } = 100f;
    public float CurrentEnergyShield { get; private set; } = 0f;
    public float MaxEnergyShield { get; private set; } = 0f;

    public bool IsGuardActive { get; private set; } = false;
    public float GuardAbsorbPool { get; private set; } = 0f;
    public float GuardMaxAbsorb { get; private set; } = 0f;
    public string GuardName { get; private set; } = "Steelskin";

    public float LifePercent => MaxLife > 0f ? CurrentLife / MaxLife : 0f;
    public float EsPercent => MaxEnergyShield > 0f ? CurrentEnergyShield / MaxEnergyShield : 0f;

    private float _animationTime = 0f;

    public LayeredHealthBar()
    {
        CustomMinimumSize = new Vector2(100, 16);
        MouseFilter = MouseFilterEnum.Stop;
    }

    public void UpdateHealth(float life, float maxLife, float es = 0f, float maxEs = 0f)
    {
        CurrentLife = MathF.Max(0f, life);
        MaxLife = MathF.Max(1f, maxLife);
        CurrentEnergyShield = MathF.Max(0f, es);
        MaxEnergyShield = MathF.Max(0f, maxEs);

        UpdateTooltip();
        QueueRedraw();
    }

    public void SetGuardSkill(bool isActive, float currentAbsorb = 0f, float maxAbsorb = 0f, string name = "Steelskin")
    {
        IsGuardActive = isActive;
        GuardAbsorbPool = MathF.Max(0f, currentAbsorb);
        GuardMaxAbsorb = MathF.Max(0f, maxAbsorb);
        GuardName = name;

        UpdateTooltip();
        QueueRedraw();
    }

    public override void _Process(double delta)
    {
        _animationTime += (float)delta;
        if (IsGuardActive)
        {
            QueueRedraw();
        }
    }

    public override void _Draw()
    {
        Vector2 size = Size;
        if (size.X <= 0 || size.Y <= 0) return;

        Rect2 barRect = new(Vector2.Zero, size);

        // 1. Dark crimson sunken cavity background
        Color bgColor = new(0.08f, 0.02f, 0.02f, 1.0f);
        DrawRect(barRect, bgColor, filled: true);

        // 2. Base Life Bar (PoE Crimson Red)
        float lifeRatio = Mathf.Clamp(LifePercent, 0f, 1f);
        if (lifeRatio > 0f)
        {
            float lifeW = size.X * lifeRatio;
            Rect2 lifeRect = new(0, 0, lifeW, size.Y);
            Color lifeColor = new(0.72f, 0.18f, 0.18f, 1.0f); // PoE Life Red
            DrawRect(lifeRect, lifeColor, filled: true);

            // Top gloss highlight
            Color gloss = new(1.0f, 0.45f, 0.45f, 0.35f);
            DrawLine(new Vector2(0, 1.5f), new Vector2(lifeW, 1.5f), gloss, 1f);
        }

        // 3. Energy Shield (ES) Layer (Luminescent Cyan Overlay)
        if (MaxEnergyShield > 0f)
        {
            float esRatio = Mathf.Clamp(EsPercent, 0f, 1f);
            if (esRatio > 0f)
            {
                float esW = size.X * esRatio;
                Rect2 esRect = new(0, 0, esW, size.Y);

                // Cyan luminescent translucent wash
                Color esFill = new(0.0f, 0.90f, 1.0f, 0.65f);
                DrawRect(esRect, esFill, filled: true);

                // Sharp cyan upper shine & boundary pip
                Color esShine = new(0.50f, 1.0f, 1.0f, 0.95f);
                DrawLine(new Vector2(0, 1.5f), new Vector2(esW, 1.5f), esShine, 1.5f);
                DrawLine(new Vector2(esW - 1f, 0), new Vector2(esW - 1f, size.Y), esShine, 1.5f);
            }
        }

        // 4. Base Border
        Color borderColor = new(0.25f, 0.10f, 0.10f);
        DrawRect(barRect, borderColor, filled: false, width: 1f);

        // 5. Guard Skill Golden Shimmer (Molten Shell / Steelskin)
        if (IsGuardActive)
        {
            float pulse = 0.65f + 0.35f * Mathf.Sin(_animationTime * 6f);
            Color goldColor = new(1.0f, 0.84f, 0.0f, pulse); // Glowing gold
            DrawRect(barRect.Grow(1f), goldColor, filled: false, width: 2f);

            // Shimmer corner accents
            DrawLine(new Vector2(-1f, -1f), new Vector2(4f, -1f), goldColor, 2f);
            DrawLine(new Vector2(-1f, -1f), new Vector2(-1f, 4f), goldColor, 2f);
            DrawLine(new Vector2(size.X + 1f, size.Y + 1f), new Vector2(size.X - 4f, size.Y + 1f), goldColor, 2f);
            DrawLine(new Vector2(size.X + 1f, size.Y + 1f), new Vector2(size.X + 1f, size.Y - 4f), goldColor, 2f);
        }

        // 6. Value Label Text (crisp PoE HUD numbers with shadow)
        Font font = ThemeDB.FallbackFont;
        int fontSize = 9;

        string text = MaxEnergyShield > 0f
            ? $"{Mathf.RoundToInt(CurrentLife):N0} / {Mathf.RoundToInt(MaxLife):N0} ({Mathf.RoundToInt(CurrentEnergyShield):N0} ES)"
            : $"{Mathf.RoundToInt(CurrentLife):N0} / {Mathf.RoundToInt(MaxLife):N0}";

        if (IsGuardActive && GuardAbsorbPool > 0f)
        {
            text += $" [🛡️ {Mathf.RoundToInt(GuardAbsorbPool)}]";
        }

        Vector2 textSize = font.GetStringSize(text, HorizontalAlignment.Left, -1, fontSize);
        Vector2 textPos = new(MathF.Max(4f, (size.X - textSize.X) / 2f), (size.Y + textSize.Y) / 2f - 2f);

        // Shadow & Foreground
        DrawString(font, textPos + new Vector2(1, 1), text, HorizontalAlignment.Left, -1, fontSize, new Color(0, 0, 0, 0.9f));
        DrawString(font, textPos, text, HorizontalAlignment.Left, -1, fontSize, new Color(1.0f, 0.95f, 0.95f, 1.0f));
    }

    private void UpdateTooltip()
    {
        string guardInfo = IsGuardActive
            ? $"\nGuard Buff: {GuardName} ACTIVE (Absorb Pool: {GuardAbsorbPool:N0} / {GuardMaxAbsorb:N0})"
            : "";

        TooltipText = $"Life: {CurrentLife:N0} / {MaxLife:N0} ({LifePercent * 100f:F1}%)\n" +
                      $"Energy Shield: {CurrentEnergyShield:N0} / {MaxEnergyShield:N0} ({EsPercent * 100f:F1}%){guardInfo}";
    }
}

/// <summary>
/// Custom Control rendering the Mana Bar with Aura Mana Reservation:
/// Usable Mana fill (Classic PoE Blue) + Dark Reserved zone with diagonal hatch pattern and reservation divider.
/// </summary>
public partial class ManaReservationBar : Control
{
    public float CurrentMana { get; private set; } = 50f;
    public float MaxMana { get; private set; } = 50f;
    public float ReservedMana { get; private set; } = 0f;

    public float UnreservedMaxMana => MathF.Max(0f, MaxMana - ReservedMana);
    public float ReservedPercent => MaxMana > 0f ? (ReservedMana / MaxMana) * 100f : 0f;
    public float ManaPercent => UnreservedMaxMana > 0f ? CurrentMana / UnreservedMaxMana : 0f;

    public ManaReservationBar()
    {
        CustomMinimumSize = new Vector2(100, 16);
        MouseFilter = MouseFilterEnum.Stop;
    }

    public void UpdateMana(float currentMana, float maxMana, float reservedMana = 0f)
    {
        MaxMana = MathF.Max(1f, maxMana);
        ReservedMana = Math.Clamp(reservedMana, 0f, MaxMana);
        CurrentMana = Math.Clamp(currentMana, 0f, UnreservedMaxMana);

        UpdateTooltip();
        QueueRedraw();
    }

    public override void _Draw()
    {
        Vector2 size = Size;
        if (size.X <= 0 || size.Y <= 0) return;

        Rect2 barRect = new(Vector2.Zero, size);

        // 1. Dark sapphire sunken cavity background
        Color bgColor = new(0.04f, 0.06f, 0.12f, 1.0f);
        DrawRect(barRect, bgColor, filled: true);

        // 2. Usable Mana Zone vs Reserved Mana Zone
        float reservedRatio = Mathf.Clamp(ReservedPercent / 100f, 0f, 1f);
        float unreservedRatio = 1f - reservedRatio;
        float usableWidth = size.X * unreservedRatio;

        // Draw Usable Mana fill
        float manaFillRatio = Mathf.Clamp(ManaPercent, 0f, 1f);
        float manaW = usableWidth * manaFillRatio;
        if (manaW > 0f)
        {
            Rect2 manaRect = new(0, 0, manaW, size.Y);
            Color manaColor = new(0.12f, 0.53f, 0.90f, 1.0f); // PoE Mana Blue
            DrawRect(manaRect, manaColor, filled: true);

            // Top gloss line
            Color gloss = new(0.50f, 0.80f, 1.0f, 0.35f);
            DrawLine(new Vector2(0, 1.5f), new Vector2(manaW, 1.5f), gloss, 1f);
        }

        // 3. Reserved Mana Zone (Locked by Auras)
        if (reservedRatio > 0f)
        {
            float reservedStartX = usableWidth;
            float reservedWidth = size.X - reservedStartX;
            Rect2 reservedRect = new(reservedStartX, 0, reservedWidth, size.Y);

            // Dark navy locked reservoir
            Color reservedColor = new(0.05f, 0.11f, 0.25f, 0.92f);
            DrawRect(reservedRect, reservedColor, filled: true);

            // Diagonal hatch stripes (PoE Mana Reservation standard)
            Color hatchColor = new(0.15f, 0.28f, 0.55f, 0.55f);
            float step = 6f;
            for (float x = reservedStartX - size.Y; x < size.X; x += step)
            {
                Vector2 start = new(MathF.Max(reservedStartX, x), size.Y);
                Vector2 end = new(x + size.Y, 0);

                if (end.X > reservedStartX && start.X < size.X)
                {
                    DrawLine(start, end, hatchColor, 1f);
                }
            }

            // Sharp reservation separator divider line
            Color dividerColor = new(0.40f, 0.70f, 1.0f, 0.95f);
            DrawLine(new Vector2(reservedStartX, 0), new Vector2(reservedStartX, size.Y), dividerColor, 1.5f);
        }

        // 4. Base Border
        Color borderColor = new(0.12f, 0.18f, 0.32f);
        DrawRect(barRect, borderColor, filled: false, width: 1f);

        // 5. Value Label Text (crisp PoE HUD numbers with shadow)
        Font font = ThemeDB.FallbackFont;
        int fontSize = 9;

        string text = ReservedMana > 0f
            ? $"{Mathf.RoundToInt(CurrentMana):N0} / {Mathf.RoundToInt(MaxMana):N0} ({Mathf.RoundToInt(ReservedPercent)}% R)"
            : $"{Mathf.RoundToInt(CurrentMana):N0} / {Mathf.RoundToInt(MaxMana):N0}";

        Vector2 textSize = font.GetStringSize(text, HorizontalAlignment.Left, -1, fontSize);
        Vector2 textPos = new(MathF.Max(4f, (size.X - textSize.X) / 2f), (size.Y + textSize.Y) / 2f - 2f);

        // Shadow & Foreground
        DrawString(font, textPos + new Vector2(1, 1), text, HorizontalAlignment.Left, -1, fontSize, new Color(0, 0, 0, 0.9f));
        DrawString(font, textPos, text, HorizontalAlignment.Left, -1, fontSize, new Color(0.90f, 0.95f, 1.0f, 1.0f));
    }

    private void UpdateTooltip()
    {
        TooltipText = $"Mana: {CurrentMana:N0} / {UnreservedMaxMana:N0} (Unreserved Max)\n" +
                      $"Total Pool: {MaxMana:N0}\n" +
                      $"Reserved: {ReservedMana:N0} ({ReservedPercent:F1}%)";
    }
}

/// <summary>
/// Controller for Task 2.4: Player Vitometer (Vitality & Defensive Layers).
/// Manages the Layered Health Bar (Life + ES + Guard Skill), Mana & Reservation Bar,
/// and the 5-Slot Quick Flask Rack with Auto-Instill charge tracking.
/// </summary>
public partial class VitometerView : PanelContainer
{
    // --- Subcomponent Controls ---
    public LayeredHealthBar HealthBar { get; } = new();
    public ManaReservationBar ManaBar { get; } = new();
    public List<FlaskSlotView> FlaskSlots { get; } = new();

    public Label? DefensesSummaryLabel { get; private set; }
    public Label? UnreservedManaLabel { get; private set; }

    // --- References to Scene-Bound Nodes (for compatibility) ---
    private ProgressBar? _sceneLifeEsBar;
    private ProgressBar? _sceneManaBar;
    private HBoxContainer? _sceneFlasksContainer;

    // --- Bound Core Models ---
    public PlayerState? BoundPlayer { get; private set; }
    public SkillLoadout? BoundLoadout { get; private set; }

    public VitometerView()
    {
        InitializeDefaultFlaskSlots();
    }

    public override void _Notification(int what)
    {
        if (what == NotificationPredelete)
        {
            if (GodotObject.IsInstanceValid(HealthBar) && HealthBar.GetParent() == null)
            {
                HealthBar.Free();
            }

            if (GodotObject.IsInstanceValid(ManaBar) && ManaBar.GetParent() == null)
            {
                ManaBar.Free();
            }

            foreach (var slot in FlaskSlots)
            {
                if (GodotObject.IsInstanceValid(slot) && slot.GetParent() == null)
                {
                    slot.Free();
                }
            }
        }
    }

    public override void _Ready()
    {
        BindSceneNodes();
        SetupSubcomponents();
    }

    private void InitializeDefaultFlaskSlots()
    {
        if (FlaskSlots.Count == 5) return;

        FlaskSlots.Clear();
        FlaskSlots.Add(new FlaskSlotView(FlaskType.Life, "Divine Life Flask", 60f, 20f, 4.0f, "1"));
        FlaskSlots.Add(new FlaskSlotView(FlaskType.Quicksilver, "Quicksilver Flask", 60f, 20f, 5.0f, "2"));
        FlaskSlots.Add(new FlaskSlotView(FlaskType.Granite, "Granite Flask", 60f, 30f, 4.0f, "3"));
        FlaskSlots.Add(new FlaskSlotView(FlaskType.Diamond, "Diamond Flask", 60f, 20f, 4.0f, "4"));
        FlaskSlots.Add(new FlaskSlotView(FlaskType.Quartz, "Quartz Flask", 60f, 30f, 4.0f, "5"));

        for (int i = 0; i < FlaskSlots.Count; i++)
        {
            FlaskSlots[i].SlotIndex = i;
        }
    }

    private void BindSceneNodes()
    {
        _sceneLifeEsBar = GetNodeOrNull<ProgressBar>("VitometerMargin/VitometerHBox/HealthVBox/LifeESBar")
            ?? Owner?.GetNodeOrNull<ProgressBar>("%LifeESBar")
            ?? GetNodeOrNull<ProgressBar>("%LifeESBar");

        _sceneManaBar = GetNodeOrNull<ProgressBar>("VitometerMargin/VitometerHBox/ManaVBox/ManaBar")
            ?? Owner?.GetNodeOrNull<ProgressBar>("%ManaBar")
            ?? GetNodeOrNull<ProgressBar>("%ManaBar");

        _sceneFlasksContainer = GetNodeOrNull<HBoxContainer>("VitometerMargin/VitometerHBox/FlasksContainer")
            ?? Owner?.GetNodeOrNull<HBoxContainer>("%FlasksContainer")
            ?? GetNodeOrNull<HBoxContainer>("%FlasksContainer");

        DefensesSummaryLabel = GetNodeOrNull<Label>("VitometerMargin/VitometerHBox/HealthVBox/DefensesSummaryLabel")
            ?? Owner?.GetNodeOrNull<Label>("%DefensesSummaryLabel")
            ?? GetNodeOrNull<Label>("%DefensesSummaryLabel");

        UnreservedManaLabel = GetNodeOrNull<Label>("VitometerMargin/VitometerHBox/ManaVBox/UnreservedManaLabel")
            ?? Owner?.GetNodeOrNull<Label>("%UnreservedManaLabel")
            ?? GetNodeOrNull<Label>("%UnreservedManaLabel");
    }

    private void SetupSubcomponents()
    {
        // 1. Attach LayeredHealthBar as overlay child to _sceneLifeEsBar if present
        if (_sceneLifeEsBar != null && HealthBar.GetParent() == null)
        {
            _sceneLifeEsBar.AddChild(HealthBar);
            HealthBar.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        }

        // 2. Attach ManaReservationBar as overlay child to _sceneManaBar if present
        if (_sceneManaBar != null && ManaBar.GetParent() == null)
        {
            _sceneManaBar.AddChild(ManaBar);
            ManaBar.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        }

        // 3. Attach FlaskSlotViews into _sceneFlasksContainer if present
        if (_sceneFlasksContainer != null)
        {
            for (int i = 0; i < FlaskSlots.Count; i++)
            {
                var slotView = FlaskSlots[i];
                var sceneSlot = _sceneFlasksContainer.GetNodeOrNull<Control>($"FlaskSlot{i + 1}");
                if (sceneSlot != null && slotView.GetParent() == null)
                {
                    sceneSlot.AddChild(slotView);
                    slotView.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
                }
            }
        }
    }

    /// <summary>
    /// Binds the Vitometer to Core simulation models for seamless reactive telemetry.
    /// </summary>
    public void Bind(PlayerState player, SkillLoadout loadout)
    {
        BoundPlayer = player;
        BoundLoadout = loadout;

        // 1. Subscribe to PlayerState events
        player.OnResourceChanged += OnPlayerResourceChanged;
        player.OnPlayerDied += OnPlayerDied;

        // 2. Subscribe to GuardSkill events
        loadout.GuardSkill.OnGuardActivated += OnGuardActivated;
        loadout.GuardSkill.OnGuardExpired += OnGuardExpired;

        // 3. Bind all Flask slots
        for (int i = 0; i < Math.Min(FlaskSlots.Count, loadout.Flasks.Count); i++)
        {
            FlaskSlots[i].Bind(loadout.Flasks[i], player);
        }

        // 4. Initial state sync
        UpdateVitals(player.CurrentLife, player.MaxLife, player.CurrentEnergyShield, player.MaxEnergyShield, player.CurrentMana, player.MaxMana, player.ReservedMana);
        SetGuardSkill(loadout.GuardSkill.IsActive, loadout.GuardSkill.CurrentAbsorbPool, loadout.GuardSkill.MaxAbsorbPool, loadout.GuardSkill.Name);
    }

    private void OnPlayerResourceChanged(float life, float es, float mana)
    {
        if (BoundPlayer != null)
        {
            UpdateVitals(life, BoundPlayer.MaxLife, es, BoundPlayer.MaxEnergyShield, mana, BoundPlayer.MaxMana, BoundPlayer.ReservedMana);
        }
    }

    private void OnPlayerDied()
    {
        SetGuardSkill(false, 0f, 0f);
    }

    private void OnGuardActivated()
    {
        if (BoundLoadout != null)
        {
            SetGuardSkill(true, BoundLoadout.GuardSkill.CurrentAbsorbPool, BoundLoadout.GuardSkill.MaxAbsorbPool, BoundLoadout.GuardSkill.Name);
        }
    }

    private void OnGuardExpired()
    {
        SetGuardSkill(false, 0f, 0f);
    }

    /// <summary>
    /// Updates all vitals telemetry: Life, Energy Shield, Mana, and Reservation.
    /// </summary>
    public void UpdateVitals(
        float currentLife,
        float maxLife,
        float currentEs,
        float maxEs,
        float currentMana,
        float maxMana,
        float reservedMana = 0f,
        bool isGuardActive = false,
        float guardAbsorb = 0f,
        float guardMax = 0f)
    {
        // 1. Health Bar
        HealthBar.UpdateHealth(currentLife, maxLife, currentEs, maxEs);
        if (isGuardActive)
        {
            HealthBar.SetGuardSkill(true, guardAbsorb, guardMax);
        }

        // Backward compatibility with raw ProgressBar
        if (_sceneLifeEsBar != null && maxLife > 0)
        {
            _sceneLifeEsBar.MaxValue = maxLife;
            _sceneLifeEsBar.Value = Mathf.Clamp(currentLife, 0, maxLife);
        }

        // 2. Mana Bar & Reservation
        ManaBar.UpdateMana(currentMana, maxMana, reservedMana);

        if (_sceneManaBar != null && maxMana > 0)
        {
            _sceneManaBar.MaxValue = maxMana;
            _sceneManaBar.Value = Mathf.Clamp(currentMana, 0, maxMana);
        }

        // 3. Unreserved Mana Label
        if (UnreservedManaLabel != null)
        {
            float unreserved = MathF.Max(0f, maxMana - reservedMana);
            float reservedPct = maxMana > 0f ? (reservedMana / maxMana) * 100f : 0f;
            string resText = reservedPct > 0 ? $" ({Mathf.RoundToInt(reservedPct)}% R)" : "";
            UnreservedManaLabel.Text = $"Mana: {Mathf.RoundToInt(currentMana)}/{Mathf.RoundToInt(unreserved)}{resText}";
        }
    }

    /// <summary>
    /// Sets the status of the Guard Skill golden layer.
    /// </summary>
    public void SetGuardSkill(bool isActive, float currentAbsorb = 0f, float maxAbsorb = 0f, string name = "Steelskin")
    {
        HealthBar.SetGuardSkill(isActive, currentAbsorb, maxAbsorb, name);
    }

    /// <summary>
    /// Updates defenses summary label (Armour, Block, EHP).
    /// </summary>
    public void SetDefensesSummary(string summary)
    {
        if (DefensesSummaryLabel != null)
        {
            DefensesSummaryLabel.Text = summary;
        }
    }

    /// <summary>
    /// Updates a specific flask slot's charges and active duration.
    /// </summary>
    public void UpdateFlaskSlot(int slotIndex, float currentCharges, float maxCharges, bool isActive, float remainingDuration)
    {
        if (slotIndex >= 0 && slotIndex < FlaskSlots.Count)
        {
            FlaskSlots[slotIndex].UpdateState(currentCharges, maxCharges, isActive, remainingDuration);
        }
    }

    /// <summary>
    /// Updates all 5 flask slots from a collection.
    /// </summary>
    public void UpdateAllFlasks(IReadOnlyList<FlaskSlot> flasks)
    {
        for (int i = 0; i < Math.Min(FlaskSlots.Count, flasks.Count); i++)
        {
            var slot = flasks[i];
            FlaskSlots[i].UpdateState(slot.CurrentCharges, slot.MaxCharges, slot.IsActive, slot.RemainingDuration);
        }
    }

    /// <summary>
    /// Attempts to drink the flask in the specified slot index (0 to 4).
    /// </summary>
    public bool TryDrinkFlask(int slotIndex)
    {
        if (slotIndex >= 0 && slotIndex < FlaskSlots.Count)
        {
            return FlaskSlots[slotIndex].TryDrink();
        }
        return false;
    }

    public override void _UnhandledKeyInput(InputEvent @event)
    {
        if (@event is InputEventKey keyEvent && keyEvent.Pressed && !keyEvent.Echo)
        {
            switch (keyEvent.Keycode)
            {
                case Key.Key1:
                    if (TryDrinkFlask(0)) GetViewport()?.SetInputAsHandled();
                    break;
                case Key.Key2:
                    if (TryDrinkFlask(1)) GetViewport()?.SetInputAsHandled();
                    break;
                case Key.Key3:
                    if (TryDrinkFlask(2)) GetViewport()?.SetInputAsHandled();
                    break;
                case Key.Key4:
                    if (TryDrinkFlask(3)) GetViewport()?.SetInputAsHandled();
                    break;
                case Key.Key5:
                    if (TryDrinkFlask(4)) GetViewport()?.SetInputAsHandled();
                    break;
            }
        }
    }
}
