namespace copilot_usage_maui.Resources.Styles;

static class AppColors
{
    static bool IsDark => MauiControls.Application.Current?.RequestedTheme == AppTheme.Dark;

    // ── Text ──
    public static Color TextSecondary => IsDark ? Color.FromArgb("#AAAAAA") : Color.FromArgb("#595959");
    public static Color TextOnAccent => Colors.White;
    public static Color Accent => Colors.RoyalBlue;

    // ── Surface ──
    public static Color CardBackground => IsDark ? Color.FromArgb("#2A2A2A") : Color.FromArgb("#F5F5F5");
    public static Color DividerColor => IsDark ? Color.FromArgb("#444444") : Colors.LightGray;
    public static Color CopyButtonText => IsDark ? Colors.White : Colors.Black;

    // ── Status (60/80 임계값) ──
    public static Color StatusSuccess => IsDark ? Color.FromArgb("#5DCAA5") : Color.FromArgb("#1D9E75");
    public static Color StatusWarning => IsDark ? Color.FromArgb("#FAC775") : Color.FromArgb("#EF9F27");
    public static Color StatusError => IsDark ? Color.FromArgb("#F09595") : Color.FromArgb("#E24B4A");
    public static Color StatusSuccessText => IsDark ? Color.FromArgb("#5DCAA5") : Color.FromArgb("#085041");
    public static Color StatusWarningText => IsDark ? Color.FromArgb("#FAC775") : Color.FromArgb("#633806");
    public static Color StatusErrorText => IsDark ? Color.FromArgb("#F09595") : Color.FromArgb("#791F1F");

    // ── Status backgrounds ──
    public static Color StatusSuccessBg => IsDark ? Color.FromArgb("#0D3326") : Color.FromArgb("#E1F5EE");
    public static Color StatusWarningBg => IsDark ? Color.FromArgb("#3D2A08") : Color.FromArgb("#FAEEDA");
    public static Color StatusErrorBg => IsDark ? Color.FromArgb("#3D1414") : Color.FromArgb("#FCEBEB");

    // ── Brand: Copilot ──
    public static Color CopilotPrimary => Color.FromArgb("#6E40C9");

    // ── Brand: Claude ──
    public static Color ClaudePrimary => Color.FromArgb("#D97757");

    // ── Popup surface tokens ──
    public static Color PopupPage => IsDark ? Color.FromArgb("#121212") : Colors.White;
    public static Color PopupSurface => IsDark ? Color.FromArgb("#2A2A2A") : Color.FromArgb("#F7F5F0");
    public static Color PopupBorder => IsDark ? Color.FromArgb("#444444") : Color.FromArgb("#E8E6E1");
    public static Color PopupText1 => IsDark ? Color.FromArgb("#E8E8E8") : Color.FromArgb("#2C2C2A");
    public static Color PopupText2 => IsDark ? Color.FromArgb("#AAAAAA") : Color.FromArgb("#5F5E5A");
    public static Color PopupText3 => IsDark ? Color.FromArgb("#777777") : Color.FromArgb("#888780");

    /// <summary>
    /// 사용률에 따른 상태 색상 (60/80 임계값)
    /// </summary>
    public static Color StatusColorForPercent(double percent)
    {
        if (percent >= 80) return StatusError;
        if (percent >= 60) return StatusWarning;
        return StatusSuccess;
    }

    public static Color StatusTextForPercent(double percent)
    {
        if (percent >= 80) return StatusErrorText;
        if (percent >= 60) return StatusWarningText;
        return StatusSuccessText;
    }

    public static Color StatusBgForPercent(double percent)
    {
        if (percent >= 80) return StatusErrorBg;
        if (percent >= 60) return StatusWarningBg;
        return StatusSuccessBg;
    }
}
