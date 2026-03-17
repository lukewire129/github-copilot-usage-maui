using copilot_usage_maui.Services;

namespace copilot_usage_maui.Components;

class AppShellState
{
    public bool ShowSettings { get; set; }
    public AppTheme CurrentTheme { get; set; }
    public int AppVersion { get; set; }
}

partial class AppShell : Component<AppShellState>
{
    protected override void OnMounted()
    {
        base.OnMounted();
        var settings = IPlatformApplication.Current!.Services.GetRequiredService<SettingsService>();
        SettingsService.ApplyTheme(settings.ThemePreference);
        if (MauiControls.Application.Current != null)
            MauiControls.Application.Current.RequestedThemeChanged += OnThemeChanged;
        SettingsService.LanguageChanged += OnLanguageChanged;
    }

    protected override void OnWillUnmount()
    {
        base.OnWillUnmount();
        if (MauiControls.Application.Current != null)
            MauiControls.Application.Current.RequestedThemeChanged -= OnThemeChanged;
        SettingsService.LanguageChanged -= OnLanguageChanged;
    }

    void OnThemeChanged(object? sender, MauiControls.AppThemeChangedEventArgs e)
        => SetState(s => s.CurrentTheme = e.RequestedTheme);

    void OnLanguageChanged(object? sender, EventArgs e)
        => SetState(s => s.AppVersion++);

    public override VisualNode Render()
        => Window(
        State.ShowSettings
            ? new SettingsPage()
                .OnBack(() => SetState(s => s.ShowSettings = false))
            : new DashboardPage()
                .OnOpenSettings(() => SetState(s => s.ShowSettings = true))
        )
        .Title("GitHub Copilot Usage")
        .Width(450)
        .Height(900);
}
