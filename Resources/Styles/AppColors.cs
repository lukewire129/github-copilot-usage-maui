namespace copilot_usage_maui.Resources.Styles;

static class AppColors
{
    static bool IsDark => MauiControls.Application.Current?.RequestedTheme == AppTheme.Dark;

    public static Color TextSecondary     => IsDark ? Color.FromArgb("#AAAAAA") : Colors.Gray;
    public static Color TextOutput        => IsDark ? Color.FromArgb("#CCCCCC") : Colors.DarkSlateGray;
    public static Color TextModelName     => IsDark ? Color.FromArgb("#BBBBBB") : Colors.DarkGray;
    public static Color TextOnAccent      => Colors.White;
    public static Color Accent            => Colors.RoyalBlue;
    public static Color CardBackground    => IsDark ? Color.FromArgb("#2A2A2A") : Color.FromArgb("#F5F5F5");
    public static Color DividerColor      => IsDark ? Color.FromArgb("#444444") : Colors.LightGray;
    public static Color CopyButtonBg      => IsDark ? Color.FromArgb("#3A3A3A") : Colors.LightGray;
    public static Color CopyButtonText    => IsDark ? Colors.White : Colors.Black;
    public static Color StatusSuccess     => IsDark ? Color.FromArgb("#4CAF50") : Colors.ForestGreen;
    public static Color StatusWarning     => IsDark ? Color.FromArgb("#FFA726") : Colors.Orange;
    public static Color StatusError       => IsDark ? Color.FromArgb("#EF5350") : Colors.Red;
    public static Color StatusSuccessText => IsDark ? Color.FromArgb("#66BB6A") : Colors.Green;
}
