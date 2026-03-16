using copilot_usage_maui.Services;

namespace copilot_usage_maui.Components;

class SettingsState
{
    public string MonthsHistory { get; set; } = "6";
    public bool IsSaved { get; set; }
    public string GhStatus { get; set; } = "";
    public bool IsCheckingGh { get; set; }
    public int ThemePreference { get; set; }
    public int LanguagePreference { get; set; }
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
            s.ThemePreference = settings.ThemePreference;
            s.LanguagePreference = settings.LanguagePreference;
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
                s.GhStatus = token.Length > 0 ? AppStrings.GhAuthenticated : AppStrings.GhNoToken;
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
                    Button(AppStrings.Back)
                        .OnClicked(() => _onBack?.Invoke())
                        .BackgroundColor(Colors.Transparent)
                        .TextColor(AppColors.TextSecondary),
                    Label(AppStrings.SettingsTitle)
                        .FontSize(20)
                        .FontAttributes(MauiControls.FontAttributes.Bold)
                        .GridColumn(1)
                        .VCenter()
                        .Margin(8, 0, 0, 0)
                ),
                BoxView().HeightRequest(1).BackgroundColor(AppColors.DividerColor),

                Label(AppStrings.Appearance)
                    .FontAttributes(MauiControls.FontAttributes.Bold),
                Picker()
                    .ItemsSource(AppStrings.ThemeItems)
                    .SelectedIndex(State.ThemePreference)
                    .OnSelectedIndexChanged(idx =>
                    {
                        SetState(s => s.ThemePreference = idx);
                        var settings = IPlatformApplication.Current!.Services.GetRequiredService<SettingsService>();
                        settings.ThemePreference = idx;
                        SettingsService.ApplyTheme(idx);
                    }),

                Label(AppStrings.Language)
                    .FontAttributes(MauiControls.FontAttributes.Bold),
                Picker()
                    .ItemsSource(AppStrings.LangItems)
                    .SelectedIndex(State.LanguagePreference)
                    .OnSelectedIndexChanged(idx =>
                    {
                        SetState(s => s.LanguagePreference = idx);
                        var settings = IPlatformApplication.Current!.Services.GetRequiredService<SettingsService>();
                        settings.LanguagePreference = idx;
                    }),

                BoxView().HeightRequest(1).BackgroundColor(AppColors.DividerColor),

                Label(AppStrings.MonthsHistory)
                    .FontAttributes(MauiControls.FontAttributes.Bold),
                Entry()
                    .Text(State.MonthsHistory)
                    .Keyboard(Keyboard.Numeric)
                    .OnTextChanged(v => SetState(s => s.MonthsHistory = v)),

                Button(AppStrings.SaveSettings)
                    .OnClicked(SaveSettings)
                    .BackgroundColor(AppColors.Accent)
                    .TextColor(AppColors.TextOnAccent),

                State.IsSaved
                    ? Label(AppStrings.SettingsSaved)
                        .TextColor(AppColors.StatusSuccessText)
                        .HCenter()
                    : Label(),

                BoxView().HeightRequest(1).BackgroundColor(AppColors.DividerColor),

                HStack(
                    Button(AppStrings.CheckGhAuth)
                        .OnClicked(async () => await CheckGhStatus())
                        .BackgroundColor(Colors.Transparent)
                        .TextColor(AppColors.TextSecondary),
                    State.IsCheckingGh
                        ? (VisualNode)ActivityIndicator().IsRunning(true).VCenter()
                        : Label(State.GhStatus)
                            .TextColor(State.GhStatus.StartsWith("✓") ? AppColors.StatusSuccessText : AppColors.StatusError)
                            .VCenter()
                ).Spacing(8)
            )
            .Spacing(12)
            .Padding(20, 16)
        );
}
