using copilot_usage_maui.Features.GithubCopilot.Components;
using copilot_usage_maui.Helpers;
using copilot_usage_maui.Models;
using copilot_usage_maui.Services;
using copilot_usage_maui.Shared.Layouts;
using MauiReactor.Parameters;
using SkiaSharp;
using SkiaSharp.Views.Maui;

namespace copilot_usage_maui.Features.Claude.Pages;

class ClaudeDashBoardPageState
{
    public bool IsLoading { get; set; } = true;
    public string? Error { get; set; }
    public bool IsTokenExpired { get; set; }
    public ClaudeUsageSnapshot? Snapshot { get; set; }
    public DateTime LastRefreshed { get; set; }
    public int AutoRefreshIntervalMs { get; set; }
}

partial class ClaudeDashBoardPage : Component<ClaudeDashBoardPageState>
{
    [Inject] ClaudeUsageService _claudeUsageService;
    [Inject] NotificationService _notificationService;
    [Inject] SettingsService _settingsService;
    [Inject] WidgetService _widgetService;

    [Param] IParameter<MainLayoutState> _providerStateParam;

    bool IsDark => MauiControls.Application.Current?.RequestedTheme == AppTheme.Dark;

    bool _initialized = false;
    protected override async void OnMounted()
    {
        System.Diagnostics.Debug.WriteLine($"[{GetType().Name}] OnMounted called!");
        SettingsService.SetAutoRefreshInterval(OnAutoRefreshIntervalChanged);

        _widgetService.SetRefreshHandler(OnWidgetRefreshRequested);
        if (!_initialized)
        {
            _providerStateParam.Set(p =>
            {
                p.Providers = p.Providers
                    .Select(x => new ProviderState
                    {
                        Name = x.Name,
                        Icon = x.Icon,
                        Url = x.Url,
                        IsSelected = x.Url == "/ai/claude"
                    })
                    .ToArray();
            });
            _initialized = true;
        }

        SetState(s => s.AutoRefreshIntervalMs = SettingsService.GetAutoRefreshIntervalMs(_settingsService.AutoRefreshInterval));
        await LoadData();
    }

    protected override void OnWillUnmount()
    {
        System.Diagnostics.Debug.WriteLine($"[{GetType().Name}] OnWillUnmount called!");
        SettingsService.SetAutoRefreshInterval(null);
        _widgetService.SetRefreshHandler(null);
    }

    async Task OnWidgetRefreshRequested() => await LoadData(forceRefresh: true);

    void OnAutoRefreshIntervalChanged(int intervalMs)
        => SetState(s => s.AutoRefreshIntervalMs = intervalMs);

    async Task LoadData(bool forceRefresh = false)
    {
        SetState(s => { s.IsLoading = true; s.Error = null; s.IsTokenExpired = false; });
        try
        {
            var snapshot = await _claudeUsageService.GetUsageSnapshotAsync(forceRefresh);
            SetState(s =>
            {
                s.Snapshot = snapshot;
                s.IsLoading = false;
                s.LastRefreshed = DateTime.Now;
                s.Error = null;
            });
            _notificationService.CheckAndNotify(snapshot);

            var mostRestrictive = snapshot.MostRestrictive;
            if (mostRestrictive is not null)
            {
                var resetLabel = mostRestrictive.TimeUntilReset is { } tr && tr > TimeSpan.Zero
                    ? AppStrings.ClaudeResetIn(tr) : "";

                var session = snapshot.SessionWindow;
                string? sessionResetText = null;
                if (session?.TimeUntilReset is { } str && str > TimeSpan.Zero)
                    sessionResetText = AppStrings.ClaudeResetIn(str);

                var weekly = snapshot.WeeklyWindow;
                string? weeklyResetText = null;
                if (weekly?.TimeUntilReset is { } wtr && wtr > TimeSpan.Zero)
                    weeklyResetText = AppStrings.ClaudeResetIn(wtr);

                _widgetService.Update(new WidgetData
                {
                    ProviderName = "Claude",
                    IconFileName = "providericon_claude.svg",
                    UsedPercent = mostRestrictive.UsedPercent,
                    ResetTimeText = resetLabel,
                    SessionUsedPercent = session?.UsedPercent,
                    SessionResetText = sessionResetText,
                    WeeklyUsedPercent = weekly?.UsedPercent,
                    WeeklyResetText = weeklyResetText
                });
            }
        }
        catch (ClaudeTokenExpiredException ex)
        {
            SetState(s => { s.Error = ex.Message; s.IsTokenExpired = true; s.IsLoading = false; });
        }
        catch (Exception ex)
        {
            SetState(s => { s.Error = ex.Message; s.IsLoading = false; });
        }
    }

    async void OnReauthViaCli()
    {
        string? claudePath = ClaudeUsageService.FindClaudePath();
        if (claudePath is null)
        {
            await MauiControls.Application.Current!.Windows[0].Page!
                .DisplayAlert("Claude CLI", "Claude CLI를 찾을 수 없습니다.\nPATH에 등록되어 있는지 확인하세요.", "OK");
            return;
        }

        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = claudePath,
            Arguments = "auth login",
            UseShellExecute = true,
        };
        System.Diagnostics.Process.Start(psi);
    }

    public override VisualNode Render()
        => Grid(
            Timer()
                .IsEnabled(!State.IsLoading && State.AutoRefreshIntervalMs > 0)
                .Interval(10_000)
                .OnTick(() =>
                {
                    if (DateTime.Now - State.LastRefreshed >= TimeSpan.FromMilliseconds(State.AutoRefreshIntervalMs))
                        _ = LoadData();
                }),
            ScrollView(
                VStack(
                    RenderBody()
                )
                .Padding(12, 8)
                .Spacing(8)
            )
        );

    VisualNode RenderBody()
    {
        if (State.IsLoading && State.Snapshot is null)
            return ActivityIndicator().IsRunning(true).Center();

        if (State.IsTokenExpired)
            return PopupCard(
                VStack(
                    Label(AppStrings.ClaudeTokenExpired)
                        .FontSize(14).FontAttributes(MauiControls.FontAttributes.Bold)
                        .TextColor(AppColors.StatusWarning).HCenter()
                        .HorizontalTextAlignment(TextAlignment.Center),
                    Label(AppStrings.ClaudeTokenExpiredDesc)
                        .FontSize(11).TextColor(AppColors.PopupText3).HCenter()
                        .HorizontalTextAlignment(TextAlignment.Center),
                    Button(AppStrings.ClaudeReauthCli)
                        .OnClicked(OnReauthViaCli).HCenter(),
                    Button(AppStrings.Retry)
                        .OnClicked(async () => await LoadData(forceRefresh: true))
                        .BackgroundColor(Colors.Transparent)
                        .TextColor(AppColors.PopupText3).HCenter()
                ).Spacing(10).Padding(12, 16)
            );

        if (State.Error is not null)
            return PopupCard(
                VStack(
                    Label(AppStrings.LoadFailed)
                        .FontSize(14).FontAttributes(MauiControls.FontAttributes.Bold)
                        .TextColor(AppColors.PopupText1).HCenter(),
                    Label(State.Error)
                        .TextColor(AppColors.StatusError).HCenter()
                        .HorizontalTextAlignment(TextAlignment.Center).FontSize(12),
                    Label(AppStrings.ClaudeAuthHint)
                        .FontSize(11).TextColor(AppColors.PopupText3).HCenter()
                        .HorizontalTextAlignment(TextAlignment.Center),
                    Button(AppStrings.Retry)
                        .OnClicked(async () => await LoadData(forceRefresh: true)).HCenter()
                ).Spacing(8).Padding(12, 10)
            );

        var snap = State.Snapshot!;
        var mostRestrictive = snap.MostRestrictive;

        return VStack(
            // 상태 배너
            mostRestrictive is not null ? RenderStatusBanner(mostRestrictive) : null,

            // Plan 카드
            RenderPlanCard(snap),

            // Session + Weekly 도넛 그리드
            snap.SessionWindow is not null || snap.WeeklyWindow is not null
                ? RenderDonutGrid(snap)
                : null,

            // 사용 상세 카드
            mostRestrictive is not null ? RenderDetailsCard(snap, mostRestrictive) : null
        ).Spacing(8).Opacity(State.IsLoading ? 0.5 : 1.0);
    }

    VisualNode RenderStatusBanner(ClaudeRateWindow w)
    {
        double projected = w.ProjectedFinalPercent;
        double pct = Math.Max(w.UsedPercent, projected >= 100 ? 80 : w.UsedPercent);
        var bgColor = AppColors.StatusBgForPercent(pct);
        var dotColor = AppColors.StatusColorForPercent(pct);
        var textColor = AppColors.StatusTextForPercent(pct);

        string title, sub;
        if (w.UsedPercent >= 90 || projected >= 100)
        {
            title = AppStrings.IsKoreanStatic
                ? $"예상 사용량 ~{projected:F0}%"
                : $"Projected ~{projected:F0}%";
            sub = AppStrings.IsKoreanStatic
                ? "사용 속도를 줄이는 것을 권장합니다"
                : "Consider slowing down usage";
        }
        else if (projected >= 80)
        {
            title = AppStrings.IsKoreanStatic
                ? $"주의 · 예상 {projected:F0}%"
                : $"Caution · ~{projected:F0}% projected";
            sub = w.TimeUntilReset is { } tr && tr > TimeSpan.Zero
                ? AppStrings.ClaudeResetIn(tr)
                : "";
        }
        else
        {
            title = AppStrings.IsKoreanStatic
                ? "여유롭게 사용 가능"
                : "Usage is on track";
            sub = w.TimeUntilReset is { } tr && tr > TimeSpan.Zero
                ? AppStrings.ClaudeResetIn(tr)
                : "";
        }

        return Border(
            HStack(
                BoxView().WidthRequest(7).HeightRequest(7).CornerRadius(4).Color(dotColor).VStart().Margin(0, 3, 0, 0),
                VStack(
                    Label(title).FontSize(12).FontAttributes(MauiControls.FontAttributes.Bold).TextColor(textColor),
                    sub.Length > 0 ? Label(sub).FontSize(10).TextColor(textColor) : null
                ).Spacing(1)
            ).Spacing(9).Padding(12, 9)
        )
        .BackgroundColor(bgColor)
        .Stroke(dotColor)
        .StrokeThickness(1)
        .StrokeCornerRadius(8);
    }

    VisualNode RenderPlanCard(ClaudeUsageSnapshot snap)
    {
        var planText = snap.Plan ?? "Pro";
        var badgeBg = IsDark ? Color.FromArgb("#085041") : Color.FromArgb("#E1F5EE");
        var badgeFg = IsDark ? Color.FromArgb("#5DCAA5") : Color.FromArgb("#085041");

        return PopupCard(
            VStack(
                Grid(
                    Label("Plan").FontSize(11).TextColor(AppColors.PopupText3).VCenter(),
                    Badge(planText, badgeBg, badgeFg).GridColumn(1)
                ).Columns("*, Auto"),
                Grid(
                    Label("Claude Code").FontSize(13).FontAttributes(MauiControls.FontAttributes.Bold).TextColor(AppColors.PopupText1).VCenter(),
                    Label("Anthropic").FontSize(10).TextColor(AppColors.PopupText3).VCenter().GridColumn(1)
                ).Columns("*, Auto")
            ).Spacing(4).Padding(11, 13)
        );
    }

    VisualNode RenderDonutGrid(ClaudeUsageSnapshot snap)
    {
        return Grid(
            snap.SessionWindow is not null
                ? DonutMiniCard(
                    AppStrings.IsKoreanStatic ? "Session (5h)" : "Session (5h)",
                    snap.SessionWindow,
                    snap.SessionWindow.TimeUntilReset is { } str && str > TimeSpan.Zero
                        ? (str.TotalHours < 1
                            ? (AppStrings.IsKoreanStatic ? "곧 리셋" : "Resets soon")
                            : AppStrings.ClaudeResetIn(str))
                        : "")
                : null,
            snap.WeeklyWindow is not null
                ? DonutMiniCard(
                    AppStrings.IsKoreanStatic ? "Weekly (7d)" : "Weekly (7d)",
                    snap.WeeklyWindow,
                    snap.WeeklyWindow.TimeUntilReset is { } wtr && wtr > TimeSpan.Zero
                        ? AppStrings.ClaudeResetIn(wtr)
                        : "")
                    .GridColumn(1)
                : null
        )
        .Columns("*, *")
        .ColumnSpacing(7);
    }

    Border DonutMiniCard(string title, ClaudeRateWindow window, string resetText)
    {
        var pct = window.UsedPercent;
        var fillColor = AppColors.StatusColorForPercent(pct);

        return Border(
            VStack(
                Label(title).FontSize(10).TextColor(AppColors.PopupText3),
                HStack(
                    new SkiaCanvas()
                        .WidthRequest(28).HeightRequest(28)
                        .OnPaintSurface((sender, e) =>
                        {
                            DonutRenderer.DrawOnCanvas(
                                e.Surface.Canvas,
                                e.Info.Width, e.Info.Height,
                                strokeWidth: 3.5f * (e.Info.Width / 28f),
                                percent: pct,
                                trackColor: DonutRenderer.GetTrackColor(),
                                fillColor: DonutRenderer.GetStatusColor(pct));
                        }),
                    Label($"{pct:F0}%")
                        .FontSize(20).FontAttributes(MauiControls.FontAttributes.Bold)
                        .TextColor(fillColor).VCenter()
                ).Spacing(6),
                resetText.Length > 0
                    ? Label(resetText).FontSize(10).TextColor(AppColors.PopupText3)
                    : null
            ).Spacing(5).Padding(9, 11)
        )
        .BackgroundColor(AppColors.PopupSurface)
        .Stroke(Colors.Transparent)
        .StrokeCornerRadius(9);
    }

    VisualNode RenderDetailsCard(ClaudeUsageSnapshot snap, ClaudeRateWindow w)
    {
        double elapsed = w.ElapsedRatio * 100.0;
        double projected = w.ProjectedFinalPercent;
        bool isFast = w.UsedPercent > elapsed + 5;

        return PopupCard(
            VStack(
                // Header
                Grid(
                    Label(AppStrings.IsKoreanStatic ? "사용 상세" : "Usage details")
                        .FontSize(12).FontAttributes(MauiControls.FontAttributes.Bold).TextColor(AppColors.PopupText1).VCenter(),
                    Label(AppStrings.LastRefreshed(State.LastRefreshed))
                        .FontSize(10).TextColor(AppColors.PopupText3).VCenter().GridColumn(1)
                ).Columns("*, Auto"),

                // 기간 경과율
                DetailRow(
                    AppStrings.IsKoreanStatic ? "기간 경과율" : "Period elapsed",
                    $"{elapsed:F0}%",
                    AppColors.PopupText1),

                // 소비 속도
                Grid(
                    Label(AppStrings.IsKoreanStatic ? "소비 속도" : "Usage pace")
                        .FontSize(11).TextColor(AppColors.PopupText3).VCenter(),
                    Badge(
                        isFast ? AppStrings.ClaudePaceFast : AppStrings.ClaudePaceNormal,
                        isFast ? AppColors.StatusErrorBg : AppColors.StatusSuccessBg,
                        isFast ? AppColors.StatusError : AppColors.StatusSuccess
                    ).GridColumn(1)
                ).Columns("*, Auto"),

                // Divider
                BoxView().HeightRequest(1).Color(AppColors.PopupBorder).Margin(0, 6),

                // 사용량 vs 경과
                Grid(
                    Label(AppStrings.IsKoreanStatic ? "사용량 vs 경과" : "Usage vs elapsed")
                        .FontSize(11).TextColor(AppColors.PopupText3).VCenter(),
                    Label($"{w.UsedPercent:F0}% / {elapsed:F0}%")
                        .FontSize(11).FontAttributes(MauiControls.FontAttributes.Bold)
                        .TextColor(AppColors.StatusError).VCenter().GridColumn(1)
                ).Columns("*, Auto"),

                // 차이 노트
                Label(AppStrings.IsKoreanStatic
                    ? $"사용량이 경과율보다 {w.UsedPercent - elapsed:F0}%p {(w.UsedPercent > elapsed ? "앞서 있음" : "뒤처져 있음")}"
                    : $"Usage is {Math.Abs(w.UsedPercent - elapsed):F0}%p {(w.UsedPercent > elapsed ? "ahead of" : "behind")} elapsed")
                    .FontSize(10).TextColor(AppColors.PopupText3)
            ).Spacing(4).Padding(11, 13)
        );
    }

    static VisualNode DetailRow(string label, string value, Color valueColor)
        => Grid(
            Label(label).FontSize(11).TextColor(AppColors.PopupText3).VCenter(),
            Label(value).FontSize(11).TextColor(valueColor).VCenter().GridColumn(1)
        ).Columns("*, Auto");

    static Border Badge(string text, Color bg, Color fg)
        => Border(
            Label(text).FontSize(10).FontAttributes(MauiControls.FontAttributes.Bold).TextColor(fg)
                .Padding(8, 2)
        )
        .BackgroundColor(bg)
        .Stroke(Colors.Transparent)
        .StrokeCornerRadius(5);

    static VisualNode PopupCard(VisualNode content)
        => Border(content)
            .BackgroundColor(AppColors.PopupSurface)
            .Stroke(Colors.Transparent)
            .StrokeCornerRadius(9);
}
