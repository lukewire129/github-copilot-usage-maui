using copilot_usage_maui.Models;
using Microsoft.Maui.Storage;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Windows.Foundation;
using Windows.Graphics;

using MUX = Microsoft.UI.Xaml;
using WinControls = Microsoft.UI.Xaml.Controls;

namespace copilot_usage_maui.Services;

public class WidgetContextMenuService
{
    readonly WidgetService _widgetService;
    readonly MainWindowService? _mainWindowService;
    readonly SettingsService? _settingsService;

    public event Action<int>? WidgetModeChangeRequested;

    public WidgetContextMenuService(WidgetService widgetService, MainWindowService? mainWindowService, SettingsService? settingsService)
    {
        _widgetService = widgetService;
        _mainWindowService = mainWindowService;
        _settingsService = settingsService;
    }

    public WinControls.MenuFlyout BuildMenu()
    {
        var menu = new WinControls.MenuFlyout();

        var current = _widgetService.Current;
        bool isCopilotActive = current?.ProviderName == "Copilot";

        var copilotItem = new WinControls.MenuFlyoutItem
        {
            Text = isCopilotActive ? "  Copilot  " : "  Copilot",
        };
        copilotItem.Click += (_, _) => SwitchProvider("/ai/githubcopilot");
        menu.Items.Add(copilotItem);

        var claudeItem = new WinControls.MenuFlyoutItem
        {
            Text = !isCopilotActive ? "  Claude  " : "  Claude",
        };
        claudeItem.Click += (_, _) => SwitchProvider("/ai/claude");
        menu.Items.Add(claudeItem);

        menu.Items.Add(new WinControls.MenuFlyoutSeparator());

        var refreshItem = new WinControls.MenuFlyoutItem
        {
            Text = "Refresh",
            Icon = new WinControls.FontIcon { Glyph = "\uE72C" },
        };
        refreshItem.Click += async (_, _) => await _widgetService.RequestRefreshAsync();
        menu.Items.Add(refreshItem);

        menu.Items.Add(new WinControls.MenuFlyoutSeparator());

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
                IsChecked = _settingsService?.WidgetMode == 0,
            };
            deskbandToggle.Click += (_, _) => RequestModeSwitch(0);
            widgetModeSubMenu.Items.Add(deskbandToggle);
        }

        var floatingToggle = new WinControls.ToggleMenuFlyoutItem
        {
            Text = "Floating (Vertical)",
            IsChecked = _settingsService?.WidgetMode == 1,
        };
        floatingToggle.Click += (_, _) => RequestModeSwitch(1);
        widgetModeSubMenu.Items.Add(floatingToggle);

        var hFloatingToggle = new WinControls.ToggleMenuFlyoutItem
        {
            Text = "Floating (Horizontal)",
            IsChecked = _settingsService?.WidgetMode == 2,
        };
        hFloatingToggle.Click += (_, _) => RequestModeSwitch(2);
        widgetModeSubMenu.Items.Add(hFloatingToggle);

        var popupToggle = new WinControls.ToggleMenuFlyoutItem
        {
            Text = "Popup",
            IsChecked = _settingsService?.WidgetMode == 3,
        };
        popupToggle.Click += (_, _) => RequestModeSwitch(3);
        widgetModeSubMenu.Items.Add(popupToggle);

        menu.Items.Add(widgetModeSubMenu);
        menu.Items.Add(new WinControls.MenuFlyoutSeparator());

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
        menu.Items.Add(settingsItem);

        menu.Items.Add(new WinControls.MenuFlyoutSeparator());

        var quitItem = new WinControls.MenuFlyoutItem
        {
            Text = "Quit",
            Icon = new WinControls.FontIcon { Glyph = "\uE711" },
        };
        quitItem.Click += (_, _) =>
        {
            _mainWindowService?.SetQuitting();
            _mainWindowService?.HideWithAnimation();
            Microsoft.Maui.Controls.Application.Current?.Quit();
        };
        menu.Items.Add(quitItem);

        return menu;
    }

    /// <summary>
    /// 스크린 좌표(트레이 아이콘 클릭 위치)를 앵커 윈도우 상대 좌표로 변환해서 메뉴를 표시한다.
    /// </summary>
    public void ShowContextMenuAtScreenPoint(MUX.FrameworkElement anchor, PointInt32 windowScreenPos, int screenX, int screenY)
    {
        var menu = BuildMenu();
        var options = new FlyoutShowOptions
        {
            Position = new Windows.Foundation.Point(screenX - windowScreenPos.X, screenY - windowScreenPos.Y),
            Placement = FlyoutPlacementMode.TopEdgeAlignedLeft,
            ShowMode = FlyoutShowMode.Standard,
        };
        menu.ShowAt(anchor, options);
    }

    public void ShowContextMenuAt(MUX.FrameworkElement anchor, Windows.Foundation.Point? position = null)
    {
        var menu = BuildMenu();
        if (position is not null)
            menu.ShowAt(anchor, position.Value);
        else
            menu.ShowAt(anchor);
    }

    public void ShowContextMenuFromCursor(MUX.FrameworkElement? anchor = null)
    {
        var target = anchor ?? MUX.Window.Current?.Content as MUX.FrameworkElement;
        if (target is null)
            return;

        ShowContextMenuAt(target);
    }

    void RequestModeSwitch(int mode)
    {
        if (_settingsService is not null)
            _settingsService.WidgetMode = mode;

        WidgetModeChangeRequested?.Invoke(mode);
    }

    void SwitchProvider(string url)
    {
        Microsoft.Maui.Controls.Application.Current?.Dispatcher.Dispatch(() =>
        {
            try { ReactorRouter.Navigation.NavigationService.Instance.NavigateTo(url); }
            catch { }
        });
    }

    static bool IsWindows11OrLater()
    {
        var version = Environment.OSVersion.Version;
        return version.Major >= 10 && version.Build >= 22000;
    }
}
