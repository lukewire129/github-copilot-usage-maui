using System.Runtime.InteropServices;
using copilot_usage_maui.Platforms.Windows.NativeWindowing;
using copilot_usage_maui.Services;

namespace copilot_usage_maui.Platforms.Windows;

public class TrayService : ITrayService
{
    WindowsTrayIcon tray;

    public Action? ClickHandler { get; set; }

    public Action<int, int>? RightClickHandler { get; set; }

    public void Initialize()
    {
        tray = new WindowsTrayIcon("Platforms/Windows/trayicon.ico");
        tray.LeftClick = () =>
        {
            WindowExtensions.BringToFront();
            ClickHandler?.Invoke();
        };
        tray.RightClick = () =>
        {
            POINT pt = default;
            GetCursorPos(out pt);
            RightClickHandler?.Invoke(pt.X, pt.Y);
        };
    }

    [DllImport("user32.dll", SetLastError = true)]
    static extern bool GetCursorPos(out POINT lpPoint);

    [StructLayout(LayoutKind.Sequential)]
    struct POINT { public int X; public int Y; }
}