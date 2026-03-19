using Microsoft.Maui.Storage;

namespace copilot_usage_maui.Services;

class SettingsService
{
    const string KeyMonthsHistory = "months_history";
    const string KeyThemePreference = "theme_preference";

    public int MonthsHistory
    {
        get => Preferences.Default.Get(KeyMonthsHistory, 6);
        set => Preferences.Default.Set(KeyMonthsHistory, value);
    }

    public int ThemePreference
    {
        get => Preferences.Default.Get(KeyThemePreference, 0);
        set => Preferences.Default.Set(KeyThemePreference, value);
    }

    public static void ApplyTheme(int preference)
    {
        if (MauiControls.Application.Current == null) return;
        MauiControls.Application.Current.UserAppTheme = preference switch
        {
            1 => AppTheme.Light,
            2 => AppTheme.Dark,
            _ => AppTheme.Unspecified
        };
    }

    const string KeyClaudeAuthMethod = "claude_auth_method";
    const string KeyClaudeCliPath = "claude_cli_path";

    public int ClaudeAuthMethod
    {
        get => Preferences.Default.Get(KeyClaudeAuthMethod, 0);
        set => Preferences.Default.Set(KeyClaudeAuthMethod, value);
    }

    public string ClaudeCliPath
    {
        get => Preferences.Default.Get(KeyClaudeCliPath, "");
        set => Preferences.Default.Set(KeyClaudeCliPath, value);
    }

    const string KeyLanguagePreference = "language_preference";

    public static event EventHandler? LanguageChanged;

    public int LanguagePreference
    {
        get => Preferences.Default.Get(KeyLanguagePreference, 0);
        set
        {
            Preferences.Default.Set(KeyLanguagePreference, value);
            LanguageChanged?.Invoke(null, EventArgs.Empty);
        }
    }
}
