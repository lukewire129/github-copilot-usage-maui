using copilot_usage_maui.Models;
using copilot_usage_maui.Services;
using copilot_usage_maui.Shared.Layouts;
using MauiReactor.Parameters;
using Microsoft.Maui.Controls;

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

    [Param] IParameter<MainLayoutState> _providerStateParam;

    protected override async void OnMounted()
    {
        base.OnMounted();

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

        SetState(s => s.AutoRefreshIntervalMs = SettingsService.GetAutoRefreshIntervalMs(_settingsService.AutoRefreshInterval));
        SettingsService.AutoRefreshIntervalChanged += OnAutoRefreshIntervalChanged;
        await LoadData();
    }

    protected override void OnWillUnmount()
    {
        SettingsService.AutoRefreshIntervalChanged -= OnAutoRefreshIntervalChanged;
        base.OnWillUnmount();
    }

    void OnAutoRefreshIntervalChanged(object? sender, EventArgs e)
        => SetState(s => s.AutoRefreshIntervalMs = SettingsService.GetAutoRefreshIntervalMs(_settingsService.AutoRefreshInterval));

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
        => ScrollView(
            Grid("auto,*", "*",
                // Header
                Grid("Auto", "*, Auto",
                    VStack(
                        Label("Claude")
                            .FontSize(20)
                            .FontAttributes(MauiControls.FontAttributes.Bold),
                        Label(DateTime.Today.ToString(AppStrings.DateFormat))
                            .FontSize(12)
                            .TextColor(AppColors.TextSecondary)
                    ).Spacing(2),
                    Timer()
                        .IsEnabled(!State.IsLoading && State.AutoRefreshIntervalMs > 0)
                        .Interval(State.AutoRefreshIntervalMs > 0 ? State.AutoRefreshIntervalMs : 60_000)
                        .OnTick(() => _ = LoadData())
                ),
                Grid(RenderBody()).GridRow(1)
            )
            .RowSpacing(20)
            .Padding(24, 20)
        );

    VisualNode RenderBody()
    {
        if (State.IsLoading && State.Snapshot is null)
            return ActivityIndicator().IsRunning(true).Center();

        if (State.IsTokenExpired)
            return SectionCard(
                VStack(
                    Label(AppStrings.ClaudeTokenExpired)
                        .FontSize(15).FontAttributes(MauiControls.FontAttributes.Bold)
                        .TextColor(AppColors.StatusWarning).HCenter()
                        .HorizontalTextAlignment(TextAlignment.Center),
                    Label(AppStrings.ClaudeTokenExpiredDesc)
                        .FontSize(12).TextColor(AppColors.TextSecondary).HCenter()
                        .HorizontalTextAlignment(TextAlignment.Center),
                    Button(AppStrings.ClaudeReauthCli)
                            .OnClicked(OnReauthViaCli)
                            .HCenter(),
                    Button(AppStrings.Retry)
                        .OnClicked(async () => await LoadData(forceRefresh: true))
                        .BackgroundColor(Colors.Transparent)
                        .TextColor(AppColors.TextSecondary)
                        .HCenter()
                ).Spacing(12).Padding(16, 20)
            );

        if (State.Error is not null)
            return SectionCard(
                VStack(
                    HStack(
                        Label(AppStrings.LastRefreshed(State.LastRefreshed))
                            .FontSize(11).VCenter().TextColor(AppColors.TextSecondary),
                        Button("⟳")
                            .OnClicked(async () => await LoadData(forceRefresh: true))
                            .BackgroundColor(Colors.Transparent)
                            .TextColor(AppColors.TextSecondary)
                            .WidthRequest(40).HeightRequest(44)
                    ).HEnd(),
                    Label(AppStrings.LoadFailed)
                        .FontSize(15).FontAttributes(MauiControls.FontAttributes.Bold).HCenter(),
                    Label(State.Error)
                        .TextColor(AppColors.StatusError)
                        .HCenter().HorizontalTextAlignment(TextAlignment.Center).FontSize(13),
                    Label(AppStrings.ClaudeAuthHint)
                        .FontSize(12).TextColor(AppColors.TextSecondary).HCenter()
                        .HorizontalTextAlignment(TextAlignment.Center),
                    Button(AppStrings.Retry)
                        .OnClicked(async () => await LoadData(forceRefresh: true)).HCenter()
                ).Spacing(10).Padding(16, 14)
            );

        var snap = State.Snapshot!;
        return VStack(
            HStack(
                Label(AppStrings.LastRefreshed(State.LastRefreshed))
                    .FontSize(11).VCenter().TextColor(AppColors.TextSecondary),
                Button("⟳")
                    .OnClicked(async () => await LoadData())
                    .BackgroundColor(Colors.Transparent)
                    .TextColor(AppColors.TextSecondary)
                    .WidthRequest(40).HeightRequest(44)
            ).HEnd(),

            // 세션 사용량 카드
            snap.SessionWindow is not null
                ? SectionCard(RenderRateWindowCard(AppStrings.ClaudeSessionUsage, snap.SessionWindow))
                : Label(),

            // 주간 사용량 카드
            snap.WeeklyWindow is not null
                ? SectionCard(RenderRateWindowCard(AppStrings.ClaudeWeeklyUsage, snap.WeeklyWindow))
                : Label(),

            // 컨디션 관리 카드
            snap.MostRestrictive is not null
                ? SectionCard(RenderConditionCard(snap))
                : Label(),

            // 모델별 제한 카드
            snap.ModelWindows.Count > 0
                ? SectionCard(RenderModelWindows(snap.ModelWindows))
                : Label(),

            // 계정 정보 카드
            (snap.Email is not null || snap.Plan is not null)
                ? SectionCard(RenderAccountInfo(snap))
                : Label()
        )
        .Spacing(16)
        .Opacity(State.IsLoading ? 0.5 : 1.0);
    }

    // ─── Rate Window Card ────────────────────────────────────────────────────

    static VisualNode RenderRateWindowCard(string title, ClaudeRateWindow window)
    {
        var barColor = window.UsedPercent >= 90 ? AppColors.StatusError
            : window.UsedPercent >= 70 ? AppColors.StatusWarning
            : AppColors.StatusSuccess;

        var resetLabel = window.TimeUntilReset is { } tr && tr > TimeSpan.Zero
            ? AppStrings.ClaudeResetIn(tr)
            : "";

        return VStack(
            Label(title).FontSize(11).TextColor(AppColors.TextSecondary),
            Label($"{window.UsedPercent:F1}%")
                .FontSize(28)
                .FontAttributes(MauiControls.FontAttributes.Bold)
                .TextColor(barColor),
            ProgressBar()
                .Progress(Math.Min(1.0, window.UsedPercent / 100.0))
                .ProgressColor(barColor)
                .HeightRequest(8),
            resetLabel.Length > 0
                ? Label(resetLabel)
                    .FontSize(12)
                    .TextColor(AppColors.TextSecondary)
                : Label()
        ).Spacing(8).Padding(16, 14);
    }

    // ─── Condition Card ──────────────────────────────────────────────────────

    static VisualNode RenderConditionCard(ClaudeUsageSnapshot snap)
    {
        var w = snap.MostRestrictive!;
        double projected = w.ProjectedFinalPercent;
        double elapsed = w.ElapsedRatio * 100.0;

        // 판정
        bool isImmediateDanger = w.UsedPercent >= 90;
        bool isDanger = isImmediateDanger || projected >= 100;
        bool isWarn = !isDanger && projected >= 80;

        Color statusColor = isDanger ? AppColors.StatusError
            : isWarn ? AppColors.StatusWarning
            : AppColors.StatusSuccess;

        string statusLabel = isDanger ? AppStrings.ClaudeUsageDanger
            : isWarn ? AppStrings.ClaudeUsageWarn
            : AppStrings.ClaudeUsageOk;

        string paceLabel = w.UsedPercent > elapsed + 5 ? AppStrings.ClaudePaceFast
            : AppStrings.ClaudePaceNormal;

        string projectedText = AppStrings.ClaudeProjectedFinal(Math.Min(projected, 200));

        var resetLabel = w.TimeUntilReset is { } tr && tr > TimeSpan.Zero
            ? AppStrings.ClaudeResetIn(tr)
            : "";

        return VStack(
            Label(AppStrings.ClaudeCondition)
                .FontSize(11).TextColor(AppColors.TextSecondary),
            Label(statusLabel)
                .FontSize(15)
                .FontAttributes(MauiControls.FontAttributes.Bold)
                .TextColor(statusColor),

            Grid("Auto,Auto,Auto", "*,*",
                Label(AppStrings.ClaudeElapsed)
                    .FontSize(12).TextColor(AppColors.TextSecondary),
                Label($"{elapsed:F0}%")
                    .FontSize(13).FontAttributes(MauiControls.FontAttributes.Bold)
                    .HEnd().GridColumn(1),

                Label(AppStrings.ClaudePaceLabel)
                    .FontSize(12).TextColor(AppColors.TextSecondary).GridRow(1),
                Label(paceLabel)
                    .FontSize(13).FontAttributes(MauiControls.FontAttributes.Bold)
                    .TextColor(w.UsedPercent > elapsed + 5 ? AppColors.StatusWarning : AppColors.StatusSuccess)
                    .HEnd().GridColumn(1).GridRow(1),

                Label(AppStrings.ClaudeResetCountdown)
                    .FontSize(12).TextColor(AppColors.TextSecondary).GridRow(2),
                Label(resetLabel.Length > 0 ? resetLabel : "-")
                    .FontSize(13).FontAttributes(MauiControls.FontAttributes.Bold)
                    .HEnd().GridColumn(1).GridRow(2)
            ).RowSpacing(10),

            Label(projectedText)
                .FontSize(12)
                .TextColor(statusColor)
        ).Spacing(10).Padding(16, 14);
    }

    // ─── Model Windows Card ──────────────────────────────────────────────────

    static VisualNode RenderModelWindows(Dictionary<string, ClaudeRateWindow> models)
    {
        var rows = new List<VisualNode>
        {
            Label(AppStrings.ClaudeModelLimits)
                .FontSize(11).TextColor(AppColors.TextSecondary)
        };

        foreach (var kv in models.OrderByDescending(x => x.Value.UsedPercent))
        {
            var barColor = kv.Value.UsedPercent >= 90 ? AppColors.StatusError
                : kv.Value.UsedPercent >= 70 ? AppColors.StatusWarning
                : AppColors.StatusSuccess;

            rows.Add(
                VStack(
                    Grid("Auto", "*, Auto",
                        Label(kv.Key).FontSize(13).TextColor(AppColors.TextModelName).VCenter(),
                        Label($"{kv.Value.UsedPercent:F1}%")
                            .FontSize(14).FontAttributes(MauiControls.FontAttributes.Bold)
                            .TextColor(barColor).HEnd().GridColumn(1)
                    ),
                    ProgressBar()
                        .Progress(Math.Min(1.0, kv.Value.UsedPercent / 100.0))
                        .ProgressColor(barColor)
                        .HeightRequest(4)
                ).Spacing(4)
            );
        }

        return VStack([.. rows]).Spacing(12).Padding(16, 14);
    }

    // ─── Account Info Card ───────────────────────────────────────────────────

    static VisualNode RenderAccountInfo(ClaudeUsageSnapshot snap)
        => VStack(
            Label(AppStrings.ClaudeAccountInfo)
                .FontSize(11).TextColor(AppColors.TextSecondary),
            Grid("Auto,Auto", "*,*",
                Label(AppStrings.ClaudeEmail)
                    .FontSize(12).TextColor(AppColors.TextSecondary),
                Label(snap.Email ?? "-")
                    .FontSize(13).FontAttributes(MauiControls.FontAttributes.Bold)
                    .HEnd().GridColumn(1),

                Label(AppStrings.ClaudePlan)
                    .FontSize(12).TextColor(AppColors.TextSecondary).GridRow(1),
                Label(snap.Plan ?? "-")
                    .FontSize(13).FontAttributes(MauiControls.FontAttributes.Bold)
                    .TextColor(AppColors.Accent).HEnd().GridColumn(1).GridRow(1)
            ).RowSpacing(10)
        ).Spacing(10).Padding(16, 14);

    // ─── Helpers ─────────────────────────────────────────────────────────────

    static VisualNode SectionCard(VisualNode content)
        => Border(content)
            .BackgroundColor(AppColors.CardBackground)
            .Stroke(AppColors.DividerColor)
            .StrokeThickness(1)
            .StrokeShape(RoundRectangle());
}
