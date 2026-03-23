using copilot_usage_maui.Features.GithubCopilot.Components;
using copilot_usage_maui.Helpers;
using copilot_usage_maui.Models;
using copilot_usage_maui.Services;
using copilot_usage_maui.Shared.Layouts;
using MauiReactor.Parameters;
using SkiaSharp;
using SkiaSharp.Views.Maui;

#if WINDOWS
using System.Runtime.InteropServices;
#endif

namespace copilot_usage_maui.Features.GithubCopilot.Pages;

class GithubDashBoardPageState
{
    public bool IsLoading { get; set; } = true;
    public string? Error { get; set; }
    public UsageSummary? Summary { get; set; }
    public DateTime LastRefreshed { get; set; }
    public bool IsRefreshingAuth { get; set; }
    public string? AuthRefreshOutput { get; set; }
    public string? AuthDeviceCode { get; set; }
    public bool ShowAuthPanel { get; set; }
    public int AutoRefreshIntervalMs { get; set; }
    public bool ShowAllModels { get; set; }
}

partial class GithubDashBoardPage : Component<GithubDashBoardPageState>
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

    [Inject] GitHubCopilotService _gitHubCopilotService;
    [Inject] SettingsService _settingsService;
    [Inject] WidgetService _widgetService;

    [Param] IParameter<MainLayoutState> _providerStateParam;

    static readonly Color[] ModelColors = new[]
    {
        Color.FromArgb("#7F77DD"),
        Color.FromArgb("#5DCAA5"),
        Color.FromArgb("#D85A30"),
        Color.FromArgb("#EF9F27"),
        Color.FromArgb("#D4537E"),
        Color.FromArgb("#378ADD"),
    };

    bool IsDark => MauiControls.Application.Current?.RequestedTheme == AppTheme.Dark;

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
                IsSelected = x.Url == "/ai/githubcopilot"
            })
            .ToArray();
        });

        SetState(s => s.AutoRefreshIntervalMs = SettingsService.GetAutoRefreshIntervalMs(_settingsService.AutoRefreshInterval));
        SettingsService.AutoRefreshIntervalChanged += OnAutoRefreshIntervalChanged;
        _widgetService.RefreshRequested += OnWidgetRefreshRequested;
        await LoadData();
    }

    protected override void OnWillUnmount()
    {
        SettingsService.AutoRefreshIntervalChanged -= OnAutoRefreshIntervalChanged;
        _widgetService.RefreshRequested -= OnWidgetRefreshRequested;
        base.OnWillUnmount();
    }

    async Task OnWidgetRefreshRequested() => await LoadData(forceRefresh: true);

    void OnAutoRefreshIntervalChanged(object? sender, EventArgs e)
        => SetState(s => s.AutoRefreshIntervalMs = SettingsService.GetAutoRefreshIntervalMs(_settingsService.AutoRefreshInterval));

    async Task RunAuthRefresh()
    {
        SetState(s => { s.IsRefreshingAuth = true; s.AuthRefreshOutput = null; s.AuthDeviceCode = null; });
        try
        {
            await Task.Run(async () => await _gitHubCopilotService.RefreshGhAuthAsync(onCodeFound: code =>
                MainThread.BeginInvokeOnMainThread(() =>
                    SetState(s =>
                    {
                        s.AuthDeviceCode = code;
                        s.AuthRefreshOutput = AppStrings.AuthInstruction;
                        s.IsRefreshingAuth = false;
                    })
                )
            ));

            SetState(s =>
            {
                s.IsRefreshingAuth = false;
                s.AuthDeviceCode = null;
                s.AuthRefreshOutput = AppStrings.AuthComplete;
            });
        }
        catch (Exception ex)
        {
            SetState(s => { s.AuthRefreshOutput = AppStrings.AuthError(ex.Message); s.IsRefreshingAuth = false; });
        }
    }

    async Task LoadData(bool forceRefresh = false)
    {
        SetState(s => { s.IsLoading = true; s.Error = null; });

        try
        {
            var summary = await _gitHubCopilotService.GetUsageSummaryAsync(_settingsService.MonthsHistory, forceRefresh);
            SetState(s =>
            {
                s.Summary = summary;
                s.IsLoading = false;
                s.LastRefreshed = DateTime.Now;
                s.Error = null;
            });

            _widgetService.Update(new WidgetData
            {
                ProviderName = "Copilot",
                IconFileName = "providericon_copilot.svg",
                UsedPercent = summary.PercentConsumed,
                ResetTimeText = AppStrings.StatusBarDaysLeft(summary.DaysRemaining)
            });
        }
        catch (Exception ex)
        {
            SetState(s => { s.Error = ex.Message; s.IsLoading = false; });
        }
    }

    public override VisualNode Render()
        => Grid(
            Timer()
                .IsEnabled(!State.IsLoading && State.AutoRefreshIntervalMs > 0)
                .Interval(10_000)
                .OnTick(() =>
                {
                    if (DateTime.Now - State.LastRefreshed >= TimeSpan.FromMilliseconds(State.AutoRefreshIntervalMs))
                        _ = LoadData(forceRefresh: true);
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
        if (State.IsLoading && State.Summary == null)
            return ActivityIndicator().IsRunning(true).Center();

        if (State.Error != null)
            return PopupCard(
                VStack(
                    Label(AppStrings.LoadFailed)
                        .FontSize(14).FontAttributes(MauiControls.FontAttributes.Bold)
                        .TextColor(AppColors.PopupText1).HCenter(),
                    Label(State.Error)
                        .TextColor(AppColors.StatusError).HCenter()
                        .HorizontalTextAlignment(TextAlignment.Center).FontSize(12),
                    Label(AppStrings.AuthHint)
                        .FontSize(11).TextColor(AppColors.PopupText3).HCenter(),
                    Button(AppStrings.Retry)
                        .OnClicked(async () => await LoadData(forceRefresh: true)).HCenter()
                ).Spacing(8).Padding(12, 10)
            );

        var s = State.Summary!;
        int totalDays = s.DaysElapsed + s.DaysRemaining;
        double projected = s.AvgDailyUsage * totalDays;
        double dailyBudget = s.Quota / (double)totalDays;

        return VStack(
            // 상태 배너
            RenderStatusBanner(s, projected),

            // Monthly Usage 카드
            RenderUsageCard(s, dailyBudget),

            // Today + Daily Budget 미니 카드
            Grid(
                MiniCard(AppStrings.TodayUsage, $"{s.TodayUsed:F0} req"),
                MiniCard(AppStrings.DailyBudget, $"{dailyBudget:F1} / day").GridColumn(1)
            )
            .Columns("*, *")
            .ColumnSpacing(7),

            // Model Usage 카드
            s.ModelBreakdown.Count > 0 ? RenderModelCard(s) : null
        ).Spacing(8).Opacity(State.IsLoading ? 0.5 : 1.0);
    }

    VisualNode RenderStatusBanner(UsageSummary s, double projected)
    {
        var pct = s.PercentConsumed;
        var bgColor = AppColors.StatusBgForPercent(pct);
        var dotColor = AppColors.StatusColorForPercent(pct);
        var textColor = AppColors.StatusTextForPercent(pct);

        string title;
        string sub;

        if (s.ProjectedOverQuota)
        {
            title = AppStrings.IsKoreanStatic
                ? $"한도 초과 예상 · {projected:F0} req"
                : $"Over quota · ~{projected:F0} req projected";
            sub = AppStrings.IsKoreanStatic
                ? $"{s.DaysRemaining}일 남음"
                : $"{s.DaysRemaining}d remaining";
        }
        else
        {
            title = AppStrings.IsKoreanStatic
                ? $"한도 이내 · 예상 {projected:F0} req"
                : $"On track · ~{projected:F0} req projected";
            sub = AppStrings.IsKoreanStatic
                ? $"{s.DaysRemaining}일 남음"
                : $"{s.DaysRemaining}d remaining";
        }

        return Border(
            HStack(
                BoxView().WidthRequest(7).HeightRequest(7).CornerRadius(4).Color(dotColor).VStart().Margin(0, 3, 0, 0),
                VStack(
                    Label(title).FontSize(12).FontAttributes(MauiControls.FontAttributes.Bold).TextColor(textColor),
                    Label(sub).FontSize(10).TextColor(textColor)
                ).Spacing(1)
            ).Spacing(9).Padding(12, 9)
        )
        .BackgroundColor(bgColor)
        .Stroke(dotColor)
        .StrokeThickness(1)
        .StrokeCornerRadius(8);
    }

    VisualNode RenderUsageCard(UsageSummary s, double dailyBudget)
    {
        var pct = s.PercentConsumed;
        var fillColor = AppColors.StatusColorForPercent(pct);

        return PopupCard(
            VStack(
                Grid(
                    Label(AppStrings.MonthlyUsage).FontSize(11).TextColor(AppColors.PopupText3).VCenter(),
                    s.PlanName.Length > 0
                        ? Badge(s.PlanName, Color.FromArgb("#EEEDFE"), Color.FromArgb("#3C3489")).GridColumn(1)
                        : null
                ).Columns("*, Auto"),

                // 도넛 + 텍스트
                HStack(
                    new SkiaCanvas()
                        .WidthRequest(36).HeightRequest(36)
                        .OnPaintSurface((sender, e) =>
                        {
                            DonutRenderer.DrawOnCanvas(
                                e.Surface.Canvas,
                                e.Info.Width, e.Info.Height,
                                strokeWidth: 4f * (e.Info.Width / 36f),
                                percent: pct,
                                trackColor: DonutRenderer.GetTrackColor(IsDark),
                                fillColor: DonutRenderer.GetStatusColor(pct, IsDark));
                        }),
                    VStack(
                        Label($"{s.MtdUsed:F0}")
                            .FontSize(22).FontAttributes(MauiControls.FontAttributes.Bold).TextColor(AppColors.PopupText1),
                        Label($"/ {s.Quota} req · {s.Remaining:F0} {(AppStrings.IsKoreanStatic ? "남음" : "left")}")
                            .FontSize(10).TextColor(AppColors.PopupText3)
                    ).Spacing(2)
                ).Spacing(8)
            ).Spacing(8).Padding(11, 13)
        );
    }

    VisualNode RenderModelCard(UsageSummary s)
    {
        var models = s.ModelBreakdown.OrderByDescending(x => x.Value).ToList();
        double total = models.Sum(m => m.Value);
        var top3 = models.Take(3).ToList();
        var rest = models.Skip(3).ToList();
        double restPct = total > 0 ? rest.Sum(m => m.Value) / total * 100 : 0;
        double restReq = rest.Sum(m => m.Value);

        return PopupCard(
            VStack(
                // Header
                Grid(
                    Label(AppStrings.ModelBreakdown).FontSize(12).FontAttributes(MauiControls.FontAttributes.Bold).TextColor(AppColors.PopupText1).VCenter(),
                    Label($"{models.Count} models").FontSize(10).TextColor(AppColors.PopupText3).VCenter().GridColumn(1)
                ).Columns("*, Auto"),

                // Stacked bar
                RenderStackedBar(models, total),

                // Top 3 models
                VStack(
                    top3.Select((kv, i) =>
                    {
                        double pct = total > 0 ? kv.Value / total * 100 : 0;
                        var color = ModelColors[i % ModelColors.Length];
                        return ModelRow(kv.Key, $"{kv.Value:F0} ({pct:F0}%)", color);
                    })
                ).Spacing(2),

                // Expandable "others"
                rest.Count > 0
                    ? VStack(
                        Grid(
                            HStack(
                                BoxView().WidthRequest(7).HeightRequest(7).CornerRadius(2)
                                    .Color(AppColors.PopupText3),
                                Label(State.ShowAllModels
                                        ? (AppStrings.IsKoreanStatic ? "접기 ▲" : "Collapse ▲")
                                        : (AppStrings.IsKoreanStatic ? $"기타 {rest.Count}개 ▼" : $"{rest.Count} more ▼"))
                                    .FontSize(11).TextColor(Color.FromArgb("#185FA5"))
                            ).Spacing(5).VCenter(),
                            Label($"{restReq:F0} ({restPct:F0}%)")
                                .FontSize(11).FontAttributes(MauiControls.FontAttributes.Bold)
                                .TextColor(AppColors.PopupText1).VCenter().GridColumn(1)
                        )
                        .Columns("*, Auto")
                        .OnTapped(() => SetState(s2 => s2.ShowAllModels = !s2.ShowAllModels)),

                        State.ShowAllModels
                            ? VStack(
                                rest.Select((kv, i) =>
                                {
                                    double pct = total > 0 ? kv.Value / total * 100 : 0;
                                    return Grid(
                                        Label($"  {kv.Key}").FontSize(10).TextColor(AppColors.PopupText3).VCenter(),
                                        Label($"{kv.Value:F0} ({pct:F0}%)").FontSize(10).TextColor(AppColors.PopupText2).VCenter().GridColumn(1)
                                    ).Columns("*, Auto");
                                })
                            ).Spacing(2)
                            : null
                    ).Spacing(4)
                    : null
            ).Spacing(6).Padding(11, 13)
        );
    }

    VisualNode RenderStackedBar(List<KeyValuePair<string, double>> models, double total)
    {
        if (total <= 0) return BoxView().HeightRequest(5);

        var columns = string.Join(",",
            models.Select((m, i) =>
            {
                double pct = m.Value / total * 100;
                return $"{Math.Max(pct, 1)}*";
            }));

        var bars = models.Select((m, i) =>
            BoxView()
                .Color(i < ModelColors.Length ? ModelColors[i] : AppColors.PopupText3)
                .HeightRequest(5)
                .GridColumn(i)
        );

        return Grid(bars.ToArray())
            .Columns(columns)
            .HeightRequest(5);
    }

    static VisualNode ModelRow(string name, string value, Color dotColor)
        => Grid(
            HStack(
                BoxView().WidthRequest(7).HeightRequest(7).CornerRadius(2).Color(dotColor).VCenter(),
                Label(name).FontSize(11).TextColor(AppColors.PopupText2).VCenter()
            ).Spacing(5),
            Label(value).FontSize(11).FontAttributes(MauiControls.FontAttributes.Bold)
                .TextColor(AppColors.PopupText1).VCenter().GridColumn(1)
        ).Columns("*, Auto");

    static Border MiniCard(string label, string value)
        => Border(
            VStack(
                Label(label).FontSize(10).TextColor(AppColors.PopupText3),
                Label(value).FontSize(16).FontAttributes(MauiControls.FontAttributes.Bold).TextColor(AppColors.PopupText1)
            ).Spacing(2).Padding(9, 11)
        )
        .BackgroundColor(AppColors.PopupSurface)
        .Stroke(Colors.Transparent)
        .StrokeCornerRadius(9);

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
