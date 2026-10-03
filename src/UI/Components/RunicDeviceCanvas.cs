using System;
using Godot;

namespace IdleExile.UI.Components;

/// <summary>
/// Custom procedural 2D canvas that renders the ancient Crucible Map Device
/// runic circle, celestial alignment glyphs, and swirling portal rift effects.
/// Follows gl_compatibility and low-processor guidelines (zero texture overhead).
/// </summary>
public partial class RunicDeviceCanvas : Control
{
    private float _rotationAngle = 0f;
    private float _pulsePhase = 0f;

    /// <summary>True when a portal to an active map run is currently open.</summary>
    public bool IsPortalActive { get; set; } = false;

    /// <summary>True when golden pulse guidance is drawing attention to the device.</summary>
    public bool IsGoldenPulse { get; set; } = false;

    // Palette tokens
    private static readonly Color ColorBaseRing = new(0.20f, 0.22f, 0.28f, 0.55f);
    private static readonly Color ColorGoldRing = new(0.85f, 0.65f, 0.25f, 0.85f);
    private static readonly Color ColorAzureRing = new(0.22f, 0.82f, 0.95f, 0.90f);
    private static readonly Color ColorRuneTick = new(0.70f, 0.72f, 0.80f, 0.60f);
    private static readonly Color ColorAltarFill = new(0.06f, 0.065f, 0.09f, 0.70f);

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
    }

    public override void _Process(double delta)
    {
        if (!IsVisibleInTree()) return;

        // Smooth slow celestial rotation (approx 1 full rotation per 20 seconds)
        _rotationAngle += (float)delta * 0.35f;
        if (_rotationAngle > Mathf.Tau) _rotationAngle -= Mathf.Tau;

        if (IsGoldenPulse || IsPortalActive)
        {
            _pulsePhase += (float)delta * 3.0f;
            if (_pulsePhase > Mathf.Tau) _pulsePhase -= Mathf.Tau;
        }

        QueueRedraw();
    }

    public override void _Draw()
    {
        Vector2 center = Size * 0.5f;
        float baseRadius = MathF.Min(Size.X, Size.Y) * 0.46f;
        if (baseRadius <= 10f) return;

        // 1. Draw Altar Backdrop Glow
        DrawCircle(center, baseRadius * 1.05f, ColorAltarFill);

        // 2. Draw Outer Subtle Celestial Ring
        DrawArc(center, baseRadius, 0f, Mathf.Tau, 48, ColorBaseRing, 1.5f, antialiased: true);

        // 3. Draw 16 Rotating Runic Tick Marks around the outer rim
        int tickCount = 16;
        float angleStep = Mathf.Tau / tickCount;
        for (int i = 0; i < tickCount; i++)
        {
            float a = _rotationAngle + (i * angleStep);
            Vector2 innerPoint = center + new Vector2(MathF.Cos(a), MathF.Sin(a)) * (baseRadius - 5f);
            Vector2 outerPoint = center + new Vector2(MathF.Cos(a), MathF.Sin(a)) * (baseRadius + (i % 4 == 0 ? 5f : 2f));
            Color tickCol = (i % 4 == 0) ? (IsPortalActive ? ColorAzureRing : ColorGoldRing) : ColorRuneTick;
            DrawLine(innerPoint, outerPoint, tickCol, (i % 4 == 0) ? 2.0f : 1.0f, antialiased: true);
        }

        // 4. Draw Middle Concentric Ring (Gold or Azure Portal)
        float midRadius = baseRadius * 0.74f;
        Color activeRingCol = IsPortalActive ? ColorAzureRing : ColorGoldRing;
        float ringWidth = IsPortalActive ? 2.2f : 1.5f;
        DrawArc(center, midRadius, 0f, Mathf.Tau, 40, activeRingCol, ringWidth, antialiased: true);

        // 5. Draw 4 Cardinal Crucible Brackets (Cross-hair runes)
        for (int i = 0; i < 4; i++)
        {
            float a = (i * Mathf.Pi * 0.5f) - _rotationAngle * 0.5f;
            Vector2 bracketInner = center + new Vector2(MathF.Cos(a), MathF.Sin(a)) * (midRadius - 6f);
            Vector2 bracketOuter = center + new Vector2(MathF.Cos(a), MathF.Sin(a)) * (midRadius + 6f);
            DrawLine(bracketInner, bracketOuter, activeRingCol, 2.0f, antialiased: true);
        }

        // 6. Portal Vortex Effect: Swirling spiral arms when map portal is active
        if (IsPortalActive)
        {
            int arms = 3;
            float pulseScale = 1.0f + MathF.Sin(_pulsePhase) * 0.06f;
            for (int arm = 0; arm < arms; arm++)
            {
                float baseArmAngle = (arm * Mathf.Tau / arms) + (_rotationAngle * 1.5f);
                Vector2 prevPt = center;
                for (int step = 1; step <= 8; step++)
                {
                    float t = step / 8f;
                    float armAngle = baseArmAngle + (t * 1.2f);
                    float r = midRadius * t * pulseScale;
                    Vector2 nextPt = center + new Vector2(MathF.Cos(armAngle), MathF.Sin(armAngle)) * r;

                    float alpha = (1f - t) * 0.55f;
                    DrawLine(prevPt, nextPt, new Color(0.2f, 0.85f, 1.0f, alpha), 1.8f, antialiased: true);
                    prevPt = nextPt;
                }
            }
        }

        // 7. Golden Guidance Pulse Ring
        if (IsGoldenPulse && !IsPortalActive)
        {
            float pulseAlpha = 0.35f + (MathF.Sin(_pulsePhase) * 0.35f);
            float pulseRadius = midRadius + (MathF.Sin(_pulsePhase) * 4f);
            DrawArc(center, pulseRadius, 0f, Mathf.Tau, 36, new Color(1.0f, 0.85f, 0.35f, pulseAlpha), 2.5f, antialiased: true);
        }
    }
}
