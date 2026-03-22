#if WINDOWS
using System.Runtime.InteropServices;
using copilot_usage_maui.Helpers;
using copilot_usage_maui.Models;
using copilot_usage_maui.Services;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Windows.Graphics;
using WinRT.Interop;

using MUX = Microsoft.UI.Xaml;
using WinControls = Microsoft.UI.Xaml.Controls;
using WinMedia = Microsoft.UI.Xaml.Media;

namespace copilot_usage_maui.Platforms.Windows;

/// <summary>
/// 위젯 클릭 시 표시되는 310px 팝업 윈도우.
/// Copilot/Claude 탭 전환, 핀 고정, 도넛 차트, 모델 사용량 등 표시.
/// </summary>
public class PopupWindow
{
    readonly WidgetService _widgetService;
    readonly SettingsService? _settingsService;

    MUX.Window? _window;
    AppWindow? _appWindow;
    IntPtr _hwnd;
    MUX.DispatcherTimer? _focusCheckTimer;

    bool _isVisible;
    bool _isAnimating;
    bool _isPinned;
    bool _isDark;
    string _activeTab = "Copilot";

    // Widget HWND for focus check exclusion
    public IntPtr WidgetHwnd { get; set; }

    // ── UI elements ──
    WinControls.Grid? _rootGrid;
    WinControls.Button? _copilotTab;
    WinControls.Button? _claudeTab;
    WinControls.Button? _pinBtn;
    WinControls.TextBlock? _pinNotice;
    WinControls.Border? _statusBanner;
    WinControls.TextBlock? _statusTitle;
    WinControls.TextBlock? _statusSub;

    // Copilot content
    WinControls.StackPanel? _copilotPanel;
    WinControls.Image? _copilotDonut;
    WinControls.TextBlock? _copilotUsageText;
    WinControls.TextBlock? _copilotRemainText;
    WinControls.TextBlock? _copilotTodayText;
    WinControls.TextBlock? _copilotBudgetText;
    WinControls.StackPanel? _copilotModelPanel;

    // Claude content
    WinControls.StackPanel? _claudePanel;
    WinControls.TextBlock? _claudePlanText;
    WinControls.Image? _claudeSessionDonut;
    WinControls.TextBlock? _claudeSessionPct;
    WinControls.TextBlock? _claudeSessionReset;
    WinControls.Image? _claudeWeeklyDonut;
    WinControls.TextBlock? _claudeWeeklyPct;
    WinControls.TextBlock? _claudeWeeklyReset;
    WinControls.TextBlock? _claudeElapsedText;
    WinControls.TextBlock? _claudePaceText;
    WinControls.TextBlock? _claudeCompareText;
    WinControls.TextBlock? _claudeCompareNote;

    // Cached data
    PopupData? _lastData;

    enum PopupDirection { Up, Down }
    PopupDirection _lastDirection = PopupDirection.Up;

    public PopupWindow(WidgetService widgetService, SettingsService? settingsService = null)
    {
        _widgetService = widgetService;
        _settingsService = settingsService;
    }

    public void Initialize()
    {
        _isDark = IsSystemDarkTheme();

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

        // Hide border
        int borderColor = unchecked((int)0xFFFFFFFE);
        DwmSetWindowAttribute(_hwnd, DWMWA_BORDER_COLOR, ref borderColor, sizeof(int));

        // Mica backdrop
        _window.SystemBackdrop = new MUX.Media.MicaBackdrop();

        // Build UI
        var content = BuildContent();
        _window.Content = content;

        // Size: 310 x 480
        _appWindow.Resize(new SizeInt32(310, 480));

        // Start hidden
        _isVisible = false;
        ShowWindow(_hwnd, SW_HIDE);

        // Closing → hide instead
        _appWindow.Closing += (_, e) =>
        {
            e.Cancel = true;
            HideWithAnimation();
        };

        // Subscribe to popup data changes
        _widgetService.PopupDataChanged += OnPopupDataChanged;
        if (_widgetService.PopupCurrent is { } data)
            OnPopupDataChanged(data);

        _window.Activate();
        ShowWindow(_hwnd, SW_HIDE);
    }

    void OnPopupDataChanged(PopupData data)
    {
        _lastData = data;
        _window?.DispatcherQueue?.TryEnqueue(() => UpdateContent(data));
    }

    #region Toggle / Show / Hide

    public void Toggle()
    {
        if (_isAnimating) return;
        if (_window is null) Initialize();

        if (_isVisible)
            HideWithAnimation();
        else
            ShowWithAnimation();
    }

    void ShowWithAnimation()
    {
        if (_hwnd == IntPtr.Zero || _appWindow is null || _isAnimating) return;
        _isAnimating = true;
        _isDark = IsSystemDarkTheme();

        var (targetX, targetY, dir) = ComputePopupPosition();
        _lastDirection = dir;

        var popupH = _appWindow.Size.Height;
        int startY = dir == PopupDirection.Down ? targetY - popupH : targetY + popupH;

        SetWindowPos(_hwnd, IntPtr.Zero, targetX, startY, 0, 0,
            SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE);

        ShowWindow(_hwnd, SW_SHOW);
        SetForegroundWindow(_hwnd);

        const int steps = 12;
        var timer = new MUX.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(12) };
        int step = 0;

        timer.Tick += (_, _) =>
        {
            step++;
            double t = (double)step / steps;
            double ease = 1.0 - Math.Pow(1.0 - t, 3);
            int currentY = startY + (int)((targetY - startY) * ease);
            SetWindowPos(_hwnd, IntPtr.Zero, targetX, currentY, 0, 0,
                SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE);

            if (step >= steps)
            {
                timer.Stop();
                _isVisible = true;
                _isAnimating = false;
                StartFocusWatch();
                if (_lastData is not null) UpdateContent(_lastData);
            }
        };
        timer.Start();
    }

    void HideWithAnimation()
    {
        if (_hwnd == IntPtr.Zero || _appWindow is null || _isAnimating || !_isVisible) return;
        _isAnimating = true;
        StopFocusWatch();

        GetWindowRect(_hwnd, out RECT currentRect);
        int startX = currentRect.left;
        int startY = currentRect.top;
        int popupH = _appWindow.Size.Height;

        int targetY = _lastDirection == PopupDirection.Down
            ? startY - popupH : startY + popupH;

        const int steps = 10;
        var timer = new MUX.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(12) };
        int step = 0;

        timer.Tick += (_, _) =>
        {
            step++;
            double t = (double)step / steps;
            double ease = t * t;
            int currentY = startY + (int)((targetY - startY) * ease);
            SetWindowPos(_hwnd, IntPtr.Zero, startX, currentY, 0, 0,
                SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE);

            if (step >= steps)
            {
                timer.Stop();
                _isVisible = false;
                _isAnimating = false;
                ShowWindow(_hwnd, SW_HIDE);
            }
        };
        timer.Start();
    }

    (int targetX, int targetY, PopupDirection direction) ComputePopupPosition()
    {
        var displayArea = DisplayArea.GetFromWindowId(_appWindow!.Id, DisplayAreaFallback.Primary);
        var screen = displayArea.OuterBounds;
        var workArea = displayArea.WorkArea;
        var popupSize = _appWindow.Size;
        int popupW = popupSize.Width;
        int popupH = popupSize.Height;

        RECT widgetRect;
        if (WidgetHwnd != IntPtr.Zero && GetWindowRect(WidgetHwnd, out widgetRect))
        {
            // OK
        }
        else
        {
            widgetRect.left = workArea.X + workArea.Width - 100;
            widgetRect.right = widgetRect.left + 60;
            widgetRect.top = workArea.Y + workArea.Height - 160;
            widgetRect.bottom = widgetRect.top + 160;
        }

        int widgetCenterX = (widgetRect.left + widgetRect.right) / 2;
        int screenMidY = screen.Y + screen.Height / 2;

        var dir = ((widgetRect.top + widgetRect.bottom) / 2 < screenMidY)
            ? PopupDirection.Down : PopupDirection.Up;

        int targetY = dir == PopupDirection.Down
            ? widgetRect.bottom + 8 : widgetRect.top - popupH - 8;

        int targetX = widgetCenterX - popupW / 2;
        targetX = Math.Max(workArea.X + 8, targetX);
        targetX = Math.Min(workArea.X + workArea.Width - popupW - 8, targetX);

        return (targetX, targetY, dir);
    }

    void StartFocusWatch()
    {
        StopFocusWatch();
        _focusCheckTimer = new MUX.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
        _focusCheckTimer.Tick += (_, _) =>
        {
            if (_isAnimating || _isPinned) return;
            var fg = GetForegroundWindow();
            if (fg != _hwnd && fg != WidgetHwnd && fg != IntPtr.Zero)
                HideWithAnimation();
        };
        _focusCheckTimer.Start();
    }

    void StopFocusWatch()
    {
        _focusCheckTimer?.Stop();
        _focusCheckTimer = null;
    }

    #endregion

    #region UI Build

    MUX.UIElement BuildContent()
    {
        var bg = _isDark ? ColorHelper.FromArgb(255, 30, 30, 30) : ColorHelper.FromArgb(255, 255, 255, 255);
        var borderColor = _isDark ? ColorHelper.FromArgb(255, 68, 68, 68) : ColorHelper.FromArgb(255, 232, 230, 225);

        _rootGrid = new WinControls.Grid
        {
            Background = new WinMedia.SolidColorBrush(bg),
            BorderBrush = new WinMedia.SolidColorBrush(borderColor),
            BorderThickness = new MUX.Thickness(1),
            CornerRadius = new MUX.CornerRadius(14),
            Padding = new MUX.Thickness(16),
        };

        // Single column, auto rows
        _rootGrid.RowDefinitions.Add(new WinControls.RowDefinition { Height = MUX.GridLength.Auto }); // header
        _rootGrid.RowDefinitions.Add(new WinControls.RowDefinition { Height = MUX.GridLength.Auto }); // pin notice
        _rootGrid.RowDefinitions.Add(new WinControls.RowDefinition { Height = MUX.GridLength.Auto }); // status banner
        _rootGrid.RowDefinitions.Add(new WinControls.RowDefinition { Height = new MUX.GridLength(1, MUX.GridUnitType.Star) }); // content

        // ── Row 0: Header (tabs + pin) ──
        var header = BuildHeader();
        WinControls.Grid.SetRow(header, 0);
        _rootGrid.Children.Add(header);

        // ── Row 1: Pin notice ──
        _pinNotice = new WinControls.TextBlock
        {
            Text = AppStrings.IsKoreanStatic
                ? "\ud83d\udccc \uace0\uc815\ub428 \u2014 \ub9c8\uc6b0\uc2a4\ub97c \ubc97\uc5b4\ub098\ub3c4 \ub2eb\ud788\uc9c0 \uc54a\uc2b5\ub2c8\ub2e4"
                : "\ud83d\udccc Pinned \u2014 stays open when you click away",
            FontSize = 10,
            Foreground = new WinMedia.SolidColorBrush(ColorHelper.FromArgb(255, 24, 95, 165)),
            Visibility = MUX.Visibility.Collapsed,
            Margin = new MUX.Thickness(0, 4, 0, 4),
            Padding = new MUX.Thickness(8, 5, 8, 5),
        };
        var pinNoticeBorder = new WinControls.Border
        {
            Background = new WinMedia.SolidColorBrush(ColorHelper.FromArgb(255, 230, 241, 251)),
            CornerRadius = new MUX.CornerRadius(6),
            Child = _pinNotice,
            Visibility = MUX.Visibility.Collapsed,
        };
        WinControls.Grid.SetRow(pinNoticeBorder, 1);
        _rootGrid.Children.Add(pinNoticeBorder);

        // ── Row 2: Status banner ──
        _statusTitle = new WinControls.TextBlock { FontSize = 12, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold };
        _statusSub = new WinControls.TextBlock { FontSize = 10, Margin = new MUX.Thickness(0, 1, 0, 0) };
        var statusDot = new MUX.Shapes.Ellipse { Width = 7, Height = 7, VerticalAlignment = MUX.VerticalAlignment.Top, Margin = new MUX.Thickness(0, 3, 0, 0) };

        var statusTextStack = new WinControls.StackPanel
        {
            Orientation = WinControls.Orientation.Vertical,
            Children = { _statusTitle, _statusSub },
        };
        var statusContent = new WinControls.StackPanel
        {
            Orientation = WinControls.Orientation.Horizontal,
            Spacing = 9,
            Children = { statusDot, statusTextStack },
        };
        _statusBanner = new WinControls.Border
        {
            CornerRadius = new MUX.CornerRadius(8),
            Padding = new MUX.Thickness(12, 9, 12, 9),
            Margin = new MUX.Thickness(0, 8, 0, 8),
            Child = statusContent,
        };
        WinControls.Grid.SetRow(_statusBanner, 2);
        _rootGrid.Children.Add(_statusBanner);

        // ── Row 3: Scrollable content ──
        _copilotPanel = BuildCopilotContent();
        _claudePanel = BuildClaudeContent();
        _claudePanel.Visibility = MUX.Visibility.Collapsed;

        var contentStack = new WinControls.StackPanel
        {
            Orientation = WinControls.Orientation.Vertical,
            Children = { _copilotPanel, _claudePanel },
        };
        var scroll = new WinControls.ScrollViewer
        {
            Content = contentStack,
            VerticalScrollBarVisibility = WinControls.ScrollBarVisibility.Auto,
        };
        WinControls.Grid.SetRow(scroll, 3);
        _rootGrid.Children.Add(scroll);

        return _rootGrid;
    }

    WinControls.Grid BuildHeader()
    {
        var grid = new WinControls.Grid { Margin = new MUX.Thickness(0, 0, 0, 4) };
        grid.ColumnDefinitions.Add(new WinControls.ColumnDefinition { Width = new MUX.GridLength(1, MUX.GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new WinControls.ColumnDefinition { Width = MUX.GridLength.Auto });

        // Tabs
        var tabPanel = new WinControls.StackPanel
        {
            Orientation = WinControls.Orientation.Horizontal,
            Spacing = 5,
        };

        _copilotTab = MakeTabButton("Copilot", true, ColorHelper.FromArgb(255, 110, 64, 201));
        _claudeTab = MakeTabButton("Claude", false, ColorHelper.FromArgb(255, 217, 119, 87));

        _copilotTab.Click += (_, _) => SwitchTab("Copilot");
        _claudeTab.Click += (_, _) => SwitchTab("Claude");

        tabPanel.Children.Add(_copilotTab);
        tabPanel.Children.Add(_claudeTab);
        WinControls.Grid.SetColumn(tabPanel, 0);
        grid.Children.Add(tabPanel);

        // Pin button
        _pinBtn = new WinControls.Button
        {
            Content = new WinControls.FontIcon { Glyph = "\uE718", FontSize = 13 },
            Width = 28,
            Height = 28,
            Padding = new MUX.Thickness(0),
            CornerRadius = new MUX.CornerRadius(7),
            Background = new WinMedia.SolidColorBrush(Microsoft.UI.Colors.Transparent),
        };
        _pinBtn.Click += (_, _) => TogglePin();
        WinControls.Grid.SetColumn(_pinBtn, 1);
        grid.Children.Add(_pinBtn);

        return grid;
    }

    WinControls.Button MakeTabButton(string text, bool active, global::Windows.UI.Color activeColor)
    {
        var textColor = active
            ? Microsoft.UI.Colors.White
            : (_isDark ? ColorHelper.FromArgb(255, 119, 119, 119) : ColorHelper.FromArgb(255, 136, 135, 128));

        // Brand icon: small colored rounded rect with SVG-loaded image
        var iconImage = new WinControls.Image { Width = 14, Height = 14, VerticalAlignment = MUX.VerticalAlignment.Center };
        _ = LoadTabIconAsync(iconImage, text == "Copilot" ? "providericon_copilot.svg" : "providericon_claude.svg");

        var content = new WinControls.StackPanel
        {
            Orientation = WinControls.Orientation.Horizontal,
            Spacing = 6,
            VerticalAlignment = MUX.VerticalAlignment.Center,
            Children =
            {
                new WinControls.TextBlock
                {
                    Text = text,
                    FontSize = 11,
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    Foreground = new WinMedia.SolidColorBrush(textColor),
                    VerticalAlignment = MUX.VerticalAlignment.Center,
                },
                iconImage,
            },
        };

        var btn = new WinControls.Button
        {
            Content = content,
            Padding = new MUX.Thickness(12, 6, 10, 6),
            CornerRadius = new MUX.CornerRadius(8),
            Background = new WinMedia.SolidColorBrush(active ? activeColor : Microsoft.UI.Colors.Transparent),
            BorderThickness = new MUX.Thickness(0),
        };
        return btn;
    }

    async Task LoadTabIconAsync(WinControls.Image imageControl, string fileName)
    {
        try
        {
            using var stream = await Microsoft.Maui.Storage.FileSystem.OpenAppPackageFileAsync(fileName);
            using var reader = new StreamReader(stream);
            var svgContent = await reader.ReadToEndAsync();
            var svgBytes = System.Text.Encoding.UTF8.GetBytes(svgContent);

            _window?.DispatcherQueue?.TryEnqueue(async () =>
            {
                var svgSource = new Microsoft.UI.Xaml.Media.Imaging.SvgImageSource();
                using var ms = new MemoryStream(svgBytes);
                using var ras = ms.AsRandomAccessStream();
                await svgSource.SetSourceAsync(ras);
                imageControl.Source = svgSource;
            });
        }
        catch { }
    }

    void SwitchTab(string tab)
    {
        _activeTab = tab;
        var copilotColor = ColorHelper.FromArgb(255, 110, 64, 201);
        var claudeColor = ColorHelper.FromArgb(255, 217, 119, 87);
        var offColor = _isDark ? ColorHelper.FromArgb(255, 119, 119, 119) : ColorHelper.FromArgb(255, 136, 135, 128);

        UpdateTabStyle(_copilotTab, tab == "Copilot", copilotColor, offColor);
        UpdateTabStyle(_claudeTab, tab == "Claude", claudeColor, offColor);

        if (_copilotPanel is not null)
            _copilotPanel.Visibility = tab == "Copilot" ? MUX.Visibility.Visible : MUX.Visibility.Collapsed;
        if (_claudePanel is not null)
            _claudePanel.Visibility = tab == "Claude" ? MUX.Visibility.Visible : MUX.Visibility.Collapsed;

        if (_lastData is not null) UpdateContent(_lastData);
    }

    void UpdateTabStyle(WinControls.Button? btn, bool active, global::Windows.UI.Color activeColor, global::Windows.UI.Color offColor)
    {
        if (btn is null) return;
        btn.Background = new WinMedia.SolidColorBrush(active ? activeColor : Microsoft.UI.Colors.Transparent);
        // Content is StackPanel { TextBlock, Image }
        if (btn.Content is WinControls.StackPanel sp && sp.Children.Count > 0 && sp.Children[0] is WinControls.TextBlock tb)
            tb.Foreground = new WinMedia.SolidColorBrush(active ? Microsoft.UI.Colors.White : offColor);
    }

    void TogglePin()
    {
        _isPinned = !_isPinned;
        var pinColor = _isPinned
            ? ColorHelper.FromArgb(255, 24, 95, 165) : (_isDark ? ColorHelper.FromArgb(255, 119, 119, 119) : ColorHelper.FromArgb(255, 136, 135, 128));

        if (_pinBtn?.Content is WinControls.FontIcon fi)
            fi.Foreground = new WinMedia.SolidColorBrush(pinColor);
        _pinBtn!.Background = new WinMedia.SolidColorBrush(
            _isPinned ? ColorHelper.FromArgb(255, 230, 241, 251) : Microsoft.UI.Colors.Transparent);

        // Pin notice
        var pinNoticeParent = _rootGrid?.Children.OfType<WinControls.Border>().FirstOrDefault(b => b.Child == _pinNotice);
        if (pinNoticeParent is not null)
            pinNoticeParent.Visibility = _isPinned ? MUX.Visibility.Visible : MUX.Visibility.Collapsed;
        if (_pinNotice is not null)
            _pinNotice.Visibility = _isPinned ? MUX.Visibility.Visible : MUX.Visibility.Collapsed;
    }

    #endregion

    #region Copilot Content

    WinControls.StackPanel BuildCopilotContent()
    {
        var panel = new WinControls.StackPanel { Orientation = WinControls.Orientation.Vertical, Spacing = 8 };

        // ── Monthly usage card ──
        var usageCard = MakeCard();
        var usageHeader = MakeCardHeader(AppStrings.IsKoreanStatic ? "Monthly usage" : "Monthly usage", "Pro", "#EEEDFE", "#3C3489");

        _copilotDonut = new WinControls.Image { Width = 36, Height = 36 };
        _copilotUsageText = new WinControls.TextBlock { FontSize = 22, FontWeight = Microsoft.UI.Text.FontWeights.Bold };
        _copilotRemainText = new WinControls.TextBlock { FontSize = 10, Foreground = DimBrush() };

        var donutInfo = new WinControls.StackPanel
        {
            Orientation = WinControls.Orientation.Vertical,
            VerticalAlignment = MUX.VerticalAlignment.Center,
            Spacing = 2,
            Children = { _copilotUsageText, _copilotRemainText },
        };
        var donutRow = new WinControls.StackPanel
        {
            Orientation = WinControls.Orientation.Horizontal,
            Spacing = 8,
            Margin = new MUX.Thickness(0, 6, 0, 0),
            Children = { _copilotDonut, donutInfo },
        };

        usageCard.Children.Add(usageHeader);
        usageCard.Children.Add(donutRow);
        panel.Children.Add(WrapInBorder(usageCard));

        // ── Today + Daily budget ──
        var miniGrid = new WinControls.Grid { ColumnSpacing = 7 };
        miniGrid.ColumnDefinitions.Add(new WinControls.ColumnDefinition { Width = new MUX.GridLength(1, MUX.GridUnitType.Star) });
        miniGrid.ColumnDefinitions.Add(new WinControls.ColumnDefinition { Width = new MUX.GridLength(1, MUX.GridUnitType.Star) });

        _copilotTodayText = new WinControls.TextBlock { FontSize = 16, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold };
        var todayCard = MakeMiniCard(AppStrings.IsKoreanStatic ? "오늘" : "Today", _copilotTodayText);
        WinControls.Grid.SetColumn(todayCard, 0);

        _copilotBudgetText = new WinControls.TextBlock { FontSize = 16, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold };
        var budgetCard = MakeMiniCard(AppStrings.IsKoreanStatic ? "일 예산" : "Daily budget", _copilotBudgetText);
        WinControls.Grid.SetColumn(budgetCard, 1);

        miniGrid.Children.Add(todayCard);
        miniGrid.Children.Add(budgetCard);
        panel.Children.Add(miniGrid);

        // ── Model usage card ──
        _copilotModelPanel = new WinControls.StackPanel { Orientation = WinControls.Orientation.Vertical, Spacing = 2 };
        var modelCard = MakeCard();
        var modelHeader = new WinControls.TextBlock
        {
            Text = AppStrings.IsKoreanStatic ? "모델 사용량" : "Model usage",
            FontSize = 12,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = TextBrush(),
            Margin = new MUX.Thickness(0, 0, 0, 6),
        };
        modelCard.Children.Add(modelHeader);
        modelCard.Children.Add(_copilotModelPanel);
        panel.Children.Add(WrapInBorder(modelCard));

        return panel;
    }

    #endregion

    #region Claude Content

    WinControls.StackPanel BuildClaudeContent()
    {
        var panel = new WinControls.StackPanel { Orientation = WinControls.Orientation.Vertical, Spacing = 8 };

        // ── Plan card ──
        var planCard = MakeCard();
        var planHeader = MakeCardHeader("Plan", "Pro", "#E1F5EE", "#085041");
        _claudePlanText = new WinControls.TextBlock
        {
            Text = "Claude Code",
            FontSize = 13,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = TextBrush(),
            Margin = new MUX.Thickness(0, 4, 0, 0),
        };
        planCard.Children.Add(planHeader);
        planCard.Children.Add(_claudePlanText);
        panel.Children.Add(WrapInBorder(planCard));

        // ── Session + Weekly donut grid ──
        var donutGrid = new WinControls.Grid { ColumnSpacing = 7 };
        donutGrid.ColumnDefinitions.Add(new WinControls.ColumnDefinition { Width = new MUX.GridLength(1, MUX.GridUnitType.Star) });
        donutGrid.ColumnDefinitions.Add(new WinControls.ColumnDefinition { Width = new MUX.GridLength(1, MUX.GridUnitType.Star) });

        // Session card
        _claudeSessionDonut = new WinControls.Image { Width = 28, Height = 28 };
        _claudeSessionPct = new WinControls.TextBlock { FontSize = 20, FontWeight = Microsoft.UI.Text.FontWeights.Bold };
        _claudeSessionReset = new WinControls.TextBlock { FontSize = 10, Foreground = DimBrush(), Margin = new MUX.Thickness(0, 4, 0, 0) };
        var sessionDonutRow = new WinControls.StackPanel
        {
            Orientation = WinControls.Orientation.Horizontal,
            Spacing = 6,
            Children = { _claudeSessionDonut, _claudeSessionPct },
        };
        var sessionCard = MakeMiniCardComplex("Session (5h)", sessionDonutRow, _claudeSessionReset);
        WinControls.Grid.SetColumn(sessionCard, 0);

        // Weekly card
        _claudeWeeklyDonut = new WinControls.Image { Width = 28, Height = 28 };
        _claudeWeeklyPct = new WinControls.TextBlock { FontSize = 20, FontWeight = Microsoft.UI.Text.FontWeights.Bold };
        _claudeWeeklyReset = new WinControls.TextBlock { FontSize = 10, Foreground = DimBrush(), Margin = new MUX.Thickness(0, 4, 0, 0) };
        var weeklyDonutRow = new WinControls.StackPanel
        {
            Orientation = WinControls.Orientation.Horizontal,
            Spacing = 6,
            Children = { _claudeWeeklyDonut, _claudeWeeklyPct },
        };
        var weeklyCard = MakeMiniCardComplex("Weekly (7d)", weeklyDonutRow, _claudeWeeklyReset);
        WinControls.Grid.SetColumn(weeklyCard, 1);

        donutGrid.Children.Add(sessionCard);
        donutGrid.Children.Add(weeklyCard);
        panel.Children.Add(donutGrid);

        // ── Usage detail card ──
        var detailCard = MakeCard();
        var detailHeader = new WinControls.TextBlock
        {
            Text = AppStrings.IsKoreanStatic ? "사용 상세" : "Usage details",
            FontSize = 12,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = TextBrush(),
            Margin = new MUX.Thickness(0, 0, 0, 6),
        };

        _claudeElapsedText = new WinControls.TextBlock { FontSize = 11 };
        _claudePaceText = new WinControls.TextBlock { FontSize = 11 };
        _claudeCompareText = new WinControls.TextBlock { FontSize = 11 };
        _claudeCompareNote = new WinControls.TextBlock { FontSize = 10, Foreground = DimBrush(), Margin = new MUX.Thickness(0, 2, 0, 0) };

        var elapsedRow = MakeDetailRow(AppStrings.IsKoreanStatic ? "기간 경과율" : "Period elapsed", _claudeElapsedText);
        var paceRow = MakeDetailRow(AppStrings.IsKoreanStatic ? "소비 속도" : "Usage pace", _claudePaceText);
        var compareRow = MakeDetailRow(AppStrings.IsKoreanStatic ? "사용량 vs 경과" : "Usage vs elapsed", _claudeCompareText);

        detailCard.Children.Add(detailHeader);
        detailCard.Children.Add(elapsedRow);
        detailCard.Children.Add(paceRow);
        detailCard.Children.Add(new WinControls.Border
        {
            BorderBrush = new WinMedia.SolidColorBrush(_isDark ? ColorHelper.FromArgb(255, 68, 68, 68) : ColorHelper.FromArgb(255, 232, 230, 225)),
            BorderThickness = new MUX.Thickness(0, 1, 0, 0),
            Margin = new MUX.Thickness(0, 6, 0, 6),
            Padding = new MUX.Thickness(0, 6, 0, 0),
            Child = new WinControls.StackPanel
            {
                Children = { compareRow, _claudeCompareNote },
            },
        });
        panel.Children.Add(WrapInBorder(detailCard));

        return panel;
    }

    #endregion

    #region Update Content

    void UpdateContent(PopupData data)
    {
        if (_activeTab == "Copilot")
            UpdateCopilotContent(data.CopilotSummary);
        else
            UpdateClaudeContent(data.ClaudeSnapshot);
    }

    void UpdateCopilotContent(UsageSummary? summary)
    {
        if (summary is null) return;

        // Status banner
        var pct = summary.PercentConsumed;
        UpdateStatusBanner(pct,
            pct >= 80
                ? (AppStrings.IsKoreanStatic ? $"한도 초과 우려 · 예상 {summary.MtdUsed + (summary.AvgDailyUsage * summary.DaysRemaining):F0} req" : $"Over quota risk · projected {summary.MtdUsed + (summary.AvgDailyUsage * summary.DaysRemaining):F0} req")
                : (AppStrings.IsKoreanStatic ? $"한도 이내 · 예상 {summary.MtdUsed + (summary.AvgDailyUsage * summary.DaysRemaining):F0} req" : $"Within quota · projected {summary.MtdUsed + (summary.AvgDailyUsage * summary.DaysRemaining):F0} req"),
            AppStrings.IsKoreanStatic ? $"{summary.DaysRemaining}일 남음" : $"{summary.DaysRemaining} days left");

        // Donut
        _ = RenderDonutAsync(_copilotDonut, 36, 4f, pct, _isDark);

        // Usage text
        _copilotUsageText!.Text = $"{summary.MtdUsed:F0}";
        _copilotUsageText.Foreground = TextBrush();
        _copilotRemainText!.Text = $"/ {summary.Quota} req · {summary.Remaining:F0} req {(AppStrings.IsKoreanStatic ? "남음" : "left")}";

        // Today & Budget
        _copilotTodayText!.Text = $"{summary.TodayUsed:F0} req";
        _copilotTodayText.Foreground = TextBrush();
        _copilotBudgetText!.Text = $"{summary.AvgDailyUsage:F1} / day";
        _copilotBudgetText.Foreground = TextBrush();

        // Model breakdown
        UpdateModelBreakdown(summary.ModelBreakdown);
    }

    void UpdateClaudeContent(ClaudeUsageSnapshot? snapshot)
    {
        if (snapshot is null) return;

        var session = snapshot.SessionWindow;
        var weekly = snapshot.WeeklyWindow;
        double maxPct = Math.Max(session?.UsedPercent ?? 0, weekly?.UsedPercent ?? 0);
        double projected = weekly?.ProjectedFinalPercent ?? session?.ProjectedFinalPercent ?? 0;

        // Status banner
        UpdateStatusBanner(maxPct,
            projected >= 100
                ? (AppStrings.IsKoreanStatic ? $"예상 사용량 ~{projected:F0}%" : $"Projected usage ~{projected:F0}%")
                : maxPct >= 80
                    ? (AppStrings.IsKoreanStatic ? "사용량 주의" : "Usage warning")
                    : (AppStrings.IsKoreanStatic ? "정상 범위" : "Within limits"),
            projected >= 100
                ? (AppStrings.IsKoreanStatic ? "사용 속도를 줄이는 것을 권장합니다" : "Consider reducing usage pace")
                : "");

        // Plan
        _claudePlanText!.Text = $"Claude Code — {snapshot.Plan ?? "Pro"}";

        // Session donut
        if (session is not null)
        {
            _ = RenderDonutAsync(_claudeSessionDonut, 28, 3.5f, session.UsedPercent, _isDark);
            var sc = DonutRenderer.GetStatusWinColor(session.UsedPercent);
            _claudeSessionPct!.Text = $"{session.UsedPercent:F0}%";
            _claudeSessionPct.Foreground = new WinMedia.SolidColorBrush(sc);
            _claudeSessionReset!.Text = session.TimeUntilReset is { } str && str > TimeSpan.Zero
                ? (AppStrings.IsKoreanStatic ? $"{FormatTimeSpan(str)} 후 리셋" : $"Resets in {FormatTimeSpan(str)}")
                : (AppStrings.IsKoreanStatic ? "곧 리셋" : "Resets soon");
        }

        // Weekly donut
        if (weekly is not null)
        {
            _ = RenderDonutAsync(_claudeWeeklyDonut, 28, 3.5f, weekly.UsedPercent, _isDark);
            var wc = DonutRenderer.GetStatusWinColor(weekly.UsedPercent);
            _claudeWeeklyPct!.Text = $"{weekly.UsedPercent:F0}%";
            _claudeWeeklyPct.Foreground = new WinMedia.SolidColorBrush(wc);
            _claudeWeeklyReset!.Text = weekly.TimeUntilReset is { } wtr && wtr > TimeSpan.Zero
                ? (AppStrings.IsKoreanStatic ? $"{FormatTimeSpan(wtr)} 후 리셋" : $"Resets in {FormatTimeSpan(wtr)}")
                : "";
        }

        // Usage detail
        double elapsed = weekly?.ElapsedRatio ?? session?.ElapsedRatio ?? 0;
        double usedPct = weekly?.UsedPercent ?? session?.UsedPercent ?? 0;
        double elapsedPct = elapsed * 100;

        _claudeElapsedText!.Text = $"{elapsedPct:F0}%";
        _claudeElapsedText.Foreground = TextBrush();

        // Pace badge
        string paceLabel = projected >= 100
            ? (AppStrings.IsKoreanStatic ? "빠름" : "Fast")
            : projected >= 80
                ? (AppStrings.IsKoreanStatic ? "주의" : "Caution")
                : (AppStrings.IsKoreanStatic ? "정상" : "Normal");
        var paceColor = DonutRenderer.GetStatusWinColor(projected >= 100 ? 80 : projected >= 80 ? 60 : 0);
        _claudePaceText!.Text = paceLabel;
        _claudePaceText.Foreground = new WinMedia.SolidColorBrush(paceColor);
        _claudePaceText.FontWeight = Microsoft.UI.Text.FontWeights.SemiBold;

        var compareColor = DonutRenderer.GetStatusWinColor(usedPct);
        _claudeCompareText!.Text = $"{usedPct:F0}% / {elapsedPct:F0}%";
        _claudeCompareText.Foreground = new WinMedia.SolidColorBrush(compareColor);
        _claudeCompareText.FontWeight = Microsoft.UI.Text.FontWeights.SemiBold;

        double diff = usedPct - elapsedPct;
        _claudeCompareNote!.Text = diff > 0
            ? (AppStrings.IsKoreanStatic ? $"사용량이 경과율보다 {diff:F0}%p 앞서 있음" : $"Usage is {diff:F0}%p ahead of elapsed")
            : (AppStrings.IsKoreanStatic ? "정상 범위" : "Within range");
    }

    void UpdateStatusBanner(double percent, string title, string subtitle)
    {
        if (_statusBanner is null || _statusTitle is null || _statusSub is null) return;

        global::Windows.UI.Color bgColor, borderColor, titleColor, subColor, dotColor;
        if (percent >= 80)
        {
            bgColor = _isDark ? ColorHelper.FromArgb(255, 61, 20, 20) : ColorHelper.FromArgb(255, 252, 235, 235);
            borderColor = _isDark ? ColorHelper.FromArgb(255, 226, 75, 74) : ColorHelper.FromArgb(255, 240, 149, 149);
            titleColor = _isDark ? ColorHelper.FromArgb(255, 240, 149, 149) : ColorHelper.FromArgb(255, 121, 31, 31);
            subColor = _isDark ? ColorHelper.FromArgb(255, 224, 112, 112) : ColorHelper.FromArgb(255, 163, 45, 45);
            dotColor = ColorHelper.FromArgb(255, 226, 75, 74);
        }
        else if (percent >= 60)
        {
            bgColor = _isDark ? ColorHelper.FromArgb(255, 61, 42, 8) : ColorHelper.FromArgb(255, 250, 238, 218);
            borderColor = _isDark ? ColorHelper.FromArgb(255, 239, 159, 39) : ColorHelper.FromArgb(255, 239, 159, 39);
            titleColor = _isDark ? ColorHelper.FromArgb(255, 250, 199, 117) : ColorHelper.FromArgb(255, 99, 56, 6);
            subColor = _isDark ? ColorHelper.FromArgb(255, 239, 159, 39) : ColorHelper.FromArgb(255, 99, 56, 6);
            dotColor = ColorHelper.FromArgb(255, 239, 159, 39);
        }
        else
        {
            bgColor = _isDark ? ColorHelper.FromArgb(255, 13, 51, 38) : ColorHelper.FromArgb(255, 225, 245, 238);
            borderColor = _isDark ? ColorHelper.FromArgb(255, 29, 158, 117) : ColorHelper.FromArgb(255, 93, 202, 165);
            titleColor = _isDark ? ColorHelper.FromArgb(255, 93, 202, 165) : ColorHelper.FromArgb(255, 8, 80, 65);
            subColor = _isDark ? ColorHelper.FromArgb(255, 58, 171, 138) : ColorHelper.FromArgb(255, 15, 110, 86);
            dotColor = ColorHelper.FromArgb(255, 29, 158, 117);
        }

        _statusBanner.Background = new WinMedia.SolidColorBrush(bgColor);
        _statusBanner.BorderBrush = new WinMedia.SolidColorBrush(borderColor);
        _statusBanner.BorderThickness = new MUX.Thickness(1);
        _statusTitle.Text = title;
        _statusTitle.Foreground = new WinMedia.SolidColorBrush(titleColor);
        _statusSub.Text = subtitle;
        _statusSub.Foreground = new WinMedia.SolidColorBrush(subColor);

        // Update dot color
        if (_statusBanner.Child is WinControls.StackPanel sp && sp.Children[0] is MUX.Shapes.Ellipse dot)
            dot.Fill = new WinMedia.SolidColorBrush(dotColor);
    }

    void UpdateModelBreakdown(Dictionary<string, double> models)
    {
        if (_copilotModelPanel is null) return;
        _copilotModelPanel.Children.Clear();

        if (models.Count == 0) return;

        var sorted = models.OrderByDescending(kv => kv.Value).ToList();
        var total = sorted.Sum(kv => kv.Value);
        var colors = new[] { "#7F77DD", "#5DCAA5", "#D85A30", "#EF9F27", "#D4537E", "#378ADD" };

        // Stacked bar
        var barGrid = new WinControls.Grid
        {
            Height = 5,
            CornerRadius = new MUX.CornerRadius(3),
            Margin = new MUX.Thickness(0, 0, 0, 6),
        };
        for (int i = 0; i < sorted.Count && i < 6; i++)
        {
            var pct = total > 0 ? sorted[i].Value / total * 100 : 0;
            barGrid.ColumnDefinitions.Add(new WinControls.ColumnDefinition
            {
                Width = new MUX.GridLength(pct, MUX.GridUnitType.Star)
            });
            var rect = new MUX.Shapes.Rectangle
            {
                Fill = new WinMedia.SolidColorBrush(ParseColor(colors[i % colors.Length])),
                RadiusX = i == 0 ? 3 : 0,
                RadiusY = i == 0 ? 3 : 0,
            };
            WinControls.Grid.SetColumn(rect, i);
            barGrid.Children.Add(rect);
        }
        _copilotModelPanel.Children.Add(barGrid);

        // Top 3 rows
        int shown = Math.Min(3, sorted.Count);
        for (int i = 0; i < shown; i++)
        {
            var kv = sorted[i];
            var pct = total > 0 ? kv.Value / total * 100 : 0;
            var row = MakeModelRow(kv.Key, $"{kv.Value:F0} ({pct:F0}%)", colors[i % colors.Length]);
            _copilotModelPanel.Children.Add(row);
        }

        // "기타 N개" expandable
        if (sorted.Count > 3)
        {
            var rest = sorted.Skip(3).ToList();
            var restTotal = rest.Sum(kv => kv.Value);
            var restPct = total > 0 ? restTotal / total * 100 : 0;

            var expandPanel = new WinControls.StackPanel
            {
                Orientation = WinControls.Orientation.Vertical,
                Visibility = MUX.Visibility.Collapsed,
            };
            foreach (var kv in rest)
            {
                var p = total > 0 ? kv.Value / total * 100 : 0;
                var r = new WinControls.Grid { Padding = new MUX.Thickness(14, 2, 0, 2) };
                r.ColumnDefinitions.Add(new WinControls.ColumnDefinition { Width = new MUX.GridLength(1, MUX.GridUnitType.Star) });
                r.ColumnDefinitions.Add(new WinControls.ColumnDefinition { Width = MUX.GridLength.Auto });
                var nameBlock = new WinControls.TextBlock { Text = kv.Key, FontSize = 10, Foreground = DimBrush() };
                var valBlock = new WinControls.TextBlock { Text = $"{kv.Value:F0} ({p:F0}%)", FontSize = 10, Foreground = DimBrush() };
                WinControls.Grid.SetColumn(nameBlock, 0);
                WinControls.Grid.SetColumn(valBlock, 1);
                r.Children.Add(nameBlock);
                r.Children.Add(valBlock);
                expandPanel.Children.Add(r);
            }

            var expandBtn = new WinControls.Button
            {
                Background = new WinMedia.SolidColorBrush(Microsoft.UI.Colors.Transparent),
                BorderThickness = new MUX.Thickness(0),
                Padding = new MUX.Thickness(0, 2, 0, 2),
                HorizontalAlignment = MUX.HorizontalAlignment.Stretch,
                HorizontalContentAlignment = MUX.HorizontalAlignment.Stretch,
            };
            var expandContent = new WinControls.Grid();
            expandContent.ColumnDefinitions.Add(new WinControls.ColumnDefinition { Width = new MUX.GridLength(1, MUX.GridUnitType.Star) });
            expandContent.ColumnDefinitions.Add(new WinControls.ColumnDefinition { Width = MUX.GridLength.Auto });

            var labelText = AppStrings.IsKoreanStatic ? $"기타 {rest.Count}개" : $"{rest.Count} more";
            var expandLabel = new WinControls.TextBlock
            {
                Text = $"▼ {labelText}",
                FontSize = 11,
                Foreground = new WinMedia.SolidColorBrush(ColorHelper.FromArgb(255, 24, 95, 165)),
            };
            var expandVal = new WinControls.TextBlock
            {
                Text = $"{restTotal:F0} ({restPct:F0}%)",
                FontSize = 11,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Foreground = TextBrush(),
            };
            WinControls.Grid.SetColumn(expandLabel, 0);
            WinControls.Grid.SetColumn(expandVal, 1);
            expandContent.Children.Add(expandLabel);
            expandContent.Children.Add(expandVal);
            expandBtn.Content = expandContent;

            bool isExpanded = false;
            expandBtn.Click += (_, _) =>
            {
                isExpanded = !isExpanded;
                expandPanel.Visibility = isExpanded ? MUX.Visibility.Visible : MUX.Visibility.Collapsed;
                expandLabel.Text = isExpanded
                    ? (AppStrings.IsKoreanStatic ? "▲ 접기" : "▲ Collapse")
                    : $"▼ {labelText}";
            };

            _copilotModelPanel.Children.Add(expandBtn);
            _copilotModelPanel.Children.Add(expandPanel);
        }
    }

    #endregion

    #region UI Helpers

    async Task RenderDonutAsync(WinControls.Image? img, int size, float stroke, double pct, bool dark)
    {
        if (img is null) return;
        var track = DonutRenderer.GetTrackColor(dark);
        var fill = DonutRenderer.GetStatusColor(pct, dark);
        using var bitmap = DonutRenderer.Render(size, stroke, pct, track, fill, scale: 2f);
        var bmpImage = await DonutRenderer.ToWinUIImageAsync(bitmap);
        img.Source = bmpImage;
    }

    WinControls.StackPanel MakeCard()
    {
        return new WinControls.StackPanel
        {
            Orientation = WinControls.Orientation.Vertical,
            Padding = new MUX.Thickness(11, 11, 13, 11),
        };
    }

    WinControls.Border WrapInBorder(WinControls.StackPanel card)
    {
        var surfaceColor = _isDark ? ColorHelper.FromArgb(255, 42, 42, 42) : ColorHelper.FromArgb(255, 247, 245, 240);
        return new WinControls.Border
        {
            Background = new WinMedia.SolidColorBrush(surfaceColor),
            CornerRadius = new MUX.CornerRadius(9),
            Child = card,
        };
    }

    WinControls.Grid MakeCardHeader(string label, string badge, string badgeBg, string badgeText)
    {
        var grid = new WinControls.Grid();
        grid.ColumnDefinitions.Add(new WinControls.ColumnDefinition { Width = new MUX.GridLength(1, MUX.GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new WinControls.ColumnDefinition { Width = MUX.GridLength.Auto });

        var labelBlock = new WinControls.TextBlock
        {
            Text = label,
            FontSize = 11,
            Foreground = DimBrush(),
        };
        WinControls.Grid.SetColumn(labelBlock, 0);

        var badgeBlock = new WinControls.TextBlock
        {
            Text = badge,
            FontSize = 10,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = new WinMedia.SolidColorBrush(ParseColor(badgeText)),
        };
        var badgeBorder = new WinControls.Border
        {
            Background = new WinMedia.SolidColorBrush(ParseColor(badgeBg)),
            CornerRadius = new MUX.CornerRadius(5),
            Padding = new MUX.Thickness(8, 2, 8, 2),
            Child = badgeBlock,
        };
        WinControls.Grid.SetColumn(badgeBorder, 1);

        grid.Children.Add(labelBlock);
        grid.Children.Add(badgeBorder);
        return grid;
    }

    WinControls.Border MakeMiniCard(string label, WinControls.TextBlock valueBlock)
    {
        var surfaceColor = _isDark ? ColorHelper.FromArgb(255, 42, 42, 42) : ColorHelper.FromArgb(255, 247, 245, 240);
        var stack = new WinControls.StackPanel
        {
            Orientation = WinControls.Orientation.Vertical,
            Padding = new MUX.Thickness(9, 9, 11, 9),
        };
        stack.Children.Add(new WinControls.TextBlock { Text = label, FontSize = 10, Foreground = DimBrush() });
        stack.Children.Add(valueBlock);
        return new WinControls.Border
        {
            Background = new WinMedia.SolidColorBrush(surfaceColor),
            CornerRadius = new MUX.CornerRadius(9),
            Child = stack,
        };
    }

    WinControls.Border MakeMiniCardComplex(string label, MUX.UIElement content, WinControls.TextBlock footer)
    {
        var surfaceColor = _isDark ? ColorHelper.FromArgb(255, 42, 42, 42) : ColorHelper.FromArgb(255, 247, 245, 240);
        var stack = new WinControls.StackPanel
        {
            Orientation = WinControls.Orientation.Vertical,
            Padding = new MUX.Thickness(9, 9, 11, 9),
            Spacing = 5,
        };
        stack.Children.Add(new WinControls.TextBlock { Text = label, FontSize = 10, Foreground = DimBrush() });
        stack.Children.Add(content);
        stack.Children.Add(footer);
        return new WinControls.Border
        {
            Background = new WinMedia.SolidColorBrush(surfaceColor),
            CornerRadius = new MUX.CornerRadius(9),
            Child = stack,
        };
    }

    WinControls.Grid MakeDetailRow(string label, WinControls.TextBlock valueBlock)
    {
        var grid = new WinControls.Grid { Padding = new MUX.Thickness(0, 4, 0, 4) };
        grid.ColumnDefinitions.Add(new WinControls.ColumnDefinition { Width = new MUX.GridLength(1, MUX.GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new WinControls.ColumnDefinition { Width = MUX.GridLength.Auto });

        var labelBlock = new WinControls.TextBlock { Text = label, FontSize = 11, Foreground = DimBrush() };
        WinControls.Grid.SetColumn(labelBlock, 0);
        WinControls.Grid.SetColumn(valueBlock, 1);
        grid.Children.Add(labelBlock);
        grid.Children.Add(valueBlock);
        return grid;
    }

    WinControls.Grid MakeModelRow(string name, string value, string colorHex)
    {
        var grid = new WinControls.Grid { Padding = new MUX.Thickness(0, 2, 0, 2) };
        grid.ColumnDefinitions.Add(new WinControls.ColumnDefinition { Width = new MUX.GridLength(1, MUX.GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new WinControls.ColumnDefinition { Width = MUX.GridLength.Auto });

        var dot = new MUX.Shapes.Ellipse
        {
            Width = 7, Height = 7,
            Fill = new WinMedia.SolidColorBrush(ParseColor(colorHex)),
            VerticalAlignment = MUX.VerticalAlignment.Center,
            Margin = new MUX.Thickness(0, 0, 5, 0),
        };
        var nameBlock = new WinControls.TextBlock
        {
            Text = name,
            FontSize = 11,
            Foreground = new WinMedia.SolidColorBrush(_isDark ? ColorHelper.FromArgb(255, 170, 170, 170) : ColorHelper.FromArgb(255, 95, 94, 90)),
        };
        var namePanel = new WinControls.StackPanel
        {
            Orientation = WinControls.Orientation.Horizontal,
            Children = { dot, nameBlock },
        };

        var valBlock = new WinControls.TextBlock
        {
            Text = value,
            FontSize = 11,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = TextBrush(),
        };

        WinControls.Grid.SetColumn(namePanel, 0);
        WinControls.Grid.SetColumn(valBlock, 1);
        grid.Children.Add(namePanel);
        grid.Children.Add(valBlock);
        return grid;
    }

    WinMedia.SolidColorBrush TextBrush()
        => new(_isDark ? ColorHelper.FromArgb(255, 232, 232, 232) : ColorHelper.FromArgb(255, 44, 44, 42));

    WinMedia.SolidColorBrush DimBrush()
        => new(_isDark ? ColorHelper.FromArgb(255, 119, 119, 119) : ColorHelper.FromArgb(255, 136, 135, 128));

    static global::Windows.UI.Color ParseColor(string hex)
    {
        hex = hex.TrimStart('#');
        byte r = Convert.ToByte(hex[..2], 16);
        byte g = Convert.ToByte(hex[2..4], 16);
        byte b = Convert.ToByte(hex[4..6], 16);
        return ColorHelper.FromArgb(255, r, g, b);
    }

    static string FormatTimeSpan(TimeSpan ts)
    {
        if (ts.TotalDays >= 1) return $"{(int)ts.TotalDays}d {ts.Hours}h";
        if (ts.TotalHours >= 1) return $"{(int)ts.TotalHours}h {ts.Minutes}m";
        return $"{ts.Minutes}m";
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

    #region Win32 P/Invoke

    const int GWL_EXSTYLE = -20;
    const int WS_EX_TOOLWINDOW = 0x00000080;
    const uint SWP_NOSIZE = 0x0001;
    const uint SWP_NOZORDER = 0x0004;
    const uint SWP_NOACTIVATE = 0x0010;
    const int SW_HIDE = 0;
    const int SW_SHOW = 5;
    const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    const int DWMWCP_ROUND = 2;
    const int DWMWA_BORDER_COLOR = 34;

    [DllImport("user32.dll")] static extern int GetWindowLong(IntPtr hWnd, int nIndex);
    [DllImport("user32.dll")] static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
    [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
    [DllImport("dwmapi.dll", PreserveSig = true)] static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int pvAttribute, int cbAttribute);
    [StructLayout(LayoutKind.Sequential)]
    struct RECT { public int left, top, right, bottom; }

    #endregion
}
#endif
