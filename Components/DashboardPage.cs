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

    [Inject] GitHubCopilotService _gitHubCopilotService;
    [Inject] SettingsService _settingsService;

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

    async Task LoadData()
    {
        SetState(s =>
        {
            s.IsLoading = true;
            s.Error = null;
        });

        try
        {
            var summary = await _gitHubCopilotService.GetUsageSummaryAsync(_settingsService.MonthsHistory, _settingsService.QuotaLimit);
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
                            Label(DateTime.Today.ToString(AppStrings.DateFormat))
                                .FontSize(12)
                                .TextColor(AppColors.TextSecondary)
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
                                    .TextColor(AppColors.TextSecondary)
                                    .WidthRequest(40),
                            Button("🔑")
                                .OnClicked(() => SetState(s => { s.ShowAuthPanel = !s.ShowAuthPanel; s.AuthRefreshOutput = null; }))
                                .BackgroundColor(Colors.Transparent)
                                .TextColor(State.ShowAuthPanel ? AppColors.Accent : AppColors.TextSecondary)
                                .WidthRequest(45),
                            Button("⚙")
                                .OnClicked(() => _onOpenSettings?.Invoke())
                                .BackgroundColor(Colors.Transparent)
                                .TextColor(AppColors.TextSecondary)
                                .WidthRequest(45)
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
                        : Button(AppStrings.Run)
                            .OnClicked(async () => await RunAuthRefresh())
                            .BackgroundColor(AppColors.Accent)
                            .TextColor(AppColors.TextOnAccent)
                            .GridColumn(1)
                            .WidthRequest(60)
                ),
                State.AuthRefreshOutput != null
                    ? VStack(
                        Label(State.AuthRefreshOutput)
                            .FontSize(12)
                            .TextColor(AppColors.TextOutput),
                        State.AuthDeviceCode != null
                            ? VStack(
                                Label(AppStrings.AuthCodeLabel)
                                    .FontSize(11)
                                    .TextColor(AppColors.TextSecondary),
                                HStack(
                                    Label(State.AuthDeviceCode)
                                        .FontSize(24)
                                        .FontAttributes(MauiControls.FontAttributes.Bold)
                                        .VCenter(),
                                    Button(AppStrings.Copy)
                                        .OnClicked(() =>
                                        {
                                            var code = State.AuthDeviceCode;
                                            if (code != null) CopyToClipboard(code);
                                        })
                                        .BackgroundColor(AppColors.CopyButtonBg)
                                        .TextColor(AppColors.CopyButtonText)
                                        .WidthRequest(60),
                                    Button(AppStrings.OpenBrowser)
                                        .OnClicked(async () => await Launcher.Default.OpenAsync("https://github.com/login/device"))
                                        .BackgroundColor(AppColors.Accent)
                                        .TextColor(AppColors.TextOnAccent)
                                ).Spacing(8)
                            ).Spacing(4)
                            : new Label(),
                        Button(AppStrings.RefreshAfterAuth)
                            .OnClicked(async () => { SetState(s => s.ShowAuthPanel = false); await LoadData(); })
                            .BackgroundColor(Colors.Transparent)
                            .TextColor(AppColors.Accent)
                            .HStart()
                    ).Spacing(6)
                    : new Label()
            ).Spacing(8).Padding(12, 10)
        )
        .BackgroundColor(AppColors.CardBackground)
        .Stroke(Colors.Transparent)
        .StrokeThickness(0)
        .StrokeShape(new MauiReactor.Shapes.RoundRectangle());

    VisualNode RenderBody()
    {
        if (State.IsLoading && State.Summary == null)
            return ActivityIndicator().IsRunning(true).HCenter().VCenter();

        if (State.Error != null)
            return VStack(
                Label(AppStrings.LoadFailed).FontAttributes(MauiControls.FontAttributes.Bold).HCenter(),
                Label(State.Error).TextColor(AppColors.StatusError).HCenter().HorizontalTextAlignment(TextAlignment.Center),
                Label(AppStrings.AuthHint).FontSize(12).TextColor(AppColors.TextSecondary).HCenter(),
                Button(AppStrings.Retry).OnClicked(async () => await LoadData()).HCenter()
            ).Spacing(8).VCenter().HCenter();

        var s = State.Summary!;
        return VStack(
            RenderUsageCard(s),
            RenderModelBreakdown(s),
            Label(AppStrings.LastRefreshed(State.LastRefreshed))
                .FontSize(11)
                .TextColor(AppColors.TextSecondary)
                .HEnd()
        )
        .Spacing(20)
        .Opacity(State.IsLoading ? 0.5 : 1.0);
    }

    static VisualNode RenderUsageCard(UsageSummary s)
    {
        var pct = s.PercentConsumed / 100.0;
        var barColor = s.PercentConsumed >= 90 ? AppColors.StatusError
            : s.PercentConsumed >= 70 ? AppColors.StatusWarning
            : AppColors.StatusSuccess;

        int totalDays = s.DaysElapsed + s.DaysRemaining;
        double dailyBudget = s.Quota / (double)totalDays;
        double expectedByToday = dailyBudget * s.DaysElapsed;
        double paceDiff = expectedByToday - s.MtdUsed;
        bool isAhead = paceDiff >= 0;

        return VStack(
            Label(AppStrings.MonthlyUsage)
                .FontSize(11)
                .TextColor(AppColors.TextSecondary),
            Label($"{s.MtdUsed:F0} / {s.Quota} req  ({s.PercentConsumed:F1}%)")
                .FontSize(24)
                .FontAttributes(MauiControls.FontAttributes.Bold),
            ProgressBar()
                .Progress(Math.Min(1.0, pct))
                .ProgressColor(barColor)
                .HeightRequest(8),

            Grid("Auto,Auto,Auto,Auto,Auto,Auto", "*,*",
                Label(AppStrings.TodayUsage).FontSize(12).TextColor(AppColors.TextSecondary),
                Label($"{s.TodayUsed:F0} req")
                    .FontSize(13).FontAttributes(MauiControls.FontAttributes.Bold).HEnd().GridColumn(1),

                Label(AppStrings.Remaining).FontSize(12).TextColor(AppColors.TextSecondary).GridRow(1),
                Label($"{s.Remaining:F0} req")
                    .FontSize(13).FontAttributes(MauiControls.FontAttributes.Bold).HEnd().GridColumn(1).GridRow(1),

                Label(AppStrings.DailyBudget).FontSize(12).TextColor(AppColors.TextSecondary).GridRow(2),
                Label($"{dailyBudget:F1} req/day")
                    .FontSize(13).FontAttributes(MauiControls.FontAttributes.Bold).HEnd().GridColumn(1).GridRow(2),

                Label(AppStrings.CurrentPace).FontSize(12).TextColor(AppColors.TextSecondary).GridRow(3),
                Label(isAhead
                        ? AppStrings.PaceAhead(paceDiff, expectedByToday)
                        : AppStrings.PaceBehind(-paceDiff, expectedByToday))
                    .FontSize(13)
                    .FontAttributes(MauiControls.FontAttributes.Bold)
                    .TextColor(isAhead ? AppColors.StatusSuccess : AppColors.StatusError)
                    .HEnd().GridColumn(1).GridRow(3),

                Label(AppStrings.MonthProgress).FontSize(12).TextColor(AppColors.TextSecondary).GridRow(4),
                Label(AppStrings.DaysProgress(s.DaysElapsed, s.DaysRemaining))
                    .FontSize(13).FontAttributes(MauiControls.FontAttributes.Bold).HEnd().GridColumn(1).GridRow(4),

                Label(AppStrings.ProjectedEnd).FontSize(12).TextColor(AppColors.TextSecondary).GridRow(5),
                Label($"{s.AvgDailyUsage * totalDays:F0} req  {(s.ProjectedOverQuota ? AppStrings.OverQuota : AppStrings.UnderQuota)}")
                    .FontSize(13)
                    .FontAttributes(MauiControls.FontAttributes.Bold)
                    .TextColor(s.ProjectedOverQuota ? AppColors.StatusError : AppColors.StatusSuccess)
                    .HEnd().GridColumn(1).GridRow(5)
            )
            .RowSpacing(10),

            s.ProjectedRunOutDate.HasValue
                ? Label(AppStrings.RunOutDate(s.ProjectedRunOutDate.Value))
                    .FontSize(13)
                    .TextColor(s.ProjectedOverQuota ? AppColors.StatusError : AppColors.TextSecondary)
                : new Label()
        ).Spacing(10);
    }

    static VisualNode RenderModelBreakdown(UsageSummary s)
    {
        if (s.ModelBreakdown.Count == 0)
            return Label(AppStrings.NoModelData).TextColor(AppColors.TextSecondary).FontSize(13);

        var rows = new List<VisualNode>
        {
            Label(AppStrings.ModelBreakdown)
                .FontSize(11)
                .TextColor(AppColors.TextSecondary)
        };

        double total = s.ModelBreakdown.Values.Sum();
        foreach (var kv in s.ModelBreakdown.OrderByDescending(x => x.Value))
        {
            double pct = total > 0 ? kv.Value / total * 100 : 0;
            rows.Add(
                Grid("Auto", "*, Auto",
                    Label(kv.Key).FontSize(13).TextColor(AppColors.TextModelName).VCenter(),
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
