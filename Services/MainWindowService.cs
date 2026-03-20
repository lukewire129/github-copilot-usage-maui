#if WINDOWS
using System.Runtime.InteropServices;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Windows.Graphics;
using WinRT.Interop;
using MUX = Microsoft.UI.Xaml;

namespace copilot_usage_maui.Services;

/// <summary>
/// 메인 윈도우 관리 서비스 (타이틀바 제거, 슬라이드 애니메이션, 포커스 잃으면 자동 숨김)
/// MauiReactor 컴포넌트와 WidgetWindow 양쪽에서 사용
/// </summary>
public class MainWindowService
{
    MUX.Window? _mainWinUI;
    IntPtr _hwnd;
    AppWindow? _appWindow;
    MUX.DispatcherTimer? _focusCheckTimer;
    bool _isVisible = true;
    bool _isAnimating;
    bool _isQuitting;

    public bool IsVisible => _isVisible;
    public bool IsQuitting => _isQuitting;

    /// <summary>
    /// 위젯 윈도우의 HWND (포커스 체크 시 제외 대상)
    /// </summary>
    public IntPtr WidgetHwnd { get; set; }

    /// <summary>
    /// 메인 윈도우 초기 설정 (타이틀바 제거, 위치 고정, 닫기 → 숨기기)
    /// App.xaml.cs의 OnLaunched에서 호출
    /// </summary>
    public void Initialize(MUX.Window mainWinUI)
    {
        _mainWinUI = mainWinUI;
        _hwnd = WindowNative.GetWindowHandle(mainWinUI);
        var windowId = Win32Interop.GetWindowIdFromWindow(_hwnd);
        _appWindow = AppWindow.GetFromWindowId(windowId);

        // ── 타이틀바 + 테두리 완전 제거 (borderless popup) ──
        if (_appWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.SetBorderAndTitleBar(false, false);
            presenter.IsResizable = false;
            presenter.IsMinimizable = false;
            presenter.IsMaximizable = false;
        }

        // Win32 스타일에서도 캡션/두꺼운 프레임 제거
        int style = GetWindowLong(_hwnd, GWL_STYLE);
        style &= ~(WS_CAPTION | WS_THICKFRAME);
        SetWindowLong(_hwnd, GWL_STYLE, style);
        SetWindowPos(_hwnd, IntPtr.Zero, 0, 0, 0, 0,
            SWP_NOSIZE | SWP_NOMOVE | SWP_NOZORDER | SWP_NOACTIVATE | SWP_FRAMECHANGED);

        // Fluent 라운드 코너 (8px) - Windows 11+
        int cornerPref = DWMWCP_ROUND; // 라운드 코너
        DwmSetWindowAttribute(_hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref cornerPref, sizeof(int));

        // ── 닫기 → 숨기기 ──
        _appWindow.Closing += (_, e) =>
        {
            if (!_isQuitting)
            {
                e.Cancel = true;
                HideWithAnimation();
            }
        };

        // ── 시작 시 숨김 상태로 (위젯만 보임) ──
        PositionAboveTaskbar();
        _isVisible = false;
        HideFromTaskbar();
    }

    /// <summary>
    /// 작업표시줄 바로 위, 우측 하단에 위치
    /// </summary>
    void PositionAboveTaskbar()
    {
        if (_appWindow is null) return;

        var displayArea = DisplayArea.GetFromWindowId(_appWindow.Id, DisplayAreaFallback.Primary);
        var workArea = displayArea.WorkArea;
        var windowSize = _appWindow.Size;

        int x = workArea.X + workArea.Width - windowSize.Width - 10;
        int y = workArea.Y + workArea.Height - windowSize.Height - 10;
        _appWindow.Move(new PointInt32(x, y));
    }

    /// <summary>
    /// 슬라이드 업 애니메이션과 함께 메인 윈도우 표시
    /// </summary>
    public void ShowWithAnimation()
    {
        if (_hwnd == IntPtr.Zero || _appWindow is null || _isAnimating) return;

        _isAnimating = true;

        // 시작 표시줄에 다시 표시
        RestoreInTaskbar();

        // Restore if minimized
        if (IsIconic(_hwnd))
            ShowWindow(_hwnd, SW_RESTORE);

        // 작업표시줄 위에 위치
        PositionAboveTaskbar();

        var displayArea = DisplayArea.GetFromWindowId(_appWindow.Id, DisplayAreaFallback.Primary);
        var workArea = displayArea.WorkArea;
        var windowSize = _appWindow.Size;

        int targetX = workArea.X + workArea.Width - windowSize.Width - 10;
        int targetY = workArea.Y + workArea.Height - windowSize.Height - 10;
        int startY = workArea.Y + workArea.Height; // 화면 밖(아래)에서 시작

        // 시작 위치로 이동 후 표시
        SetWindowPos(_hwnd, IntPtr.Zero, targetX, startY, 0, 0,
            SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE);

        _appWindow.Show(true);
        SetForegroundWindow(_hwnd);

        // 슬라이드 업 애니메이션
        const int steps = 12;
        var timer = new MUX.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(12) };
        int step = 0;

        timer.Tick += (_, _) =>
        {
            step++;
            double t = (double)step / steps;
            double ease = 1.0 - Math.Pow(1.0 - t, 3); // EaseOutCubic
            int currentY = startY - (int)((startY - targetY) * ease);

            SetWindowPos(_hwnd, IntPtr.Zero, targetX, currentY, 0, 0,
                SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE);

            if (step >= steps)
            {
                timer.Stop();
                _isVisible = true;
                _isAnimating = false;

                // 포커스 감시 시작
                StartFocusWatch();
            }
        };
        timer.Start();
    }

    /// <summary>
    /// 슬라이드 다운 애니메이션과 함께 메인 윈도우 숨김
    /// </summary>
    public void HideWithAnimation()
    {
        if (_hwnd == IntPtr.Zero || _appWindow is null || _isAnimating || !_isVisible) return;

        _isAnimating = true;
        StopFocusWatch();

        var displayArea = DisplayArea.GetFromWindowId(_appWindow.Id, DisplayAreaFallback.Primary);
        var workArea = displayArea.WorkArea;
        var windowSize = _appWindow.Size;

        int startX = workArea.X + workArea.Width - windowSize.Width - 10;
        int startY = workArea.Y + workArea.Height - windowSize.Height - 10;
        int targetY = workArea.Y + workArea.Height; // 화면 밖으로

        const int steps = 10;
        var timer = new MUX.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(12) };
        int step = 0;

        timer.Tick += (_, _) =>
        {
            step++;
            double t = (double)step / steps;
            double ease = t * t; // EaseInQuad
            int currentY = startY + (int)((targetY - startY) * ease);

            SetWindowPos(_hwnd, IntPtr.Zero, startX, currentY, 0, 0,
                SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE);

            if (step >= steps)
            {
                timer.Stop();
                _isVisible = false;
                _isAnimating = false;
                HideFromTaskbar();
            }
        };
        timer.Start();
    }

    /// <summary>
    /// 포커스 이탈 감시 시작 (메인/위젯 윈도우 외 클릭 시 자동 숨김)
    /// </summary>
    void StartFocusWatch()
    {
        StopFocusWatch();

        _focusCheckTimer = new MUX.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
        _focusCheckTimer.Tick += (_, _) =>
        {
            if (_isAnimating) return;

            var fg = GetForegroundWindow();
            if (fg != _hwnd && fg != WidgetHwnd && fg != IntPtr.Zero)
            {
                HideWithAnimation();
            }
        };
        _focusCheckTimer.Start();
    }

    void StopFocusWatch()
    {
        _focusCheckTimer?.Stop();
        _focusCheckTimer = null;
    }

    /// <summary>
    /// 토글: 보이면 숨기고, 숨겨져 있으면 보여줌
    /// </summary>
    public void Toggle()
    {
        if (_isAnimating) return;

        if (_isVisible)
            HideWithAnimation();
        else
            ShowWithAnimation();
    }

    public void SetQuitting()
    {
        _isQuitting = true;
        StopFocusWatch();
    }

    #region Taskbar visibility helpers

    void HideFromTaskbar()
    {
        ShowWindow(_hwnd, SW_HIDE);
        int exStyle = GetWindowLong(_hwnd, GWL_EXSTYLE);
        exStyle |= WS_EX_TOOLWINDOW;
        exStyle &= ~WS_EX_APPWINDOW;
        SetWindowLong(_hwnd, GWL_EXSTYLE, exStyle);
    }

    void RestoreInTaskbar()
    {
        int exStyle = GetWindowLong(_hwnd, GWL_EXSTYLE);
        exStyle &= ~WS_EX_TOOLWINDOW;
        exStyle |= WS_EX_APPWINDOW;
        SetWindowLong(_hwnd, GWL_EXSTYLE, exStyle);
        ShowWindow(_hwnd, SW_SHOW);
    }

    #endregion

    #region Win32 P/Invoke

    const int GWL_STYLE = -16;
    const int GWL_EXSTYLE = -20;
    const int WS_CAPTION = 0x00C00000;
    const int WS_THICKFRAME = 0x00040000;
    const int WS_EX_TOOLWINDOW = 0x00000080;
    const int WS_EX_APPWINDOW = 0x00040000;
    const uint SWP_NOSIZE = 0x0001;
    const uint SWP_NOMOVE = 0x0002;
    const uint SWP_NOZORDER = 0x0004;
    const uint SWP_NOACTIVATE = 0x0010;
    const uint SWP_FRAMECHANGED = 0x0020;
    const int SW_HIDE = 0;
    const int SW_SHOW = 5;
    const int SW_RESTORE = 9;

    // DWM 라운드 코너
    const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    const int DWMWCP_ROUND = 2; // 8px 라운드

    [DllImport("user32.dll")] static extern int GetWindowLong(IntPtr hWnd, int nIndex);
    [DllImport("user32.dll")] static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
    [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    [DllImport("user32.dll")] static extern bool IsIconic(IntPtr hWnd);
    [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);
    [DllImport("dwmapi.dll", PreserveSig = true)] static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int pvAttribute, int cbAttribute);

    #endregion
}
#endif
