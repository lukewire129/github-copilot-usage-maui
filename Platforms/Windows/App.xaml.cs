using copilot_usage_maui.Platforms.Windows;
using copilot_usage_maui.Services;
using MauiReactor.Integration;
using Microsoft.Maui.Platform;
using Microsoft.Toolkit.Uwp.Notifications;
using Microsoft.UI.Xaml;

namespace copilot_usage_maui.WinUI
{
    public partial class App : MauiWinUIApplication
    {
        WidgetWindow? _widgetWindow;
        MainWindowService? _mainWindowService;
        SettingsService? _settingsService;
        Microsoft.UI.Xaml.Window? _mainWinUI;
        Microsoft.UI.Windowing.AppWindow? _mainAppWindow;
        WidgetContextMenuService? _fallbackContextMenuService;
        WidgetContextMenuService? _trayMenuService;

        public App()
        {
            Environment.SetEnvironmentVariable("MICROSOFT_WINDOWSAPPRUNTIME_BASE_DIRECTORY", AppContext.BaseDirectory);
            this.InitializeComponent();
        }

        protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();

        protected override void OnLaunched(LaunchActivatedEventArgs args)
        {
            try
            {
                base.OnLaunched(args);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($">>> OnLaunched base FAILED: {ex}");
                throw;
            }

            // 메인 윈도우 초기화 (타이틀바 제거, 위치 고정, 닫기→숨기기)
            SetupMainWindow();

            // 위젯 윈도우 생성 (MAUI 앱 초기화 후 약간 딜레이)
            Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread().TryEnqueue(async () =>
            {
                await Task.Delay(1500);
                try
                {
                    var widgetService = IPlatformApplication.Current?.Services.GetService<WidgetService>();
                    _mainWindowService = IPlatformApplication.Current?.Services.GetService<MainWindowService>();
                    _settingsService = IPlatformApplication.Current?.Services.GetService<SettingsService>();

                    _trayMenuService = IPlatformApplication.Current?.Services.GetService<WidgetContextMenuService>();
                    if (_trayMenuService is not null)
                        _trayMenuService.WidgetModeChangeRequested += OnWidgetModeChangeRequested;

                    var trayService = IPlatformApplication.Current?.Services.GetService<ITrayService>();
                    if (trayService is not null)
                    {
                        trayService.RightClickHandler = (screenX, screenY) =>
                        {
                            // 트레이 우클릭 시 커서 위치 기준으로 메뉴 표시 (UI 쓰레드)
                            Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread()?.TryEnqueue(() =>
                            {
                                if (_mainWinUI?.Content is Microsoft.UI.Xaml.FrameworkElement anchor
                                    && _mainAppWindow is not null)
                                {
                                    var svc = _trayMenuService ?? _fallbackContextMenuService;
                                    svc?.ShowContextMenuAtScreenPoint(anchor, _mainAppWindow.Position, screenX, screenY);
                                }
                            });
                        };
                    }

                    if (widgetService is not null)
                    {
                        widgetService.SetWidgetMode(_settingsService?.WidgetMode ?? 0);
                        CreateAndShowWidget(widgetService);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Widget window error: {ex}");
                }
            });

            // unpackaged 앱: 프로세스 종료 시 정리
            AppDomain.CurrentDomain.ProcessExit += (_, _) =>
            {
                try
                {
                    _widgetWindow?.Close();
                    ToastNotificationManagerCompat.Uninstall();
                }
                catch { }
            };
        }

        void CreateAndShowWidget(WidgetService widgetService)
        {
            if (_widgetWindow is not null)
            {
                _widgetWindow.WidgetModeChangeRequested -= OnWidgetModeChangeRequested;
            }
            _widgetWindow?.Close();
            _widgetWindow = null;

            if (_fallbackContextMenuService is not null)
            {
                _fallbackContextMenuService.WidgetModeChangeRequested -= OnWidgetModeChangeRequested;
                _fallbackContextMenuService = null;
            }

            if (widgetService.WidgetType == null)
            {
                _fallbackContextMenuService = new WidgetContextMenuService(widgetService, _mainWindowService, _settingsService);
                _fallbackContextMenuService.WidgetModeChangeRequested += OnWidgetModeChangeRequested;
                _mainWindowService?.ShowWithAnimation();
                _mainWindowService?.TogglePin();
                return;
            }

            _widgetWindow = new WidgetWindow(widgetService, _mainWindowService, _settingsService);
            _widgetWindow.WidgetModeChangeRequested += OnWidgetModeChangeRequested;
            var mauiContext = new MauiContext(this.Services);
            var nativeView = new MauiControls.ContentPage()
            {
                Content = new ComponentHost()
                {
                    Component = widgetService.WidgetType
                },
            }.ToPlatform(mauiContext);
            _widgetWindow.Show(nativeView);

            if (_mainWindowService is not null)
                _mainWindowService.WidgetHwnd = _widgetWindow.Hwnd;
        }

        void OnWidgetModeChangeRequested(int mode)
        {
            Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread()?.TryEnqueue(() =>
            {
                var widgetService = IPlatformApplication.Current?.Services.GetService<WidgetService>();
                if (widgetService is not null)
                {
                    widgetService.SetWidgetMode(mode);
                    CreateAndShowWidget(widgetService);
                }
            });
        }

        void SetupMainWindow()
        {
            Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread().TryEnqueue(() =>
            {
                var mainWindow = Microsoft.Maui.Controls.Application.Current?.Windows.FirstOrDefault();
                if (mainWindow?.Handler?.PlatformView is Microsoft.UI.Xaml.Window mainWinUI)
                {
                    _mainWinUI = mainWinUI;
                    var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(mainWinUI);
                    var winId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
                    _mainAppWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(winId);
                    var svc = IPlatformApplication.Current?.Services.GetService<MainWindowService>();
                    svc?.Initialize(mainWinUI);
                }
            });
        }
    }
}
