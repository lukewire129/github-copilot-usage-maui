using Microsoft.Maui.Storage;

namespace copilot_usage_maui.Services;

class SettingsService
{
    const string KeyMonthsHistory = "months_history";

    public int MonthsHistory
    {
        get => Preferences.Default.Get(KeyMonthsHistory, 6);
        set => Preferences.Default.Set(KeyMonthsHistory, value);
    }
}
