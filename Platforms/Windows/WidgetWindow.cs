#if WINDOWS
using System.Runtime.InteropServices;
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
    WinControls.ProgressBar? _progressBar;
    WinControls.Image? _iconImage;
    MUX.UIElement? _contentRoot;

    // Claude 5h session elements (deskband only)
    WinControls.ProgressBar? _sessionProgressBar;
    WinControls.TextBlock? _sessionPercentText;
    WinControls.TextBlock? _sessionLabel;
    WinControls.TextBlock? _sessionResetText;
    WinControls.StackPanel? _sessionColumn;

    readonly Dictionary<string, byte[]> _svgCache = new();
    string? _currentIconFileName;

    bool _isDeskbandMode;
    bool _isFloatingMode;
    bool _isDarkTaskbar;
    MUX.DispatcherTimer? _repositionTimer;

    // WndProc subclassing for drag (floating mode)
    delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
    WndProcDelegate? _wndProcDelegate; // must be kept alive to prevent GC collection
    IntPtr _originalWndProc;

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
        int exStyle = GetWindowLong(_hwnd, GWL_EXSTYLE);
        SetWindowLong(_hwnd, GWL_EXSTYLE, exStyle | WS_EX_TOOLWINDOW);

        // Make window background transparent (removes white non-content area)
        // Use Mica/Acrylic backdrop so the area outside content is transparent
        _window.SystemBackdrop = new MUX.Media.MicaBackdrop();

        // Hide window border
        int borderColor = unchecked((int)0xFFFFFFFE); // DWMWA_COLOR_NONE
        DwmSetWindowAttribute(_hwnd, DWMWA_BORDER_COLOR, ref borderColor, sizeof(int));

        // Build UI
        _contentRoot = BuildContent();
        _window.Content = _contentRoot;
        _window.Activate();

        if (_isFloatingMode)
        {
            PositionAsFloating();
            InstallWndProc();
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
    }

    public void Close()
    {
        _widgetService.DataChanged -= OnDataChanged;
        _repositionTimer?.Stop();
        _repositionTimer = null;
        if (_isFloatingMode)
            UninstallWndProc();
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
        int widgetWidth = (int)(360 * scaleFactor);
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
        int widgetWidth = (int)(360 * scaleFactor);
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

        int widgetWidth  = 280;
        int widgetHeight = 80;

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

    void InstallWndProc()
    {
        _wndProcDelegate = FloatingWndProc;
        IntPtr newProc = Marshal.GetFunctionPointerForDelegate(_wndProcDelegate);
        _originalWndProc = SetWindowLongPtr(_hwnd, GWL_WNDPROC, newProc);
    }

    void UninstallWndProc()
    {
        if (_originalWndProc != IntPtr.Zero)
        {
            SetWindowLongPtr(_hwnd, GWL_WNDPROC, _originalWndProc);
            _originalWndProc = IntPtr.Zero;
        }
        _wndProcDelegate = null;
    }

    IntPtr FloatingWndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        if (msg == WM_NCHITTEST)
            return (IntPtr)HTCAPTION; // 전체 영역을 타이틀바로 처리 → OS 기본 드래그

        if (msg == WM_EXITSIZEMOVE)
        {
            // 드래그 완료 시 위치 저장
            if (GetWindowRect(_hwnd, out RECT r) && _settingsService is not null)
            {
                _settingsService.FloatingWidgetX = r.left;
                _settingsService.FloatingWidgetY = r.top;
            }
        }

        return CallWindowProc(_originalWndProc, hWnd, msg, wParam, lParam);
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
    /// Floating mode: 자유롭게 떠있는 위젯 (80px 높이, 시스템 테마 기반 색상)
    /// </summary>
    MUX.UIElement BuildFloatingContent()
    {
        bool dark = IsSystemDarkTheme();

        var bgColor = dark
            ? ColorHelper.FromArgb(220, 28, 28, 28)
            : ColorHelper.FromArgb(220, 250, 250, 250);

        var textColor = dark
            ? Microsoft.UI.Colors.White
            : ColorHelper.FromArgb(255, 30, 30, 30);

        var dimTextColor = dark
            ? ColorHelper.FromArgb(180, 255, 255, 255)
            : ColorHelper.FromArgb(180, 30, 30, 30);

        var barTrackBrush = new WinMedia.SolidColorBrush(
            dark
                ? ColorHelper.FromArgb(60, 255, 255, 255)
                : ColorHelper.FromArgb(60, 0, 0, 0));

        var rootGrid = new WinControls.Grid
        {
            Background = new WinMedia.SolidColorBrush(bgColor),
            Padding = new MUX.Thickness(8, 4, 8, 4),
            HorizontalAlignment = MUX.HorizontalAlignment.Stretch,
            VerticalAlignment = MUX.VerticalAlignment.Center,
            ColumnSpacing = 12,
            CornerRadius = new MUX.CornerRadius(8),
        };
        rootGrid.ColumnDefinitions.Add(new WinControls.ColumnDefinition { Width = MUX.GridLength.Auto });
        rootGrid.ColumnDefinitions.Add(new WinControls.ColumnDefinition { Width = new MUX.GridLength(1, MUX.GridUnitType.Star) });

        // Col 0: Icon + Provider Name
        _iconImage = new WinControls.Image { Width = 20, Height = 20, HorizontalAlignment = MUX.HorizontalAlignment.Center };
        _providerText = new WinControls.TextBlock
        {
            FontSize = 9,
            Foreground = new WinMedia.SolidColorBrush(dimTextColor),
            HorizontalAlignment = MUX.HorizontalAlignment.Center,
        };
        var col0 = new WinControls.StackPanel
        {
            Orientation = WinControls.Orientation.Vertical,
            VerticalAlignment = MUX.VerticalAlignment.Center,
            Spacing = 1,
            Children = { _iconImage, _providerText },
        };
        WinControls.Grid.SetColumn(col0, 0);
        rootGrid.Children.Add(col0);

        // Col 1: Weekly usage
        var weeklyLabel = new WinControls.TextBlock
        {
            Text = "Weekly",
            FontSize = 8,
            Foreground = new WinMedia.SolidColorBrush(dimTextColor),
        };
        _progressBar = new WinControls.ProgressBar
        {
            Minimum = 0,
            Maximum = 100,
            Height = 4,
            VerticalAlignment = MUX.VerticalAlignment.Center,
            CornerRadius = new MUX.CornerRadius(2),
            Background = barTrackBrush,
        };
        _percentText = new WinControls.TextBlock
        {
            FontSize = 10,
            FontWeight = Microsoft.UI.Text.FontWeights.Bold,
            Foreground = new WinMedia.SolidColorBrush(textColor),
            VerticalAlignment = MUX.VerticalAlignment.Center,
        };
        _resetText = new WinControls.TextBlock
        {
            FontSize = 8,
            Foreground = new WinMedia.SolidColorBrush(dimTextColor),
            VerticalAlignment = MUX.VerticalAlignment.Center,
        };
        var barRow = new WinControls.StackPanel
        {
            Orientation = WinControls.Orientation.Horizontal,
            Spacing = 3,
            Children = { _progressBar, _percentText, _resetText },
        };
        var col1 = new WinControls.StackPanel
        {
            Orientation = WinControls.Orientation.Vertical,
            VerticalAlignment = MUX.VerticalAlignment.Center,
            Spacing = 1,
            Children = { weeklyLabel, barRow },
        };
        WinControls.Grid.SetColumn(col1, 1);
        rootGrid.Children.Add(col1);

        return rootGrid;
    }

    /// <summary>
    /// Deskband mode: 3-column horizontal layout for taskbar (~48px)
    /// Col 0: [Icon + Name]  |  Col 1: [Session ██ 45% 3h]  |  Col 2: [Weekly ██ 12% 6d]
    /// Copilot: Col 1 hidden, Col 2 only
    /// </summary>
    MUX.UIElement BuildDeskbandContent()
    {
        var bgColor = _isDarkTaskbar
            ? ColorHelper.FromArgb(255, 32, 32, 32)
            : ColorHelper.FromArgb(255, 243, 243, 243);

        var textColor = _isDarkTaskbar
            ? Microsoft.UI.Colors.White
            : ColorHelper.FromArgb(255, 30, 30, 30);

        var dimTextColor = _isDarkTaskbar
            ? ColorHelper.FromArgb(180, 255, 255, 255)
            : ColorHelper.FromArgb(180, 30, 30, 30);

        var barTrackBrush = new WinMedia.SolidColorBrush(
            _isDarkTaskbar
                ? ColorHelper.FromArgb(60, 255, 255, 255)
                : ColorHelper.FromArgb(60, 0, 0, 0));

        var rootGrid = new WinControls.Grid
        {
            Background = new WinMedia.SolidColorBrush(bgColor),
            Padding = new MUX.Thickness(8, 2, 8, 2),
            HorizontalAlignment = MUX.HorizontalAlignment.Right,
            VerticalAlignment = MUX.VerticalAlignment.Center,
            ColumnSpacing = 12,
        };
        // 3 columns
        rootGrid.ColumnDefinitions.Add(new WinControls.ColumnDefinition { Width = MUX.GridLength.Auto }); // Col 0: icon+name
        rootGrid.ColumnDefinitions.Add(new WinControls.ColumnDefinition { Width = MUX.GridLength.Auto }); // Col 1: session (Claude only)
        rootGrid.ColumnDefinitions.Add(new WinControls.ColumnDefinition { Width = MUX.GridLength.Auto }); // Col 2: weekly

        // ══════ Column 0: Icon + Provider Name (vertical stack) ══════
        _iconImage = new WinControls.Image
        {
            Width = 20,
            Height = 20,
            HorizontalAlignment = MUX.HorizontalAlignment.Center,
        };
        _providerText = new WinControls.TextBlock
        {
            FontSize = 9,
            Foreground = new WinMedia.SolidColorBrush(dimTextColor),
            HorizontalAlignment = MUX.HorizontalAlignment.Center,
        };

        var col0 = new WinControls.StackPanel
        {
            Orientation = WinControls.Orientation.Vertical,
            VerticalAlignment = MUX.VerticalAlignment.Center,
            Spacing = 1,
            Children = { _iconImage, _providerText },
        };
        WinControls.Grid.SetColumn(col0, 0);
        rootGrid.Children.Add(col0);

        // ══════ Column 1: Session (Claude 5h) ══════
        _sessionLabel = new WinControls.TextBlock
        {
            Text = "Session",
            FontSize = 8,
            Foreground = new WinMedia.SolidColorBrush(dimTextColor),
            Visibility = MUX.Visibility.Collapsed,
        };
        _sessionProgressBar = new WinControls.ProgressBar
        {
            Minimum = 0,
            Maximum = 100,
            Height = 4,
            Width = 50,
            VerticalAlignment = MUX.VerticalAlignment.Center,
            CornerRadius = new MUX.CornerRadius(2),
            Background = barTrackBrush,
            Visibility = MUX.Visibility.Collapsed,
        };
        _sessionPercentText = new WinControls.TextBlock
        {
            FontSize = 10,
            FontWeight = Microsoft.UI.Text.FontWeights.Bold,
            VerticalAlignment = MUX.VerticalAlignment.Center,
            Visibility = MUX.Visibility.Collapsed,
        };
        var sessionResetText = new WinControls.TextBlock
        {
            FontSize = 8,
            Foreground = new WinMedia.SolidColorBrush(dimTextColor),
            VerticalAlignment = MUX.VerticalAlignment.Center,
            Visibility = MUX.Visibility.Collapsed,
        };
        _sessionResetText = sessionResetText;

        var sessionBarRow = new WinControls.StackPanel
        {
            Orientation = WinControls.Orientation.Horizontal,
            Spacing = 3,
            Children = { _sessionProgressBar, _sessionPercentText, sessionResetText },
        };
        var col1 = new WinControls.StackPanel
        {
            Orientation = WinControls.Orientation.Vertical,
            VerticalAlignment = MUX.VerticalAlignment.Center,
            Spacing = 1,
            Children = { _sessionLabel, sessionBarRow },
            Visibility = MUX.Visibility.Collapsed,
        };
        _sessionColumn = col1;
        WinControls.Grid.SetColumn(col1, 1);
        rootGrid.Children.Add(col1);

        // ══════ Column 2: Weekly / Total ══════
        var weeklyLabel = new WinControls.TextBlock
        {
            Text = "Weekly",
            FontSize = 8,
            Foreground = new WinMedia.SolidColorBrush(dimTextColor),
        };
        _progressBar = new WinControls.ProgressBar
        {
            Minimum = 0,
            Maximum = 100,
            Height = 4,
            Width = 50,
            VerticalAlignment = MUX.VerticalAlignment.Center,
            CornerRadius = new MUX.CornerRadius(2),
            Background = barTrackBrush,
        };
        _percentText = new WinControls.TextBlock
        {
            FontSize = 10,
            FontWeight = Microsoft.UI.Text.FontWeights.Bold,
            Foreground = new WinMedia.SolidColorBrush(textColor),
            VerticalAlignment = MUX.VerticalAlignment.Center,
        };
        _resetText = new WinControls.TextBlock
        {
            FontSize = 8,
            Foreground = new WinMedia.SolidColorBrush(dimTextColor),
            VerticalAlignment = MUX.VerticalAlignment.Center,
        };

        var weeklyBarRow = new WinControls.StackPanel
        {
            Orientation = WinControls.Orientation.Horizontal,
            Spacing = 3,
            Children = { _progressBar, _percentText, _resetText },
        };
        var col2 = new WinControls.StackPanel
        {
            Orientation = WinControls.Orientation.Vertical,
            VerticalAlignment = MUX.VerticalAlignment.Center,
            Spacing = 1,
            Children = { weeklyLabel, weeklyBarRow },
        };
        WinControls.Grid.SetColumn(col2, 2);
        rootGrid.Children.Add(col2);

        return rootGrid;
    }

    #endregion

    #region Context Menu

    void BuildContextMenu()
    {
        _contextMenu = new WinControls.MenuFlyout();

        // ── Service submenu ──
        var serviceSubMenu = new WinControls.MenuFlyoutSubItem
        {
            Text = "Service",
            Icon = new WinControls.FontIcon { Glyph = "\uE8AB" },
        };

        var copilotItem = new WinControls.MenuFlyoutItem { Text = "GitHub Copilot" };
        copilotItem.Click += (_, _) => SwitchProvider("/ai/githubcopilot");

        var claudeItem = new WinControls.MenuFlyoutItem { Text = "Claude" };
        claudeItem.Click += (_, _) => SwitchProvider("/ai/claude");

        serviceSubMenu.Items.Add(copilotItem);
        serviceSubMenu.Items.Add(claudeItem);
        _contextMenu.Items.Add(serviceSubMenu);

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
        if (_providerText is null) return;

        _providerText.Text = data.ProviderName;
        _resetText!.Text = _isDeskbandMode ? ShortenResetText(data.ResetTimeText) : data.ResetTimeText;
        _percentText!.Text = $"{data.UsedPercent:F0}%";
        _progressBar!.Value = Math.Min(100, data.UsedPercent);

        var color = GetPercentColor(data.UsedPercent);
        var brush = new WinMedia.SolidColorBrush(color);
        _progressBar.Foreground = brush;
        _percentText.Foreground = brush;

        // Claude 5h session column
        if (_isDeskbandMode && data.SessionUsedPercent.HasValue)
        {
            var sessionPct = data.SessionUsedPercent.Value;
            var sessionColor = GetPercentColor(sessionPct);
            var sessionBrush = new WinMedia.SolidColorBrush(sessionColor);

            // Show entire session column
            if (_sessionColumn is not null)
                _sessionColumn.Visibility = MUX.Visibility.Visible;
            if (_sessionLabel is not null)
                _sessionLabel.Visibility = MUX.Visibility.Visible;
            if (_sessionProgressBar is not null)
            {
                _sessionProgressBar.Visibility = MUX.Visibility.Visible;
                _sessionProgressBar.Value = Math.Min(100, sessionPct);
                _sessionProgressBar.Foreground = sessionBrush;
            }
            if (_sessionPercentText is not null)
            {
                _sessionPercentText.Visibility = MUX.Visibility.Visible;
                _sessionPercentText.Text = $"{sessionPct:F0}%";
                _sessionPercentText.Foreground = sessionBrush;
            }
            if (_sessionResetText is not null)
            {
                _sessionResetText.Visibility = MUX.Visibility.Visible;
                _sessionResetText.Text = ShortenResetText(data.SessionResetText ?? "5h");
            }
        }
        else
        {
            // Hide entire session column for non-Claude
            if (_sessionColumn is not null)
                _sessionColumn.Visibility = MUX.Visibility.Collapsed;
        }

        if (!string.IsNullOrEmpty(data.IconFileName) && data.IconFileName != _currentIconFileName)
        {
            _currentIconFileName = data.IconFileName;
            _ = LoadIconAsync(data.IconFileName);
        }
    }

    static global::Windows.UI.Color GetPercentColor(double percent)
    {
        return percent >= 90
            ? ColorHelper.FromArgb(255, 239, 83, 80)    // red
            : percent >= 70
                ? ColorHelper.FromArgb(255, 255, 167, 38)  // orange
                : ColorHelper.FromArgb(255, 76, 175, 80);  // green
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
            string cacheKey = _isDeskbandMode ? $"tinted_{fileName}" : fileName;

            if (!_svgCache.TryGetValue(cacheKey, out var svgBytes))
            {
                using var stream = await FileSystem.OpenAppPackageFileAsync(fileName);
                using var reader = new StreamReader(stream);
                var svgContent = await reader.ReadToEndAsync();

                if (_isDeskbandMode)
                {
                    svgContent = System.Text.RegularExpressions.Regex.Replace(
                        svgContent, @"fill=""[^""]*""", @"fill=""#676780""");
                    svgContent = System.Text.RegularExpressions.Regex.Replace(
                        svgContent, @"fill:[^;""]+", "fill:#676780");
                }

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
        if (_contextMenu is null)
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
    const uint WM_NCHITTEST    = 0x0084;
    const uint WM_EXITSIZEMOVE = 0x0232;
    const int  HTCAPTION       = 2;

    [DllImport("user32.dll")] static extern int GetWindowLong(IntPtr hWnd, int nIndex);
    [DllImport("user32.dll")] static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
    [DllImport("user32.dll")] static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);
    [DllImport("user32.dll")] static extern IntPtr CallWindowProc(IntPtr lpPrevWndFunc, IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr FindWindow(string? lpClassName, string? lpWindowName);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr FindWindowEx(IntPtr hWndParent, IntPtr hWndChildAfter, string? lpszClass, string? lpszWindow);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
    [DllImport("user32.dll")] static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);
    [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);
    [DllImport("gdi32.dll")] static extern IntPtr CreateRectRgn(int nLeftRect, int nTopRect, int nRightRect, int nBottomRect);
    [DllImport("user32.dll")] static extern int SetWindowRgn(IntPtr hWnd, IntPtr hRgn, bool bRedraw);
    [DllImport("user32.dll")] static extern uint GetDpiForWindow(IntPtr hwnd);

    const int DWMWA_BORDER_COLOR = 34;
    [DllImport("dwmapi.dll", PreserveSig = true)]
    static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int pvAttribute, int cbAttribute);

    [StructLayout(LayoutKind.Sequential)]
    struct RECT { public int left, top, right, bottom; }

    #endregion
}
#endif
