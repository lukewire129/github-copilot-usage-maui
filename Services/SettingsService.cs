using Microsoft.Maui.Storage;

namespace copilot_usage_maui.Services;

public class SettingsService
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

    const string KeyAutoRefreshInterval = "auto_refresh_interval";

    public static event EventHandler? AutoRefreshIntervalChanged;

    // 0=Off, 1=10분, 2=30분, 3=1시간
    public int AutoRefreshInterval
    {
        get => Preferences.Default.Get(KeyAutoRefreshInterval, 0);
        set
        {
            Preferences.Default.Set(KeyAutoRefreshInterval, value);
            AutoRefreshIntervalChanged?.Invoke(null, EventArgs.Empty);
        }
    }

    public static int GetAutoRefreshIntervalMs(int index) => index switch
    {
        1 => (int)TimeSpan.FromMinutes(10).TotalMilliseconds,
        2 => (int)TimeSpan.FromMinutes(30).TotalMilliseconds,
        3 => (int)TimeSpan.FromHours(1).TotalMilliseconds,
        _ => 0
    };

    const string KeyWidgetMode      = "widget_mode";
    const string KeyFloatingWidgetX = "floating_widget_x";
    const string KeyFloatingWidgetY = "floating_widget_y";

    // 0 = DeskBand (Win11+), 1 = Floating
    public int WidgetMode
    {
        get => Preferences.Default.Get(KeyWidgetMode, 0);
        set => Preferences.Default.Set(KeyWidgetMode, value);
    }

    public int FloatingWidgetX
    {
        get => Preferences.Default.Get(KeyFloatingWidgetX, -1);
        set => Preferences.Default.Set(KeyFloatingWidgetX, value);
    }

    public int FloatingWidgetY
    {
        get => Preferences.Default.Get(KeyFloatingWidgetY, -1);
        set => Preferences.Default.Set(KeyFloatingWidgetY, value);
    }
}
