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
    public static string GhAuthDesc      => IsKorean
        ? "gh CLI가 GitHub에 정상 인증되어 있는지 확인합니다.\n데이터 로드에 실패할 때 눌러보세요."
        : "Verifies that gh CLI is authenticated with GitHub.\nTry this if usage data fails to load.";
    public static string GhAuthenticated => IsKorean ? "✓ gh CLI 인증됨" : "✓ gh CLI authenticated";
    public static string GhNoToken       => IsKorean ? "✗ 토큰 없음" : "✗ No token found";

    // Picker item lists
    public static List<string> ThemeItems => IsKorean
        ? ["시스템 기본값", "라이트", "다크"]
        : ["System Default", "Light", "Dark"];

    // Language names are shown as-is in both languages
    public static List<string> LangItems => ["System Default / 시스템", "English", "한국어"];

    // --- Claude Dashboard ---
    public static string ClaudeSessionUsage  => IsKorean ? "세션 사용량 (5시간)" : "Session Usage (5h)";
    public static string ClaudeWeeklyUsage   => IsKorean ? "주간 사용량 (7일)" : "Weekly Usage (7d)";
    public static string ClaudeModelLimits   => IsKorean ? "모델별 제한" : "Model Limits";
    public static string ClaudeAccountInfo   => IsKorean ? "계정 정보" : "Account Info";
    public static string ClaudeEmail         => IsKorean ? "이메일" : "Email";
    public static string ClaudePlan          => IsKorean ? "플랜" : "Plan";
    public static string ClaudeNoData        => IsKorean ? "Claude 사용 데이터 없음" : "No Claude usage data";
    public static string ClaudeAuthHint      => IsKorean
        ? "Claude CLI 또는 OAuth 인증이 필요합니다.\n'claude' 명령으로 로그인하세요."
        : "Claude CLI or OAuth authentication required.\nRun 'claude' to log in.";

    // Condition management
    public static string ClaudeCondition     => IsKorean ? "컨디션 관리" : "Usage Condition";
    public static string ClaudeUsageOk       => IsKorean ? "✓ 여유롭게 사용 가능" : "✓ Usage is on track";
    public static string ClaudeUsageWarn     => IsKorean ? "⚠ 사용 속도가 빠릅니다" : "⚠ Usage pace is fast";
    public static string ClaudeUsageDanger   => IsKorean ? "🔴 한도 초과 예상 / 임박" : "🔴 Near or over limit";
    public static string ClaudeElapsed       => IsKorean ? "경과" : "Elapsed";
    public static string ClaudeRemaining     => IsKorean ? "남은 시간" : "Time Left";
    public static string ClaudeResetCountdown => IsKorean ? "초기화까지" : "Resets in";
    public static string ClaudePaceLabel     => IsKorean ? "사용 속도" : "Pace";
    public static string ClaudePaceNormal    => IsKorean ? "정상" : "Normal";
    public static string ClaudePaceFast      => IsKorean ? "빠름" : "Fast";

    public static string ClaudeProjectedFinal(double pct) => IsKorean
        ? $"리셋 시점 예상 사용률: 약 {pct:F0}%"
        : $"Projected usage at reset: ~{pct:F0}%";

    public static string ClaudeResetIn(TimeSpan remaining) => IsKorean
        ? $"{(int)remaining.TotalHours}시간 {remaining.Minutes}분 후 초기화"
        : $"Resets in {(int)remaining.TotalHours}h {remaining.Minutes}m";

    public static string ClaudeWindowUsage(double used, double window) => IsKorean
        ? $"{used:F0}% 사용 / {window:F0}% 경과"
        : $"{used:F0}% used / {window:F0}% elapsed";

    // Claude Settings
    public static string ClaudeSettingsTitle  => IsKorean ? "Claude 설정" : "Claude Settings";
    public static string ClaudeAuthMethod     => IsKorean ? "인증 방법" : "Auth Method";
    public static string ClaudeCliPathLabel   => IsKorean ? "Claude CLI 경로 (선택)" : "Claude CLI Path (optional)";
    public static List<string> ClaudeAuthItems => IsKorean
        ? ["자동 (OAuth → CLI)", "OAuth만", "CLI만"]
        : ["Auto (OAuth → CLI)", "OAuth Only", "CLI Only"];
    public static string CheckClaudeAuth      => IsKorean ? "Claude 인증 확인" : "Check Claude auth";
    public static string ClaudeAuthDesc       => IsKorean
        ? "Claude CLI 또는 OAuth 토큰이 정상 인증되어 있는지 확인합니다."
        : "Verifies Claude CLI or OAuth token authentication status.";
    public static string ClaudeAuthenticated  => IsKorean ? "✓ Claude 인증됨" : "✓ Claude authenticated";
    public static string ClaudeNotAuth        => IsKorean ? "✗ 인증 없음" : "✗ Not authenticated";

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
