using System.Runtime.InteropServices;
using copilot_usage_maui.Services;
using Microsoft.Maui.Storage;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Windows.Graphics;
using WinRT.Interop;

using MUX = Microsoft.UI.Xaml;
using WinControls = Microsoft.UI.Xaml.Controls;

namespace copilot_usage_maui.Platforms.Windows;

public class WidgetWindow
{
    readonly WidgetService _widgetService;
    readonly MainWindowService? _mainWindowService;
    readonly SettingsService? _settingsService;
    MUX.Window? _window;
    AppWindow? _appWindow;
    IntPtr _hwnd;

    public IntPtr Hwnd => _hwnd;
    MUX.UIElement? _contentRoot;

    bool _isDeskbandMode;
    bool _isFloatingMode;
    bool _isHorizontalFloatingMode;

    bool IsAnyFloatingMode => _isFloatingMode || _isHorizontalFloatingMode;
    MUX.DispatcherTimer? _repositionTimer;

    readonly WidgetContextMenuService _contextMenuService;

    // Event raised when user requests widget mode switch
    public event Action<int>? WidgetModeChangeRequested;

    public WidgetWindow(WidgetService widgetService, MainWindowService? mainWindowService, SettingsService? settingsService = null, WidgetContextMenuService? contextMenuService = null)
    {
        _widgetService = widgetService;
        _mainWindowService = mainWindowService;
        _settingsService = settingsService;
        _contextMenuService = contextMenuService ?? new WidgetContextMenuService(widgetService, mainWindowService, settingsService);
        _contextMenuService.WidgetModeChangeRequested += mode => WidgetModeChangeRequested?.Invoke(mode);
    }
    public void Show(FrameworkElement element)
    {
        // 모드 결정: 저장된 설정 기반, Win10이면 항상 Floating
        int savedMode = _settingsService?.WidgetMode ?? 0;
        bool canDeskband = IsWindows11OrLater();

        _isDeskbandMode = false;
        _isFloatingMode = false;
        _isHorizontalFloatingMode = false;
        switch (savedMode)
        {
            case 0 when canDeskband:
                _isDeskbandMode = true;
                break;
            case 2:
                _isHorizontalFloatingMode = true;
                break;
            default: // mode 1, or mode 0 on Win10
                _isFloatingMode = true;
                break;
        }

        _window = new MUX.Window();
        _hwnd = WindowNative.GetWindowHandle(_window);
        var windowId = Win32Interop.GetWindowIdFromWindow(_hwnd);
        _appWindow = AppWindow.GetFromWindowId(windowId);

        // Borderless, always-on-top
        if (_appWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.SetBorderAndTitleBar(false, false);
            presenter.IsAlwaysOnTop = true;
            presenter.IsResizable = false;
            presenter.IsMinimizable = false;
            presenter.IsMaximizable = false;
        }

        // Hide from Alt+Tab & taskbar
        _appWindow.IsShownInSwitchers = false;
        // int style = GetWindowLong(_hwnd, GWL_STYLE);
        // style &= ~(WS_CAPTION | WS_THICKFRAME);
        // SetWindowLong(_hwnd, GWL_STYLE, style);

        _window.SystemBackdrop = new MUX.Media.MicaBackdrop();
        // Build UI
        _contentRoot = element;
        _window.Content = _contentRoot;


        if (IsAnyFloatingMode)
        {
            // Win32 스타일에서도 캡션/두꺼운 프레임 제거
            int style = GetWindowLong(_hwnd, GWL_STYLE);
            style &= ~(WS_CAPTION | WS_THICKFRAME);
            SetWindowLong(_hwnd, GWL_STYLE, style);
            SetWindowPos(_hwnd, IntPtr.Zero, 0, 0, 0, 0,
                SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE | SWP_FRAMECHANGED);

            // Fluent 라운드 코너 (8px) - Windows 11+
            int cornerPref = DWMWCP_ROUND; // 라운드 코너
            DwmSetWindowAttribute(_hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref cornerPref, sizeof(int));

            if (_isHorizontalFloatingMode)
                PositionAsHorizontalFloating();
            else
                PositionAsFloating();

            SetupFloatingDrag();
        }
        else if (_isDeskbandMode)
            AttachToTaskbar();
        else
            PositionAboveTaskbar();

        // Left-click: show main window
        if (_contentRoot is not null)
        {
            _contentRoot.Tapped += OnWidgetTapped;
            _contentRoot.RightTapped += OnWidgetRightTapped;
        }
        _window.Activate();
    }

    public void Close()
    {
        _repositionTimer?.Stop();
        _repositionTimer = null;
        // WndProc subclassing은 더 이상 사용하지 않음 (XAML ManipulationDelta로 대체)
        _window?.Close();
        _window = null;
    }

    #region Deskband11 Mode (Windows 11+)

    void AttachToTaskbar()
    {
        IntPtr taskbarHwnd = FindWindow("Shell_TrayWnd", null);
        if (taskbarHwnd == IntPtr.Zero)
        {
            PositionAboveTaskbar();
            return;
        }

        if (!GetWindowRect(taskbarHwnd, out RECT taskbarRect))
        {
            PositionAboveTaskbar();
            return;
        }

        IntPtr trayNotifyHwnd = FindWindowEx(taskbarHwnd, IntPtr.Zero, "TrayNotifyWnd", null);
        RECT trayNotifyRect = default;
        bool hasTrayNotify = trayNotifyHwnd != IntPtr.Zero && GetWindowRect(trayNotifyHwnd, out trayNotifyRect);

        float scaleFactor = GetDpiForWindow(_hwnd) / 96.0f;
        int taskbarHeight = taskbarRect.bottom - taskbarRect.top;
        int widgetWidth = (int)(240 * scaleFactor);
        int widgetHeight = taskbarHeight;

        // Change window style: remove WS_POPUP, add WS_CHILD
        int style = GetWindowLong(_hwnd, GWL_STYLE);
        style = (style & ~WS_POPUP) | WS_CHILD;
        SetWindowLong(_hwnd, GWL_STYLE, style);

        if (_appWindow?.Presenter is OverlappedPresenter presenter)
            presenter.IsAlwaysOnTop = false;

        SetParent(_hwnd, taskbarHwnd);

        int x;
        if (hasTrayNotify)
        {
            int trayNotifyLeft = trayNotifyRect.left - taskbarRect.left;
            x = trayNotifyLeft - widgetWidth;
        }
        else
        {
            x = (taskbarRect.right - taskbarRect.left) - widgetWidth - 200;
        }

        SetWindowPos(_hwnd, IntPtr.Zero, x, 0, widgetWidth, widgetHeight,
            SWP_NOZORDER | SWP_NOACTIVATE | SWP_FRAMECHANGED);

        ApplyClipRegion(scaleFactor, widgetWidth, widgetHeight);

        _repositionTimer = new MUX.DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        _repositionTimer.Tick += (_, _) => RepositionInTaskbar();
        _repositionTimer.Start();
    }

    void ApplyClipRegion(float scaleFactor, int widgetWidth, int widgetHeight)
    {
        int padding = (int)(2 * scaleFactor);
        IntPtr hrgn = CreateRectRgn(padding, padding, widgetWidth - padding, widgetHeight - padding);
        SetWindowRgn(_hwnd, hrgn, true);
    }

    void RepositionInTaskbar()
    {
        if (!_isDeskbandMode || _hwnd == IntPtr.Zero) return;

        IntPtr taskbarHwnd = FindWindow("Shell_TrayWnd", null);
        if (taskbarHwnd == IntPtr.Zero) return;
        if (!GetWindowRect(taskbarHwnd, out RECT taskbarRect)) return;

        IntPtr trayNotifyHwnd = FindWindowEx(taskbarHwnd, IntPtr.Zero, "TrayNotifyWnd", null);
        if (trayNotifyHwnd == IntPtr.Zero) return;
        if (!GetWindowRect(trayNotifyHwnd, out RECT trayNotifyRect)) return;

        float scaleFactor = GetDpiForWindow(_hwnd) / 96.0f;
        int widgetWidth = (int)(240 * scaleFactor);
        int taskbarHeight = taskbarRect.bottom - taskbarRect.top;

        int trayNotifyLeft = trayNotifyRect.left - taskbarRect.left;
        int x = trayNotifyLeft - widgetWidth;

        SetWindowPos(_hwnd, IntPtr.Zero, x, 0, widgetWidth, taskbarHeight,
            SWP_NOZORDER | SWP_NOACTIVATE);

        ApplyClipRegion(scaleFactor, widgetWidth, taskbarHeight);
    }

    #endregion

    #region Overlay Mode (Windows 10)

    void PositionAboveTaskbar()
    {
        var displayArea = DisplayArea.GetFromWindowId(
            _appWindow!.Id, DisplayAreaFallback.Primary);
        var workArea = displayArea.WorkArea;

        int widgetWidth = 280;
        int widgetHeight = 64;

        int x = workArea.X + workArea.Width - widgetWidth - 8;
        int y = workArea.Y + workArea.Height - widgetHeight;

        _appWindow.MoveAndResize(new RectInt32(x, y, widgetWidth, widgetHeight));
    }

    #endregion

    #region Floating Mode

    void PositionAsFloating()
    {
        if (_appWindow?.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsAlwaysOnTop = true;
            presenter.IsResizable = false;
        }

        int widgetWidth = 60;
        int widgetHeight = 180;

        int x = _settingsService?.FloatingWidgetX ?? -1;
        int y = _settingsService?.FloatingWidgetY ?? -1;

        if (x < 0 || y < 0)
        {
            // 저장된 위치 없으면 우측 상단 기본값
            var displayArea = DisplayArea.GetFromWindowId(
                _appWindow!.Id, DisplayAreaFallback.Primary);
            var workArea = displayArea.WorkArea;
            x = workArea.X + workArea.Width - widgetWidth - 12;
            y = workArea.Y + 12;
        }

        _appWindow!.MoveAndResize(new RectInt32(x, y, widgetWidth, widgetHeight));
    }

    void PositionAsHorizontalFloating()
    {
        if (_appWindow?.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsAlwaysOnTop = true;
            presenter.IsResizable = false;
        }

        int widgetWidth = 240;
        int widgetHeight = 48;

        int x = _settingsService?.HFloatingWidgetX ?? -1;
        int y = _settingsService?.HFloatingWidgetY ?? -1;

        if (x < 0 || y < 0)
        {
            var displayArea = DisplayArea.GetFromWindowId(
                _appWindow!.Id, DisplayAreaFallback.Primary);
            var workArea = displayArea.WorkArea;
            x = workArea.X + workArea.Width - widgetWidth - 12;
            y = workArea.Y + 12;
        }

        _appWindow!.MoveAndResize(new RectInt32(x, y, widgetWidth, widgetHeight));
    }

    /// <summary>
    /// XAML ManipulationDelta 기반 드래그 — Tapped/RightTapped와 공존 가능
    /// </summary>
    void SetupFloatingDrag()
    {
        if (_contentRoot is not MUX.UIElement el) return;

        el.ManipulationMode = MUX.Input.ManipulationModes.TranslateX | MUX.Input.ManipulationModes.TranslateY;
        el.ManipulationDelta += (_, e) =>
        {
            if (_appWindow is null) return;
            var pos = _appWindow.Position;
            int newX = pos.X + (int)e.Delta.Translation.X;
            int newY = pos.Y + (int)e.Delta.Translation.Y;
            _appWindow.Move(new PointInt32(newX, newY));
        };
        el.ManipulationCompleted += (_, _) =>
        {
            // 드래그 완료 시 위치 저장
            if (_appWindow is not null && _settingsService is not null)
            {
                var pos = _appWindow.Position;
                if (_isHorizontalFloatingMode)
                {
                    _settingsService.HFloatingWidgetX = pos.X;
                    _settingsService.HFloatingWidgetY = pos.Y;
                }
                else
                {
                    _settingsService.FloatingWidgetX = pos.X;
                    _settingsService.FloatingWidgetY = pos.Y;
                }
            }
        };
    }

    #endregion

    #region Context Menu

    public void ShowContextMenuFromTray()
    {
        _window.DispatcherQueue?.TryEnqueue(() =>
        {
            if (_contentRoot is MUX.FrameworkElement fe)
            {
                _contextMenuService.ShowContextMenuAt(fe);
            }
        });
    }

    void OnWidgetRightTapped(object sender, RightTappedRoutedEventArgs e)
    {
        if (_contentRoot is MUX.FrameworkElement fe)
        {
            var point = e.GetPosition(fe);
            _contextMenuService.ShowContextMenuAt(fe, point);
        }
        else
        {
            _contextMenuService.ShowContextMenuFromCursor();
        }
    }

    #endregion

    #region Interaction

    void OnWidgetTapped(object sender, TappedRoutedEventArgs e)
    {
        _mainWindowService?.Toggle();
    }

    #endregion

    #region OS Version Detection

    static bool IsWindows11OrLater()
    {
        var version = Environment.OSVersion.Version;
        return version.Major >= 10 && version.Build >= 22000;
    }

    #endregion

    #region Win32 P/Invoke

    const int GWL_STYLE = -16;
    const int WS_POPUP = unchecked((int)0x80000000);
    const int WS_CHILD = 0x40000000;
    const uint SWP_NOZORDER = 0x0004;
    const uint SWP_NOACTIVATE = 0x0010;
    const uint SWP_FRAMECHANGED = 0x0020;
    const uint SWP_NOSIZE = 0x0001;
    [DllImport("user32.dll")] static extern int GetWindowLong(IntPtr hWnd, int nIndex);
    [DllImport("user32.dll")] static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr FindWindow(string? lpClassName, string? lpWindowName);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr FindWindowEx(IntPtr hWndParent, IntPtr hWndChildAfter, string? lpszClass, string? lpszWindow);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
    [DllImport("user32.dll")] static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);
    [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);
    [DllImport("gdi32.dll")] static extern IntPtr CreateRectRgn(int nLeftRect, int nTopRect, int nRightRect, int nBottomRect);
    [DllImport("user32.dll")] static extern int SetWindowRgn(IntPtr hWnd, IntPtr hRgn, bool bRedraw);
    [DllImport("user32.dll")] static extern uint GetDpiForWindow(IntPtr hwnd);
    [DllImport("user32.dll")] static extern bool GetCursorPos(out POINT lpPoint);

    const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    const int DWMWCP_ROUND = 2; // 22px 라운드
    const int WS_CAPTION = 0x00C00000;
    const int WS_THICKFRAME = 0x00040000;
    [DllImport("dwmapi.dll", PreserveSig = true)]
    static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int pvAttribute, int cbAttribute);

    [StructLayout(LayoutKind.Sequential)]
    struct RECT { public int left, top, right, bottom; }

    [StructLayout(LayoutKind.Sequential)]
    struct POINT { public int X; public int Y; }

    #endregion
}
