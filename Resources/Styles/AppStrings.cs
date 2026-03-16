using Microsoft.Maui.Storage;
using System.Globalization;

namespace copilot_usage_maui.Resources.Styles;

static class AppStrings
{
    const string KeyLanguage = "language_preference";

    static bool IsKorean
    {
        get
        {
            int pref = Preferences.Default.Get(KeyLanguage, 0);
            if (pref == 1) return false;
            if (pref == 2) return true;
            return CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ko";
        }
    }

    // --- Dashboard ---
    public static string DateFormat       => IsKorean ? "yyyy년 MM월 dd일 (ddd)" : "ddd, MMM d, yyyy";
    public static string Run              => IsKorean ? "실행" : "Run";
    public static string AuthCodeLabel    => IsKorean ? "인증 코드" : "Auth Code";
    public static string Copy             => IsKorean ? "복사" : "Copy";
    public static string OpenBrowser      => IsKorean ? "브라우저 열기" : "Open Browser";
    public static string RefreshAfterAuth => IsKorean ? "인증 완료 후 새로고침" : "Refresh after auth";
    public static string LoadFailed       => IsKorean ? "불러오기 실패" : "Load Failed";
    public static string AuthHint         => IsKorean ? "인증 문제라면 상단 🔑 버튼을 눌러주세요." : "If auth issue, tap 🔑 above.";
    public static string Retry            => IsKorean ? "다시 시도" : "Retry";
    public static string MonthlyUsage     => IsKorean ? "이번 달 사용량" : "Monthly Usage";
    public static string TodayUsage       => IsKorean ? "오늘 사용" : "Today";
    public static string Remaining        => IsKorean ? "남은 할당량" : "Remaining";
    public static string DailyBudget      => IsKorean ? "권장 일 사용량" : "Daily Budget";
    public static string CurrentPace      => IsKorean ? "현재 페이스" : "Current Pace";
    public static string MonthProgress    => IsKorean ? "이번 달 진행" : "Month Progress";
    public static string ProjectedEnd     => IsKorean ? "예상 월말 사용량" : "Projected Month-end";
    public static string OverQuota        => IsKorean ? "⚠ 초과" : "⚠ Over";
    public static string UnderQuota       => IsKorean ? "✓ 여유" : "✓ Under";
    public static string NoModelData      => IsKorean ? "모델별 사용 데이터 없음" : "No model usage data";
    public static string ModelBreakdown   => IsKorean ? "모델별 사용량" : "By Model";
    public static string AuthComplete     => IsKorean ? "✓ 인증 완료" : "✓ Auth complete";
    public static string AuthInstruction  => IsKorean ? "코드를 복사하고 브라우저에서 입력하세요." : "Copy the code and enter it in the browser.";

    // --- Settings ---
    public static string Back            => IsKorean ? "← 뒤로" : "← Back";
    public static string SettingsTitle   => IsKorean ? "설정" : "Settings";
    public static string Appearance      => IsKorean ? "화면" : "Appearance";
    public static string Language        => IsKorean ? "언어" : "Language";
    public static string MonthsHistory   => IsKorean ? "기록 기간 (월)" : "Months of History";
    public static string SaveSettings    => IsKorean ? "저장" : "Save Settings";
    public static string SettingsSaved   => IsKorean ? "✓ 저장 완료" : "✓ Settings saved";
    public static string CheckGhAuth     => IsKorean ? "gh 인증 확인" : "Check gh auth status";
    public static string GhAuthenticated => IsKorean ? "✓ gh CLI 인증됨" : "✓ gh CLI authenticated";
    public static string GhNoToken       => IsKorean ? "✗ 토큰 없음" : "✗ No token found";

    // Picker item lists
    public static List<string> ThemeItems => IsKorean
        ? ["시스템 기본값", "라이트", "다크"]
        : ["System Default", "Light", "Dark"];

    // Language names are shown as-is in both languages
    public static List<string> LangItems => ["System Default / 시스템", "English", "한국어"];

    // --- Format methods ---
    public static string LastRefreshed(DateTime dt) => IsKorean
        ? $"마지막 갱신: {dt:HH:mm:ss}"
        : $"Last updated: {dt:HH:mm:ss}";

    public static string DaysProgress(int elapsed, int remaining) => IsKorean
        ? $"{elapsed}일 경과 / {remaining}일 남음"
        : $"{elapsed}d elapsed / {remaining}d left";

    public static string PaceAhead(double diff, double expected) => IsKorean
        ? $"✓ {diff:F0} req 여유  /  {expected:F0} req"
        : $"✓ {diff:F0} req to spare  /  {expected:F0} req";

    public static string PaceBehind(double diff, double expected) => IsKorean
        ? $"⚠ {diff:F0} req 초과  /  {expected:F0} req"
        : $"⚠ {diff:F0} req over  /  {expected:F0} req";

    public static string RunOutDate(DateOnly date) => IsKorean
        ? $"⚠ 할당량 소진 예상일: {date:MM월 dd일}"
        : $"⚠ Quota runs out: {date:MMM d}";

    public static string AuthError(string message) => IsKorean
        ? $"오류: {message}"
        : $"Error: {message}";
}
