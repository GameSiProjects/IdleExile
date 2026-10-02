using System;
using System.Runtime.InteropServices;
using Godot;

namespace IdleExile.UI;

public enum WindowDockPosition
{
    BottomRight,
    BottomCenter,
    BottomLeft
}

/// <summary>
/// Windows desktop overlay bridge for Idle Exile.
/// Manages Godot DisplayServer window positioning above taskbar,
/// multi-monitor cycling, dynamic sizing with bottom-invariance,
/// and native Win32 P/Invoke declarations.
/// </summary>
public class DesktopWindow
{
    private Window? _window;
    private Vector2I _windowSize;
    private int _currentScreen = 0;

    public Vector2I WindowSize => _windowSize;

    /// <summary>
    /// Configure the game window for companion desktop overlay behavior.
    /// </summary>
    public void SetupWindow(Window window, Vector2I size, bool borderless = false, bool alwaysOnTop = true, bool resizable = true)
    {
        _window = window;
        _windowSize = size;

        if (_window == null)
        {
            return;
        }

        _window.Borderless = borderless;
        _window.AlwaysOnTop = alwaysOnTop;
        _window.Unresizable = !resizable;
        _window.MinSize = new Vector2I(400, 340);
        _window.Size = _windowSize;
    }

    /// <summary>
    /// Set the window size dynamically. If keepBottom is true, adjusts the Y position
    /// so that the bottom edge on the desktop remains invariant (e.g. docked above taskbar).
    /// </summary>
    public void SetWindowSize(Vector2I newSize, bool keepBottom = true)
    {
        if (_windowSize == newSize)
        {
            return;
        }

        Vector2I currentSize = _window != null ? _window.Size : _windowSize;
        int deltaY = newSize.Y - currentSize.Y;
        _windowSize = newSize;

        if (_window != null)
        {
            _window.Size = newSize;
        }

        if (keepBottom)
        {
            Vector2I currentPos = DisplayServer.WindowGetPosition();
            DisplayServer.WindowSetPosition(new Vector2I(currentPos.X, currentPos.Y - deltaY));
        }
    }

    /// <summary>
    /// Position the window flush against the bottom of the usable work area (just above the Windows taskbar).
    /// Supports BottomRight (default companion dock), BottomCenter, and BottomLeft.
    /// </summary>
    public void PositionAboveTaskbar(int screen = -1, WindowDockPosition dock = WindowDockPosition.BottomRight)
    {
        if (screen < 0)
        {
            screen = DisplayServer.WindowGetCurrentScreen();
        }

        _currentScreen = screen;

        Rect2I usableRect = DisplayServer.ScreenGetUsableRect(screen);

        int margin = 12;
        int x = dock switch
        {
            WindowDockPosition.BottomRight => usableRect.Position.X + usableRect.Size.X - _windowSize.X - margin,
            WindowDockPosition.BottomLeft => usableRect.Position.X + margin,
            _ => usableRect.Position.X + (usableRect.Size.X - _windowSize.X) / 2
        };

        int y = usableRect.Position.Y + usableRect.Size.Y - _windowSize.Y;

        DisplayServer.WindowSetPosition(new Vector2I(x, y));
    }

    /// <summary>
    /// Toggle always on top flag via DisplayServer.
    /// </summary>
    public void SetAlwaysOnTop(bool enabled)
    {
        if (_window != null)
        {
            _window.AlwaysOnTop = enabled;
        }
        DisplayServer.WindowSetFlag(DisplayServer.WindowFlags.AlwaysOnTop, enabled);

        if (OperatingSystem.IsWindows())
        {
            try
            {
                IntPtr hWnd = Win32.GetForegroundWindow();
                if (hWnd != IntPtr.Zero)
                {
                    Win32.SetAlwaysOnTop(hWnd, enabled);
                }
            }
            catch (Exception ex)
            {
                GD.PrintErr($"[DesktopWindow] Win32 SetAlwaysOnTop error: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Make the entire window click-through via DisplayServer flag.
    /// </summary>
    public void SetClickThrough(bool enabled)
    {
        DisplayServer.WindowSetFlag(
            DisplayServer.WindowFlags.MousePassthrough,
            enabled
        );

        if (OperatingSystem.IsWindows())
        {
            try
            {
                IntPtr hWnd = Win32.GetForegroundWindow();
                if (hWnd != IntPtr.Zero)
                {
                    Win32.SetClickThrough(hWnd, enabled);
                }
            }
            catch (Exception ex)
            {
                GD.PrintErr($"[DesktopWindow] Win32 SetClickThrough error: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Define a polygon region where the window captures mouse input.
    /// Everything outside this polygon is click-through to the desktop.
    /// </summary>
    public void SetInteractiveRegion(Vector2[] polygon)
    {
        if (_window != null)
        {
            _window.MousePassthroughPolygon = polygon;
        }
    }

    /// <summary>
    /// Build a rectangular passthrough polygon from a Rect2.
    /// </summary>
    public static Vector2[] RectToPolygon(Rect2 rect)
    {
        return new Vector2[]
        {
            rect.Position,
            new Vector2(rect.End.X, rect.Position.Y),
            rect.End,
            new Vector2(rect.Position.X, rect.End.Y)
        };
    }

    /// <summary>
    /// Returns total number of connected monitors.
    /// </summary>
    public int GetScreenCount()
    {
        return DisplayServer.GetScreenCount();
    }

    /// <summary>
    /// Cycle the overlay window to the next connected monitor.
    /// </summary>
    public void MoveToNextScreen(WindowDockPosition dock = WindowDockPosition.BottomRight)
    {
        int count = GetScreenCount();
        if (count <= 1)
        {
            return;
        }

        _currentScreen = (DisplayServer.WindowGetCurrentScreen() + 1) % count;
        PositionAboveTaskbar(_currentScreen, dock);
    }

    /// <summary>
    /// Estimate taskbar position and size by comparing screen rect to usable rect.
    /// </summary>
    public Godot.Collections.Dictionary GetTaskbarInfo(int screen = -1)
    {
        if (screen < 0)
        {
            screen = DisplayServer.WindowGetCurrentScreen();
        }

        Vector2I fullRect = DisplayServer.ScreenGetSize(screen);
        Vector2I screenPos = DisplayServer.ScreenGetPosition(screen);
        Rect2I usableRect = DisplayServer.ScreenGetUsableRect(screen);

        var info = new Godot.Collections.Dictionary
        {
            { "screen", screen },
            { "edge", "bottom" },
            { "position", Vector2I.Zero },
            { "size", Vector2I.Zero }
        };

        int topGap = usableRect.Position.Y - screenPos.Y;
        int bottomGap = (screenPos.Y + fullRect.Y) - (usableRect.Position.Y + usableRect.Size.Y);
        int leftGap = usableRect.Position.X - screenPos.X;
        int rightGap = (screenPos.X + fullRect.X) - (usableRect.Position.X + usableRect.Size.X);

        int maxGap = Math.Max(topGap, Math.Max(bottomGap, Math.Max(leftGap, rightGap)));

        if (maxGap == bottomGap)
        {
            info["edge"] = "bottom";
            info["position"] = new Vector2I(screenPos.X, usableRect.Position.Y + usableRect.Size.Y);
            info["size"] = new Vector2I(fullRect.X, bottomGap);
        }
        else if (maxGap == topGap)
        {
            info["edge"] = "top";
            info["position"] = new Vector2I(screenPos.X, screenPos.Y);
            info["size"] = new Vector2I(fullRect.X, topGap);
        }
        else if (maxGap == leftGap)
        {
            info["edge"] = "left";
            info["position"] = new Vector2I(screenPos.X, screenPos.Y);
            info["size"] = new Vector2I(leftGap, fullRect.Y);
        }
        else
        {
            info["edge"] = "right";
            info["position"] = new Vector2I(usableRect.Position.X + usableRect.Size.X, screenPos.Y);
            info["size"] = new Vector2I(rightGap, fullRect.Y);
        }

        return info;
    }

    // ─── Win32 Native P/Invoke Integration ───────────────────────────────

    public static class Win32
    {
        public const int GWL_EXSTYLE = -20;

        public const uint WS_EX_LAYERED = 0x00080000;
        public const uint WS_EX_TRANSPARENT = 0x00000020;
        public const uint WS_EX_TOOLWINDOW = 0x00000080;
        public const uint WS_EX_NOACTIVATE = 0x08000000;

        public static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        public static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);

        public const uint SWP_NOMOVE = 0x0002;
        public const uint SWP_NOSIZE = 0x0001;
        public const uint SWP_NOACTIVATE = 0x0010;

        public const int WM_NCHITTEST = 0x0084;
        public const int HTTRANSPARENT = -1;
        public const int HTCLIENT = 1;

        public const uint ABM_GETTASKBARPOS = 0x00000005;

        public const uint ABE_LEFT = 0;
        public const uint ABE_TOP = 1;
        public const uint ABE_RIGHT = 2;
        public const uint ABE_BOTTOM = 3;

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;

            public int Width => Right - Left;
            public int Height => Bottom - Top;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct APPBARDATA
        {
            public uint cbSize;
            public IntPtr hWnd;
            public uint uCallbackMessage;
            public uint uEdge;
            public RECT rc;
            public int lParam;
        }

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetWindowPos(
            IntPtr hWnd,
            IntPtr hWndInsertAfter,
            int X, int Y,
            int cx, int cy,
            uint uFlags
        );

        [DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("shell32.dll")]
        public static extern uint SHAppBarMessage(uint dwMessage, ref APPBARDATA pData);

        public static void SetClickThrough(IntPtr hWnd, bool enabled)
        {
            if (!OperatingSystem.IsWindows()) return;

            IntPtr exStyle = GetWindowLongPtr(hWnd, GWL_EXSTYLE);
            uint style = (uint)exStyle.ToInt64();

            if (enabled)
            {
                style |= WS_EX_TRANSPARENT | WS_EX_LAYERED;
            }
            else
            {
                style &= ~WS_EX_TRANSPARENT;
            }

            SetWindowLongPtr(hWnd, GWL_EXSTYLE, new IntPtr((long)style));
        }

        public static void SetAlwaysOnTop(IntPtr hWnd, bool enabled)
        {
            if (!OperatingSystem.IsWindows()) return;

            IntPtr insertAfter = enabled ? HWND_TOPMOST : HWND_NOTOPMOST;
            SetWindowPos(
                hWnd, insertAfter,
                0, 0, 0, 0,
                SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE
            );
        }

        public static (uint edge, RECT rect) GetTaskbarPosition()
        {
            if (!OperatingSystem.IsWindows()) return (ABE_BOTTOM, new RECT());

            APPBARDATA abd = new APPBARDATA();
            abd.cbSize = (uint)Marshal.SizeOf(typeof(APPBARDATA));
            SHAppBarMessage(ABM_GETTASKBARPOS, ref abd);
            return (abd.uEdge, abd.rc);
        }
    }
}
