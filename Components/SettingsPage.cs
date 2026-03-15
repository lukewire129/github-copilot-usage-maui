using copilot_usage_maui.Services;

namespace copilot_usage_maui.Components;

class SettingsState
{
    public string MonthsHistory { get; set; } = "6";
    public bool IsSaved { get; set; }
    public string GhStatus { get; set; } = "";
    public bool IsCheckingGh { get; set; }
}

partial class SettingsPage : Component<SettingsState>
{
    Action? _onBack;

    public SettingsPage OnBack(Action action)
    {
        _onBack = action;
        return this;
    }

    protected override void OnMounted()
    {
        base.OnMounted();
        var settings = IPlatformApplication.Current!.Services.GetRequiredService<SettingsService>();
        SetState(s =>
        {
            s.MonthsHistory = settings.MonthsHistory.ToString();
        });
    }

    void SaveSettings()
    {
        var settings = IPlatformApplication.Current!.Services.GetRequiredService<SettingsService>();
        if (int.TryParse(State.MonthsHistory, out int months)) settings.MonthsHistory = months;
        SetState(s => s.IsSaved = true);
        Task.Delay(2000).ContinueWith(_ => SetState(s => s.IsSaved = false));
    }

    async Task CheckGhStatus()
    {
        SetState(s => { s.IsCheckingGh = true; s.GhStatus = ""; });
        try
        {
            var service = IPlatformApplication.Current!.Services.GetRequiredService<GitHubCopilotService>();
            string token = await service.GetGhTokenAsync();
            SetState(s =>
            {
                s.GhStatus = token.Length > 0 ? "✓ gh CLI authenticated" : "✗ No token found";
                s.IsCheckingGh = false;
            });
        }
        catch (Exception ex)
        {
            SetState(s =>
            {
                s.GhStatus = $"✗ {ex.Message}";
                s.IsCheckingGh = false;
            });
        }
    }

    public override VisualNode Render()
        => ContentPage(
            VStack(
                Grid("Auto", "Auto, *",
                    Button("← Back")
                        .OnClicked(() => _onBack?.Invoke())
                        .BackgroundColor(Colors.Transparent)
                        .TextColor(Colors.Gray),
                    Label("Settings")
                        .FontSize(20)
                        .FontAttributes(MauiControls.FontAttributes.Bold)
                        .GridColumn(1)
                        .VCenter()
                        .Margin(8, 0, 0, 0)
                ),
                BoxView().HeightRequest(1).BackgroundColor(Colors.LightGray),

                Label("Months of History")
                    .FontAttributes(MauiControls.FontAttributes.Bold),
                Entry()
                    .Text(State.MonthsHistory)
                    .Keyboard(Keyboard.Numeric)
                    .OnTextChanged(v => SetState(s => s.MonthsHistory = v)),

                Button("Save Settings")
                    .OnClicked(SaveSettings)
                    .BackgroundColor(Colors.RoyalBlue)
                    .TextColor(Colors.White),

                State.IsSaved
                    ? Label("✓ Settings saved")
                        .TextColor(Colors.Green)
                        .HCenter()
                    : Label(),

                BoxView().HeightRequest(1).BackgroundColor(Colors.LightGray),

                HStack(
                    Button("Check gh auth status")
                        .OnClicked(async () => await CheckGhStatus())
                        .BackgroundColor(Colors.Transparent)
                        .TextColor(Colors.Gray),
                    State.IsCheckingGh
                        ? (VisualNode)ActivityIndicator().IsRunning(true).VCenter()
                        : Label(State.GhStatus)
                            .TextColor(State.GhStatus.StartsWith("✓") ? Colors.Green : Colors.Red)
                            .VCenter()
                ).Spacing(8)
            )
            .Spacing(12)
            .Padding(20, 16)
        );
}
