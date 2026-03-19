using copilot_usage_maui.Models;
using copilot_usage_maui.Services;
using copilot_usage_maui.SettingsComponents;
using ReactorRouter.Navigation;

namespace copilot_usage_maui.Features.Settings.Pages;

class SettingsState
{
    public string MonthsHistory { get; set; } = "6";
    public bool IsSaved { get; set; }
    public string GhStatus { get; set; } = "";
    public bool IsCheckingGh { get; set; }
    public int ThemePreference { get; set; }
    public int LanguagePreference { get; set; }
    public int ClaudeAuthMethod { get; set; }
    public string ClaudeCliPath { get; set; } = "";
    public string ClaudeStatus { get; set; } = "";
    public bool IsCheckingClaude { get; set; }
    public int AutoRefreshInterval { get; set; }
}

partial class SettingsPage : Component<SettingsState>
{

    [Inject] GitHubCopilotService _gitHubCopilotService;
    [Inject] ClaudeUsageService _claudeUsageService;

    protected override void OnMounted()
    {
        base.OnMounted();
        var settings = IPlatformApplication.Current!.Services.GetRequiredService<SettingsService>();
        SetState(s =>
        {
            s.MonthsHistory = settings.MonthsHistory.ToString();
            s.ThemePreference = settings.ThemePreference;
            s.LanguagePreference = settings.LanguagePreference;
            s.ClaudeAuthMethod = settings.ClaudeAuthMethod;
            s.ClaudeCliPath = settings.ClaudeCliPath;
            s.AutoRefreshInterval = settings.AutoRefreshInterval;
        });
    }

    void SaveSettings()
    {
        var settings = IPlatformApplication.Current!.Services.GetRequiredService<SettingsService>();
        if (int.TryParse(State.MonthsHistory, out int months)) settings.MonthsHistory = months;
        SetState(s => s.IsSaved = true);
        Task.Delay(2000).ContinueWith(_ => SetState(s => s.IsSaved = false));
    }

    async Task CheckClaudeStatus()
    {
        SetState(s => { s.IsCheckingClaude = true; s.ClaudeStatus = ""; });
        string result = await _claudeUsageService.DiagnoseAsync();
        SetState(s => { s.ClaudeStatus = result; s.IsCheckingClaude = false; });
    }

    async Task CheckGhStatus()
    {
        SetState(s => { s.IsCheckingGh = true; s.GhStatus = ""; });
        try
        {
            string token = await _gitHubCopilotService.GetGhTokenAsync();
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
        => ScrollView(
            VStack(
                // Header
                Grid("48", "48, *, 48",
                    Button("←")
                        .OnClicked(() => NavigationService.Instance.GoBack())
                        .BackgroundColor(Colors.Transparent)
                        .TextColor(AppColors.Accent)
                        .FontSize(20)
                        .HeightRequest(48)
                        .WidthRequest(48),
                    Label(AppStrings.SettingsTitle)
                        .FontSize(17)
                        .FontAttributes(MauiControls.FontAttributes.Bold)
                        .GridColumn(1)
                        .HCenter()
                        .VCenter(),
                    new Label().GridColumn(2)
                ),
                BoxView().HeightRequest(1).BackgroundColor(AppColors.DividerColor),
                ScrollView(
                VStack(

                // Appearance card
                SectionCard(
                    VStack(
                        Label(AppStrings.Appearance)
                            .FontAttributes(MauiControls.FontAttributes.Bold)
                            .FontSize(13),
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
                        ),
                        FieldRow(AppStrings.AutoRefresh,
                            StyledPicker(AppStrings.AutoRefreshItems, State.AutoRefreshInterval, idx =>
                            {
                                SetState(s => s.AutoRefreshInterval = idx);
                                var settings = IPlatformApplication.Current!.Services.GetRequiredService<SettingsService>();
                                settings.AutoRefreshInterval = idx;
                            })
                        )
                    ).Spacing(10).Padding(14, 12)
                ),

                // Usage card
                SectionCard(
                    VStack(
                        Label(AppStrings.MonthsHistory)
                            .FontAttributes(MauiControls.FontAttributes.Bold)
                            .FontSize(13),
                        BoxView().HeightRequest(1).BackgroundColor(AppColors.DividerColor),
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
                            .FontSize(13),
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
                ),

                // Claude settings card
                SectionCard(
                    VStack(
                        Label(AppStrings.ClaudeSettingsTitle)
                            .FontAttributes(MauiControls.FontAttributes.Bold)
                            .FontSize(13),
                        BoxView().HeightRequest(1).BackgroundColor(AppColors.DividerColor),
                        FieldRow(AppStrings.ClaudeAuthMethod,
                            StyledPicker(AppStrings.ClaudeAuthItems, State.ClaudeAuthMethod, idx =>
                            {
                                SetState(s => s.ClaudeAuthMethod = idx);
                                var settings = IPlatformApplication.Current!.Services.GetRequiredService<SettingsService>();
                                settings.ClaudeAuthMethod = idx;
                            })
                        ),
                        FieldRow(AppStrings.ClaudeCliPathLabel,
                            Entry()
                                .Text(State.ClaudeCliPath)
                                .Placeholder("(auto-detect)")
                                .OnTextChanged(v =>
                                {
                                    SetState(s => s.ClaudeCliPath = v);
                                    var settings = IPlatformApplication.Current!.Services.GetRequiredService<SettingsService>();
                                    settings.ClaudeCliPath = v;
                                })
                        ),
                        Label(AppStrings.ClaudeAuthDesc)
                            .FontSize(12)
                            .TextColor(AppColors.TextSecondary),
                        HStack(
                            Button(AppStrings.CheckClaudeAuth)
                                .OnClicked(async () => await CheckClaudeStatus())
                                .BackgroundColor(AppColors.Accent)
                                .TextColor(AppColors.TextOnAccent)
                                .WidthRequest(140),
                            State.IsCheckingClaude
                                ? (VisualNode)ActivityIndicator().IsRunning(true).VCenter()
                                : Label(State.ClaudeStatus)
                                    .TextColor(State.ClaudeStatus.StartsWith("✓") ? AppColors.StatusSuccessText : AppColors.StatusError)
                                    .FontSize(12)
                                    .LineBreakMode(LineBreakMode.WordWrap)
                        ).Spacing(12)
                    ).Spacing(10).Padding(14, 12)
                )

                ).Spacing(12).Padding(20, 16)
                )
            )
            .Spacing(0)
        );
}
