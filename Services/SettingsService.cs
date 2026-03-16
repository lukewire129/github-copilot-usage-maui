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
}
