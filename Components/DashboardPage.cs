using copilot_usage_maui.Models;
using copilot_usage_maui.Services;
#if WINDOWS
using System.Runtime.InteropServices;
#endif

namespace copilot_usage_maui.Components;

class DashboardState
{
    public bool IsLoading { get; set; } = true;
    public string? Error { get; set; }
    public UsageSummary? Summary { get; set; }
    public DateTime LastRefreshed { get; set; }
    public bool IsRefreshingAuth { get; set; }
    public string? AuthRefreshOutput { get; set; }
    public string? AuthDeviceCode { get; set; }
    public bool ShowAuthPanel { get; set; }
}

partial class DashboardPage : Component<DashboardState>
{
#if WINDOWS
    [DllImport("user32.dll", SetLastError = true)]
    static extern bool OpenClipboard(IntPtr hWndNewOwner);
    [DllImport("user32.dll", SetLastError = true)]
    static extern bool CloseClipboard();
    [DllImport("user32.dll", SetLastError = true)]
    static extern bool EmptyClipboard();
    [DllImport("user32.dll", SetLastError = true)]
    static extern IntPtr SetClipboardData(uint uFormat, IntPtr hMem);
    [DllImport("kernel32.dll", SetLastError = true)]
    static extern IntPtr GlobalAlloc(uint uFlags, UIntPtr dwBytes);
    [DllImport("kernel32.dll", SetLastError = true)]
    static extern IntPtr GlobalLock(IntPtr hMem);
    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool GlobalUnlock(IntPtr hMem);

    const uint CF_UNICODETEXT = 13;
    const uint GMEM_MOVEABLE = 0x0002;

    static void CopyToClipboard(string text)
    {
        if (!OpenClipboard(IntPtr.Zero))
            return;
        try
        {
            EmptyClipboard();
            var chars = text.ToCharArray();
            int bytes = (chars.Length + 1) * sizeof(char);
            var hGlobal = GlobalAlloc(GMEM_MOVEABLE, (UIntPtr)bytes);
            if (hGlobal == IntPtr.Zero) return;
            var ptr = GlobalLock(hGlobal);
            if (ptr == IntPtr.Zero) return;
            Marshal.Copy(chars, 0, ptr, chars.Length);
            Marshal.WriteInt16(ptr, chars.Length * sizeof(char), 0);
            GlobalUnlock(hGlobal);
            SetClipboardData(CF_UNICODETEXT, hGlobal);
        }
        finally
        {
            CloseClipboard();
        }
    }
#else
    static void CopyToClipboard(string text)
    {
        Microsoft.Maui.ApplicationModel.DataTransfer.Clipboard.Default.SetTextAsync(text).ConfigureAwait(false);
    }
#endif

    Action? _onOpenSettings;
    public DashboardPage OnOpenSettings(Action action)
    {
        _onOpenSettings = action;
        return this;
    }

    protected override async void OnMounted()
    {
        base.OnMounted();
        await LoadData();
    }

    async Task RunAuthRefresh()
    {
        SetState(s => { s.IsRefreshingAuth = true; s.AuthRefreshOutput = null; s.AuthDeviceCode = null; });
        try
        {
            var service = IPlatformApplication.Current!.Services.GetRequiredService<GitHubCopilotService>();

            await Task.Run(async () => await service.RefreshGhAuthAsync(onCodeFound: code =>
                MainThread.BeginInvokeOnMainThread(() =>
                    SetState(s =>
                    {
                        s.AuthDeviceCode = code;
                        s.AuthRefreshOutput = "코드를 복사하고 브라우저에서 입력하세요.";
                        s.IsRefreshingAuth = false;
                    })
                )
            ));

            SetState(s =>
            {
                s.IsRefreshingAuth = false;
                s.AuthDeviceCode = null;
                s.AuthRefreshOutput = "✓ 인증 완료";
            });
        }
        catch (Exception ex)
        {
            SetState(s => { s.AuthRefreshOutput = $"오류: {ex.Message}"; s.IsRefreshingAuth = false; });
        }
    }

    async Task LoadData()
    {
        SetState(s =>
        {
            s.IsLoading = true;
            s.Error = null;
        });

        try
        {
            var settings = IPlatformApplication.Current!.Services.GetRequiredService<SettingsService>();
            var service = IPlatformApplication.Current!.Services.GetRequiredService<GitHubCopilotService>();
            var summary = await service.GetUsageSummaryAsync(settings.MonthsHistory);
            SetState(s =>
            {
                s.Summary = summary;
                s.IsLoading = false;
                s.LastRefreshed = DateTime.Now;
                s.Error = null;
            });
        }
        catch (Exception ex)
        {
            SetState(s =>
            {
                s.Error = ex.Message;
                s.IsLoading = false;
            });
        }
    }

    public override VisualNode Render()
        => ContentPage(
            ScrollView(
                VStack(
                    // Header
                    Grid("Auto", "*, Auto",
                        VStack(
                            Label("GitHub Copilot")
                                .FontSize(20)
                                .FontAttributes(MauiControls.FontAttributes.Bold),
                            Label(DateTime.Today.ToString("yyyy년 MM월 dd일 (ddd)"))
                                .FontSize(12)
                                .TextColor(Colors.Gray)
                        ).Spacing(2),
                        HStack(
                            State.IsLoading
                                ? (VisualNode)ActivityIndicator()
                                    .IsRunning(true)
                                    .WidthRequest(32)
                                    .HeightRequest(32)
                                    .VCenter()
                                : Button("⟳")
                                    .OnClicked(async () => await LoadData())
                                    .BackgroundColor(Colors.Transparent)
                                    .TextColor(Colors.Gray)
                                    .WidthRequest(40),
                            Button("🔑")
                                .OnClicked(() => SetState(s => { s.ShowAuthPanel = !s.ShowAuthPanel; s.AuthRefreshOutput = null; }))
                                .BackgroundColor(Colors.Transparent)
                                .TextColor(State.ShowAuthPanel ? Colors.RoyalBlue : Colors.Gray)
                                .WidthRequest(40),
                            Button("⚙")
                                .OnClicked(() => _onOpenSettings?.Invoke())
                                .BackgroundColor(Colors.Transparent)
                                .TextColor(Colors.Gray)
                                .WidthRequest(40)
                        )
                        .GridColumn(1)
                        .VCenter()
                    ),
                    State.ShowAuthPanel ? RenderAuthPanel() : new Label().HeightRequest(0),
                    RenderBody()
                )
                .Spacing(20)
                .Padding(24, 20)
            )
        );

    VisualNode RenderAuthPanel()
        => Border(
            VStack(
                Grid("Auto", "*, Auto",
                    Label("gh auth refresh").FontSize(13).FontAttributes(MauiControls.FontAttributes.Bold).VCenter(),
                    State.IsRefreshingAuth
                        ? (VisualNode)ActivityIndicator().IsRunning(true).GridColumn(1).VCenter()
                        : Button("실행")
                            .OnClicked(async () => await RunAuthRefresh())
                            .BackgroundColor(Colors.RoyalBlue)
                            .TextColor(Colors.White)
                            .GridColumn(1)
                            .WidthRequest(60)
                ),
                State.AuthRefreshOutput != null
                    ? VStack(
                        Label(State.AuthRefreshOutput)
                            .FontSize(12)
                            .TextColor(Colors.DarkSlateGray),
                        State.AuthDeviceCode != null
                            ? VStack(
                                Label("인증 코드")
                                    .FontSize(11)
                                    .TextColor(Colors.Gray),
                                HStack(
                                    Label(State.AuthDeviceCode)
                                        .FontSize(24)
                                        .FontAttributes(MauiControls.FontAttributes.Bold)
                                        .VCenter(),
                                    Button("복사")
                                        .OnClicked(() =>
                                        {
                                            var code = State.AuthDeviceCode;
                                            if (code != null) CopyToClipboard(code);
                                        })
                                        .BackgroundColor(Colors.LightGray)
                                        .TextColor(Colors.Black)
                                        .WidthRequest(60),
                                    Button("브라우저 열기")
                                        .OnClicked(async () => await Launcher.Default.OpenAsync("https://github.com/login/device"))
                                        .BackgroundColor(Colors.RoyalBlue)
                                        .TextColor(Colors.White)
                                ).Spacing(8)
                            ).Spacing(4)
                            : new Label(),
                        Button("인증 완료 후 새로고침")
                            .OnClicked(async () => { SetState(s => s.ShowAuthPanel = false); await LoadData(); })
                            .BackgroundColor(Colors.Transparent)
                            .TextColor(Colors.RoyalBlue)
                            .HStart()
                    ).Spacing(6)
                    : new Label()
            ).Spacing(8).Padding(12, 10)
        )
        .BackgroundColor(Color.FromArgb("#F5F5F5"))
        .Stroke(Colors.Transparent)
        .StrokeThickness(0)
        .StrokeShape(new MauiReactor.Shapes.RoundRectangle());

    VisualNode RenderBody()
    {
        if (State.IsLoading && State.Summary == null)
            return ActivityIndicator().IsRunning(true).HCenter().VCenter();

        if (State.Error != null)
            return VStack(
                Label("불러오기 실패").FontAttributes(MauiControls.FontAttributes.Bold).HCenter(),
                Label(State.Error).TextColor(Colors.Red).HCenter().HorizontalTextAlignment(TextAlignment.Center),
                Label("인증 문제라면 상단 🔑 버튼을 눌러주세요.").FontSize(12).TextColor(Colors.Gray).HCenter(),
                Button("다시 시도").OnClicked(async () => await LoadData()).HCenter()
            ).Spacing(8).VCenter().HCenter();

        var s = State.Summary!;
        return VStack(
            RenderUsageCard(s),
            RenderModelBreakdown(s),
            Label($"마지막 갱신: {State.LastRefreshed:HH:mm:ss}")
                .FontSize(11)
                .TextColor(Colors.Gray)
                .HEnd()
        )
        .Spacing(20)
        .Opacity(State.IsLoading ? 0.5 : 1.0);
    }

    static VisualNode RenderUsageCard(UsageSummary s)
    {
        var pct = s.PercentConsumed / 100.0;
        var barColor = s.PercentConsumed >= 90 ? Colors.Red
            : s.PercentConsumed >= 70 ? Colors.Orange
            : Colors.ForestGreen;

        int totalDays = s.DaysElapsed + s.DaysRemaining;
        double dailyBudget = s.Quota / (double)totalDays;
        double expectedByToday = dailyBudget * s.DaysElapsed;
        double paceDiff = expectedByToday - s.MtdUsed;
        bool isAhead = paceDiff >= 0;

        return VStack(
            Label("이번 달 사용량")
                .FontSize(11)
                .TextColor(Colors.Gray),
            Label($"{s.MtdUsed:F0} / {s.Quota} req  ({s.PercentConsumed:F1}%)")
                .FontSize(24)
                .FontAttributes(MauiControls.FontAttributes.Bold),
            ProgressBar()
                .Progress(Math.Min(1.0, pct))
                .ProgressColor(barColor)
                .HeightRequest(8),

            Grid("Auto,Auto,Auto,Auto,Auto,Auto", "*,*",
                Label("오늘 사용").FontSize(12).TextColor(Colors.Gray),
                Label($"{s.TodayUsed:F0} req")
                    .FontSize(13).FontAttributes(MauiControls.FontAttributes.Bold).HEnd().GridColumn(1),

                Label("남은 할당량").FontSize(12).TextColor(Colors.Gray).GridRow(1),
                Label($"{s.Remaining:F0} req")
                    .FontSize(13).FontAttributes(MauiControls.FontAttributes.Bold).HEnd().GridColumn(1).GridRow(1),

                Label("권장 일 사용량").FontSize(12).TextColor(Colors.Gray).GridRow(2),
                Label($"{dailyBudget:F1} req/day")
                    .FontSize(13).FontAttributes(MauiControls.FontAttributes.Bold).HEnd().GridColumn(1).GridRow(2),

                Label("현재 페이스").FontSize(12).TextColor(Colors.Gray).GridRow(3),
                Label(isAhead
                        ? $"✓ {paceDiff:F0} req 여유  /  {expectedByToday:F0} req"
                        : $"⚠ {-paceDiff:F0} req 초과  /  {expectedByToday:F0} req")
                    .FontSize(13)
                    .FontAttributes(MauiControls.FontAttributes.Bold)
                    .TextColor(isAhead ? Colors.ForestGreen : Colors.Red)
                    .HEnd().GridColumn(1).GridRow(3),

                Label("이번 달 진행").FontSize(12).TextColor(Colors.Gray).GridRow(4),
                Label($"{s.DaysElapsed}일 경과 / {s.DaysRemaining}일 남음")
                    .FontSize(13).FontAttributes(MauiControls.FontAttributes.Bold).HEnd().GridColumn(1).GridRow(4),

                Label("예상 월말 사용량").FontSize(12).TextColor(Colors.Gray).GridRow(5),
                Label($"{s.AvgDailyUsage * totalDays:F0} req  {(s.ProjectedOverQuota ? "⚠ 초과" : "✓ 여유")}")
                    .FontSize(13)
                    .FontAttributes(MauiControls.FontAttributes.Bold)
                    .TextColor(s.ProjectedOverQuota ? Colors.Red : Colors.ForestGreen)
                    .HEnd().GridColumn(1).GridRow(5)
            )
            .RowSpacing(10),

            s.ProjectedRunOutDate.HasValue
                ? Label($"⚠ 할당량 소진 예상일: {s.ProjectedRunOutDate.Value:MM월 dd일}")
                    .FontSize(13)
                    .TextColor(s.ProjectedOverQuota ? Colors.Red : Colors.Gray)
                : new Label()
        ).Spacing(10);
    }

    static VisualNode RenderModelBreakdown(UsageSummary s)
    {
        if (s.ModelBreakdown.Count == 0)
            return Label("모델별 사용 데이터 없음").TextColor(Colors.Gray).FontSize(13);

        var rows = new List<VisualNode>
        {
            Label("모델별 사용량")
                .FontSize(11)
                .TextColor(Colors.Gray)
        };

        double total = s.ModelBreakdown.Values.Sum();
        foreach (var kv in s.ModelBreakdown.OrderByDescending(x => x.Value))
        {
            double pct = total > 0 ? kv.Value / total * 100 : 0;
            rows.Add(
                Grid("Auto", "*, Auto",
                    Label(kv.Key).FontSize(13).TextColor(Colors.DarkGray).VCenter(),
                    Label($"{kv.Value:F0} req ({pct:F0}%)")
                        .FontSize(13)
                        .FontAttributes(MauiControls.FontAttributes.Bold)
                        .GridColumn(1)
                        .HEnd()
                )
            );
        }

        return VStack([.. rows]).Spacing(8);
    }
}
