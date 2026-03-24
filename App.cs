using copilot_usage_maui.Services;
using ReactorRouter.Components;
using ReactorRouter.Routing;

namespace copilot_usage_maui;

class AppState
{
    public AppTheme CurrentTheme { get; set; }
    public int AppVersion { get; set; }
}

partial class App : Component<AppState>
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
                ContentPage(new Router())
           )
           .Title("GitHub Copilot Usage")
           .Width(310)
           .Height(520);
}
