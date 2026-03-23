#if WINDOWS
using System.Runtime.InteropServices;
using copilot_usage_maui.Helpers;
using copilot_usage_maui.Models;
using copilot_usage_maui.Services;
using Microsoft.Maui.Storage;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml.Input;
using Windows.Graphics;
using WinRT.Interop;

using MUX = Microsoft.UI.Xaml;
using WinControls = Microsoft.UI.Xaml.Controls;
using WinMedia = Microsoft.UI.Xaml.Media;

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

    // UI elements
    WinControls.TextBlock? _providerText;
    WinControls.TextBlock? _resetText;
    WinControls.TextBlock? _percentText;
    WinControls.Image? _donutImage;       // 도넛 차트 (weekly/total)
    WinControls.Image? _iconImage;
    MUX.UIElement? _contentRoot;

    // Claude 5h session elements (deskband only)
    WinControls.Image? _sessionDonutImage; // 도넛 차트 (session)
    WinControls.TextBlock? _sessionPercentText;
    WinControls.TextBlock? _sessionLabel;
    WinControls.StackPanel? _sessionColumn;

    // Floating widget에서 세로 레이아웃용
    WinControls.Image? _floatingDonutImage;
    WinControls.Image? _floatingSessionDonutImage;
    WinControls.TextBlock? _floatingSessionLabel;
    WinControls.StackPanel? _floatingSessionColumn;

    readonly Dictionary<string, byte[]> _svgCache = new();
    string? _currentIconFileName;

    bool _isDeskbandMode;
    bool _isFloatingMode;
    bool _isDarkTaskbar;
    MUX.DispatcherTimer? _repositionTimer;

    // Context menu
    WinControls.MenuFlyout? _contextMenu;

    // Event raised when user requests widget mode switch
    public event Action<int>? WidgetModeChangeRequested;

    public WidgetWindow(WidgetService widgetService, MainWindowService? mainWindowService, SettingsService? settingsService = null)
    {
        _widgetService = widgetService;
        _mainWindowService = mainWindowService;
        _settingsService = settingsService;
    }

    public void Show()
    {
        // 모드 결정: 저장된 설정 기반, Win10이면 항상 Floating
        int savedMode = _settingsService?.WidgetMode ?? 0;
        bool canDeskband = IsWindows11OrLater();
        _isFloatingMode = (savedMode == 1) || !canDeskband;
        _isDeskbandMode = !_isFloatingMode && canDeskband;
        _isDarkTaskbar = IsTaskbarDarkTheme();

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

        // _window.SystemBackdrop = new MUX.Media.MicaBackdrop();
        // Build UI
        _contentRoot = BuildContent();
        _window.Content = _contentRoot;


        if (_isFloatingMode)
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
            PositionAsFloating();
            SetupFloatingDrag();
        }
        else if (_isDeskbandMode)
            AttachToTaskbar();
        else
            PositionAboveTaskbar();

        // Subscribe to data changes
        _widgetService.DataChanged += OnDataChanged;
        if (_widgetService.Current is { } data)
            UpdateUI(data);

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
        _widgetService.DataChanged -= OnDataChanged;
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
        int widgetHeight = 160;

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
                _settingsService.FloatingWidgetX = pos.X;
                _settingsService.FloatingWidgetY = pos.Y;
            }
        };
    }

    static bool IsSystemDarkTheme()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            var value = key?.GetValue("AppsUseLightTheme");
            return value is not int lightTheme || lightTheme == 0;
        }
        catch { return true; }
    }

    #endregion

    #region UI

    MUX.UIElement BuildContent()
    {
        return _isFloatingMode ? BuildFloatingContent() : BuildDeskbandContent();
    }

    /// <summary>
    /// Floating mode: 세로 캡슐 위젯 (프로토타입: 56×120)
    /// [BrandIcon] → [Donut] → [Label] (세로 스택)
    /// Claude: [BrandIcon] → [SessionDonut] → [S] → [─] → [WeeklyDonut] → [W]
    /// </summary>
    MUX.UIElement BuildFloatingContent()
    {
        bool dark = IsSystemDarkTheme();

        var bgColor = dark
            ? ColorHelper.FromArgb(220, 28, 28, 28)
            : ColorHelper.FromArgb(220, 250, 250, 250);

        var dimTextColor = dark
            ? ColorHelper.FromArgb(180, 255, 255, 255)
            : ColorHelper.FromArgb(180, 30, 30, 30);

        var root = new WinControls.StackPanel
        {
            Orientation = WinControls.Orientation.Vertical,
            HorizontalAlignment = MUX.HorizontalAlignment.Center,
            Padding = new MUX.Thickness(8, 10, 8, 10),
            Spacing = 5,
            Background = new WinMedia.SolidColorBrush(bgColor)
        };

        // Brand icon
        _iconImage = new WinControls.Image
        {
            Width = 20,
            Height = 20,
            HorizontalAlignment = MUX.HorizontalAlignment.Center,
        };
        root.Children.Add(_iconImage);

        // Main donut (weekly/total)
        _floatingDonutImage = new WinControls.Image
        {
            Width = 30,
            Height = 30,
            HorizontalAlignment = MUX.HorizontalAlignment.Center,
        };
        root.Children.Add(_floatingDonutImage);

        // Reset time label
        _resetText = new WinControls.TextBlock
        {
            FontSize = 7,
            Foreground = new WinMedia.SolidColorBrush(dimTextColor),
            HorizontalAlignment = MUX.HorizontalAlignment.Center,
        };
        root.Children.Add(_resetText);

        // Claude session column (hidden by default, shown for Claude)
        var divider = new MUX.Shapes.Rectangle
        {
            Width = 14,
            Height = 1,
            Fill = new WinMedia.SolidColorBrush(
                dark ? ColorHelper.FromArgb(255, 51, 51, 51) : ColorHelper.FromArgb(255, 232, 230, 225)),
            HorizontalAlignment = MUX.HorizontalAlignment.Center,
        };

        _floatingSessionDonutImage = new WinControls.Image
        {
            Width = 28,
            Height = 28,
            HorizontalAlignment = MUX.HorizontalAlignment.Center,
        };

        _floatingSessionLabel = new WinControls.TextBlock
        {
            Text = "S",
            FontSize = 7,
            Foreground = new WinMedia.SolidColorBrush(dimTextColor),
            HorizontalAlignment = MUX.HorizontalAlignment.Center,
        };

        // Weekly label (W) for Claude mode
        _percentText = new WinControls.TextBlock
        {
            Text = "W",
            FontSize = 7,
            Foreground = new WinMedia.SolidColorBrush(dimTextColor),
            HorizontalAlignment = MUX.HorizontalAlignment.Center,
            Visibility = MUX.Visibility.Collapsed,
        };

        _floatingSessionColumn = new WinControls.StackPanel
        {
            Orientation = WinControls.Orientation.Vertical,
            Spacing = 3,
            HorizontalAlignment = MUX.HorizontalAlignment.Center,
            Visibility = MUX.Visibility.Collapsed,
            Children = { _floatingSessionLabel, _floatingSessionDonutImage, divider },
        };

        // Insert session column before the main donut (Session first, then Weekly)
        // Reorder: icon → sessionColumn → mainDonut → weeklyLabel → resetText
        root.Children.Clear();
        root.Children.Add(_iconImage);
        root.Children.Add(_floatingSessionColumn);
        root.Children.Add(_floatingDonutImage);
        root.Children.Add(_percentText);  // "W" label for Claude, hidden for Copilot
        root.Children.Add(_resetText);

        // Hidden provider text (used by UpdateUI but not displayed in floating capsule)
        _providerText = new WinControls.TextBlock { Visibility = MUX.Visibility.Collapsed };
        root.Children.Add(_providerText);

        return root;
    }

    /// <summary>
    /// Deskband mode: 프로토타입 기준 가로 레이아웃
    /// Copilot: [BrandIcon 18px] [Donut 20px] [62%] [9d]
    /// Claude:  [BrandIcon 18px] [S] [Donut 18px] [25%] · [W] [Donut 18px] [95%]
    /// </summary>
    MUX.UIElement BuildDeskbandContent()
    {
        var bgColor = _isDarkTaskbar
            ? ColorHelper.FromArgb(255, 26, 26, 26)
            : ColorHelper.FromArgb(255, 243, 243, 243);

        var textColor = _isDarkTaskbar
            ? Microsoft.UI.Colors.White
            : ColorHelper.FromArgb(255, 30, 30, 30);

        var dimTextColor = _isDarkTaskbar
            ? ColorHelper.FromArgb(120, 255, 255, 255)
            : ColorHelper.FromArgb(120, 30, 30, 30);

        var root = new WinControls.StackPanel
        {
            Orientation = WinControls.Orientation.Horizontal,
            Background = new WinMedia.SolidColorBrush(bgColor),
            Padding = new MUX.Thickness(10, 0, 10, 0),
            VerticalAlignment = MUX.VerticalAlignment.Center,
            Spacing = 6,
            CornerRadius = new MUX.CornerRadius(6),
        };

        // Brand icon
        _iconImage = new WinControls.Image
        {
            Width = 18,
            Height = 18,
            VerticalAlignment = MUX.VerticalAlignment.Center,
        };
        root.Children.Add(_iconImage);

        // ── Session column (Claude only, hidden by default) ──
        _sessionLabel = new WinControls.TextBlock
        {
            Text = "S",
            FontSize = 9,
            Foreground = new WinMedia.SolidColorBrush(dimTextColor),
            VerticalAlignment = MUX.VerticalAlignment.Center,
        };
        _sessionDonutImage = new WinControls.Image
        {
            Width = 18,
            Height = 18,
            VerticalAlignment = MUX.VerticalAlignment.Center,
        };
        _sessionPercentText = new WinControls.TextBlock
        {
            FontSize = 11,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            VerticalAlignment = MUX.VerticalAlignment.Center,
        };

        // Dot separator
        var dotSep = new MUX.Shapes.Ellipse
        {
            Width = 3,
            Height = 3,
            Fill = new WinMedia.SolidColorBrush(dimTextColor),
            VerticalAlignment = MUX.VerticalAlignment.Center,
        };

        _sessionColumn = new WinControls.StackPanel
        {
            Orientation = WinControls.Orientation.Horizontal,
            Spacing = 4,
            VerticalAlignment = MUX.VerticalAlignment.Center,
            Visibility = MUX.Visibility.Collapsed,
            Children = { _sessionLabel, _sessionDonutImage, _sessionPercentText, dotSep },
        };
        root.Children.Add(_sessionColumn);

        // ── Main donut (weekly/total) ──
        // Weekly label (W) for Claude, hidden for Copilot
        var weeklyLabel = new WinControls.TextBlock
        {
            Text = "W",
            FontSize = 9,
            Foreground = new WinMedia.SolidColorBrush(dimTextColor),
            VerticalAlignment = MUX.VerticalAlignment.Center,
            Visibility = MUX.Visibility.Collapsed,
        };
        _providerText = weeklyLabel; // reuse field; we'll toggle visibility in UpdateUI

        _donutImage = new WinControls.Image
        {
            Width = 20,
            Height = 20,
            VerticalAlignment = MUX.VerticalAlignment.Center,
        };
        _percentText = new WinControls.TextBlock
        {
            FontSize = 11,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = new WinMedia.SolidColorBrush(textColor),
            VerticalAlignment = MUX.VerticalAlignment.Center,
        };
        _resetText = new WinControls.TextBlock
        {
            FontSize = 9,
            Foreground = new WinMedia.SolidColorBrush(dimTextColor),
            VerticalAlignment = MUX.VerticalAlignment.Center,
        };

        root.Children.Add(weeklyLabel);
        root.Children.Add(_donutImage);
        root.Children.Add(_percentText);
        root.Children.Add(_resetText);

        return root;
    }

    #endregion

    #region Context Menu

    void BuildContextMenu()
    {
        _contextMenu = new WinControls.MenuFlyout();

        // 현재 위젯 데이터에서 사용률 가져오기
        var current = _widgetService.Current;
        double currentPct = current?.UsedPercent ?? 0;
        bool isCopilotActive = current?.ProviderName == "Copilot";

        // ── Copilot (flat, no submenu) ──
        var copilotItem = new WinControls.MenuFlyoutItem
        {
            Text = isCopilotActive ? "  Copilot  ✓" : "  Copilot",
        };
        _ = SetMenuItemIconAsync(copilotItem, "providericon_copilot.svg");
        copilotItem.Click += (_, _) => SwitchProvider("/ai/githubcopilot");
        _contextMenu.Items.Add(copilotItem);

        // ── Claude (flat, no submenu) ──
        var claudeItem = new WinControls.MenuFlyoutItem
        {
            Text = !isCopilotActive ? "  Claude  ✓" : "  Claude",
        };
        _ = SetMenuItemIconAsync(claudeItem, "providericon_claude.svg");
        claudeItem.Click += (_, _) => SwitchProvider("/ai/claude");
        _contextMenu.Items.Add(claudeItem);

        _contextMenu.Items.Add(new WinControls.MenuFlyoutSeparator());

        // ── Refresh ──
        var refreshItem = new WinControls.MenuFlyoutItem
        {
            Text = "Refresh",
            Icon = new WinControls.FontIcon { Glyph = "\uE72C" },
        };
        refreshItem.Click += async (_, _) => await _widgetService.RequestRefreshAsync();
        _contextMenu.Items.Add(refreshItem);

        _contextMenu.Items.Add(new WinControls.MenuFlyoutSeparator());

        // ── Widget Mode submenu ──
        var widgetModeSubMenu = new WinControls.MenuFlyoutSubItem
        {
            Text = "Widget Mode",
            Icon = new WinControls.FontIcon { Glyph = "\uE78B" },
        };

        if (IsWindows11OrLater())
        {
            var deskbandToggle = new WinControls.ToggleMenuFlyoutItem
            {
                Text = "DeskBand",
                IsChecked = _isDeskbandMode,
            };
            deskbandToggle.Click += (_, _) => RequestModeSwitch(0);
            widgetModeSubMenu.Items.Add(deskbandToggle);
        }

        var floatingToggle = new WinControls.ToggleMenuFlyoutItem
        {
            Text = "Floating Widget",
            IsChecked = _isFloatingMode,
        };
        floatingToggle.Click += (_, _) => RequestModeSwitch(1);
        widgetModeSubMenu.Items.Add(floatingToggle);

        _contextMenu.Items.Add(widgetModeSubMenu);

        _contextMenu.Items.Add(new WinControls.MenuFlyoutSeparator());

        // ── Settings ──
        var settingsItem = new WinControls.MenuFlyoutItem
        {
            Text = "Settings",
            Icon = new WinControls.FontIcon { Glyph = "\uE713" },
        };
        settingsItem.Click += (_, _) =>
        {
            Microsoft.Maui.Controls.Application.Current?.Dispatcher.Dispatch(() =>
            {
                try { ReactorRouter.Navigation.NavigationService.Instance.NavigateTo("/settings"); }
                catch { }
            });
            _mainWindowService?.Toggle();
        };
        _contextMenu.Items.Add(settingsItem);

        _contextMenu.Items.Add(new WinControls.MenuFlyoutSeparator());

        // ── Quit ──
        var quitItem = new WinControls.MenuFlyoutItem
        {
            Text = "Quit",
            Icon = new WinControls.FontIcon { Glyph = "\uE711" },
        };
        quitItem.Click += (_, _) =>
        {
            _mainWindowService?.SetQuitting();
            Close();
            Microsoft.Maui.Controls.Application.Current?.Quit();
        };
        _contextMenu.Items.Add(quitItem);
    }

    async Task SetMenuItemIconAsync(WinControls.MenuFlyoutItem item, string svgFileName)
    {
        try
        {
            string cacheKey = $"tinted_{svgFileName}";

            if (!_svgCache.TryGetValue(cacheKey, out var svgBytes))
            {
                using var stream = await FileSystem.OpenAppPackageFileAsync(svgFileName);
                using var reader = new StreamReader(stream);
                var svgContent = await reader.ReadToEndAsync();

                svgContent = System.Text.RegularExpressions.Regex.Replace(
                    svgContent, @"fill=""[^""]*""", @"fill=""#676780""");
                svgContent = System.Text.RegularExpressions.Regex.Replace(
                    svgContent, @"fill:[^;""]+", "fill:#676780");


                svgBytes = System.Text.Encoding.UTF8.GetBytes(svgContent);
                _svgCache[cacheKey] = svgBytes;
            }

            _window?.DispatcherQueue?.TryEnqueue(async () =>
            {
                var svgSource = new Microsoft.UI.Xaml.Media.Imaging.SvgImageSource();
                using var ms = new MemoryStream(svgBytes);
                using var ras = ms.AsRandomAccessStream();
                await svgSource.SetSourceAsync(ras);
                item.Icon = new WinControls.ImageIcon { Source = svgSource, Width = 16, Height = 16 };
            });
        }
        catch { }
    }

    void RequestModeSwitch(int mode)
    {
        if (_settingsService is not null)
            _settingsService.WidgetMode = mode;
        WidgetModeChangeRequested?.Invoke(mode);
    }

    void SwitchProvider(string url)
    {
        // 서비스만 전환, 대시보드는 열지 않음
        Microsoft.Maui.Controls.Application.Current?.Dispatcher.Dispatch(() =>
        {
            try
            {
                ReactorRouter.Navigation.NavigationService.Instance.NavigateTo(url);
            }
            catch { }
        });
    }

    #endregion

    #region Data Update

    void OnDataChanged(WidgetData data)
    {
        _window?.DispatcherQueue?.TryEnqueue(() => UpdateUI(data));
    }

    void UpdateUI(WidgetData data)
    {
        if (_percentText is null) return;

        bool isDark = _isDeskbandMode ? _isDarkTaskbar : IsSystemDarkTheme();
        var color = DonutRenderer.GetStatusWinColor(data.UsedPercent);
        var brush = new WinMedia.SolidColorBrush(color);

        if (_isDeskbandMode)
        {
            // ── Deskband 모드 ──
            _resetText!.Text = ShortenResetText(data.ResetTimeText);

            bool isClaude = data.SessionUsedPercent.HasValue;
            if (_sessionColumn is not null)
                _sessionColumn.Visibility = isClaude ? MUX.Visibility.Visible : MUX.Visibility.Collapsed;
            if (_providerText is not null)
                _providerText.Visibility = isClaude ? MUX.Visibility.Visible : MUX.Visibility.Collapsed;

            if (isClaude)
            {
                // S donut: Session(5h)
                var sessionPct = data.SessionUsedPercent!.Value;
                var sessionColor = DonutRenderer.GetStatusWinColor(sessionPct);
                _sessionPercentText!.Text = $"{sessionPct:F0}%";
                _sessionPercentText.Foreground = new WinMedia.SolidColorBrush(sessionColor);
                _ = RenderDonutAsync(_sessionDonutImage, 18, 2.5f, sessionPct, isDark, showText: false);

                // W donut: Weekly(7d) — UsedPercent가 아닌 WeeklyUsedPercent 사용
                var weeklyPct = data.WeeklyUsedPercent ?? data.UsedPercent;
                var weeklyColor = DonutRenderer.GetStatusWinColor(weeklyPct);
                _percentText.Text = $"{weeklyPct:F0}%";
                _percentText.Foreground = new WinMedia.SolidColorBrush(weeklyColor);
                _ = RenderDonutAsync(_donutImage, 20, 3f, weeklyPct, isDark, showText: false);
            }
            else
            {
                // Copilot: 단일 도넛
                _percentText.Text = $"{data.UsedPercent:F0}%";
                _percentText.Foreground = brush;
                _ = RenderDonutAsync(_donutImage, 20, 3f, data.UsedPercent, isDark, showText: false);
            }
        }
        else
        {
            // ── Floating 세로 캡슐 모드 ──
            _resetText!.Text = ShortenResetText(data.ResetTimeText);

            bool isClaude = data.SessionUsedPercent.HasValue;

            // Claude session column
            if (_floatingSessionColumn is not null)
                _floatingSessionColumn.Visibility = isClaude ? MUX.Visibility.Visible : MUX.Visibility.Collapsed;
            if (_percentText is not null)
                _percentText.Visibility = isClaude ? MUX.Visibility.Visible : MUX.Visibility.Collapsed;

            if (isClaude)
            {
                // S donut: Session(5h)
                var sessionPct = data.SessionUsedPercent!.Value;
                _ = RenderDonutAsync(_floatingSessionDonutImage, 28, 3f, sessionPct, isDark,
                    showText: true, centerText: $"{sessionPct:F0}", fontSize: 8f);

                // W donut: Weekly(7d) — UsedPercent가 아닌 WeeklyUsedPercent 사용
                var weeklyPct = data.WeeklyUsedPercent ?? data.UsedPercent;
                _ = RenderDonutAsync(_floatingDonutImage, 30, 3f, weeklyPct, isDark,
                    showText: true, centerText: $"{weeklyPct:F0}", fontSize: 9f);
            }
            else
            {
                // Copilot: 단일 도넛
                _ = RenderDonutAsync(_floatingDonutImage, 30, 3f, data.UsedPercent, isDark,
                    showText: true, centerText: $"{data.UsedPercent:F0}", fontSize: 9f);
            }
        }

        if (!string.IsNullOrEmpty(data.IconFileName) && data.IconFileName != _currentIconFileName)
        {
            _currentIconFileName = data.IconFileName;
            _ = LoadIconAsync(data.IconFileName);
        }
    }

    async Task RenderDonutAsync(WinControls.Image? imageControl, int size, float strokeWidth,
        double percent, bool isDark, bool showText = false, string? centerText = null, float fontSize = 0)
    {
        if (imageControl is null) return;

        var trackColor = DonutRenderer.GetTrackColor(isDark);
        var fillColor = DonutRenderer.GetStatusColor(percent, isDark);

        using var bitmap = DonutRenderer.Render(
            size, strokeWidth, percent, trackColor, fillColor,
            showText ? centerText : null,
            showText ? fillColor : (SkiaSharp.SKColor?)null,
            fontSize,
            scale: 2f); // 2x for HiDPI

        var bitmapImage = await DonutRenderer.ToWinUIImageAsync(bitmap);
        imageControl.Source = bitmapImage;
    }

    /// <summary>
    /// Deskband용 짧은 리셋 텍스트
    /// "3시간 42분 후 초기화" → "3h 42m"
    /// "5일 후 초기화" → "5d"
    /// "Resets in 3h 42m" → "3h 42m"
    /// "Resets in 5d" → "5d"
    /// </summary>
    static string ShortenResetText(string text)
    {
        if (string.IsNullOrEmpty(text)) return "";

        // Korean: "3시간 42분 후 초기화"
        var krMatch = System.Text.RegularExpressions.Regex.Match(text,
            @"(\d+)시간\s*(\d+)분");
        if (krMatch.Success)
            return $"{krMatch.Groups[1].Value}h {krMatch.Groups[2].Value}m";

        var krDayMatch = System.Text.RegularExpressions.Regex.Match(text, @"(\d+)일");
        if (krDayMatch.Success)
            return $"{krDayMatch.Groups[1].Value}d";

        // English: "Resets in 3h 42m"
        var enMatch = System.Text.RegularExpressions.Regex.Match(text,
            @"(\d+h\s*\d+m|\d+[dhm])");
        if (enMatch.Success)
            return enMatch.Groups[1].Value;

        return text;
    }

    async Task LoadIconAsync(string fileName)
    {
        try
        {
            string cacheKey = $"tinted_{fileName}";

            if (!_svgCache.TryGetValue(cacheKey, out var svgBytes))
            {
                using var stream = await FileSystem.OpenAppPackageFileAsync(fileName);
                using var reader = new StreamReader(stream);
                var svgContent = await reader.ReadToEndAsync();

                svgContent = System.Text.RegularExpressions.Regex.Replace(
                    svgContent, @"fill=""[^""]*""", @"fill=""#676780""");
                svgContent = System.Text.RegularExpressions.Regex.Replace(
                    svgContent, @"fill:[^;""]+", "fill:#676780");


                svgBytes = System.Text.Encoding.UTF8.GetBytes(svgContent);
                _svgCache[cacheKey] = svgBytes;
            }

            _window?.DispatcherQueue?.TryEnqueue(async () =>
            {
                var svgSource = new Microsoft.UI.Xaml.Media.Imaging.SvgImageSource();
                using var ms = new MemoryStream(svgBytes);
                using var ras = ms.AsRandomAccessStream();
                await svgSource.SetSourceAsync(ras);
                _iconImage!.Source = svgSource;
            });
        }
        catch
        {
            // SVG load failed
        }
    }

    #endregion

    #region Interaction

    void OnWidgetTapped(object sender, TappedRoutedEventArgs e)
    {
        _mainWindowService?.Toggle();
    }

    void OnWidgetRightTapped(object sender, RightTappedRoutedEventArgs e)
    {
        // 매번 재생성하여 최신 사용률 + 활성 프로바이더 반영
        BuildContextMenu();

        if (_contentRoot is MUX.FrameworkElement fe)
        {
            _contextMenu!.ShowAt(fe, e.GetPosition(fe));
        }
    }

    #endregion

    #region OS Version Detection

    static bool IsWindows11OrLater()
    {
        var version = Environment.OSVersion.Version;
        return version.Major >= 10 && version.Build >= 22000;
    }

    static bool IsTaskbarDarkTheme()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            var value = key?.GetValue("SystemUsesLightTheme");
            return value is not int lightTheme || lightTheme == 0;
        }
        catch
        {
            return true;
        }
    }

    #endregion

    #region Win32 P/Invoke

    const int GWL_EXSTYLE = -20;
    const int GWL_STYLE = -16;
    const int GWL_WNDPROC = -4;
    const int WS_EX_TOOLWINDOW = 0x00000080;
    const int WS_POPUP = unchecked((int)0x80000000);
    const int WS_CHILD = 0x40000000;
    const uint SWP_NOZORDER = 0x0004;
    const uint SWP_NOACTIVATE = 0x0010;
    const uint SWP_FRAMECHANGED = 0x0020;
    const uint WM_NCHITTEST = 0x0084;
    const uint WM_EXITSIZEMOVE = 0x0232;
    const int HTCAPTION = 2;
    const uint SWP_NOSIZE = 0x0001;
    [DllImport("user32.dll")] static extern int GetWindowLong(IntPtr hWnd, int nIndex);
    [DllImport("user32.dll")] static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr FindWindow(string? lpClassName, string? lpWindowName);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr FindWindowEx(IntPtr hWndParent, IntPtr hWndChildAfter, string? lpszClass, string? lpszWindow);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
    [DllImport("user32.dll")] static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);
    [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);
    [DllImport("gdi32.dll")] static extern IntPtr CreateRectRgn(int nLeftRect, int nTopRect, int nRightRect, int nBottomRect);
    [DllImport("gdi32.dll")] static extern IntPtr CreateRoundRectRgn(int x1, int y1, int x2, int y2, int cx, int cy);
    [DllImport("user32.dll")] static extern int SetWindowRgn(IntPtr hWnd, IntPtr hRgn, bool bRedraw);
    [DllImport("user32.dll")] static extern uint GetDpiForWindow(IntPtr hwnd);
    const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    const int DWMWCP_ROUND = 2; // 22px 라운드
    const int WS_CAPTION = 0x00C00000;
    const int WS_THICKFRAME = 0x00040000;
    [DllImport("dwmapi.dll", PreserveSig = true)]
    static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int pvAttribute, int cbAttribute);

    [StructLayout(LayoutKind.Sequential)]
    struct RECT { public int left, top, right, bottom; }

    #endregion
}
#endif
