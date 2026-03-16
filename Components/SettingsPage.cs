using copilot_usage_maui.Services;

namespace copilot_usage_maui.Components;

class SettingsState
{
    public string MonthsHistory { get; set; } = "6";
    public string QuotaLimit { get; set; } = "300";
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
            s.QuotaLimit = settings.QuotaLimit.ToString();
            s.ThemePreference = settings.ThemePreference;
            s.LanguagePreference = settings.LanguagePreference;
        });
    }

    void SaveSettings()
    {
        var settings = IPlatformApplication.Current!.Services.GetRequiredService<SettingsService>();
        if (int.TryParse(State.MonthsHistory, out int months)) settings.MonthsHistory = months;
        if (int.TryParse(State.QuotaLimit, out int quota) && quota > 0) settings.QuotaLimit = quota;
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

    static VisualNode StyledPicker(List<string> items, int selectedIndex, Action<int> onChanged)
        => new CustomDropDown()
            .ItemsSource(items.ToArray())
            .SelectedIndex(selectedIndex)
            .OnSelectedIndexChanged(onChanged);

    static VisualNode SectionCard(VisualNode content)
        => Border(content)
            .BackgroundColor(AppColors.CardBackground)
            .Stroke(AppColors.DividerColor)
            .StrokeThickness(1)
            .StrokeShape(new MauiReactor.Shapes.RoundRectangle());

    static VisualNode FieldRow(string label, VisualNode control)
        => VStack(
            Label(label)
                .FontSize(12)
                .TextColor(AppColors.TextSecondary),
            control
        ).Spacing(4);

    public override VisualNode Render()
        => ContentPage(
            VStack(
                // Header
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

                // Appearance card
                SectionCard(
                    VStack(
                        Label(AppStrings.Appearance)
                            .FontAttributes(MauiControls.FontAttributes.Bold)
                            .FontSize(13)
                            .TextColor(AppColors.TextSecondary),
                        BoxView().HeightRequest(1).BackgroundColor(AppColors.DividerColor),
                        FieldRow(AppStrings.Language,
                            StyledPicker(AppStrings.LangItems, State.LanguagePreference, idx =>
                            {
                                SetState(s => s.LanguagePreference = idx);
                                var settings = IPlatformApplication.Current!.Services.GetRequiredService<SettingsService>();
                                settings.LanguagePreference = idx;
                            })
                        ),
                        FieldRow(AppStrings.Appearance,
                            StyledPicker(AppStrings.ThemeItems, State.ThemePreference, idx =>
                            {
                                SetState(s => s.ThemePreference = idx);
                                var settings = IPlatformApplication.Current!.Services.GetRequiredService<SettingsService>();
                                settings.ThemePreference = idx;
                                SettingsService.ApplyTheme(idx);
                            })
                        )
                    ).Spacing(10).Padding(14, 12)
                ),

                // Usage card
                SectionCard(
                    VStack(
                        Label(AppStrings.MonthsHistory + " / " + AppStrings.QuotaLimit)
                            .FontAttributes(MauiControls.FontAttributes.Bold)
                            .FontSize(13)
                            .TextColor(AppColors.TextSecondary),
                        BoxView().HeightRequest(1).BackgroundColor(AppColors.DividerColor),
                        FieldRow(AppStrings.QuotaLimit,
                            Entry()
                                .Text(State.QuotaLimit)
                                .Keyboard(Keyboard.Numeric)
                                .OnTextChanged(v => SetState(s => s.QuotaLimit = v))
                        ),
                        FieldRow(AppStrings.MonthsHistory,
                            Entry()
                                .Text(State.MonthsHistory)
                                .Keyboard(Keyboard.Numeric)
                                .OnTextChanged(v => SetState(s => s.MonthsHistory = v))
                        ),
                        Button(AppStrings.SaveSettings)
                            .OnClicked(SaveSettings)
                            .BackgroundColor(AppColors.Accent)
                            .TextColor(AppColors.TextOnAccent),
                        State.IsSaved
                            ? Label(AppStrings.SettingsSaved)
                                .TextColor(AppColors.StatusSuccessText)
                                .HCenter()
                            : Label()
                    ).Spacing(10).Padding(14, 12)
                ),

                // gh auth card
                SectionCard(
                    VStack(
                        Label(AppStrings.CheckGhAuth)
                            .FontAttributes(MauiControls.FontAttributes.Bold)
                            .FontSize(13)
                            .TextColor(AppColors.TextSecondary),
                        BoxView().HeightRequest(1).BackgroundColor(AppColors.DividerColor),
                        Label(AppStrings.GhAuthDesc)
                            .FontSize(12)
                            .TextColor(AppColors.TextSecondary),
                        HStack(
                            Button(AppStrings.CheckGhAuth)
                                .OnClicked(async () => await CheckGhStatus())
                                .BackgroundColor(AppColors.Accent)
                                .TextColor(AppColors.TextOnAccent)
                                .WidthRequest(140),
                            State.IsCheckingGh
                                ? (VisualNode)ActivityIndicator().IsRunning(true).VCenter()
                                : Label(State.GhStatus)
                                    .TextColor(State.GhStatus.StartsWith("✓") ? AppColors.StatusSuccessText : AppColors.StatusError)
                                    .VCenter()
                                    .FontSize(13)
                        ).Spacing(12)
                    ).Spacing(10).Padding(14, 12)
                )
            )
            .Spacing(12)
            .Padding(20, 16)
        );
}
