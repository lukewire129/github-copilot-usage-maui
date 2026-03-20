using copilot_usage_maui.Platforms.Windows;
using copilot_usage_maui.Services;
using Microsoft.Maui;
using Microsoft.Toolkit.Uwp.Notifications;
using Microsoft.UI.Xaml;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace copilot_usage_maui.WinUI
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public partial class App : MauiWinUIApplication
    {
        WidgetWindow? _widgetWindow;
        bool _isQuitting;

        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
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

            // 메인 창 닫기 → 숨기기 처리 (위젯이 살아있으므로 완전 종료 대신 숨김)
            SetupMainWindowCloseHandler();

            // 위젯 윈도우 생성 (MAUI 앱 초기화 후 약간 딜레이)
            Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread().TryEnqueue(async () =>
            {
                // MAUI DI 컨테이너가 준비될 때까지 대기
                await Task.Delay(1500);
                try
                {
                    var widgetService = IPlatformApplication.Current?.Services.GetService<WidgetService>();
                    if (widgetService is not null)
                    {
                        _widgetWindow = new WidgetWindow(widgetService);
                        _widgetWindow.Show();
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Widget window error: {ex}");
                }
            });

            // unpackaged 앱: 프로세스 종료 시 토스트 등록 정리
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

        void SetupMainWindowCloseHandler()
        {
            Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread().TryEnqueue(async () =>
            {
                var mainWindow = Microsoft.Maui.Controls.Application.Current?.Windows.FirstOrDefault();

                if (mainWindow?.Handler?.PlatformView is Microsoft.UI.Xaml.Window mainWinUI)
                {
                    var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(mainWinUI);
                    var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
                    var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);

                    // 화면 크기 가져오기
                    var displayArea = Microsoft.UI.Windowing.DisplayArea.GetFromWindowId(
                        windowId,
                        Microsoft.UI.Windowing.DisplayAreaFallback.Primary);

                    // 창 크기 가져오기
                    var windowSize = appWindow.Size;

                    int x = displayArea.WorkArea.Width - windowSize.Width;
                    int y = displayArea.WorkArea.Height - windowSize.Height;

                    appWindow.Move(new Windows.Graphics.PointInt32(x, y));

                    appWindow.Closing += (s, e) =>
                    {
                        if (!_isQuitting)
                        {
                            // 닫기 대신 숨기기
                            e.Cancel = true;
                            HideMainWindow(hwnd);
                        }
                    };
                }
            });

        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        const int SW_HIDE = 0;
        const int SW_SHOW = 5;
        const int GWL_EXSTYLE = -20;
        const int WS_EX_TOOLWINDOW = 0x00000080;
        const int WS_EX_APPWINDOW = 0x00040000;

        void HideMainWindow(IntPtr hwnd)
        {
            // 시작 표시줄에서도 완전히 제거
            ShowWindow(hwnd, SW_HIDE);
            int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
            exStyle |= WS_EX_TOOLWINDOW;   // 작업 표시줄에서 숨김
            exStyle &= ~WS_EX_APPWINDOW;   // 앱 표시 제거
            SetWindowLong(hwnd, GWL_EXSTYLE, exStyle);
        }

        internal void RestoreMainWindow(IntPtr hwnd)
        {
            // 시작 표시줄에 다시 표시
            int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
            exStyle &= ~WS_EX_TOOLWINDOW;
            exStyle |= WS_EX_APPWINDOW;
            SetWindowLong(hwnd, GWL_EXSTYLE, exStyle);
            ShowWindow(hwnd, SW_SHOW);
        }

        /// <summary>
        /// Called when the app is truly quitting (via Quit menu or Application.Quit())
        /// </summary>
        internal void SetQuitting()
        {
            _isQuitting = true;
        }
    }
}
