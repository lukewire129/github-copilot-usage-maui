using copilot_usage_maui.Platforms.Windows;
using copilot_usage_maui.Services;
using Microsoft.Maui;
using Microsoft.Toolkit.Uwp.Notifications;
using Microsoft.UI.Xaml;

namespace copilot_usage_maui.WinUI
{
    public partial class App : MauiWinUIApplication
    {
        WidgetWindow? _widgetWindow;
        MainWindowService? _mainWindowService;

        public App()
        {
#if WINDOWS
            Environment.SetEnvironmentVariable("MICROSOFT_WINDOWSAPPRUNTIME_BASE_DIRECTORY", AppContext.BaseDirectory);
#endif
            this.InitializeComponent();
        }

        protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();

        protected override void OnLaunched(LaunchActivatedEventArgs args)
        {
            base.OnLaunched(args);

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

                    if (widgetService is not null)
                    {
                        _widgetWindow = new WidgetWindow(widgetService, _mainWindowService);
                        _widgetWindow.Show();

                        // 위젯 HWND를 MainWindowService에 알려줌 (포커스 체크용)
                        if (_mainWindowService is not null)
                            _mainWindowService.WidgetHwnd = _widgetWindow.Hwnd;
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

        void SetupMainWindow()
        {
            Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread().TryEnqueue(() =>
            {
                var mainWindow = Microsoft.Maui.Controls.Application.Current?.Windows.FirstOrDefault();
                if (mainWindow?.Handler?.PlatformView is Microsoft.UI.Xaml.Window mainWinUI)
                {
                    var svc = IPlatformApplication.Current?.Services.GetService<MainWindowService>();
                    svc?.Initialize(mainWinUI);
                }
            });
        }
    }
}
