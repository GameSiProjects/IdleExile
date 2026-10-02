using System;
using Godot;

namespace IdleExile.UI;

public enum WindowResolutionMode
{
    Compact,
    Expanded,
    Wide
}

/// <summary>
/// WindowController.cs — Manages dynamic companion window resizing,
/// taskbar docking, multi-monitor movement, and borderless dragging.
/// Inspired by the reference desktop overlay architecture.
/// </summary>
public partial class WindowController : Node
{
    [Signal]
    public delegate void WindowResizedEventHandler(Vector2I newSize, bool isExpanded);

    [Signal]
    public delegate void WindowModeChangedEventHandler(int mode);

    public static readonly Vector2I CompactWindowSize = new(440, 380);
    public static readonly Vector2I ExpandedWindowSize = new(440, 780);
    public static readonly Vector2I WideWindowSize = new(800, 780);

    [Export]
    public Vector2I WindowSize { get; set; } = ExpandedWindowSize;

    [Export]
    public bool Borderless { get; set; } = false;

    [Export]
    public bool DefaultAlwaysOnTop { get; set; } = true;

    [Export]
    public WindowDockPosition DefaultDock { get; set; } = WindowDockPosition.BottomRight;

    public WindowResolutionMode CurrentMode { get; private set; } = WindowResolutionMode.Expanded;
    public bool IsExpanded => CurrentMode != WindowResolutionMode.Compact;

    private readonly DesktopWindow _desktopWindow = new();
    public DesktopWindow DesktopWindow => _desktopWindow;

    public bool IsDragging { get; private set; } = false;
    public Vector2I DragOffset { get; private set; } = Vector2I.Zero;

    public void Setup(Window window)
    {
        if (window != null)
        {
            _desktopWindow.SetupWindow(
                window,
                WindowSize,
                borderless: Borderless,
                alwaysOnTop: DefaultAlwaysOnTop,
                resizable: true
            );

            // Activate Low Processor Usage Mode to consume < 1% CPU when idle in background
            OS.LowProcessorUsageMode = true;
            OS.LowProcessorUsageModeSleepUsec = 6900;
        }
    }

    public void ToggleExpand(bool keepBottom = true)
    {
        if (IsExpanded)
        {
            ShrinkWindow(keepBottom);
        }
        else
        {
            ExpandWindow(keepBottom);
        }
    }

    public void ExpandWindow(bool keepBottom = true)
    {
        SetResolutionMode(WindowResolutionMode.Expanded, keepBottom);
    }

    public void ShrinkWindow(bool keepBottom = true)
    {
        SetResolutionMode(WindowResolutionMode.Compact, keepBottom);
    }

    public void SetResolutionMode(WindowResolutionMode mode, bool keepBottom = true)
    {
        CurrentMode = mode;
        Vector2I targetSize = mode switch
        {
            WindowResolutionMode.Compact => CompactWindowSize,
            WindowResolutionMode.Wide => WideWindowSize,
            _ => ExpandedWindowSize
        };

        SetWindowResolution(targetSize, keepBottom);
        EmitSignal(SignalName.WindowModeChanged, (int)mode);
    }

    public void CycleResolution(bool keepBottom = true)
    {
        WindowResolutionMode nextMode = CurrentMode switch
        {
            WindowResolutionMode.Compact => WindowResolutionMode.Expanded,
            WindowResolutionMode.Expanded => WindowResolutionMode.Wide,
            _ => WindowResolutionMode.Compact
        };

        SetResolutionMode(nextMode, keepBottom);
    }

    public void SetWindowResolution(Vector2I newSize, bool keepBottom = true)
    {
        WindowSize = newSize;
        _desktopWindow.SetWindowSize(newSize, keepBottom);
        EmitSignal(SignalName.WindowResized, newSize, IsExpanded);
    }

    public void PositionAboveTaskbar(int screen = -1, WindowDockPosition? dock = null)
    {
        _desktopWindow.PositionAboveTaskbar(screen, dock ?? DefaultDock);
    }

    public void MoveToNextScreen()
    {
        _desktopWindow.MoveToNextScreen(DefaultDock);
    }

    public void ToggleAlwaysOnTop(bool isEnabled)
    {
        _desktopWindow.SetAlwaysOnTop(isEnabled);
    }

    public Godot.Collections.Dictionary GetTaskbarInfo()
    {
        return _desktopWindow.GetTaskbarInfo();
    }

    public int GetScreenCount()
    {
        return _desktopWindow.GetScreenCount();
    }

    /// <summary>
    /// Processes mouse input for smooth window dragging across screens when borderless or dragging from titlebar.
    /// </summary>
    public bool HandleInput(InputEvent @event, Func<Vector2, bool>? isPointerOverInteractive = null)
    {
        if (!Borderless)
        {
            return false;
        }

        if (@event is InputEventMouseButton mouseButton && mouseButton.ButtonIndex == MouseButton.Left)
        {
            if (mouseButton.Pressed)
            {
                if (isPointerOverInteractive != null && isPointerOverInteractive(mouseButton.Position))
                {
                    return false;
                }

                IsDragging = true;
                DragOffset = DisplayServer.MouseGetPosition() - DisplayServer.WindowGetPosition();
                return true;
            }
            else
            {
                IsDragging = false;
                return true;
            }
        }
        else if (@event is InputEventMouseMotion && IsDragging)
        {
            Vector2I newPos = DisplayServer.MouseGetPosition() - DragOffset;
            if (newPos != DisplayServer.WindowGetPosition())
            {
                DisplayServer.WindowSetPosition(newPos);
            }
            return true;
        }

        return false;
    }

    public override void _Process(double delta)
    {
        if (IsDragging && !Input.IsMouseButtonPressed(MouseButton.Left))
        {
            IsDragging = false;
        }
    }
}
